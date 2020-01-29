﻿using HunterPie.GUIControls;
using HunterPie.Logger;
using HunterPie.Memory;
using HunterPie.Core;
using HunterPie.GUI;
using System;
using System.Windows;
using System.Windows.Input;
using System.IO;


namespace HunterPie {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class Hunterpie : Window {
        
        // Classes
        Game MonsterHunter = new Game();
        Presence Discord;
        Overlay GameOverlay;

        // HunterPie version
        const string HUNTERPIE_VERSION = "1.0.2.7";

        public Hunterpie() {
            InitializeComponent();
            OpenDebugger();
            // Initialize rich presence
            Discord = new Presence(MonsterHunter);
            // Initialize everything under this line
            UserSettings.InitializePlayerConfig();
            CheckIfUpdateEnableAndStart();
            // Updates version_text
            this.version_text.Content = $"Version: {HUNTERPIE_VERSION} ({UserSettings.PlayerConfig.HunterPie.Update.Branch})";
            Debugger.Warn("Initializing HunterPie!");
            GStrings.InitStrings();
            StartEverything();
        }

        private bool StartUpdateProcess() {
            if (!File.Exists("Update.exe")) return false;

            System.Diagnostics.Process UpdateProcess = new System.Diagnostics.Process();
            UpdateProcess.StartInfo.FileName = "Update.exe";
            UpdateProcess.StartInfo.Arguments = $"version={HUNTERPIE_VERSION} branch={UserSettings.PlayerConfig.HunterPie.Update.Branch}";
            UpdateProcess.Start();
            return true;
        }

