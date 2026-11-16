using System.Collections.Generic;
using DeadCargo.Cargo;
using DeadCargo.Heat;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityStandardAssets.CrossPlatformInput;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using VSX.UniversalVehicleCombat;
using VSX.UniversalVehicleCombat.Radar;

namespace DeadCargo.DevTesting
{
    public sealed class DevTestConsole : MonoBehaviour
    {
        public const string RangePath = "Assets/DeadCargo/DevTest/Dev Test Range.unity";

        private const KeyCode PanelKey = KeyCode.F6;
        private const KeyCode ResetKey = KeyCode.F7;

#if UNITY_EDITOR
        public const string MobilePath = "Assets/DeadCargo/DevTest/Dev Mobile Controls.unity";

        private const string DemoRoot = "Assets/SpaceCombatKit/SpaceCombat/Demos/Scenes/";
        private const string MainScenes = DemoRoot + "MainScenes/";
        private const string SimpleScenes = DemoRoot + "SimpleDemos/";

        private static readonly (string Label, string Path)[] Scenarios =
        {
            ("Test range", RangePath),
            ("Flight controls", SimpleScenes + "SCK_Basics_Flying.unity"),
            ("Weapons and module mounts", SimpleScenes + "SCK_Basics_Weapons.unity"),
            ("Carrier assault", MainScenes + "SCK_SpaceCombat.unity"),
            ("Capital ships", MainScenes + "SCK_CapitalShips.unity"),
            ("Enemy waves and warp", MainScenes + "SCK_EnemyWaves.unity"),
            ("Ship selection and loadouts", MainScenes + "SCK_Loadout.unity"),
            ("Demo main menu", MainScenes + "SCK_MainMenu.unity"),
            ("Mobile controls", MobilePath),
            ("Camera views", "Assets/SpaceCombatKit/Systems/Basics/CameraSystem/Demo/Scenes/DemoScene.unity"),
            ("Flat Kit shading", "Assets/FlatKit/Demos/[Demo] Fruit Vase/2-FruitVaseScene-CelShadingMode-Single.unity")
        };

#endif

        private static DevTestConsole instance;
        private readonly List<GeneralInput> suspendedInputs = new List<GeneralInput>();
        private readonly List<EventSystem> suspendedEventSystems = new List<EventSystem>();
        private DevTestRange range;
        private GameAgent panelPlayer;
        private bool showAtStart = true;
        private bool panelOpen;
#if UNITY_EDITOR
        private bool touchInputOverride;
#endif
        private AsyncOperation sceneLoad;
        private Vector2 scroll;
        private float previousTimeScale;
        private bool previousAudioPause;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLock;
        private string message;
        private GUIStyle headerStyle;
        private GUIStyle textStyle;
        private GUIStyle hintStyle;

        /// <summary>Creates the console once per play session. <paramref name="openPanel"/> opens it straight away.</summary>
        internal static void StartSession(bool openPanel)
        {
            if (instance != null)
                return;

            var root = new GameObject("Dev Test Console");
            instance = root.AddComponent<DevTestConsole>();
            instance.showAtStart = openPanel;
            DontDestroyOnLoad(root);
        }

        internal static void RestartRange()
        {
            if (instance != null)
                instance.LoadScenario(RangePath);
        }

        private void OnEnable()
        {
            instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
            range = FindFirstObjectByType<DevTestRange>();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ClosePanel();
#if UNITY_EDITOR
            RestoreInputMethod();
#endif
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            range = FindFirstObjectByType<DevTestRange>();
            scroll = Vector2.zero;
#if UNITY_EDITOR
            if (scene.path != MobilePath)
                RestoreInputMethod();
#endif

            // Some kit demos include an EventSystem both directly and through SceneObjects.
            var currentEventSystem = EventSystem.current;
            if (currentEventSystem == null)
                return;

            foreach (var eventSystem in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if (eventSystem != currentEventSystem && eventSystem.enabled)
                    eventSystem.enabled = false;
            }
        }

