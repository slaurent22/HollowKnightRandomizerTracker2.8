using Modding;
using System.Collections.Generic;
using WebSocketSharp;
using WebSocketSharp.Server;
using UnityEngine;
using System.Reflection;

namespace PlayerDataDump
{
    internal class SocketServer : WebSocketBehavior
    {
        public SocketServer()
        {
            IgnoreExtensions = true;
        }

        private bool PlayerData_GetBool(On.PlayerData.orig_GetBool orig, PlayerData self, string boolName)
        {
            throw new System.NotImplementedException();
        }

        private static readonly HashSet<string> IntKeysToSend = new HashSet<string> {"simpleKeys", "nailDamage", "maxHealth", "MPReserveMax", "ore", "rancidEggs", "grubsCollected", "charmSlotsFilled", "charmSlots", "flamesCollected" };

        public void Broadcast(string s)
        {
            Sessions.Broadcast(s);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            if (State != WebSocketState.Open) return;

            switch (e.Data)
            {
                case "mods":
                    Send(JsonUtility.ToJson(ModHooks.Instance.LoadedModsWithVersions));
                    break;
                case "version":
                    Send($"{{ \"version\":\"{PlayerDataDump.Instance.GetVersion()}\" }}");
                    break;
                case "json":
                    Send(GetJson());
                    SendPercentage();
                    GetRandom();
                    break;
                default:
                    if (e.Data.Contains('|'))
                    {
                        switch (e.Data.Split('|')[0])
                        {
                            case "bool":
                                string b = PlayerData.instance.GetBool(e.Data.Split('|')[1]).ToString();
                                SendMessage(e.Data.Split('|')[1], b);
                                break;
                            case "int":
                                string i = PlayerData.instance.GetInt(e.Data.Split('|')[1]).ToString();
                                SendMessage(e.Data.Split('|')[1], i);
                                break;
                        }
                    }
                    else
                    {
                        Send("mods,version,json,bool|{var},int|{var}");
                    }
                    break;
            }
        }

        protected override void OnError(ErrorEventArgs e)
        {
            PlayerDataDump.Instance.LogError(e.Message);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);

            ModHooks.Instance.NewGameHook -= NewGame;
            ModHooks.Instance.SavegameLoadHook -= LoadSave;
            ModHooks.Instance.BeforeSavegameSaveHook -= BeforeSave;
            ModHooks.Instance.SetPlayerBoolHook -= EchoBool;
            ModHooks.Instance.SetPlayerIntHook -= EchoInt;

            ModHooks.Instance.ApplicationQuitHook -= OnQuit;

            PlayerDataDump.Instance.Log("CLOSE: Code:" + e.Code + ", Reason:" + e.Reason);
        }



        protected override void OnOpen()
        {
            PlayerDataDump.Instance.Log("OPEN");
            if (PlayerDataDump.firstConnect)
            {
                SendMessage("Reload", "true");
                PlayerDataDump.firstConnect = false;
            } else
            {
                Send(GetJson());
                SendPercentage();
                GetRandom();
            }
        }

        public void SendMessage(string var, string value)
        {
            if (State != WebSocketState.Open) return;

            Send(new Row(var, value).ToJsonElementPair);
        }

        public void LoadSave(int slot)
        {
            if (State != WebSocketState.Open) return;
            GetRandom();
            SendMessage("SaveLoaded", "true");
        }

        public void BeforeSave(SaveGameData data)
        {
            if (State != WebSocketState.Open) return;
            SendMessage("SaveLoaded", "true");
        }

        public void EchoBool(string var, bool value)
        {
            PlayerDataDump.Instance.LogDebug($"EchoBool: {var} = {value}");

            if (var == "RandomizerMod.Monomon" || var == "AreaRando.Monomon" || var == "monomonDefeated")
            {
                var= "maskBrokenMonomon";
            }
            else if (var == "RandomizerMod.Lurien" || var == "AreaRando.Lurien" || var == "lurienDefeated")
            {
                var= "maskBrokenLurien";
            }
            else if (var == "RandomizerMod.Herrah" || var == "AreaRando.Herrah" || var == "hegemolDefeated")
            {
                var= "maskBrokenHegemol";
            }
            if (var.StartsWith("RandomizerMod"))
            {
                var = var.Remove(0, 14);
            }
            else if (var.StartsWith("AreaRando"))
            {
                var = var.Remove(0, 10);
            }
            if (var.StartsWith("RandomizerMod.has") || var.StartsWith("gotCharm_") || var.StartsWith("brokenCharm_") || var.StartsWith("equippedCharm_") || var.StartsWith("has") || var.StartsWith("maskBroken") || var == "overcharmed" || var.StartsWith("used") || var.StartsWith("opened") || var.StartsWith("gave") || var == "unlockedCompletionRate" || var.EndsWith("Collected"))
            {
                SendMessage(var, value.ToString());
            }
            SendPercentage();
            PlayerData.instance.SetBoolInternal(var, value);
            SendMessage("bench", PlayerData.instance.respawnScene.ToString());
        }

