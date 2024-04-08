﻿using Frosty.Core.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Frosty.Core
{

    public static class KyberSettings
    {
        public static string CliDirectory { get { return Config.Get("Kyber_CliDirectory", "", ConfigScope.Global); } set { Config.Add("Kyber_CliDirectory", value, ConfigScope.Global); } }
        public static string GameMode { get { return Config.Get("Kyber_SelectedMode", "Mode1", ConfigScope.Global); } set { Config.Add("Kyber_SelectedMode", value, ConfigScope.Global); } }
        public static string Level { get { return Config.Get("Kyber_SelectedLevel", "S6_2/Geonosis_02/Levels/Geonosis_02/Geonosis_02", ConfigScope.Global); } set { Config.Add("Kyber_SelectedLevel", value, ConfigScope.Global); } }
        public static int TeamId { get { return Config.Get("Kyber_TeamId", 1, ConfigScope.Global); } set { Config.Add("Kyber_TeamId", value, ConfigScope.Global); } }
        public static string AutoplayerType { get { return Config.Get("Kyber_AutoplayerType", "Gamemode Tied", ConfigScope.Global); } set { Config.Add("Kyber_AutoplayerType", value, ConfigScope.Global); } }
        public static int Team1Bots { get { return Config.Get("Kyber_Team1Bots", 20, ConfigScope.Global); } set { Config.Add("Kyber_Team1Bots", value, ConfigScope.Global); } }
        public static int Team2Bots { get { return Config.Get("Kyber_Team2Bots", 20, ConfigScope.Global); } set { Config.Add("Kyber_Team2Bots", value, ConfigScope.Global); } }
        public static string SelectedLoadOrder { get { return Config.Get("Kyber_SelectedLoadOrder", "No Order", ConfigScope.Global); } set { Config.Add("Kyber_SelectedLoadOrder", value, ConfigScope.Global); } }
        public static bool Autostart { get { return Config.Get("Kyber_AutoStart", false, ConfigScope.Global); } set { Config.Add("Kyber_AutoStart", value, ConfigScope.Global); } }
        public static List<string> LaunchCommands {
            get {
                string strList = Config.Get<string>("Kyber_LaunchCommands", null, ConfigScope.Global);
                if (strList == null)
                    return new List<string>();
                else
                    return strList.Split('$').ToList();
            }
            set {
                Config.Add("Kyber_LaunchCommands", string.Join("$", value.ToList()), ConfigScope.Global);
            }
        }
    }

    public class KyberJsonSettings
    {
        public List<KyberGamemodeJsonSettings> GamemodeOverrides { get; set; }
        public List<KyberLevelJsonSettings> LevelOverrides { get; set; }
        public List<KyberLoadOrderJsonSettings> LoadOrders { get; set; }
        public KyberJsonSettings()
        {

        }
    }
    public class KyberGamemodeJsonSettings
    {
        public string Name { get; set; }
        public string ModeId { get; set; }
        public int PlayerCount { get; set; }
        public KyberGamemodeJsonSettings()
        {

        }
    }
    public class KyberLevelJsonSettings
    {
        public string Name { get; set; }
        public string LevelId { get; set; }
        public List<string> ModeIds { get; set; }
        public KyberLevelJsonSettings()
        {

        }
    }
    public class KyberLoadOrderJsonSettings
    {
        public string Name { get; set; }
        public List<string> FbmodNames { get; set; }
        public KyberLoadOrderJsonSettings()
        {

        }
    }

    public class KyberModsJson
    {
        public string basePath { get; set; }
        public List<string> modPaths { get; set; }
    }
    public static class KyberIntegration
    {
        public static KyberJsonSettings GetKyberJsonSettings()
        {
            string jsonName = "Mods/Kyber/Overrides.json";
            if (!File.Exists(jsonName))
            {
                if (!Directory.Exists(Path.GetDirectoryName(jsonName)))
                    Directory.CreateDirectory(Path.GetDirectoryName(jsonName));
                App.Logger.Log("No Kyber Overrides.json file found, creating one with generic settings");
                KyberJsonSettings baseJsonSettings = new KyberJsonSettings();

                List<KyberGamemodeJsonSettings> baseModes = new List<KyberGamemodeJsonSettings>()
                {
                    new KyberGamemodeJsonSettings() { Name = "Modded Gamemode - Example Custom Mode", ModeId = "ExampleCustomMode", PlayerCount = 40},
                    new KyberGamemodeJsonSettings() { Name = "Modded Gamemode - Conquest Clone Wars", ModeId = "Conquest1CloneWars", PlayerCount = 64 },
                    new KyberGamemodeJsonSettings() { Name = "Modded Gamemode - Conquest Original Trilogy", ModeId = "Conquest1OriginalTrilogy", PlayerCount = 64 },
                    new KyberGamemodeJsonSettings() { Name = "Modded Gamemode - Conquest Sequel Trilogy", ModeId = "Conquest1SequelTrilogy", PlayerCount = 64 },
                    new KyberGamemodeJsonSettings() { Name = "Modded Gamemode - Conquest Order 66", ModeId = "Conquest1Order66", PlayerCount = 64 },
                    new KyberGamemodeJsonSettings() { Name = "Modded Gamemode - Conquest Map Specific", ModeId = "Conquest1MapSpecific", PlayerCount = 64 },
                    new KyberGamemodeJsonSettings() { Name = "Modded Gamemode - Conquest Sandbox", ModeId = "Conquest1Sandbox", PlayerCount = 64 },
                    new KyberGamemodeJsonSettings() { Name = "Modded Gamemode - Extraction HvsV", ModeId = "ExtractionHvsV", PlayerCount = 6 },
                    new KyberGamemodeJsonSettings() { Name = "Custom Arcade - Blast", ModeId = "SkirmishBlast", PlayerCount = 0},
                    new KyberGamemodeJsonSettings() { Name = "Custom Arcade - Onslaught", ModeId = "SkirmishOnslaught", PlayerCount = 0},
                    new KyberGamemodeJsonSettings() { Name = "Custom Arcade - Duel", ModeId = "SkirmishDuel", PlayerCount = 0},
                    new KyberGamemodeJsonSettings() { Name = "Custom Arcade - Starfighter Blast", ModeId = "SkirmishSpaceBlast", PlayerCount = 0},
                    new KyberGamemodeJsonSettings() { Name = "Custom Arcade - Starfighter Onslaught", ModeId = "SkirmishSpaceOnslaught" , PlayerCount = 0},
                    new KyberGamemodeJsonSettings() { Name = "Instant Action - Supremacy", ModeId = "Mode1", PlayerCount = 0},
                    new KyberGamemodeJsonSettings() { Name = "Instant Action - Missions Attack", ModeId = "ModeF", PlayerCount = 0},
                    new KyberGamemodeJsonSettings() { Name = "Instant Action - Missions Defend", ModeId = "ModeE", PlayerCount = 0},
                    new KyberGamemodeJsonSettings() { Name = "Multiplayer - Galactic Assault", ModeId = "PlanetaryBattles", PlayerCount = 40},
                    new KyberGamemodeJsonSettings() { Name = "Multiplayer - Supremacy", ModeId = "Mode1", PlayerCount = 64},
                    new KyberGamemodeJsonSettings() { Name = "Multiplayer - COOP Attack", ModeId = "Mode9", PlayerCount = 20},
                    new KyberGamemodeJsonSettings() { Name = "Multiplayer - COOP Defend", ModeId = "ModeDefend", PlayerCount = 20},
                    new KyberGamemodeJsonSettings() { Name = "Multiplayer - Ewok Hunt", ModeId = "Mode3", PlayerCount = 0},
                    new KyberGamemodeJsonSettings() { Name = "Multiplayer - Extraction", ModeId = "Mode5", PlayerCount = 16},
                    new KyberGamemodeJsonSettings() { Name = "Multiplayer - Hero Showdown", ModeId = "Mode6", PlayerCount = 4},
                    new KyberGamemodeJsonSettings() { Name = "Multiplayer - Starfighter HvsV", ModeId = "Mode7", PlayerCount = 6},
                    new KyberGamemodeJsonSettings() { Name = "Multiplayer - Jetpack Cargo", ModeId = "ModeC", PlayerCount = 16},
                    new KyberGamemodeJsonSettings() { Name = "Multiplayer - Strike", ModeId = "PlanetaryMissions", PlayerCount = 16},
                    new KyberGamemodeJsonSettings() { Name = "Multiplayer - Blast", ModeId = "Blast", PlayerCount = 16},
                    new KyberGamemodeJsonSettings() { Name = "Multiplayer - Heroes Versus Villains", ModeId = "HeroesVersusVillains", PlayerCount = 6},
                    new KyberGamemodeJsonSettings() { Name = "Multiplayer - Starfighter Assault", ModeId = "SpaceBattle", PlayerCount = 24},
                    new KyberGamemodeJsonSettings() { Name = "DO NOT USE - ModeX", ModeId = "ModeX", PlayerCount = 0},
                };
                baseJsonSettings.LoadOrders = new List<KyberLoadOrderJsonSettings>() { (new KyberLoadOrderJsonSettings() { FbmodNames = new List<string>() { "Instant Online Overhaul", "KyberMod" }, Name = "Example Load Order" }) };
                baseJsonSettings.GamemodeOverrides = baseModes;
                baseJsonSettings.LevelOverrides = new List<KyberLevelJsonSettings>() { (new KyberLevelJsonSettings() { Name = "Custom Level Example", LevelId = "Level/Directory/Goes/Here", ModeIds = new List<string>() { "Mode1", "Mode8" } }) };
                File.WriteAllText(jsonName, JsonConvert.SerializeObject(baseJsonSettings, new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented
                }));
            }
            return JsonConvert.DeserializeObject<KyberJsonSettings>(File.ReadAllText(jsonName));
        }

        public static bool DoesCliExist()
        {
            if (!File.Exists(KyberSettings.CliDirectory))
            {
                FrostyOpenFileDialog ofd = new FrostyOpenFileDialog("Set Kyber CLI", "*.exe (kyber_cli)|*.exe", "Kyber CLI");
                if (ofd.ShowDialog())
                {
                    if (Path.GetFileNameWithoutExtension(ofd.FileName) != "kyber_cli")
                    {
                        App.Logger.LogError($"Kyber Launcher:\tAborting Launch:\tCould not find kyber_cli.exe");
                        return false;
                    }
                    else
                    {
                        KyberSettings.CliDirectory = ofd.FileName;
                        Config.Save();
                    }
                }
                else
                {
                    App.Logger.LogError($"Kyber Launcher:\tAborting Launch:\tCould not find kyber_cli.exe");
                    return false;
                }
            }
            return true;
        }

        public static List<string> GetLoadOrder(string basePath, string editorModName = "KyberMod.fbmod")
        {
            List<string> fbmodNames = new List<string>();
            foreach (KyberLoadOrderJsonSettings loadOrder in KyberIntegration.GetKyberJsonSettings().LoadOrders.Where(order => order.Name == KyberSettings.SelectedLoadOrder))
            {
                foreach (string mod in loadOrder.FbmodNames)
                    fbmodNames.Add(mod.EndsWith(".fbmod") ? mod : $"{mod}.fbmod");
            }
            if (!fbmodNames.Contains(editorModName))
                fbmodNames.Add(editorModName);

            List<string> unfoundMods = new List<string>();
            foreach (string modName in new List<string>(fbmodNames))
            {
                if (!File.Exists($@"{basePath}/{modName}") && modName != editorModName)
                {
                    unfoundMods.Add(modName);
                    fbmodNames.Remove(modName);
                }
            }

            if (unfoundMods.Count > 0)
                App.Logger.LogError($"Kyber Launcher:\tCould not find following \"{KyberSettings.SelectedLoadOrder}\" load order mods:\t{string.Join(", \t", unfoundMods)}");

            return fbmodNames;
        }
    }
}
