using System;
using uAdventure.Runner;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityFx.Async.Promises;
using uAdventure.Analytics;
using Simva;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xasu;
using Xasu.Auth.Protocols.OAuth2;
using Xasu.Auth.Protocols;
using Xasu.Config;
using Xasu.Requests;
using UnityFx.Async;

namespace uAdventure.Simva
{
    public class SimvaExtension : GameExtension, Interactuable, ISimvaBridge
    {
        public const string SIMVA_DISCLAIMER_ACCEPTED = "simva_disclaimer_accepted";

        private const bool AutoStart = true;
        private const bool ShowLoginOnStartup = true;

        public bool SaveAuthUntilCompleted = true;
        public bool RunGameIfSimvaIsNotConfigured = true;
        public bool ContinueOnQuit = true;
        public bool EnableLoginDemoButton = true;
        public string LanguageByDefault;
        public bool SaveDisclaimerAccepted = false;
        public bool BasicScormXAPIDataManagementByGame = false;
        public bool EnableDebugLogging = false;

        private string savedGameTarget;
        private bool wasAutoSave;
        private bool firstTimeDisabling = true;
        private OAuth2Token auth;
        private bool hasStartedGameplay;

        private Dictionary<string, string> languageDictionary;
        private Dictionary<string, string> defaultLanguageDictionary;
        private bool languageReady;
        private bool defaultLanguageReady;
        public bool IsLanguageReady { get { return languageReady && defaultLanguageReady; } }

        private IHttpRequestHandler requestHandler;
        public IHttpRequestHandler RequestHandler
        {
            get { return requestHandler ?? (requestHandler = new UnityRequestHandler()); }
            set { requestHandler = value; }
        }

        public void ApplySettings(SimvaPluginSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            SaveAuthUntilCompleted = settings.SaveAuthUntilCompleted;
            RunGameIfSimvaIsNotConfigured = settings.RunGameIfSimvaIsNotConfigured;
            ContinueOnQuit = settings.ContinueOnQuit;
            EnableLoginDemoButton = settings.EnableLoginDemoButton;
            LanguageByDefault = settings.LanguageByDefault;
            SaveDisclaimerAccepted = settings.SaveDisclaimerAccepted;
            BasicScormXAPIDataManagementByGame = settings.BasicScormXAPIDataManagementByGame;
            EnableDebugLogging = settings.EnableDebugLogging;
            SyncToHiddenPlugin();
        }

