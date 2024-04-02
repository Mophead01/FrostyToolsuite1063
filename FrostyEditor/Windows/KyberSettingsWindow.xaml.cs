using Frosty.Controls;
using System.Windows.Media.Imaging;
using System.IO;
using Frosty.Core;
using Frosty.Core.Mod;
using System.Collections.Generic;
using System;
using System.Linq;
using FrostySdk.Managers;
using FrostySdk.Ebx;
using System.Windows.Markup;
using Frosty.Core.Sdk.AnthemDemo;

namespace FrostyEditor.Windows
{
    public static class KyberSettings
    {
        public static string CliDirectory { get { return Config.Get("Kyber_CliDirectory", "", ConfigScope.Game); } set { Config.Add("Kyber_CliDirectory", value, ConfigScope.Game); } }
        public static string GameMode { get { return Config.Get("Kyber_SelectedMode", "Mode1", ConfigScope.Game); } set { Config.Add("Kyber_SelectedMode", value, ConfigScope.Game); } }
        public static string Level { get { return Config.Get("Kyber_SelectedLevel", "S6_2/Geonosis_02/Levels/Geonosis_02/Geonosis_02", ConfigScope.Game); } set { Config.Add("Kyber_SelectedLevel", value, ConfigScope.Game); } }
        public static int TeamId { get { return Config.Get("Kyber_TeamId", 1, ConfigScope.Game); } set { Config.Add("Kyber_TeamId", value, ConfigScope.Game); } }
        public static string AutoplayerType { get { return Config.Get("Kyber_AutoplayerType", "Gamemode Tied", ConfigScope.Game); } set { Config.Add("Kyber_AutoplayerType", value, ConfigScope.Game); } }
        public static int Team1Bots { get { return Config.Get("Kyber_Team1Bots", 20, ConfigScope.Game); } set { Config.Add("Kyber_Team1Bots", value, ConfigScope.Game); } }
        public static int Team2Bots { get { return Config.Get("Kyber_Team2Bots", 20, ConfigScope.Game); } set { Config.Add("Kyber_Team2Bots", value, ConfigScope.Game); } }
        public static string SelectedLoadOrder { get { return Config.Get("Kyber_SelectedLoadOrder", "No Order", ConfigScope.Game); } set { Config.Add("Kyber_SelectedLoadOrder", value, ConfigScope.Game); } }
        public static bool Autostart { get { return Config.Get("Kyber_AutoStart", false, ConfigScope.Game); } set { Config.Add("Kyber_AutoStart", false, ConfigScope.Game); } }
        public static List<string> LaunchCommands {
            get {
                string strList = Config.Get<string>("Kyber_LaunchCommands", null, ConfigScope.Game);
                if (strList == null)
                    return new List<string>();
                else
                    return strList.Split('$').ToList();
            }
            set {
                Config.Add("Kyber_LaunchCommands", string.Join("$", value.ToList()), ConfigScope.Game);
            }
        }
    }
    /// <summary>
    /// Interaction logic for KyberSettingsWindow.xaml
    /// </summary>
    public partial class KyberSettingsWindow : FrostyDockableWindow
    {
        List<(string, string, List<(string, string)>, int)> gameModesData = new List<(string, string, List<(string, string)>, int)> ();

        public KyberSettingsWindow(FrostyProject inProject = null)
        {
            InitializeComponent();

            Loaded += ModSettingsWindow_Loaded;
        }

        private string ConvertToTitleCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Convert the string to lowercase
            input = input.ToLower();

            // Capitalize the first letter of each word
            string[] words = input.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (!string.IsNullOrEmpty(words[i]))
                {
                    char[] letters = words[i].ToCharArray();
                    letters[0] = char.ToUpper(letters[0]);
                    words[i] = new string(letters);
                }
            }