        private void Update()
        {
            if (sceneLoad != null)
            {
                if (!sceneLoad.isDone)
                    return;
                sceneLoad = null;
            }

            if (showAtStart || Input.GetKeyDown(PanelKey))
            {
                showAtStart = false;
                if (panelOpen)
                    ClosePanel();
                else
                    OpenPanel();
            }

            if (Input.GetKeyDown(ResetKey))
                LoadScenario(SceneManager.GetActiveScene().path);
        }

        private static GameAgent FindPlayer()
        {
            var manager = GameAgentManager.Instance;
            if (manager != null && manager.FocusedGameAgent != null && manager.FocusedGameAgent.IsPlayer)
                return manager.FocusedGameAgent;

            foreach (var agent in FindObjectsByType<GameAgent>(FindObjectsSortMode.None))
                if (agent.IsPlayer)
                    return agent;

            return null;
        }

        private void OpenPanel()
        {
            if (panelOpen)
                return;

            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            previousCursorVisible = Cursor.visible;
            previousCursorLock = Cursor.lockState;
            panelOpen = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var player = panelPlayer = FindPlayer();
            if (player != null)
            {
                foreach (var input in player.GetComponentsInChildren<GeneralInput>())
                {
                    if (!input.InputEnabled)
                        continue;

                    input.DisableInput();
                    suspendedInputs.Add(input);
                }
            }

            foreach (var eventSystem in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if (!eventSystem.enabled)
                    continue;

                eventSystem.enabled = false;
                suspendedEventSystems.Add(eventSystem);
            }
        }

        private void ClosePanel()
        {
            if (!panelOpen)
                return;

            panelOpen = false;
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;

            foreach (var input in suspendedInputs)
                if (input != null)
                    input.EnableInput();

            foreach (var eventSystem in suspendedEventSystems)
                if (eventSystem != null)
                    eventSystem.enabled = true;

            panelPlayer = null;
            suspendedInputs.Clear();
            suspendedEventSystems.Clear();
        }

#if UNITY_EDITOR
        private void RestoreInputMethod()
        {
            if (!touchInputOverride)
                return;

            touchInputOverride = false;
#if !UNITY_ANDROID && !UNITY_IOS
            CrossPlatformInputManager.SwitchActiveInputMethod(CrossPlatformInputManager.ActiveInputMethod.Hardware);
#endif
        }

#endif

