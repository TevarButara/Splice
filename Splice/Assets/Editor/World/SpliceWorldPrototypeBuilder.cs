#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Splice.Base;
using Splice.Data;
using Splice.UI;
using Splice.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Splice.Editor.World
{
    public static class SpliceWorldPrototypeBuilder
    {
        private const string MapsFolder = "Assets/Maps";
        private const string SceneFolder = "Assets/=======SCENES";
        private const string TownMapPath = MapsFolder + "/TownMap_Default.asset";
        private const string WorldMapPath = MapsFolder + "/WorldMap_Default.asset";
        private const string ForestPath = MapsFolder + "/ForestZone_01.asset";
        private const string WorldScenePath = SceneFolder + "/WorldMap.unity";
        private const string ForestScenePath = SceneFolder + "/ForestZone.unity";

        [MenuItem("Splice/World/Rebuild World + Forest Prototype Scenes")]
        public static void RebuildAll()
        {
            if (!EditorUtility.DisplayDialog("Rebuild World Prototype?",
                    "This replaces WorldMap.unity and ForestZone.unity, refreshes their map assets, " +
                    "and updates the BuildZone Town Expansion panel. Existing hand edits in those generated " +
                    "objects will be overwritten.", "Rebuild", "Cancel")) return;
            BuildAllWithoutPrompt();
        }

        public static void BuildAllWithoutPrompt()
        {
            EnsureFolder("Assets", "Maps");
            var town = BuildTownDefinition();
            var world = BuildWorldDefinition();
            var forest = BuildForestDefinition();
            // Newly-created ScriptableObjects must enter AssetDatabase before scenes serialize references.
            // Without this import, the first bake after a clean checkout writes {fileID: 0}; the second bake
            // appears to fix it and hides the defect.
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TownMapPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(WorldMapPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ForestPath, ImportAssetOptions.ForceUpdate);
            town = AssetDatabase.LoadAssetAtPath<TownMapDefinitionSO>(TownMapPath);
            world = AssetDatabase.LoadAssetAtPath<WorldMapDefinitionSO>(WorldMapPath);
            forest = AssetDatabase.LoadAssetAtPath<ForestZoneDefinitionSO>(ForestPath);
            if (town == null || world == null || forest == null)
                throw new System.InvalidOperationException(
                    "Map definitions failed to import; scenes were not modified.");
            BuildWorldScene(world);
            BuildForestScene(forest);
            IntegrateBuildZone(town);
            EnsureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SpliceWorldBuilder] Town regions, WorldMap and ForestZone rebuilt successfully.");
        }

        private static TownMapDefinitionSO BuildTownDefinition()
        {
            var asset = LoadOrCreate<TownMapDefinitionSO>(TownMapPath);
            SetMapBase(asset, "town-default-v1", 1, MapGameMode.Town, "BuildZone",
                Vector3.zero, 90f);
            var serialized = new SerializedObject(asset);
            var regions = serialized.FindProperty("regions");
            var definitions = new[]
            {
                CreateRegion("core", "Town Core", 0, 0, true, 0, 0),
                CreateRegion("north", "North Ridge", 0, 40, false, 500, 20, "core"),
                CreateRegion("east", "East Quarter", 40, 0, false, 500, 20, "core"),
                CreateRegion("south", "South Gate", 0, -40, false, 700, 25, "core"),
                CreateRegion("west", "West Quarter", -40, 0, false, 700, 25, "core"),
                CreateRegion("outer-north", "Outer North", 0, 80, false, 1200, 30, "north"),
            };
            regions.arraySize = definitions.Length;
            for (var i = 0; i < definitions.Length; i++) WriteRegion(regions.GetArrayElementAtIndex(i),
                definitions[i]);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static WorldMapDefinitionSO BuildWorldDefinition()
        {
            var asset = LoadOrCreate<WorldMapDefinitionSO>(WorldMapPath);
            SetMapBase(asset, "world-default-v1", 1, MapGameMode.World, "WorldMap",
                Vector3.zero, 80f);
            var serialized = new SerializedObject(asset);
            var nodes = serialized.FindProperty("nodes");
            var definitions = new[]
            {
                new NodeData("town-home", "Your Town", WorldNodeKind.PlayerTown, -420, -60, "BuildZone", ""),
                new NodeData("forest-01", "Whispering Forest", WorldNodeKind.Forest, 0, 90, "ForestZone", "forest-01"),
                new NodeData("raid-search", "Raid Frontier", WorldNodeKind.RaidTarget, 420, -60, "BuildZone", ""),
            };
            nodes.arraySize = definitions.Length;
            for (var i = 0; i < definitions.Length; i++) WriteNode(nodes.GetArrayElementAtIndex(i),
                definitions[i]);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static ForestZoneDefinitionSO BuildForestDefinition()
        {
            var asset = LoadOrCreate<ForestZoneDefinitionSO>(ForestPath);
            SetMapBase(asset, "forest-01-v1", 1, MapGameMode.Forest, "ForestZone",
                Vector3.zero, 35f);
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("zoneId").stringValue = "forest-01";
            serialized.FindProperty("encounterDurationSeconds").intValue = 75;
            serialized.FindProperty("monsterCount").intValue = 6;
            serialized.FindProperty("fragmentDropMin").intValue = 15;
            serialized.FindProperty("fragmentDropMax").intValue = 25;
            serialized.FindProperty("fragmentsPerDiamond").intValue = 100;
            serialized.FindProperty("weeklyDiamondCap").intValue = 3;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void BuildWorldScene(WorldMapDefinitionSO definition)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "WorldMap";
            var camera = CreateCamera(scene, new Vector3(0f, 24f, -30f), new Vector3(35f, 0f, 0f), 55f);
            CreateLight(scene);
            var map = GameObject.CreatePrimitive(PrimitiveType.Plane);
            map.name = "World Map Surface";
            map.transform.localScale = new Vector3(7f, 1f, 4.5f);
            SceneManager.MoveGameObjectToScene(map, scene);

            CreateWorldMarker(scene, PrimitiveType.Cube, "Town Marker", new Vector3(-12f, 0.8f, 0f),
                new Vector3(4f, 1.5f, 4f));
            CreateWorldMarker(scene, PrimitiveType.Sphere, "Forest Marker", new Vector3(0f, 1.2f, 5f),
                new Vector3(4f, 3f, 4f));
            CreateWorldMarker(scene, PrimitiveType.Cylinder, "Raid Frontier Marker",
                new Vector3(12f, 0.9f, 0f), new Vector3(4f, 1.8f, 4f));

            var canvas = CreateCanvas(scene, "World Map UI");
            var title = CreateText(canvas.transform, "Title", "WORLD FRONTIER",
                new Vector2(0f, -55f), new Vector2(760f, 90f), 44, TextAlignmentOptions.Center);
            Anchor(title.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f));
            var player = CreateText(canvas.transform, "Player Summary", "", new Vector2(36f, -44f),
                new Vector2(580f, 90f), 22, TextAlignmentOptions.Left);
            Anchor(player.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f));
            var forestSummary = CreateText(canvas.transform, "Forest Summary", "",
                new Vector2(-36f, -44f), new Vector2(600f, 90f), 22, TextAlignmentOptions.Right);
            Anchor(forestSummary.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f));

            var town = CreateButton(canvas.transform, "Town Node", "YOUR TOWN\nBUILD • DEFEND",
                new Vector2(-430f, -80f), new Vector2(330f, 130f));
            var forest = CreateButton(canvas.transform, "Forest Node", "WHISPERING FOREST\nHUNT DIAMOND FRAGMENTS",
                new Vector2(0f, 110f), new Vector2(390f, 140f));
            var raid = CreateButton(canvas.transform, "Raid Node", "RAID FRONTIER\nSCOUT • ATTACK",
                new Vector2(430f, -80f), new Vector2(330f, 130f));
            Anchor(town.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            Anchor(forest.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            Anchor(raid.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            var footer = CreateText(canvas.transform, "Footer",
                "NODE MAP • SERVER MATCHMAKING • NO GPS / OPEN-WORLD SIMULATION",
                new Vector2(0f, 36f), new Vector2(1000f, 50f), 18, TextAlignmentOptions.Center);
            Anchor(footer.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f));

            var controller = canvas.gameObject.AddComponent<WorldMapController>();
            var so = new SerializedObject(controller);
            so.FindProperty("townButton").objectReferenceValue = town;
            so.FindProperty("forestButton").objectReferenceValue = forest;
            so.FindProperty("raidButton").objectReferenceValue = raid;
            so.FindProperty("playerSummary").objectReferenceValue = player;
            so.FindProperty("forestSummary").objectReferenceValue = forestSummary;
            so.ApplyModifiedPropertiesWithoutUndo();
            SetPrivateReference(controller, "definition", definition);
            Selection.activeObject = camera;
            EditorSceneManager.SaveScene(scene, WorldScenePath);
        }

        private static void BuildForestScene(ForestZoneDefinitionSO definition)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "ForestZone";
            var camera = CreateCamera(scene, new Vector3(0f, 19f, -18f), new Vector3(42f, 0f, 0f), 52f);
            CreateLight(scene);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Forest Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 3.2f);
            SceneManager.MoveGameObjectToScene(ground, scene);

            for (var i = 0; i < 14; i++)
            {
                var tree = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tree.name = $"Forest Prop Tree {i + 1:00}";
                var angle = i / 14f * Mathf.PI * 2f;
                tree.transform.position = new Vector3(Mathf.Sin(angle) * 17f, 1.5f,
                    Mathf.Cos(angle) * 12f + 3f);
                tree.transform.localScale = new Vector3(.8f, 3f, .8f);
                Object.DestroyImmediate(tree.GetComponent<Collider>());
                SceneManager.MoveGameObjectToScene(tree, scene);
            }

            var hero = CreateForestHero(scene, camera);
            var monsterRoot = new GameObject("Forest Monsters").transform;
            SceneManager.MoveGameObjectToScene(monsterRoot.gameObject, scene);
            var positions = new[]
            {
                new Vector3(-8f, 0f, 5f), new Vector3(-2f, 0f, 8f),
                new Vector3(5f, 0f, 7f), new Vector3(10f, 0f, 3f),
                new Vector3(4f, 0f, -3f), new Vector3(-5f, 0f, -5f),
            };
            for (var i = 0; i < positions.Length; i++)
                CreateForestMonster(scene, monsterRoot, positions[i], i + 101);

            var canvas = CreateCanvas(scene, "Forest HUD");
            var title = CreateText(canvas.transform, "Title", "WHISPERING FOREST • DIAMOND HUNT",
                new Vector2(0f, -38f), new Vector2(900f, 70f), 34, TextAlignmentOptions.Center);
            Anchor(title.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f));
            var timer = CreateText(canvas.transform, "Timer", "TIME 75",
                new Vector2(40f, -45f), new Vector2(260f, 65f), 30, TextAlignmentOptions.Left);
            Anchor(timer.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f));
            var loot = CreateText(canvas.transform, "Loot", "CARRIED 0 • HOSTILES 6",
                new Vector2(-40f, -45f), new Vector2(520f, 65f), 28, TextAlignmentOptions.Right);
            Anchor(loot.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f));
            var objective = CreateText(canvas.transform, "Objective",
                "MOVE WITH WASD OR CLICK GROUND • SPACE / ATTACK WHEN CLOSE • EXTRACT BEFORE TIMEOUT",
                new Vector2(0f, 90f), new Vector2(1300f, 65f), 21, TextAlignmentOptions.Center);
            Anchor(objective.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f));
            var attack = CreateButton(canvas.transform, "Attack Button", "ATTACK\n[SPACE]",
                new Vector2(-210f, 175f), new Vector2(260f, 100f));
            Anchor(attack.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f));
            var extract = CreateButton(canvas.transform, "Extract Button", "EXTRACT LOOT",
                new Vector2(-210f, 60f), new Vector2(260f, 90f));
            Anchor(extract.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f));
            var back = CreateButton(canvas.transform, "Return Button", "RETURN TO WORLD MAP",
                new Vector2(0f, 190f), new Vector2(380f, 90f));
            Anchor(back.GetComponent<RectTransform>(), new Vector2(.5f, 0f), new Vector2(.5f, 0f));

            var systems = new GameObject("Forest Encounter");
            SceneManager.MoveGameObjectToScene(systems, scene);
            var controller = systems.AddComponent<ForestEncounterController>();
            var so = new SerializedObject(controller);
            so.FindProperty("hero").objectReferenceValue = hero;
            so.FindProperty("monsterRoot").objectReferenceValue = monsterRoot;
            so.FindProperty("timerText").objectReferenceValue = timer;
            so.FindProperty("lootText").objectReferenceValue = loot;
            so.FindProperty("objectiveText").objectReferenceValue = objective;
            so.FindProperty("attackButton").objectReferenceValue = attack;
            so.FindProperty("extractButton").objectReferenceValue = extract;
            so.FindProperty("returnButton").objectReferenceValue = back;
            so.ApplyModifiedPropertiesWithoutUndo();
            SetPrivateReference(controller, "definition", definition);
            EditorSceneManager.SaveScene(scene, ForestScenePath);
        }

        private static ForestHeroController CreateForestHero(Scene scene, Camera camera)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "Player Hero";
            root.transform.position = new Vector3(0f, 1f, -8f);
            SceneManager.MoveGameObjectToScene(root, scene);
            var controller = root.AddComponent<ForestHeroController>();
            controller.MovementCamera = camera;
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.name = "Hero Blade";
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(.65f, 0f, .15f);
            child.transform.localScale = new Vector3(.15f, 1.4f, .2f);
            Object.DestroyImmediate(child.GetComponent<Collider>());
            return controller;
        }

        private static void CreateForestMonster(Scene scene, Transform parent, Vector3 position, int seed)
        {
            var monster = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            monster.name = $"Forest Raptor {seed - 100:00}";
            monster.transform.SetParent(parent);
            monster.transform.position = position + Vector3.up;
            monster.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
            var target = monster.AddComponent<ForestMonsterTarget>();
            var so = new SerializedObject(target);
            so.FindProperty("deterministicSeed").intValue = seed;
            so.FindProperty("fragmentDropMin").intValue = 15;
            so.FindProperty("fragmentDropMax").intValue = 25;
            so.ApplyModifiedPropertiesWithoutUndo();
            var beak = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beak.name = "Raptor Head";
            beak.transform.SetParent(monster.transform, false);
            beak.transform.localPosition = new Vector3(0f, .55f, .65f);
            beak.transform.localScale = new Vector3(.65f, .45f, .8f);
            Object.DestroyImmediate(beak.GetComponent<Collider>());
        }

        private static void IntegrateBuildZone(TownMapDefinitionSO definition)
        {
            var scene = EditorSceneManager.OpenScene(SceneFolder + "/BuildZone.unity", OpenSceneMode.Single);
            var manager = Object.FindFirstObjectByType<BaseBuildManager>();
            if (manager == null)
            {
                Debug.LogError("[SpliceWorldBuilder] BuildZone has no BaseBuildManager.");
                return;
            }
            SetPrivateReference(manager, "townMap", definition);

            var oldRoot = GameObject.Find("Town Expansion UI");
            if (oldRoot != null) Object.DestroyImmediate(oldRoot);
            var canvas = CreateCanvas(scene, "Town Expansion UI");
            canvas.sortingOrder = 180;
            var open = CreateButton(canvas.transform, "Open Expansion", "EXPAND TOWN",
                new Vector2(190f, 145f), new Vector2(260f, 78f));
            Anchor(open.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f));
            var panel = CreatePanel(canvas.transform, "Expansion Panel", new Vector2(0f, 0f),
                new Vector2(650f, 760f));
            Anchor(panel.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            CreateText(panel.transform, "Heading", "TOWN EXPANSION",
                new Vector2(0f, -58f), new Vector2(560f, 64f), 32, TextAlignmentOptions.Center);
            var status = CreateText(panel.transform, "Status", "MAP CONTRACT LOADING",
                new Vector2(0f, -120f), new Vector2(560f, 54f), 18, TextAlignmentOptions.Center);
            var close = CreateButton(panel.transform, "Close", "CLOSE", new Vector2(0f, 50f),
                new Vector2(250f, 70f));
            Anchor(close.GetComponent<RectTransform>(), new Vector2(.5f, 0f), new Vector2(.5f, 0f));
            var ids = new[] { "north", "east", "south", "west", "outer-north" };
            var views = new List<TownRegionPurchaseButtonView>();
            for (var i = 0; i < ids.Length; i++)
            {
                var button = CreateButton(panel.transform, "Region " + ids[i],
                    ids[i].ToUpperInvariant(), new Vector2(0f, 235f - i * 92f),
                    new Vector2(510f, 72f));
                var view = button.gameObject.AddComponent<TownRegionPurchaseButtonView>();
                var viewSo = new SerializedObject(view);
                viewSo.FindProperty("regionId").stringValue = ids[i];
                viewSo.FindProperty("button").objectReferenceValue = button;
                viewSo.FindProperty("label").objectReferenceValue =
                    button.GetComponentInChildren<TMP_Text>();
                viewSo.ApplyModifiedPropertiesWithoutUndo();
                views.Add(view);
            }
            var controller = canvas.gameObject.AddComponent<TownRegionPurchaseController>();
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("buildManager").objectReferenceValue = manager;
            controllerSo.FindProperty("panel").objectReferenceValue = panel.gameObject;
            controllerSo.FindProperty("openButton").objectReferenceValue = open;
            controllerSo.FindProperty("closeButton").objectReferenceValue = close;
            controllerSo.FindProperty("statusText").objectReferenceValue = status;
            var list = controllerSo.FindProperty("regionButtons");
            list.arraySize = views.Count;
            for (var i = 0; i < views.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
            controllerSo.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(true); // visible/editable in Editor; runtime Awake closes it.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static Camera CreateCamera(Scene scene, Vector3 position, Vector3 euler, float fieldOfView)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
            var camera = go.GetComponent<Camera>();
            camera.fieldOfView = fieldOfView;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(14, 25, 38, 255);
            SceneManager.MoveGameObjectToScene(go, scene);
            return camera;
        }

        private static void CreateLight(Scene scene)
        {
            var go = new GameObject("Directional Light", typeof(Light));
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            go.GetComponent<Light>().type = LightType.Directional;
            go.GetComponent<Light>().intensity = 1.2f;
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        private static void CreateWorldMarker(Scene scene, PrimitiveType primitive, string name,
            Vector3 position, Vector3 scale)
        {
            var marker = GameObject.CreatePrimitive(primitive);
            marker.name = name;
            marker.transform.position = position;
            marker.transform.localScale = scale;
            SceneManager.MoveGameObjectToScene(marker, scene);
        }

        private static Canvas CreateCanvas(Scene scene, string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(eventSystem, scene);
            }
            return canvas;
        }

        private static Image CreatePanel(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var image = go.GetComponent<Image>();
            image.color = new Color32(19, 34, 53, 245);
            return image;
        }

        private static Button CreateButton(Transform parent, string name, string text,
            Vector2 position, Vector2 size)
        {
            var panel = CreatePanel(parent, name, position, size);
            panel.color = new Color32(32, 93, 121, 245);
            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel;
            var label = CreateText(panel.transform, "Label", text, Vector2.zero,
                size - new Vector2(24f, 12f), 22, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return button;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string value,
            Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color32(238, 246, 252, 255);
            text.enableWordWrapping = true;
            return text;
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = (min + max) * .5f;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureBuildSettings()
        {
            var required = new[] { WorldScenePath, ForestScenePath };
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (var path in required)
            {
                var existing = scenes.FindIndex(scene => scene.path == path);
                if (existing >= 0) scenes[existing] = new EditorBuildSettingsScene(path, true);
                else scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SetMapBase(MapDefinitionSO asset, string id, int version,
            MapGameMode mode, string sceneName, Vector3 focus, float radius)
        {
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("mapId").stringValue = id;
            serialized.FindProperty("mapVersion").intValue = version;
            serialized.FindProperty("gameMode").enumValueIndex = (int)mode;
            serialized.FindProperty("sceneName").stringValue = sceneName;
            serialized.FindProperty("cameraFocus").vector3Value = focus;
            serialized.FindProperty("cameraRadius").floatValue = radius;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RegionData CreateRegion(string id, string name, float x, float z, bool initial,
            int cost, int capacity, params string[] prerequisites) =>
            new RegionData(id, name, new Vector2(x, z), new Vector2(40f, 40f), initial, cost, capacity,
                prerequisites);

        private static void WriteRegion(SerializedProperty property, RegionData region)
        {
            property.FindPropertyRelative("regionId").stringValue = region.Id;
            property.FindPropertyRelative("displayName").stringValue = region.Name;
            property.FindPropertyRelative("localCenter").vector2Value = region.Center;
            property.FindPropertyRelative("size").vector2Value = region.Size;
            property.FindPropertyRelative("initiallyUnlocked").boolValue = region.Initial;
            property.FindPropertyRelative("purchaseGoldCost").intValue = region.Cost;
            property.FindPropertyRelative("additionalDefenseCapacity").intValue = region.Capacity;
            var prerequisites = property.FindPropertyRelative("prerequisiteRegionIds");
            prerequisites.arraySize = region.Prerequisites.Length;
            for (var i = 0; i < region.Prerequisites.Length; i++)
                prerequisites.GetArrayElementAtIndex(i).stringValue = region.Prerequisites[i];
        }

        private static void WriteNode(SerializedProperty property, NodeData node)
        {
            property.FindPropertyRelative("nodeId").stringValue = node.Id;
            property.FindPropertyRelative("displayName").stringValue = node.Name;
            property.FindPropertyRelative("kind").enumValueIndex = (int)node.Kind;
            property.FindPropertyRelative("mapPosition").vector2Value = node.Position;
            property.FindPropertyRelative("destinationScene").stringValue = node.Scene;
            property.FindPropertyRelative("contentId").stringValue = node.ContentId;
            property.FindPropertyRelative("requiredPlayerLevel").intValue = 0;
            property.FindPropertyRelative("prerequisiteNodeIds").arraySize = 0;
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }

        private static void SetPrivateReference(Object target, string fieldName, Object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field == null)
                throw new System.MissingFieldException(target.GetType().FullName, fieldName);
            field.SetValue(target, value);
            EditorUtility.SetDirty(target);
        }

        private sealed class RegionData
        {
            public readonly string Id;
            public readonly string Name;
            public readonly Vector2 Center;
            public readonly Vector2 Size;
            public readonly bool Initial;
            public readonly int Cost;
            public readonly int Capacity;
            public readonly string[] Prerequisites;

            public RegionData(string id, string name, Vector2 center, Vector2 size, bool initial,
                int cost, int capacity, string[] prerequisites)
            {
                Id = id;
                Name = name;
                Center = center;
                Size = size;
                Initial = initial;
                Cost = cost;
                Capacity = capacity;
                Prerequisites = prerequisites;
            }
        }

        private sealed class NodeData
        {
            public readonly string Id;
            public readonly string Name;
            public readonly WorldNodeKind Kind;
            public readonly float X;
            public readonly float Y;
            public readonly string Scene;
            public readonly string ContentId;
            public Vector2 Position => new Vector2(X, Y);

            public NodeData(string id, string name, WorldNodeKind kind, float x, float y,
                string scene, string contentId)
            {
                Id = id;
                Name = name;
                Kind = kind;
                X = x;
                Y = y;
                Scene = scene;
                ContentId = contentId;
            }
        }
    }
}
#endif