            return string.Join(" ", words);
        }

        private void RepopulateLevelComboBox(int oldPlayerCount)
        {
            string curLevel = levelComboBox.Text;
            levelComboBox.Items.Clear();
            foreach((string, string) pair in gameModesData[gamemodeComboBox.SelectedIndex].Item3)
            {
                levelComboBox.Items.Add(pair.Item2);
                if (pair.Item2 == curLevel)
                    levelComboBox.SelectedIndex = levelComboBox.Items.Count - 1;
                else if (levelComboBox.Items.Count == 1)
                    levelComboBox.SelectedIndex = 0;
            }

            if ((Convert.ToInt32(Math.Floor((float)oldPlayerCount / 2) * 2) == team1AutoplayerCountComboBox.SelectedIndex + team2AutoplayerCountComboBox.SelectedIndex))
            {
                int newPlayerCount = gameModesData[gamemodeComboBox.SelectedIndex].Item4;
                int perTeamCount = Convert.ToInt32(Math.Floor((float)newPlayerCount / 2));
                team1AutoplayerCountComboBox.SelectedIndex = perTeamCount;
                team2AutoplayerCountComboBox.SelectedIndex = perTeamCount;
            }
            //else
            //{
            //    App.Logger.Log(oldPlayerCount.ToString());
            //    App.Logger.Log((team1AutoplayerCountComboBox.SelectedIndex + team2AutoplayerCountComboBox.SelectedIndex).ToString());
            //}
        }

        private void ModSettingsWindow_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            teamIdComboBox.Items.Add("TeamNeutral");
            for (int i = 0; i < 16; i++)
                teamIdComboBox.Items.Add($"Team{i + 1}");
            for (int i = 0; i < 33; i++)
            {
                team1AutoplayerCountComboBox.Items.Add((int)i);
                team2AutoplayerCountComboBox.Items.Add((int)i);
            }
            loadOrderComboBox.Items.Add("No Order");

            autoplayerTypeComboBox.Items.Add("No Bots");
            autoplayerTypeComboBox.Items.Add("Dummy Bots");
            autoplayerTypeComboBox.Items.Add("Gamemode Tied");

            autoStartComboBox.Items.Add("Disabled");
            autoStartComboBox.Items.Add("Enabled");

            //Fill Gamemodes/Levels
            EbxAssetEntry modesEntry = App.AssetManager.GetEbxEntry("UI/Data/GameModes/GameModes");
            dynamic modesRoot = App.AssetManager.GetEbx(modesEntry).RootObject;

            foreach (PointerRef modePr in modesRoot.GameModes)
            {
                EbxAssetEntry modeEntry = App.AssetManager.GetEbxEntry(modePr.External.FileGuid);
                if (modeEntry != null)
                {
                    dynamic modeRoot = App.AssetManager.GetEbx(modeEntry).RootObject;
                    string modeName = $"{ConvertToTitleCase(modeRoot.AurebeshGameModeName)} \t[{modeRoot.GameModeId}]";

                    List<(string, string)> levelPairs = new List<(string, string)>();
                    foreach(PointerRef levelPr in modeRoot.Levels)
                    {
                        EbxAssetEntry levEntry = App.AssetManager.GetEbxEntry(levelPr.External.FileGuid);
                        if (levEntry != null)
                        {
                            dynamic levRoot = App.AssetManager.GetEbx(levEntry).RootObject;
                            string aurabesh = ConvertToTitleCase(levRoot.LevelAurebesh.Internal.String);
                            if (aurabesh.StartsWith("Yavin") && aurabesh.EndsWith("4"))
                                aurabesh = "Yavin IV";
                            else if (aurabesh == "Death Star Ii")
                                aurabesh = "Death Star II";

                            string levName = $"{aurabesh} - {ConvertToTitleCase(LocalizedStringDatabase.Current.GetString((uint)levRoot.LevelName.Internal.StringHash))}";
                            levelPairs.Add((levRoot.LevelId, levName));
                        }
                    }
                    levelPairs = levelPairs.OrderBy(item => item.Item2).ToList();
                    List<string> duplicates = levelPairs.Select(item => item.Item2).GroupBy(x => x).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
                    for (int i = 0; i < levelPairs.Count; i++)
                    {
                        (string, string) pair = levelPairs[i];
                        if (duplicates.Contains(pair.Item2))
                            levelPairs[i] = (pair.Item1, $"{pair.Item2} [{pair.Item1}]");
                    }

                    gameModesData.Add((modeRoot.GameModeId, modeName, levelPairs, modeRoot.NumberOfPlayers));
                }
            }
            gameModesData = gameModesData.OrderBy(data => data.Item2).ToList();
            gameModesData.Insert(0, ("NOGAMEMODE", "Main Menu \t[FRONTEND]", new List<(string, string)>() { ("win32/Levels/Frontend/Frontend", "Frontend")}, 0));
            foreach (var item in gameModesData)
            {
                gamemodeComboBox.Items.Add(item.Item2);
            }
            //gamemodeComboBox.SelectedIndex = 0;

            //Set selected index
            kyberCliTextBox.Text = KyberSettings.CliDirectory;
            loadOrderComboBox.SelectedIndex = loadOrderComboBox.Items.Contains(KyberSettings.SelectedLoadOrder) ? loadOrderComboBox.Items.IndexOf(KyberSettings.SelectedLoadOrder) : 0;
            gamemodeComboBox.SelectedIndex = gameModesData.Select(data => data.Item1).Contains(KyberSettings.GameMode) ? gameModesData.Select(data => data.Item1).ToList().IndexOf(KyberSettings.GameMode) : 0;
            RepopulateLevelComboBox(0);
            levelComboBox.SelectedIndex = gameModesData[gamemodeComboBox.SelectedIndex].Item3.Select(data => data.Item1).Contains(KyberSettings.Level) ? gameModesData[gamemodeComboBox.SelectedIndex].Item3.Select(data => data.Item1).ToList().IndexOf(KyberSettings.Level) : 0;
            autoStartComboBox.SelectedIndex = KyberSettings.Autostart ? 1 : 0;
            teamIdComboBox.SelectedIndex = KyberSettings.TeamId;
            team1AutoplayerCountComboBox.SelectedIndex = KyberSettings.Team1Bots;
            team2AutoplayerCountComboBox.SelectedIndex = KyberSettings.Team2Bots;
            autoplayerTypeComboBox.SelectedIndex = autoplayerTypeComboBox.Items.Contains(KyberSettings.AutoplayerType) ? autoplayerTypeComboBox.Items.IndexOf(KyberSettings.AutoplayerType) : 0;
            launchCommandTextBox.Text = string.Join("\r\n", KyberSettings.LaunchCommands);

            lastGamemodeIndex = gamemodeComboBox.SelectedIndex;
        }
        private int lastGamemodeIndex = -1;

        private void modCategoryComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (lastGamemodeIndex != -1)
            {
                RepopulateLevelComboBox(gameModesData[lastGamemodeIndex].Item4);
                lastGamemodeIndex = gamemodeComboBox.SelectedIndex;
            }
        }

        private void cancelButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void saveButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