        internal bool LoadScenario(string path)
        {
            if (sceneLoad != null && !sceneLoad.isDone)
                return false;

#if UNITY_EDITOR
            bool sceneAvailable = AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
#else
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(path);
            bool sceneAvailable = buildIndex >= 0;
#endif
            if (!sceneAvailable)
            {
                message = "Scene asset unavailable. Check the Unity Console.";
                Debug.LogWarning("Dev Test cannot find scene " + path, this);
                return false;
            }

            message = null;
            ClosePanel();
#if UNITY_EDITOR
            if (path == MobilePath)
            {
                touchInputOverride = true;
                CrossPlatformInputManager.SwitchActiveInputMethod(CrossPlatformInputManager.ActiveInputMethod.Touch);
            }

            sceneLoad = EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            sceneLoad = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
#endif
            return sceneLoad != null;
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (!panelOpen)
            {
                var hint = sceneLoad != null ? "Loading scenario…" : $"{PanelKey}  Dev Test     {ResetKey}  Reset scene";
                GUI.Label(new Rect(12, Screen.height - 34, 248, 23), hint, hintStyle);
                return;
            }

            var area = new Rect(12, 12, Mathf.Min(420, Screen.width - 24), Mathf.Min(680, Screen.height - 24));
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Dead Cargo Dev Test", headerStyle);
            if (GUILayout.Button($"Resume  {PanelKey}", GUILayout.Width(100), GUILayout.Height(26)))
                ClosePanel();
            GUILayout.EndHorizontal();

            GUILayout.Label(ScenarioLabel(), textStyle);
            if (!string.IsNullOrEmpty(message))
                GUILayout.Label(message, textStyle);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Player", GUI.skin.label);
            DrawPlayer(panelPlayer);
            DrawHeat();

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Reset scene  {ResetKey}", GUILayout.Height(26)))
                LoadScenario(SceneManager.GetActiveScene().path);
            if (range == null && GUILayout.Button("Return to range", GUILayout.Height(26)))
                LoadScenario(RangePath);
            GUILayout.EndHorizontal();

#if UNITY_EDITOR
            GUILayout.Space(12);
            GUILayout.Label("Scenarios", GUI.skin.label);
            var activePath = SceneManager.GetActiveScene().path;
            foreach (var scenario in Scenarios)
            {
                var previousEnabled = GUI.enabled;
                GUI.enabled = previousEnabled && scenario.Path != activePath;
                if (GUILayout.Button(scenario.Label, GUILayout.Height(25)))
                    LoadScenario(scenario.Path);
                GUI.enabled = previousEnabled;
            }
#endif
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawPlayer(GameAgent player)
        {
            var vehicle = player != null ? player.Vehicle : null;
            if (vehicle == null || vehicle.Destroyed)
            {
                GUILayout.Label(player == null ? "No player vehicle in this scenario." : "Player vehicle unavailable. Reset to retry.", textStyle);
                return;
            }

            var speed = vehicle.CachedRigidbody != null ? vehicle.CachedRigidbody.linearVelocity.magnitude : 0f;
            GUILayout.Label($"{vehicle.Label}   ·   {speed:0.0} m/s", textStyle);
            var health = vehicle.GetComponentInChildren<Health>();
            if (health != null)
            {
                foreach (var damageable in health.Damageables)
                {
                    if (damageable != null && damageable.HealthType != null)
                        GUILayout.Label($"{damageable.HealthType.name}   {damageable.CurrentHealth:0}/{damageable.HealthCapacity:0}", textStyle);
                }
            }

            var selector = vehicle.GetComponentInChildren<TargetSelector>();
            if (selector != null && selector.SelectedTarget != null)
                GUILayout.Label("Target   " + selector.SelectedTarget.Label, textStyle);

            var hold = vehicle.GetComponent<CargoHold>();
            if (hold != null)
                GUILayout.Label($"Cargo   {hold.TotalMass:0.#}/{hold.MassCapacity:0.#} t   ·   {hold.TotalValue:N0} cr", textStyle);

            GUILayout.BeginHorizontal();
            if (health != null && GUILayout.Button("Restore health", GUILayout.Height(26)))
                health.ResetHealth();
            if (hold != null && !hold.IsEmpty && GUILayout.Button("Empty hold", GUILayout.Height(26)))
                hold.Clear();
            var jettison = vehicle.GetComponent<CargoJettison>();
            if (jettison != null && jettison.CanJettison && GUILayout.Button("Jettison all", GUILayout.Height(26)))
                jettison.JettisonAll();
            if (range != null && range.CanBoardFighter(player) && GUILayout.Button("Board fighter", GUILayout.Height(26)))
            {
                ClosePanel();
                range.BoardFighter(player);
            }
            GUILayout.EndHorizontal();

            if (range != null)
            {
                GUILayout.Label("F boards or exits at vehicle prompts. In the fighter, L lands or launches.", textStyle);
                var previousEnabled = GUI.enabled;
                GUI.enabled = previousEnabled && range.CanSpawnEnemy;
                if (GUILayout.Button("Spawn AI fighter", GUILayout.Height(26)))
                {
                    range.SpawnEnemy();
                    ClosePanel();
                }
                GUI.enabled = previousEnabled;
            }
        }

        private void DrawHeat()
        {
            var heat = HeatTracker.Active;
            if (heat == null)
                return;

            GUILayout.Space(10);
            GUILayout.Label($"Heat   {heat.Heat:0}/{heat.MaxHeat:0}   ·   {heat.CurrentTier.Label}", textStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add 25 heat", GUILayout.Height(26)))
                heat.Add(25f);
            if (GUILayout.Button("Clear heat", GUILayout.Height(26)))
                heat.Clear();
            GUILayout.EndHorizontal();
        }

        private static string ScenarioLabel()
        {
            var scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            foreach (var scenario in Scenarios)
                if (scenario.Path == scene.path)
                    return scenario.Label;

#endif
            return scene.name;
        }

        private void EnsureStyles()
        {
            if (headerStyle != null)
                return;

            headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            hintStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
        }
    }
}