        private void CheckIfUpdateEnableAndStart() {
            if (UserSettings.PlayerConfig.HunterPie.Update.Enabled) {
                bool justUpdated = false;
                bool latestVersion = false;
                string[] args = Environment.GetCommandLineArgs();
                foreach (string argument in args) {
                    if (argument.StartsWith("justUpdated")) {
                        string parsed = ParseArgs(argument);
                        justUpdated = parsed == "True";
                    }
                    if (argument.StartsWith("latestVersion")) {
                        string parsed = ParseArgs(argument);
                        latestVersion = parsed == "True";
                    }
                }
                if (justUpdated) {
                    OpenChangelog();
                    return;
                }
                if (latestVersion) {
                    return;
                }
                // This will update Update.exe
                AutoUpdate au = new AutoUpdate(UserSettings.PlayerConfig.HunterPie.Update.Branch);
                au.checkAutoUpdate();
                if (au.offlineMode) {
                    Debugger.Error("Failed to update HunterPie. Check if you're connected to the internet.");
                    Debugger.Warn("HunterPie is now in offline mode.");
                    Discord.SetOfflineMode();
                    return;
                }
                bool StartUpdate = StartUpdateProcess();
                if (StartUpdate) {
                    Environment.Exit(0);
                } else {
                    MessageBox.Show("Update.exe not found! Skipping auto-update...", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            } else {
                Debugger.Error("Auto-update is disabled. If your HunterPie has any issues or doesn't support the current game version, try re-enabling auto-update!");
            }
        }

        private string ParseArgs(string arg) {
            try {
                return arg.Split('=')[1];
            } catch {
                return "";
            }
        }

        private void StartEverything() {
            MonsterHunter.StartScanning();
            HookEvents();
            Scanner.StartScanning(); // Scans game memory
            GameOverlay = new Overlay(MonsterHunter);
            UserSettings.TriggerSettingsEvent();
            GameOverlay.Show();
        }

        /* Game events */
        private void HookEvents() {
            // Scanner events
            Scanner.OnGameStart += OnGameStart;
            Scanner.OnGameClosed += OnGameClose;
            // Game events
            MonsterHunter.Player.OnZoneChange += OnZoneChange;
            MonsterHunter.Player.OnCharacterLogin += OnLogin;
            // Settings
            UserSettings.OnSettingsUpdate += SendToOverlay;
        }

        private void UnhookEvents() {
            // Scanner events
            Scanner.OnGameStart -= OnGameStart;
            Scanner.OnGameClosed -= OnGameClose;
            // Game events
            MonsterHunter.Player.OnZoneChange -= OnZoneChange;
            MonsterHunter.Player.OnCharacterLogin -= OnLogin;
            // Settings
            UserSettings.OnSettingsUpdate -= SendToOverlay;
        }

        public void SendToOverlay(object source, EventArgs e) {
            GameOverlay.Dispatch(() => {
                GameOverlay.GlobalSettingsEventHandler(source, e);
            });
        }

        public void OnZoneChange(object source, EventArgs e) {
            Debugger.Log($"ZoneID: {MonsterHunter.Player.ZoneID}");
        }

        public void OnLogin(object source, EventArgs e) {
            Debugger.Log($"Logged on {MonsterHunter.Player.Name}");
        }

        public void OnGameStart(object source, EventArgs e) {
            if (Address.LoadMemoryMap(Scanner.GameVersion) || Scanner.GameVersion == Address.GAME_VERSION) {
                Debugger.Warn($"Loaded 'MonsterHunterWorld.{Scanner.GameVersion}.map'");
            } else {
                Debugger.Error($"Detected game version ({Scanner.GameVersion}) not mapped yet!");
                return;
            }
            
        }

        public void OnGameClose(object source, EventArgs e) {
            if (UserSettings.PlayerConfig.HunterPie.Options.CloseWhenGameCloses) {
                this.Close();
            }
        }

        /* Open sub windows */

        private void OpenDebugger() {
            ConsolePanel.Children.Clear();
            ConsolePanel.Children.Add(Debugger.Instance);
        }

        private void OpenSettings() {
            ConsolePanel.Children.Clear();
            ConsolePanel.Children.Add(Settings.Instance);
            Settings.RefreshSettingsUI();
        }

        private void OpenChangelog() {
            ConsolePanel.Children.Clear();
            ConsolePanel.Children.Add(Changelog.Instance);
        }

        /* Events */

        private void OnCloseWindowButtonClick(object sender, MouseButtonEventArgs e) {
            // X button function
            bool ExitConfirmation = MessageBox.Show("Are you sure you want to exit HunterPie?", "HunterPie", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            if (ExitConfirmation) {
                this.Close();
            }
        }

        private void OnWindowDrag(object sender, MouseButtonEventArgs e) {
            // When top bar is held by LMB
            this.DragMove();
        }

        private void OnMinimizeButtonClick(object sender, MouseButtonEventArgs e) {
            this.WindowState = WindowState.Minimized;
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e) {
            this.WindowState = WindowState.Minimized;
            try {
                // Stop Threads
                GameOverlay.Destroy();
                MonsterHunter.StopScanning();
                Scanner.StopScanning();
            } catch { }
            // Close stuff
            this.UnhookEvents();
            Environment.Exit(0);
        }

        private void OnGithubButtonClick(object sender, RoutedEventArgs e) {
            System.Diagnostics.Process.Start("https://github.com/Haato3o/HunterPie");
        }

        private void OnConsoleButtonClick(object sender, RoutedEventArgs e) {
            OpenDebugger();
        }

        private void OnSettingsButtonClick(object sender, RoutedEventArgs e) {
            OpenSettings();
        }

        private void OnChangelogButtonClick(object sender, RoutedEventArgs e) {
            OpenChangelog();
        }

        private void OnLaunchGameButtonClick(object sender, RoutedEventArgs e) {
            // Shorten the class name
            var launchOptions = UserSettings.PlayerConfig.HunterPie.Launch;

            if (launchOptions.GamePath == "") {
                if (MessageBox.Show("You haven't added the game path yet. Do you want to do it now?", "Monster Hunter World path not found", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes) {
                    OpenSettings();
                }
            } else {
                LaunchGame();
            }
        }

        private void LaunchGame() {
            try {
                System.Diagnostics.Process createGameProcess = new System.Diagnostics.Process();
                createGameProcess.StartInfo.FileName = UserSettings.PlayerConfig.HunterPie.Launch.GamePath;
                createGameProcess.StartInfo.Arguments = UserSettings.PlayerConfig.HunterPie.Launch.LaunchArgs;
                createGameProcess.Start();
            } catch {
                Debugger.Error("Failed to launch Monster Hunter World. Common reasons for this error are:\n- Wrong file path;");
            }
        }

    }
}