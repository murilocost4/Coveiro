using System.Collections.Generic;
using System.IO;
using OCoveiro.AI;
using OCoveiro.Combat;
using OCoveiro.Core;
using OCoveiro.Data;
using OCoveiro.Network;
using OCoveiro.Player;
using OCoveiro.UI;
using OCoveiro.World;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
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

        // ================================================================ 5. Semana 3

        private const string EnemyDataFolder = DataFolder + "/Inimigos";
        private const string EnemyPrefabFolder = Root + "/Prefabs/Inimigos";

        [MenuItem("O Coveiro/5. Atualizar para a Semana 3 (inimigos)", priority = 21)]
        public static void UpgradeToWeek3()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // Garante que a semana 2 já foi aplicada.
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (player == null || player.GetComponent<PlayerCombat>() == null) UpgradeToWeek2();

            var errante = GetOrCreateEnemyPrefab("Errante", GetOrCreateEnemyData("Errante", d =>
            {
                d.displayName = "Errante";
                d.maxHealth = 30f; d.poise = 25f; d.staggerDuration = 0.7f;
                d.chaseSpeed = 2.2f; d.turnSpeed = 240f;
                d.detectionRadius = 8f; d.loseRadius = 14f; d.needsLineOfSight = true;
                d.attackRange = 1.7f; d.attackWindup = 0.9f; d.attackActive = 0.12f; d.attackRecovery = 0.8f;
                d.attackCooldown = 0.8f; d.attackDamage = 15f; d.attackReach = 2f; d.attackArc = 100f;
                d.attackKnockback = 5f; d.attackLunge = 2f;
            }), new Color(0.32f, 0.36f, 0.29f), ghost: false, size: 0.95f, hunch: 14f, eyes: false);

            var vigia = GetOrCreateEnemyPrefab("ErranteVigia", GetOrCreateEnemyData("ErranteVigia", d =>
            {
                d.displayName = "Errante Vigia";
                d.maxHealth = 45f; d.poise = 30f; d.staggerDuration = 0.6f;
                d.chaseSpeed = 2.6f; d.turnSpeed = 280f;
                d.detectionRadius = 10f; d.loseRadius = 16f; d.needsLineOfSight = true;
                d.attackRange = 1.8f; d.attackWindup = 0.8f; d.attackActive = 0.12f; d.attackRecovery = 0.7f;
                d.attackCooldown = 0.7f; d.attackDamage = 18f; d.attackReach = 2.1f; d.attackArc = 110f;
                d.attackKnockback = 5.5f; d.attackLunge = 2.2f;
                d.alertsOthers = true; d.alertRadius = 14f; d.alertDuration = 1.1f;
            }), new Color(0.22f, 0.22f, 0.25f), ghost: false, size: 1.1f, hunch: 4f, eyes: true);

            var espectro = GetOrCreateEnemyPrefab("Espectro", GetOrCreateEnemyData("Espectro", d =>
            {
                d.displayName = "Espectro";
                d.maxHealth = 20f; d.poise = 15f; d.staggerDuration = 0.5f;
                d.movement = EnemyMovement.Ghost;
                d.chaseSpeed = 3.2f; d.turnSpeed = 420f;
                d.detectionRadius = 9f; d.loseRadius = 15f; d.needsLineOfSight = false;
                d.attackRange = 1.6f; d.attackWindup = 0.55f; d.attackActive = 0.1f; d.attackRecovery = 0.6f;
                d.attackCooldown = 1f; d.attackDamage = 12f; d.attackReach = 1.9f; d.attackArc = 120f;
                d.attackKnockback = 3f; d.attackLunge = 3f;
                d.spectral = true; d.flankFromBehind = true;
            }), new Color(0.62f, 0.74f, 0.9f), ghost: true, size: 0.85f, hunch: 0f, eyes: false);

            var scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            EnsureNavMesh();
            EnsureEnemies(errante, vigia, espectro);
            EnsureSpectralDemo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[O Coveiro] Semana 3 pronta: 3 tipos de inimigo, NavMesh no chão e o nicho com a parede ilusória. Aperte Play.");
        }

        private static EnemyData GetOrCreateEnemyData(string fileName, System.Action<EnemyData> configure)
        {
            string path = $"{EnemyDataFolder}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (existing != null) return existing; // não sobrescreve ajustes feitos no Inspector

            EnsureFolder(EnemyDataFolder);
            var data = ScriptableObject.CreateInstance<EnemyData>();
            configure(data);
            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();
            return data;
        }

        private static GameObject GetOrCreateEnemyPrefab(string fileName, EnemyData data, Color color,
                                                         bool ghost, float size, float hunch, bool eyes)
        {
            string path = $"{EnemyPrefabFolder}/{fileName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            EnsureFolder(EnemyPrefabFolder);

            var root = new GameObject(fileName);
            root.AddComponent<NetworkObject>();

            var netTransform = root.AddComponent<NetworkTransform>(); // autoridade do host (padrão)
            netTransform.SyncRotAngleX = false;
            netTransform.SyncRotAngleZ = false;
            netTransform.SyncScaleX = false;
            netTransform.SyncScaleY = false;
            netTransform.SyncScaleZ = false;

            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, size, 0f);
            collider.height = 2f * size;
            collider.radius = 0.42f * size;
            collider.isTrigger = ghost; // o Espectro atravessa jogadores e paredes

            if (ghost)
            {
                root.AddComponent<GhostMotor>();
            }
            else
            {
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.42f * size;
                agent.height = 2f * size;
                agent.speed = data.chaseSpeed;
                agent.angularSpeed = 360f;
                agent.acceleration = 20f;
                agent.stoppingDistance = 0.2f;
                agent.enabled = false; // ligado só no host, pelo NavMeshMotor
                root.AddComponent<NavMeshMotor>();
            }

            var modifier = root.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true; // inimigo não vira buraco no NavMesh

            var health = root.AddComponent<Health>();
            SetEnum(health, "team", (int)Team.Enemies);
            SetFloat(health, "maxHealth", data.maxHealth);
            SetRef(root.AddComponent<EnemyBrain>(), "data", data);

            // Visual provisório: pivô no pé, para cair e cambalear a partir da base.
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = ghost ? new Vector3(0f, 0.25f, 0f) : Vector3.zero;
            visual.localRotation = Quaternion.Euler(hunch, 0f, 0f);

            var skin = ghost
                ? GetOrCreateMaterial($"Pele{fileName}", color, color * 0.6f)
                : GetOrCreateMaterial($"Pele{fileName}", color);
            CreatePart("Corpo", visual, new Vector3(0f, size, 0f), new Vector3(0.8f, size, 0.65f) * 1f, skin, PrimitiveType.Capsule);
            CreatePart("Cabeca", visual, new Vector3(0f, size * 2f + 0.05f, 0.08f), Vector3.one * 0.42f * size, skin, PrimitiveType.Sphere);

            if (eyes)
            {
                var eyeMaterial = GetOrCreateMaterial("OlhoVigia", new Color(1f, 0.15f, 0.1f), new Color(2f, 0.2f, 0.1f));
                CreatePart("Olho E", visual, new Vector3(-0.09f, size * 2f + 0.1f, 0.24f), Vector3.one * 0.08f, eyeMaterial, PrimitiveType.Sphere);
                CreatePart("Olho D", visual, new Vector3(0.09f, size * 2f + 0.1f, 0.24f), Vector3.one * 0.08f, eyeMaterial, PrimitiveType.Sphere);
            }

            var arm = new GameObject("BracoPivo").transform;
            arm.SetParent(visual, false);
            arm.localPosition = new Vector3(0.42f * size, size * 1.55f, 0.05f);
            arm.localRotation = Quaternion.Euler(60f, 0f, 0f);
            CreatePart("Braco", arm, new Vector3(0f, 0f, 0.45f * size), new Vector3(0.16f, 0.16f, 0.9f * size), skin);

            var look = root.AddComponent<EnemyVisual>();
            SetRef(look, "body", visual);
            SetRef(look, "arm", arm);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[O Coveiro] Inimigo salvo em {path}");
            return prefab;
        }

        /// <summary>NavMesh gerado ao abrir a cena, a partir dos colliders do chão e das paredes.</summary>
        private static void EnsureNavMesh()
        {
            if (Object.FindAnyObjectByType<NavMeshSurface>() != null) return;

            var ground = GameObject.Find("Chao");
            if (ground == null)
            {
                Debug.LogWarning("[O Coveiro] Objeto 'Chao' não encontrado; o NavMesh não foi configurado.");
                return;
            }

            var surface = ground.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            ground.AddComponent<RuntimeNavMeshBaker>();
        }

        private static void EnsureEnemies(GameObject errante, GameObject vigia, GameObject espectro)
        {
            if (Object.FindAnyObjectByType<EnemyBrain>() != null) return;

            // Na raiz da cena: objetos de rede não devem ficar dentro de pastas que não são de rede.
            Place(errante, "Errante 1", new Vector3(-9f, 0f, 15f));
            Place(errante, "Errante 2", new Vector3(7f, 0f, 17f));
            Place(vigia, "Errante Vigia", new Vector3(0f, 0f, 21f));
            Place(espectro, "Espectro 1", new Vector3(-14f, 0f, 24f));
            Place(espectro, "Espectro 2", new Vector3(15f, 0f, 9f));
        }

        private static void Place(GameObject prefab, string instanceName, Vector3 position)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = instanceName;
            instance.transform.position = position;
            Vector3 toCenter = -position;
            toCenter.y = 0f;
            instance.transform.rotation = Quaternion.LookRotation(toCenter.normalized, Vector3.up);
        }

        /// <summary>Nicho de pedra fechado por uma parede ilusória + uma runa no chão que só aparece na luz.</summary>
        private static void EnsureSpectralDemo()
        {
            if (Object.FindAnyObjectByType<SpectralObject>() != null) return;

            var stone = GetOrCreateMaterial("Pedra", new Color(0.32f, 0.32f, 0.34f));
            var ghostStone = GetOrCreateMaterial("PedraIlusoria", new Color(0.3f, 0.3f, 0.34f));
            var rune = GetOrCreateMaterial("Runa", new Color(0.55f, 0.85f, 1f), new Color(0.6f, 1.4f, 2f));
            var relic = GetOrCreateMaterial("Reliquia", new Color(1f, 0.8f, 0.4f), new Color(1.5f, 1f, 0.3f));

            var root = new GameObject("Demo Espectral").transform;
            root.position = new Vector3(18f, 0f, -10f);

            CreateSolid("Parede Fundo", root, new Vector3(3f, 1.5f, 0f), new Vector3(0.6f, 3f, 4.6f), stone);
            CreateSolid("Parede Norte", root, new Vector3(1.5f, 1.5f, 2f), new Vector3(3.6f, 3f, 0.6f), stone);
            CreateSolid("Parede Sul", root, new Vector3(1.5f, 1.5f, -2f), new Vector3(3.6f, 3f, 0.6f), stone);
            CreatePart("Reliquia", root, new Vector3(2f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0.5f), relic);

            // Parede ilusória: sólida no escuro, some quando a lanterna chega perto.
            var illusory = CreateSolid("Parede Ilusoria", root, new Vector3(0f, 1.5f, 0f), new Vector3(0.6f, 3f, 3.4f), ghostStone);
            SetEnum(illusory.AddComponent<SpectralObject>(), "mode", (int)SpectralMode.VanishesInLight);
            illusory.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            // Runa no chão: só aparece na luz e aponta o segredo.
            var mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mark.name = "Runa Espectral";
            mark.transform.SetParent(root, false);
            mark.transform.localPosition = new Vector3(-4f, 0.02f, 0f);
            mark.transform.localScale = new Vector3(1.4f, 0.01f, 1.4f);
            Object.DestroyImmediate(mark.GetComponent<Collider>());
            mark.GetComponent<Renderer>().sharedMaterial = rune;
            SetEnum(mark.AddComponent<SpectralObject>(), "mode", (int)SpectralMode.AppearsInLight);
        }

        private static GameObject CreateSolid(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
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

        private static Material GetOrCreateMaterial(string name, Color color, Color? emission = null)
        {
            EnsureFolder(MaterialsFolder);
            string path = $"{MaterialsFolder}/{name}.mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (emission.HasValue)
            {
                // Brilho próprio: aparece mesmo no escuro (Espectro, runas, olhos do Vigia).
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", emission.Value);
            }
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