       public void EchoInt(string var, int value)
        {
            PlayerDataDump.Instance.LogDebug($"EchoInt: {var} = {value}");
            if ( var == "royalCharmState" && (value == 1 || value == 2 || value == 3 || value == 4 ))
            {
                EchoBool("gotCharm_36", true);
            }
            if (IntKeysToSend.Contains(var) || var.EndsWith("Level") || var.StartsWith("trinket") || var.StartsWith("soldTrinket") || var == "nailSmithUpgrades" || var == "rancidEggs" || var == "royalCharmState" || var == "dreamOrbs" || var.EndsWith("Collected"))
            {
                SendMessage(var, value.ToString());
            }
            SendPercentage();
            PlayerData.instance.SetIntInternal(var, value);
        }

        public static string GetJson()
        {
            PlayerData playerData = PlayerData.instance;
            string json = JsonUtility.ToJson(playerData);

            return json;
        }

        public void SendPercentage()
        {
            if (State != WebSocketState.Open) return;
            try
            {
                int a = RandomizerMod.RandoLogger.obtainedLocations.Count;
                int b = RandomizerMod.RandoLogger.randomizedLocations.Count;
                int c = RandomizerMod.RandoLogger.uncheckedLocations.Count;

                float tPercent = Mathf.Round((1000f * a) / (float)b) /10f;
                float rPercent = Mathf.Round((1000f * a) / (float)(a+c)) / 10f;

                SendMessage("tpercent", PlayerData.instance.completionPercentage.ToString());

                //SendMessage("tpercent", tPercent.ToString());
                SendMessage("rpercent", rPercent.ToString());


            } catch
            {

            }
        }

