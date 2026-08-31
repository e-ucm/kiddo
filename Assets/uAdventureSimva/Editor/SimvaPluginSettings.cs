using System.Collections.Generic;
using UnityEngine;

namespace uAdventure.Simva
{
    [CreateAssetMenu(fileName = "SimvaPluginSettings", menuName = "uAdventure/Simva/Simva Plugin Settings")]
    public class SimvaPluginSettings : ScriptableObject
    {
        public bool SaveAuthUntilCompleted = true;
        public bool ShowLoginOnStartup = true;
        public bool RunGameIfSimvaIsNotConfigured = true;
        public bool ContinueOnQuit = true;
        public bool EnableLoginDemoButton = true;
        public bool EnableLanguageScene = true;
        public List<string> SelectedLanguages = new List<string>();
        public string LanguageByDefault;
        public bool AutoStart = true;
        public string StartScene;
        public string GamePlayScene;
        public bool SaveDisclaimerAccepted = false;
        public bool BasicScormXAPIDataManagementByGame = false;
        public bool EnableDebugLogging = false;
    }
}