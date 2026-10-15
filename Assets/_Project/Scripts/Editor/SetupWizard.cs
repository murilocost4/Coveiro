using System.Collections.Generic;
using System.IO;
using OCoveiro.Combat;
using OCoveiro.Core;
using OCoveiro.Data;
using OCoveiro.Network;
using OCoveiro.Player;
using OCoveiro.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace OCoveiro.EditorTools
{
    /// <summary>
    /// Menu "O Coveiro" na barra da Unity: monta pastas, dados, prefabs e a cena de teste
    /// automaticamente, para reduzir o trabalho de clicar e arrastar.
    /// Todos os itens podem ser rodados de novo: eles completam o que falta sem apagar o que existe.
    /// </summary>
    public static class SetupWizard
    {
        private const string Root = "Assets/_Project";
        private const string MaterialsFolder = Root + "/Art/Materials";
        private const string DataFolder = Root + "/Data";
        private const string PlayerPrefabPath = Root + "/Prefabs/Player.prefab";
        private const string DummyPrefabPath = Root + "/Prefabs/BonecoDeTreino.prefab";
        private const string TestScenePath = Root + "/Scenes/TesteRede.unity";
        private const string WeaponPath = DataFolder + "/Pa.asset";
        private const string StatsPath = DataFolder + "/AtributosJogador.asset";

        private static readonly string[] Folders =
        {
            Root + "/Scripts/Core", Root + "/Scripts/Player", Root + "/Scripts/Combat",
            Root + "/Scripts/AI", Root + "/Scripts/World", Root + "/Scripts/UI",
            Root + "/Scripts/Audio", Root + "/Scripts/Network", Root + "/Scripts/Data",
            Root + "/Data", Root + "/Prefabs", Root + "/Scenes",
            Root + "/Art/Materials", Root + "/Art/Shaders", Root + "/Art/VFX",
            Root + "/Audio", "Assets/ThirdParty",
        };

        // ================================================================ 1. Pastas

        [MenuItem("O Coveiro/1. Criar estrutura de pastas", priority = 1)]
        public static void CreateFolders()
        {
            foreach (var folder in Folders) EnsureFolder(folder);
            AssetDatabase.Refresh();
            Debug.Log("[O Coveiro] Estrutura de pastas pronta.");
        }

        // ================================================================ 2. Prefab do jogador

        [MenuItem("O Coveiro/2. Criar prefab do jogador", priority = 2)]
        public static GameObject CreatePlayerPrefab()
        {
            CreateFolders();

            var root = new GameObject("Player");

            var controller = root.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 1f, 0f);
            controller.height = 2f;
            controller.radius = 0.4f;

            root.AddComponent<NetworkObject>();

            var netTransform = root.AddComponent<OwnerNetworkTransform>();
            netTransform.SyncRotAngleX = false; // só gira no eixo Y
            netTransform.SyncRotAngleZ = false;
            netTransform.SyncScaleX = false;
            netTransform.SyncScaleY = false;
            netTransform.SyncScaleZ = false;

            root.AddComponent<PlayerInputReader>();
            root.AddComponent<PlayerController>();
            var appearance = root.AddComponent<PlayerAppearance>();

            // Visuais provisórios (cápsulas). Trocar pelos modelos mantendo os nomes dos objetos.
            var coveiro = CreatePlaceholderBody("Visual_Coveiro", root.transform,
                GetOrCreateMaterial("Coveiro", new Color(0.16f, 0.15f, 0.14f)), 1f);
            var aprendiz = CreatePlaceholderBody("Visual_Aprendiz", root.transform,
                GetOrCreateMaterial("Aprendiz", new Color(0.42f, 0.18f, 0.16f)), 0.9f);

            var lanternObject = new GameObject("Lanterna");
            lanternObject.transform.SetParent(root.transform, false);
            lanternObject.transform.localPosition = new Vector3(-0.45f, 1.1f, 0.35f); // mão esquerda; a pá fica na direita
            var lantern = lanternObject.AddComponent<Light>();
            lantern.type = LightType.Point;
            lantern.range = 7f;
            lantern.intensity = 3f;
            lantern.color = new Color(1f, 0.62f, 0.28f);
            lantern.shadows = LightShadows.Soft;

            SetRef(appearance, "coveiroVisual", coveiro);
            SetRef(appearance, "aprendizVisual", aprendiz);
            SetRef(appearance, "lanternLight", lantern);

            EnsurePlayerCombatSetup(root);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);

            Debug.Log($"[O Coveiro] Prefab do jogador salvo em {PlayerPrefabPath}");
            return prefab;
        }

        // ================================================================ 3. Cena de teste

        [MenuItem("O Coveiro/3. Criar cena de teste de rede", priority = 3)]
        public static void CreateTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null) playerPrefab = CreatePlayerPrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildAtmosphere();
            BuildArena();
            BuildCamera();
            BuildNetworkManager(playerPrefab);
            EnsureTrainingArea(GetOrCreateDummyPrefab());

            EditorSceneManager.SaveScene(scene, TestScenePath);
            AddSceneToBuild(TestScenePath);
            Debug.Log($"[O Coveiro] Cena de teste criada em {TestScenePath}. Aperte Play.");
        }

        // ================================================================ 4. Semana 2

        [MenuItem("O Coveiro/4. Atualizar para a Semana 2 (combate)", priority = 20)]
        public static void UpgradeToWeek2()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateFolders();

            // Prefab do jogador: completa sem apagar o que já existe (ex.: modelos trocados pelo Dev B).
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
            {
                CreatePlayerPrefab();
            }
            else
            {
                var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
                EnsurePlayerCombatSetup(root);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                PrefabUtility.UnloadPrefabContents(root);
            }

            var dummyPrefab = GetOrCreateDummyPrefab();

            // Cena de teste: bonecos + HUD.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) == null)
            {
                CreateTestScene();
                return;
            }

            var scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            EnsureTrainingArea(dummyPrefab);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[O Coveiro] Semana 2 pronta: combate no prefab, dados em _Project/Data, bonecos na cena. Aperte Play.");
        }

        /// <summary>Adiciona ao jogador tudo o que o combate precisa (só o que estiver faltando).</summary>
        private static void EnsurePlayerCombatSetup(GameObject root)
        {
            var weapon = GetOrCreateAsset<WeaponData>(WeaponPath);
            var stats = GetOrCreateAsset<PlayerStatsData>(StatsPath);

            Ensure<PlayerInputReader>(root);
            Ensure<PlayerController>(root);

            var health = Ensure<Health>(root);
            SetEnum(health, "team", (int)Team.Players);
            SetFloat(health, "maxHealth", stats.maxHealth);

            SetRef(Ensure<PlayerStamina>(root), "stats", stats);

            var lantern = Ensure<Lantern>(root);
            SetRef(lantern, "stats", stats);
            var lanternTransform = root.transform.Find("Lanterna");
            if (lanternTransform != null)
            {
                SetRef(lantern, "lanternLight", lanternTransform.GetComponent<Light>());
                // Prefab da semana 1 tinha a lanterna na mão direita; a pá vai para lá agora.
                var p = lanternTransform.localPosition;
                if (p.x > 0f) lanternTransform.localPosition = new Vector3(-p.x, p.y, p.z);
            }

            SetRef(Ensure<PlayerLife>(root), "stats", stats);

            var shovel = Ensure<ShovelVisual>(root);
            SetRef(shovel, "pivot", EnsureShovelModel(root.transform));

            var combat = Ensure<PlayerCombat>(root);
            SetRef(combat, "weapon", weapon);
            SetRef(combat, "stats", stats);
            SetRef(combat, "shovel", shovel);

            Ensure<HitFlash>(root);
        }

        /// <summary>Pá provisória: cabo + lâmina, presa num pivô no ombro direito.</summary>
        private static Transform EnsureShovelModel(Transform root)
        {
            var existing = root.Find("Pa");
            if (existing != null) return existing;

            var pivot = new GameObject("Pa").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(0.45f, 1.25f, 0.1f);
            pivot.localRotation = Quaternion.Euler(65f, 20f, 0f);

            CreatePart("Cabo", pivot, new Vector3(0f, 0f, 0.55f), new Vector3(0.07f, 0.07f, 1.1f),
                GetOrCreateMaterial("Madeira", new Color(0.33f, 0.22f, 0.13f)));
            CreatePart("Lamina", pivot, new Vector3(0f, 0f, 1.22f), new Vector3(0.34f, 0.04f, 0.36f),
                GetOrCreateMaterial("Ferro", new Color(0.46f, 0.47f, 0.5f)));
            return pivot;
        }

        // ================================================================ Boneco de treino

        private static GameObject GetOrCreateDummyPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
            if (existing != null) return existing;

            var root = new GameObject("BonecoDeTreino");
            root.AddComponent<NetworkObject>();

            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1f, 0f);
            collider.height = 2f;
            collider.radius = 0.45f;

            // O "Visual" fica com o pivô no pé, para o boneco balançar a partir da base.
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            var wood = GetOrCreateMaterial("Madeira", new Color(0.33f, 0.22f, 0.13f));
            var straw = GetOrCreateMaterial("Palha", new Color(0.55f, 0.46f, 0.26f));
            CreatePart("Poste", visual, new Vector3(0f, 0.8f, 0f), new Vector3(0.14f, 1.6f, 0.14f), wood);
            CreatePart("Saco", visual, new Vector3(0f, 1.3f, 0f), new Vector3(0.7f, 0.7f, 0.5f), straw, PrimitiveType.Capsule);
            CreatePart("Cabeca", visual, new Vector3(0f, 2.05f, 0f), new Vector3(0.42f, 0.42f, 0.42f), straw, PrimitiveType.Sphere);
            CreatePart("Braco", visual, new Vector3(0f, 1.55f, 0f), new Vector3(1.3f, 0.12f, 0.12f), wood);

            var health = root.AddComponent<Health>();
            SetEnum(health, "team", (int)Team.Enemies);
            SetFloat(health, "maxHealth", 300f);

            root.AddComponent<HitFlash>();
            SetRef(root.AddComponent<TrainingDummy>(), "visual", visual);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, DummyPrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[O Coveiro] Boneco de treino salvo em {DummyPrefabPath}");
            return prefab;
        }

        /// <summary>Coloca 3 bonecos e o HUD provisório na cena aberta, se ainda não estiverem lá.</summary>
        private static void EnsureTrainingArea(GameObject dummyPrefab)
        {
            if (Object.FindAnyObjectByType<TrainingDummy>() == null && dummyPrefab != null)
            {
                var spots = new[] { new Vector3(0f, 0f, 5f), new Vector3(-4f, 0f, 3f), new Vector3(4f, 0f, 3f) };
                for (int i = 0; i < spots.Length; i++)
                {
                    var dummy = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab);
                    dummy.name = $"BonecoDeTreino {i + 1}";
                    dummy.transform.position = spots[i];
                    dummy.transform.rotation = Quaternion.LookRotation(-spots[i].normalized, Vector3.up);
                }
            }

            if (Object.FindAnyObjectByType<DebugHUD>() == null)
                new GameObject("HUD (provisorio)").AddComponent<DebugHUD>();
        }

        // ================================================================ Cena

        private static void BuildAtmosphere()
        {
            var moon = new GameObject("Luz da Lua");
            var light = moon.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.62f, 0.8f);
            light.intensity = 0.25f;
            light.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.045f, 0.06f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.035f;
            RenderSettings.fogColor = new Color(0.03f, 0.035f, 0.045f);
        }

        private static void BuildArena()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Chao";
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
            ground.GetComponent<Renderer>().sharedMaterial =
                GetOrCreateMaterial("Chao", new Color(0.2f, 0.21f, 0.2f));

            var stone = GetOrCreateMaterial("Pedra", new Color(0.32f, 0.32f, 0.34f));
            var pillarPositions = new[]
            {
                new Vector3(6f, 0f, 6f), new Vector3(-6f, 0f, 6f),
                new Vector3(6f, 0f, -6f), new Vector3(-6f, 0f, -6f),
                new Vector3(0f, 0f, 11f),
            };
            foreach (var position in pillarPositions)
            {
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = "Pilar";
                pillar.transform.position = position + Vector3.up * 2f;
                pillar.transform.localScale = new Vector3(1.2f, 4f, 1.2f);
                pillar.GetComponent<Renderer>().sharedMaterial = stone;
            }

            // Uma vela: a luz quente indica o caminho (pilar de design "enxergar custa").
            var candle = new GameObject("Vela");
            candle.transform.position = new Vector3(0f, 1.2f, 9.8f);
            var candleLight = candle.AddComponent<Light>();
            candleLight.type = LightType.Point;
            candleLight.range = 5f;
            candleLight.intensity = 2f;
            candleLight.color = new Color(1f, 0.55f, 0.2f);
        }

        private static void BuildCamera()
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.fieldOfView = 35f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<IsoCamera>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(-8f, 14f, -8f), Quaternion.Euler(50f, 45f, 0f));
        }

        private static void BuildNetworkManager(GameObject playerPrefab)
        {
            var networkObject = new GameObject("NetworkManager");
            var networkManager = networkObject.AddComponent<NetworkManager>();
            var transport = networkObject.AddComponent<UnityTransport>();

            if (networkManager.NetworkConfig == null) networkManager.NetworkConfig = new NetworkConfig();
            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
            networkManager.NetworkConfig.ConnectionApproval = true;

            networkObject.AddComponent<ConnectionManager>();
            networkObject.AddComponent<ConnectionDebugUI>();
            EditorUtility.SetDirty(networkManager);
        }

        // ================================================================ utilitários

        private static T Ensure<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static void SetRef(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            var prop = serialized.FindProperty(property);
            if (prop == null)
            {
                Debug.LogWarning($"[O Coveiro] Campo '{property}' não encontrado em {target.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string property, float value)
        {
            var serialized = new SerializedObject(target);
            var prop = serialized.FindProperty(property);
            if (prop == null) return;
            prop.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object target, string property, int index)
        {
            var serialized = new SerializedObject(target);
            var prop = serialized.FindProperty(property);
            if (prop == null) return;
            prop.enumValueIndex = index;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreatePlaceholderBody(string name, Transform parent, Material material, float scale)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = name;
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0f, scale, 0f); // pé no chão
            body.transform.localScale = Vector3.one * scale;
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = material;

            // "Nariz" para enxergar para onde o personagem olha.
            var front = GameObject.CreatePrimitive(PrimitiveType.Cube);
            front.name = "Frente";
            front.transform.SetParent(body.transform, false);
            front.transform.localPosition = new Vector3(0f, 0.5f, 0.5f);
            front.transform.localScale = new Vector3(0.3f, 0.15f, 0.3f);
            Object.DestroyImmediate(front.GetComponent<Collider>());
            front.GetComponent<Renderer>().sharedMaterial = material;

            return body;
        }

        private static void CreatePart(string name, Transform parent, Vector3 position, Vector3 scale,
                                       Material material, PrimitiveType shape = PrimitiveType.Cube)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            EnsureFolder(MaterialsFolder);
            string path = $"{MaterialsFolder}/{name}.mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void AddSceneToBuild(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
