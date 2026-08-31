using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using uAdventure.Core;
using uAdventure.Editor;
using Simva;

namespace uAdventure.Simva
{
    public class SimvaPluginConfigurationWindow : LayoutWindow
    {
        private SimvaPluginSettings settings;
        private string[] languageOptions;
        private Vector2 scrollPosition;

        public SimvaPluginConfigurationWindow(Rect rect, GUIContent content, GUIStyle style, params GUILayoutOption[] options)
            : base(rect, content, style, options)
        {
        }

        public override void Draw(int aID)
        {
            if (settings == null)
            {
                LoadSettings();
            }

            if (languageOptions == null || languageOptions.Length == 0)
            {
                LoadLanguageOptions();
            }

            using (var scope = new GUILayout.ScrollViewScope(scrollPosition, Options))
            {
                scrollPosition = scope.scrollPosition;
                EditorGUIUtility.labelWidth = Rect.width - 30;

                EditorGUILayout.LabelField("Simva Plugin Configuration", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                DrawGeneralSettings();
                DrawLanguageSettings();
                DrawSceneSettings();
                DrawAdvancedSettings();

                EditorGUILayout.Space();
                EditorGUILayout.Space();

                if (GUILayout.Button("Save Settings", GUILayout.Height(30)))
                {
                    SaveSettings();
                    EditorUtility.DisplayDialog("Simva", TC.get("Simva.SettingsSaved"), "OK");
                }
            }
        }

        private void DrawGeneralSettings()
        {
            EditorGUILayout.LabelField(TC.get("Simva.Tab.Configuration"), EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            settings.SaveAuthUntilCompleted = EditorGUILayout.Toggle(TC.get("Simva.SaveAuthUntilCompleted"), settings.SaveAuthUntilCompleted);
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            EditorGUI.BeginChangeCheck();
            settings.ShowLoginOnStartup = EditorGUILayout.Toggle(TC.get("Simva.ShowLoginOnStartup"), settings.ShowLoginOnStartup);
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            EditorGUI.BeginChangeCheck();
            settings.RunGameIfSimvaIsNotConfigured = EditorGUILayout.Toggle(TC.get("Simva.RunGameIfSimvaIsNotConfigured"), settings.RunGameIfSimvaIsNotConfigured);
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            EditorGUI.BeginChangeCheck();
            settings.ContinueOnQuit = EditorGUILayout.Toggle(TC.get("Simva.ContinueOnQuit"), settings.ContinueOnQuit);
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            EditorGUI.BeginChangeCheck();
            settings.EnableLoginDemoButton = EditorGUILayout.Toggle(TC.get("Simva.EnableLoginDemoButton"), settings.EnableLoginDemoButton);
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            EditorGUI.BeginChangeCheck();
            settings.EnableLanguageScene = EditorGUILayout.Toggle(TC.get("Simva.EnableLanguageScene"), settings.EnableLanguageScene);
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            EditorGUI.BeginChangeCheck();
            settings.SaveDisclaimerAccepted = EditorGUILayout.Toggle(TC.get("Simva.SaveDisclaimerAccepted"), settings.SaveDisclaimerAccepted);
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            EditorGUI.BeginChangeCheck();
            settings.BasicScormXAPIDataManagementByGame = EditorGUILayout.Toggle(TC.get("Simva.BasicScormXAPIDataManagementByGame"), settings.BasicScormXAPIDataManagementByGame);
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            EditorGUI.BeginChangeCheck();
            settings.EnableDebugLogging = EditorGUILayout.Toggle(TC.get("Simva.EnableDebugLogging"), settings.EnableDebugLogging);
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            EditorGUILayout.Space();
        }

        private void DrawLanguageSettings()
        {
            EditorGUILayout.LabelField(TC.get("Simva.SelectedLanguages"), EditorStyles.boldLabel);

            bool languageSceneEnabled = settings.EnableLanguageScene;
            
            using (new EditorGUI.DisabledGroupScope(!languageSceneEnabled))
            {
                EditorGUI.BeginChangeCheck();
                
                var newSelected = new List<string>();
                foreach (var lang in languageOptions)
                {
                    bool isSelected = settings.SelectedLanguages.Contains(lang);
                    bool newIsSelected = EditorGUILayout.ToggleLeft(lang, isSelected);
                    if (newIsSelected)
                    {
                        newSelected.Add(lang);
                    }
                }
                
                if (EditorGUI.EndChangeCheck())
                {
                    settings.SelectedLanguages = newSelected;
                    MarkDirty();
                }
            }

            EditorGUI.BeginChangeCheck();
            var currentIndex = string.IsNullOrEmpty(settings.LanguageByDefault) ? -1 : System.Array.IndexOf(languageOptions, settings.LanguageByDefault);
            if (currentIndex < 0 && languageOptions.Length > 0) currentIndex = 0;
            var newIndex = EditorGUILayout.Popup(TC.get("Simva.LanguageByDefault"), currentIndex, languageOptions);
            if (EditorGUI.EndChangeCheck())
            {
                settings.LanguageByDefault = languageOptions[newIndex];
                MarkDirty();
            }

            if (!languageSceneEnabled)
            {
                EditorGUILayout.HelpBox("Enable 'Enable Language Scene' to configure languages for the language selection scene.", MessageType.Info);
            }

            EditorGUILayout.Space();
        }

        private void DrawSceneSettings()
        {
            EditorGUILayout.LabelField("Scene Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            settings.AutoStart = EditorGUILayout.Toggle(TC.get("Simva.AutoStart"), settings.AutoStart);
            if (EditorGUI.EndChangeCheck()) MarkDirty();

            string[] sceneNames = GetSceneNames();
            int startSceneIndex = GetSceneIndex(sceneNames, settings.StartScene);
            int gameplaySceneIndex = GetSceneIndex(sceneNames, settings.GamePlayScene);

            using (new EditorGUI.DisabledGroupScope(settings.AutoStart))
            {
                EditorGUI.BeginChangeCheck();
                startSceneIndex = EditorGUILayout.Popup(TC.get("Simva.StartScene"), startSceneIndex, sceneNames);
                if (EditorGUI.EndChangeCheck())
                {
                    settings.StartScene = startSceneIndex >= 0 ? sceneNames[startSceneIndex] : "";
                    MarkDirty();
                }
            }

            EditorGUI.BeginChangeCheck();
            gameplaySceneIndex = EditorGUILayout.Popup(TC.get("Simva.GamePlayScene"), gameplaySceneIndex, sceneNames);
            if (EditorGUI.EndChangeCheck())
            {
                settings.GamePlayScene = gameplaySceneIndex >= 0 ? sceneNames[gameplaySceneIndex] : "";
                MarkDirty();
            }

            EditorGUILayout.Space();
        }

        private string[] GetSceneNames()
        {
            var scenes = UnityEditor.EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))
                .ToArray();
            return scenes;
        }

        private int GetSceneIndex(string[] sceneNames, string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return 0;
            var index = System.Array.IndexOf(sceneNames, sceneName);
            return index >= 0 ? index : 0;
        }

        private void DrawAdvancedSettings()
        {
            EditorGUILayout.LabelField("Advanced Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox("These settings affect runtime behavior. Modify with caution.", MessageType.Info);
            
            // Add any additional advanced settings here if needed
            EditorGUILayout.Space();
        }

        private void LoadSettings()
        {
            var guids = AssetDatabase.FindAssets("t:SimvaPluginSettings");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                settings = AssetDatabase.LoadAssetAtPath<SimvaPluginSettings>(path);
            }

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<SimvaPluginSettings>();
                var path = "Assets/Resources/SimvaPluginSettings.asset";
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                AssetDatabase.CreateAsset(settings, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private void SaveSettings()
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            ApplyToSimvaPlugin();
        }

        private void ApplyToSimvaPlugin()
        {
            if (!Application.isPlaying)
            {
                return;
            }
            
            var plugin = global::Simva.SimvaPlugin.Instance;
            if (plugin != null)
            {
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
                
                EditorUtility.SetDirty(plugin);
            }
        }

        private void LoadLanguageOptions()
        {
            TextAsset[] allLanguageMarkers = Resources.LoadAll<TextAsset>("Localization");

            Dictionary<string, string> languages = new Dictionary<string, string>();
            foreach (TextAsset asset in allLanguageMarkers)
            {
                if (asset.name == "lang")
                {
                    JObject jObject = JObject.Parse(asset.text);
                    var code = "";
                    var name = "";
                    foreach (var entry in jObject)
                    {
                        if (entry.Key == "code")
                        {
                            code = (string)entry.Value;
                        }
                        if (entry.Key == "displayName")
                        {
                            name = (string)entry.Value;
                        }
                    }
                    var modifName = name + " [" + code + "]";
                    if (!languages.ContainsKey(code))
                    {
                        languages.Add(code, modifName);
                    }
                }
            }
            languageOptions = languages.Values
                .Distinct()
                .OrderBy(n => n)
                .ToArray();

            if (languageOptions.Length > 0 && settings != null)
            {
                if (settings.SelectedLanguages.Count == 0)
                {
                    settings.SelectedLanguages.Add(languageOptions[0]);
                }
                if (string.IsNullOrEmpty(settings.LanguageByDefault))
                {
                    settings.LanguageByDefault = languageOptions[0];
                }
            }
        }

        private void MarkDirty()
        {
            if (settings != null)
            {
                EditorUtility.SetDirty(settings);
            }
        }
    }
}