        public void GetRandom()
        {
            if (State != WebSocketState.Open) return;
            try
            {
                var settings = RandomizerMod.RandomizerMod.Instance.Settings;
                if (settings.Randomizer)
                {
                    var msgText = "";
                    if (settings.Cursed)
                        msgText += "Cursed ";

                    if (settings.ConnectAreas || settings.RandomizeTransitions)
                    {
                        if (settings.ConnectAreas)
                            msgText += "Connected-Area ";

                        if (settings.RandomizeRooms)
                            msgText += "Room ";
                        else if (settings.RandomizeAreas)
                            msgText += "Area ";

                        msgText += "Rando ";
                    }
                    else
                    {
                        msgText += "Item Rando";
                    }
                    SendMessage("rando_type", msgText.Trim());

                    // Preset reference:
                    // https://github.com/flibber-hk/HollowKnight.RandomizerMod/blob/6d46547e79a1d472791070477aa18450f3364363/RandomizerMod3.0/MenuChanger.cs#L269

                    // "Standard", formerly "Super Mini Junk Pit"
                    // "Junk Pit" was this minus Stags
                    bool selectionsTrueStandard =
                        settings.RandomizeDreamers &&
                        settings.RandomizeSkills &&
                        settings.RandomizeCharms &&
                        settings.RandomizeKeys &&
                        settings.RandomizeGeoChests &&
                        settings.RandomizeMaskShards &&
                        settings.RandomizeVesselFragments &&
                        settings.RandomizePaleOre &&
                        settings.RandomizeCharmNotches &&
                        settings.RandomizeRancidEggs &&
                        settings.RandomizeRelics &&
                        settings.RandomizeStags;

                    // "Super", formerly "Super Junk Pit"
                    bool selectionsTrueSuper =
                        selectionsTrueStandard &&
                        settings.RandomizeMaps &&
                        settings.RandomizeGrubs &&
                        settings.RandomizeWhisperingRoots;

                    // "LifeTotems"
                    bool selectionsTrueLifeTotems =
                        selectionsTrueStandard &&
                        settings.RandomizeLifebloodCocoons &&
                        settings.RandomizeSoulTotems &&
                        settings.RandomizePalaceTotems;

                    // "Spoiler DAB" (Double Anti Bingo)
                    bool selectionsTrueSpoilerDAB =
                        selectionsTrueStandard &&
                        settings.RandomizeMaps &&
                        settings.RandomizeWhisperingRoots &&
                        settings.RandomizeLifebloodCocoons &&
                        settings.RandomizeSoulTotems;


                    // new age set - stuff added more recently
                    bool selectionsFalseNewAgeSet =
                        !settings.RandomizeLoreTablets &&
                        !settings.RandomizePalaceTablets &&
                        !settings.RandomizeGrimmkinFlames &&
                        !settings.RandomizeBossEssence &&
                        !settings.RandomizeBossGeo;

                    // junk set - stuff that Super adds to Standard
                    bool selectionsFalseJunkSet =
                        !settings.RandomizeMaps &&
                        !settings.RandomizeGrubs &&
                        !settings.RandomizeWhisperingRoots;

                    // "Super"
                    bool selectionsFalseSuper =
                        !settings.RandomizeRocks &&
                        !settings.RandomizeLifebloodCocoons &&
                        !settings.RandomizeSoulTotems &&
                        !settings.RandomizePalaceTotems &&
                        selectionsFalseNewAgeSet;

                    // "Standard"
                    bool selectionsFalseStandard =
                        selectionsFalseJunkSet &&
                        selectionsFalseSuper;

                    // "LifeTotems"
                    bool selectionsFalseLifeTotems =
                        selectionsFalseJunkSet &&
                        !settings.RandomizeRocks &&
                        selectionsFalseNewAgeSet;

                    // "Spoiler DAB"
                    bool selectionsFalseSpoilerDAB =
                        !settings.RandomizeGrubs &&
                        !settings.RandomizeRocks &&
                        !settings.RandomizePalaceTotems &&
                        selectionsFalseNewAgeSet;

                    bool presetStandard   = selectionsTrueStandard   && selectionsFalseStandard;
                    bool presetSuper      = selectionsTrueSuper      && selectionsFalseSuper;
                    bool presetLifeTotems = selectionsTrueLifeTotems && selectionsFalseLifeTotems;
                    bool presetSpoilerDAB = selectionsTrueSpoilerDAB && selectionsFalseSpoilerDAB;

                    // "EVERYTHING" in aggressive all-caps
                    bool presetEverything =
                        selectionsTrueSuper &&
                        selectionsTrueLifeTotems &&
                        selectionsTrueSpoilerDAB &&
                        settings.RandomizeRocks &&
                        settings.RandomizeLoreTablets &&
                        settings.RandomizeGrimmkinFlames &&
                        settings.RandomizeBossEssence &&
                        settings.RandomizeBossGeo;

                    SendMessage("seed", settings.Seed.ToString());
                    if (settings.AcidSkips && settings.FireballSkips && settings.MildSkips && settings.ShadeSkips && settings.SpikeTunnels && settings.DarkRooms && settings.SpicySkips)
                        SendMessage("mode", "Hard");
                    else if (!settings.AcidSkips && !settings.FireballSkips && !settings.MildSkips && !settings.ShadeSkips && !settings.SpikeTunnels && !settings.DarkRooms &&!settings.SpicySkips)
                        SendMessage("mode", "Easy");
                    else
                        SendMessage("mode", "Custom");

                    if (presetStandard)
                        msgText = "Standard";
                    else if (presetSuper)
                        msgText = "Super";
                    else if (presetLifeTotems)
                        msgText = "LifeTotems";
                    else if (presetSpoilerDAB)
                        msgText = "Spoiler DAB";
                    else if (presetEverything)
                        msgText = "EVERYTHING";
                    else
                        msgText = $"Custom";

                    SendMessage("preset", msgText.Trim());

                }
            }
            catch
            {
                SendMessage("randomizer", "false");
            }
            SendMessage("bench", PlayerData.instance.respawnScene.ToString());
        }


        public void NewGame()
        {
            if (State != WebSocketState.Open) return;
            GetRandom();
            SendMessage("NewSave", "true");
        }


        public void OnQuit()
        {
            if (State != WebSocketState.Open) return;
            SendMessage("GameExiting", "true");
        }

        public struct Row
        {
            // ReSharper disable once InconsistentNaming
            public string var { get; set; }
            // ReSharper disable once InconsistentNaming
            public object value { get; set; }

            public Row(string var, object value)
            {
                this.var = var;
                this.value = value;
            }

            public string ToJsonElementPair => " { \"var\" : \"" + var + "\",  \"value\" :  \"" + value + "\" }";
            public string ToJsonElement => $"\"{var}\" : \"{value}\"";
        }

    }
}
