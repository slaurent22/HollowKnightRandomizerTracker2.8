using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Modding;
using WebSocketSharp.Server;
using System.Reflection;

namespace PlayerDataDump
{

    /// <summary>
    /// Main mod class for PlayerDataDump.  Provides the server and version handling.
    /// </summary>
    public class PlayerDataDump : Mod, ITogglableMod
    {
        public override int LoadPriority() => 9999;
        private readonly WebSocketServer _wss = new WebSocketServer(11420);
        internal static PlayerDataDump Instance;

        private static SocketServer _ss;

        public static bool firstConnect = true;

        /// <summary>
        /// Fetches the list of the current mods installed.
        /// </summary>
        public static string GetCurrentMods()
        {
            List<string> mods = ModHooks.Instance.LoadedMods;
            string output = mods.Aggregate("[", (current, mod) => current + $"\"{mod}\",");
            output = output.TrimEnd(',') + "]";
            return output;
        }
        public override bool IsCurrent() {return true;}
        public override string GetVersion() => FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(PlayerDataDump)).Location).FileVersion;
        /// <summary>
        /// Creates and starts the WebSocket Server instances.
        /// </summary>
        public override void Initialize()
        {
            Instance = this;
            Log("Initializing PlayerDataDump");

            //Setup websockets server
            _wss.AddWebSocketService<SocketServer>("/playerData", ss =>
            {
                _ss = ss;

                ModHooks.Instance.NewGameHook += ss.NewGame;
                ModHooks.Instance.SavegameLoadHook += ss.LoadSave;
                ModHooks.Instance.BeforeSavegameSaveHook += ss.BeforeSave;

                ModHooks.Instance.SetPlayerBoolHook += ss.EchoBool;
                ModHooks.Instance.SetPlayerIntHook += ss.EchoInt;

                ModHooks.Instance.ApplicationQuitHook += ss.OnQuit;
                ModHooks.Instance.ApplicationQuitHook += Instance_ApplicationQuitHook;
            });

            //Setup ProfileStorage Server
            _wss.AddWebSocketService<ProfileStorageServer>("/ProfileStorage", ss => { });

            _wss.Start();

            On.PlayerData.Reset += PlayerData_Reset;

            Log("Initialized PlayerDataDump");
        }

        private void Instance_ApplicationQuitHook()
        {
            if (_ss != null)
                _ss.OnQuit();
            Unload();
        }

        private void PlayerData_Reset(On.PlayerData.orig_Reset orig, PlayerData self)
        {
            orig(self);
            if(_ss != null)
                _ss.NewGame();
        }

        /// <summary>
        /// Called when the mod is disabled, stops the web socket server and removes the socket services.
        /// </summary>
        public void Unload()
        {
            _wss.Stop();
            _wss.RemoveWebSocketService("/playerData");
            _wss.RemoveWebSocketService("/ProfileStorage");
        }
    }
}
