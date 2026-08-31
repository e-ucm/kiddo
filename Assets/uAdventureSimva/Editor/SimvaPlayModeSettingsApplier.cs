using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Simva;

namespace uAdventure.Simva
{
    [InitializeOnLoad]
    public static class SimvaPlayModeSettingsApplier
    {
        static SimvaPlayModeSettingsApplier()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                ApplySettingsOnPlayModeEnter();
            }
        }

        private static void ApplySettingsOnPlayModeEnter()
        {
            var settingsPath = "Assets/Resources/SimvaPluginSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<SimvaPluginSettings>(settingsPath);
            
            if (settings == null)
            {
                return;
            }

            var plugin = global::Simva.SimvaPlugin.Instance;
            if (plugin == null)
            {
                return;
            }

            plugin.SaveAuthUntilCompleted = settings.SaveAuthUntilCompleted;
            plugin.ShowLoginOnStartup = settings.ShowLoginOnStartup;
            plugin.RunGameIfSimvaIsNotConfigured = settings.RunGameIfSimvaIsNotConfigured;
            plugin.ContinueOnQuit = settings.ContinueOnQuit;
            plugin.EnableLoginDemoButton = settings.EnableLoginDemoButton;
            plugin.EnableLanguageScene = settings.EnableLanguageScene;
            plugin.SelectedLanguages = new List<string>(settings.SelectedLanguages);
            plugin.LanguageByDefault = settings.LanguageByDefault;
            plugin.AutoStart = settings.AutoStart;
            plugin.StartScene = settings.StartScene;
            plugin.GamePlayScene = settings.GamePlayScene;
            plugin.SaveDisclaimerAccepted = settings.SaveDisclaimerAccepted;
            plugin.BasicScormXAPIDataManagementByGame = settings.BasicScormXAPIDataManagementByGame;
            plugin.EnableDebugLogging = settings.EnableDebugLogging;
        }
    }
}