        [Priority(10)]
        public override IEnumerator OnAfterGameLoad()
        {
            SimvaManager.Instance.Bridge = this;
            var settings = Resources.Load<SimvaPluginSettings>("SimvaPluginSettings");
            if (settings != null)
            {
                ApplySettings(settings);
            }

            Log("[SIMVA] Starting...");
            if (SimvaConf.Local == null)
            {
                SimvaConf.Local = new SimvaConf();
                yield return StartCoroutine(SimvaConf.Local.LoadAsync());
                Log("[SIMVA] Conf Loaded...");
            }

            if (PlayerPrefs.HasKey(SIMVA_DISCLAIMER_ACCEPTED) && !SaveDisclaimerAccepted)
            {
                PlayerPrefs.DeleteKey(SIMVA_DISCLAIMER_ACCEPTED);
            }

            if (!SimvaManager.Instance.IsEnabled)
            {
                if (RunGameIfSimvaIsNotConfigured)
                {
                    Log("Simlet is not set! Running the game without Simva...");
                    SimvaManager.Instance.Bridge = this;
                    GetInstance<AnalyticsExtension>().AutoStart = false;
                    if (XasuTracker.Instance.Status.State == TrackerState.Uninitialized)
                    {
                        var config = new Xasu.Config.TrackerConfig
                        {
                            Offline = true,
                            TraceFormat = TraceFormats.XAPI,
                            FileName = "traces.log",
                            HomePage = "https://articoding/"
                        };
                        XasuTracker.Instance.Init(config, RequestHandler)
                            .ContinueWith(t => {
                                if (t.IsFaulted)
                                    LogWarning("Tracker fallback init failed: " + t.Exception);
                            }, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                    savedGameTarget = Game.Instance.GameState.CurrentTarget;
                }
                else
                {
                    Log("Simlet is not set! Stopping...");
                    if (Application.isEditor)
                    {
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#endif
                    }
                    else
                    {
                        Application.Quit();
                    }
                }
                yield break;
            }
            else if (SimvaManager.Instance.IsActive)
            {
                Log("[SIMVA] Simva is already started...");
                yield return null;
            }
            else
            {
                SimvaManager.Instance.Bridge = this;
                SyncToHiddenPlugin();
                Log("[SIMVA] Disabling tracker autostart...");
                GetInstance<AnalyticsExtension>().AutoStart = false;

                Log("[SIMVA] Adding scenes...");
                Game.Instance.GameState.Data.getChapters()[0].getObjects<SimvaScene>().AddRange(new SimvaScene[]
                {
                    new LoginScene(),
                    new SurveyScene(),
                    new FinalizeScene(),
                    new EndScene()
                });

                if (ShowLoginOnStartup && AutoStart)
                {
                    LoadLanguageDictionaries(LanguageByDefault);
                    Log("[SIMVA] Setting current target to Simva.Login...");
                    DisableAutoSave();
                    savedGameTarget = Game.Instance.GameState.CurrentTarget;
                    Game.Instance.GameState.CurrentTarget = "Simva.Login";
                }

                if (PlayerPrefs.HasKey("simva_auth") && SaveAuthUntilCompleted)
                {
                    var stored = JsonConvert.DeserializeObject<OAuth2Token>(PlayerPrefs.GetString("simva_auth"));
                    yield return new WaitForFixedUpdate();
                    stored.ClientId = "uadventure";
                    SimvaManager.Instance.LoginWithRefreshToken(stored.RefreshToken);
                }

                if (ContinueOnQuit)
                {
                    Application.wantsToQuit -= WantsToQuit;
                    Application.wantsToQuit += WantsToQuit;
                }
            }
        }

        private void DisableAutoSave()
        {
            if (firstTimeDisabling)
            {
                wasAutoSave = Game.Instance.GameState.Data.isAutoSave();
                firstTimeDisabling = false;
            }
            Game.Instance.GameState.Data.setAutoSave(false);
        }

        private void RestoreAutoSave()
        {
            firstTimeDisabling = true;
            Game.Instance.GameState.Data.setAutoSave(wasAutoSave);
        }

        public override void OnBeforeGameSave()
        {
            if (auth != null && SaveAuthUntilCompleted)
            {
                PlayerPrefs.SetString("simva_auth", JsonConvert.SerializeObject(auth));
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && auth != null && SaveAuthUntilCompleted)
            {
                PlayerPrefs.SetString("simva_auth", JsonConvert.SerializeObject(auth));
            }
        }

        public bool WantsToQuit()
        {
            if (SimvaManager.Instance.IsActive && hasStartedGameplay && !SimvaManager.Instance.Finalized)
            {
                SimvaManager.Instance.OnGameFinished();
                return false;
            }
            else
            {
                PlayerPrefs.DeleteKey("simva_auth");
            }
            return true;
        }

        [Priority(10)]
        public override IEnumerator OnGameFinished()
        {
            yield return new WaitWhile(() => Game.Instance.isSomethingRunning());
            if (SimvaManager.Instance.IsActive)
            {
                var readyToClose = false;
                SimvaManager.Instance.OnGameFinished()
                    .Then(() => readyToClose = true);

                yield return new WaitUntil(() => readyToClose);
            }
            else
            {
                yield return GetInstance<AnalyticsExtension>().OnGameFinished();
            }
        }

        public override IEnumerator OnGameReady()
        {
            if (PlayerPrefs.HasKey("simva_auth") && SaveAuthUntilCompleted)
            {
                var stored = JsonConvert.DeserializeObject<OAuth2Token>(PlayerPrefs.GetString("simva_auth"));
                stored.ClientId = "uadventure";
                SimvaManager.Instance.LoginWithRefreshToken(stored.RefreshToken);
            }
            else if (HasLoginInfo())
            {
                SimvaManager.Instance.ContinueLoginAndSchedule();
            }
            yield return null;
        }

        public override IEnumerator Restart()
        {
            yield return null;
        }


        public InteractuableResult Interacted(PointerEventData pointerData = null)
        {
            return InteractuableResult.IGNORES;
        }

        private static bool HasLoginInfo()
        {
            return false;// OpenIdUtility.HasLoginInfo();
        }

        public bool canBeInteracted()
        {
            return false;
        }

        public void setInteractuable(bool state)
        {
        }

        public void StartGameplay()
        {
            hasStartedGameplay = true;
            Game.Instance.AbortQuit();
            Log("Starting Gameplay");
            RunScene(savedGameTarget);
        }

        public void RunScene(string name)
        {
            Game.Instance.AbortQuit();
            var target = name == "Simva.Login.Demo" ? "Simva.Login" : name;
            switch (target)
            {
                case "Simva.Login":
                case "Simva.Survey":
                case "Simva.Finalize":
                case "Simva.End":
                    DisableAutoSave();
                    Game.Instance.RunTarget(target, null, false);
                    break;
                default:
                    RestoreAutoSave();
                    Game.Instance.RunTarget(target, null);
                    break;
            }
        }

        public IAsyncOperation StartTracker(Xasu.Config.TrackerConfig config, IAuthProtocol onlineProtocol, IAuthProtocol backupProtocol)
        {
            Log("Starting Tracker");
            var result = new AsyncCompletionSource();
            StartCoroutine(StartTrackerRoutine(config, onlineProtocol, backupProtocol, () => result.SetCompleted()));
            return result;
        }

        private IEnumerator StartTrackerRoutine(Xasu.Config.TrackerConfig config, IAuthProtocol onlineProtocol, IAuthProtocol backupProtocol, Action done)
        {
            yield return StartCoroutine(GetInstance<AnalyticsExtension>().StartTracker(config, onlineProtocol, backupProtocol));
            done();
        }

        public IAsyncOperation StopTracker()
        {
            Log("Stopping Tracker");
            var progress = new Progress<float>();
            progress.ProgressChanged += (_, p) =>
            {
                Debug.Log("Finalization progress: " + p);
            };
            XasuTracker.Instance.Finalize(progress)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        LogWarning("Tracker finalize failed: " + t.Exception);
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            return null;
        }

        public void OnAuthUpdated(OAuth2Token token)
        {
            auth = token;
            var hidden = GetHiddenPlugin();
            if (hidden != null)
            {
                hidden.OnAuthUpdated(token);
            }
        }

        public void Demo()
        {
            PreviewManager.Instance.InPreviewMode = true;
            Game.Instance.Restart();
            Game.Instance.GameState.Data.setAutoSave(false);
            Game.Instance.GameState.Data.setSaveOnSuspend(false);
            Game.Instance.RunTarget(Game.Instance.GameState.InitialChapterTarget.getId());
        }

        public void SetLanguageDictionary(Dictionary<string, string> dictionary, bool defaultDict)
        {
            if (defaultDict)
            {
                defaultLanguageDictionary = dictionary;
                defaultLanguageReady = true;
            }
            else
            {
                languageDictionary = dictionary;
                languageReady = true;
            }
            SyncToHiddenPlugin();
        }

        public string GetName(string objectName)
        {
            bool useDefault = false;
            if (languageDictionary == null || !languageDictionary.ContainsKey(objectName))
            {
                if (defaultLanguageDictionary != null && defaultLanguageDictionary.ContainsKey(objectName))
                {
                    useDefault = true;
                }
                else
                {
                    LogError("The sequence with key " + objectName + " doesn't exit (Object " + ")");
                    return null;
                }
            }
            var dictionary = useDefault ? defaultLanguageDictionary : languageDictionary;
            var newWord = dictionary[objectName];
            if (newWord.Contains("\\n"))
                newWord = newWord.Replace("\\n", "\n");

            Log(objectName + " : " + newWord);
            return newWord;
        }

        private void LoadLanguageDictionaries(string language)
        {
            var langCode = ExtractLangCode(language);
            if (string.IsNullOrEmpty(langCode))
            {
                return;
            }

            var jsonFiles = LoadLanguageJSON(langCode);
            SetLanguageDictionary(LoadDictionary(jsonFiles), false);
            SetLanguageDictionary(LoadDictionary(jsonFiles), true);
        }

        private static string ExtractLangCode(string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                return null;
            }

            if (language.Contains("["))
            {
                var start = language.IndexOf("[") + 1;
                var end = language.IndexOf("]", start);
                if (start > 0 && end > start)
                {
                    return language.Substring(start, end - start);
                }
            }
            return language;
        }

        private List<TextAsset> LoadLanguageJSON(string language)
        {
            Log("Loading Dictionaries directory (Localization/" + language + "/" + "Dictionaries)...");
            var filler = Resources.LoadAll("Localization/" + language + "/" + "Dictionaries", typeof(TextAsset));
            if (filler == null || filler.Length == 0)
            {
                LogError("No JSON Files in Dictionaries directory found (Localization/" + language + "/" + "Dictionaries) !");
            }

            var json = new List<TextAsset>();
            foreach (var file in filler)
            {
                json.Add((TextAsset)file);
            }
            return json;
        }

        private Dictionary<string, string> LoadDictionary(List<TextAsset> json)
        {
            var dictionary = new Dictionary<string, string>();
            foreach (var jsonFile in json)
            {
                var jObject = JObject.Parse(jsonFile.text);
                foreach (var entry in jObject)
                {
                    if (!dictionary.ContainsKey(entry.Key))
                    {
                        dictionary.Add(entry.Key, (string)entry.Value);
                    }
                }
            }
            return dictionary;
        }

        private global::Simva.SimvaPlugin GetHiddenPlugin()
        {
            try
            {
                return global::Simva.SimvaPlugin.Instance;
            }
            catch (Exception ex)
            {
                LogWarning("Hidden SimvaPlugin unavailable: " + ex.Message);
                return null;
            }
        }

        private void SyncToHiddenPlugin()
        {
            var hidden = GetHiddenPlugin();
            if (hidden == null)
            {
                return;
            }

            hidden.SaveAuthUntilCompleted = SaveAuthUntilCompleted;
            hidden.RunGameIfSimvaIsNotConfigured = RunGameIfSimvaIsNotConfigured;
            hidden.ContinueOnQuit = false;
            hidden.EnableLoginDemoButton = EnableLoginDemoButton;
            hidden.LanguageByDefault = LanguageByDefault;
            hidden.SaveDisclaimerAccepted = SaveDisclaimerAccepted;
            hidden.BasicScormXAPIDataManagementByGame = BasicScormXAPIDataManagementByGame;
            hidden.EnableDebugLogging = EnableDebugLogging;
            if (languageDictionary != null)
            {
                hidden.SetLanguageDictionary(new Dictionary<string, string>(languageDictionary), false);
            }
            if (defaultLanguageDictionary != null)
            {
                hidden.SetLanguageDictionary(new Dictionary<string, string>(defaultLanguageDictionary), true);
            }
        }

        internal void Log(string message)
        {
            if (EnableDebugLogging)
            {
                Debug.Log("[SimvaExtension] " + message);
            }
        }

        internal void LogWarning(string message)
        {
            if (EnableDebugLogging)
            {
                Debug.LogWarning("[SimvaExtension] " + message);
            }
        }

        internal void LogError(string message)
        {
            if (EnableDebugLogging)
            {
                Debug.LogError("[SimvaExtension] " + message);
            }
        }
    }
}
