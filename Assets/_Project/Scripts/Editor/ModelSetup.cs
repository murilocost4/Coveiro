using System.Collections.Generic;
using System.IO;
using System.Linq;
using OCoveiro.AI;
using OCoveiro.Core;
using OCoveiro.Data;
using OCoveiro.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace OCoveiro.EditorTools
{
    /// <summary>
    /// Menu "O Coveiro / 6. Aplicar modelos KayKit":
    /// 1. Garante que os modelos foram importados com as configurações certas (KayKitImporter).
    /// 2. Monta dois Animator Controllers: Jogador e MortoVivo (esqueletos).
    /// 3. Troca as cápsulas do jogador e as formas dos inimigos pelos modelos, sem mexer no resto do prefab.
    /// Pode ser rodado de novo: refaz os controllers e pula o que já foi trocado.
    /// </summary>
    public static class ModelSetup
    {
        private const string KayKit = "Assets/ThirdParty/KayKit";
        private const string ControllerFolder = "Assets/_Project/Animation";
        private const string PlayerControllerPath = ControllerFolder + "/Jogador.controller";
        private const string UndeadControllerPath = ControllerFolder + "/MortoVivo.controller";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string EnemyPrefabFolder = "Assets/_Project/Prefabs/Inimigos";
        private const string GhostMaterialPath = "Assets/_Project/Art/Materials/EspectroFantasma.mat";

        private static readonly string[] PlayerActions =
        {
            ShovelVisual.LightAttack1, ShovelVisual.LightAttack2, ShovelVisual.LightAttack3, ShovelVisual.HeavyAttack,
            ShovelVisual.Dodge, ShovelVisual.Drink, ShovelVisual.Hit, ShovelVisual.Death, ShovelVisual.StandUp,
            "Hit_B", "Interact", "PickUp", "Dig", "Digging", "Cheering",
        };

        private static readonly string[] UndeadActions =
        {
            "Melee_1H_Attack_Chop", "Melee_2H_Attack_Chop", "Melee_1H_Attack_Slice_Diagonal",
            "Melee_Unarmed_Attack_Punch_A", "Skeletons_Taunt", "Hit_A", "Hit_B",
            "Skeletons_Death", "Skeletons_Awaken_Floor", "Skeletons_Spawn_Ground",
        };

        /// <summary>Ações que terminam numa pose mantida (caído, morto).</summary>
        private static readonly Dictionary<string, string> HoldPoses = new Dictionary<string, string>
        {
            { "Death_A", "Death_A_Pose" },
            { "Skeletons_Death", "Skeletons_Death_Pose" },
        };

        [MenuItem("O Coveiro/6. Aplicar modelos KayKit", priority = 40)]
        public static void Apply()
        {
            if (!AssetDatabase.IsValidFolder(KayKit))
            {
                Debug.LogError($"[O Coveiro] Pasta {KayKit} não encontrada.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ReimportIfNeeded();

            var clips = LoadClips();
            if (clips.Count == 0)
            {
                Debug.LogError("[O Coveiro] Nenhuma animação encontrada em " + KayKit + "/Animations.");
                return;
            }

            var playerController = BuildController(PlayerControllerPath, clips,
                "Idle_A", "Walking_A", "Running_A", 1.8f, 5f, PlayerActions);
            var undeadController = BuildController(UndeadControllerPath, clips,
                "Skeletons_Idle", "Skeletons_Walking", "Running_A", 1.5f, 4f, UndeadActions);

            ApplyToPlayer(playerController);
            ApplyToEnemy("Errante", "Skeletons/Skeleton_Minion.fbx", 1.75f, "Skeleton_Blade", "Melee_1H_Attack_Chop", false, undeadController);
            ApplyToEnemy("ErranteVigia", "Skeletons/Skeleton_Warrior.fbx", 1.95f, "Skeleton_Axe", "Melee_2H_Attack_Chop", false, undeadController);
            ApplyToEnemy("Espectro", "Skeletons/Skeleton_Mage.fbx", 1.8f, null, "Melee_1H_Attack_Slice_Diagonal", true, undeadController);

            AssetDatabase.SaveAssets();
            Debug.Log("[O Coveiro] Modelos KayKit aplicados: Coveiro (Rogue_Hooded), Aprendiz (Ranger), " +
                      "Errante (Minion), Vigia (Warrior), Espectro (Mage). Aperte Play.");
        }

        // ================================================================ importação

        private static void ReimportIfNeeded()
        {
            var folders = new[] { KayKit + "/Characters", KayKit + "/Skeletons", KayKit + "/Animations" };
            foreach (var guid in AssetDatabase.FindAssets("t:Model", folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;

                bool weapon = path.Contains("/Weapons/");
                bool needs = weapon
                    ? importer.animationType != ModelImporterAnimationType.None
                    : importer.animationType != ModelImporterAnimationType.Generic || !importer.bakeAxisConversion;
                if (path.Contains("/Animations/")) needs |= importer.clipAnimations.Length == 0;

                if (needs) importer.SaveAndReimport(); // o KayKitImporter aplica as configurações
            }
        }

        private static Dictionary<string, AnimationClip> LoadClips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { KayKit + "/Animations" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                    if (!clip.name.StartsWith("__preview__") && !clips.ContainsKey(clip.name)) clips[clip.name] = clip;
            }
            return clips;
        }

        // ================================================================ Animator Controllers

        private static AnimatorController BuildController(string path, Dictionary<string, AnimationClip> clips,
            string idle, string walk, string run, float walkSpeed, float runSpeed, IEnumerable<string> actions)
        {
            EnsureFolder(ControllerFolder);

            // Reaproveita o mesmo arquivo (mesmo GUID), para os prefabs não perderem a referência.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
                             ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states.ToArray()) machine.RemoveState(child.state);
            foreach (var parameter in controller.parameters.ToArray()) controller.RemoveParameter(parameter);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "ActionSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f,
            });

            // Andar: mistura parado / andando / correndo pela velocidade real (parâmetro Speed).
            var tree = new BlendTree
            {
                name = "Locomotion",
                blendParameter = "Speed",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            AddChild(tree, clips, idle, 0f);
            AddChild(tree, clips, walk, walkSpeed);
            AddChild(tree, clips, run, runSpeed);

            var locomotion = machine.AddState("Locomotion");
            locomotion.motion = tree;
            machine.defaultState = locomotion;

            // Ações: cada uma é um estado; a velocidade vem do parâmetro ActionSpeed (ajuste de tempo).
            foreach (string name in actions)
            {
                if (!clips.TryGetValue(name, out var clip))
                {
                    Debug.LogWarning($"[O Coveiro] Animação '{name}' não encontrada.");
                    continue;
                }

                var state = machine.AddState(name);
                state.motion = clip;
                state.speedParameterActive = true;
                state.speedParameter = "ActionSpeed";

                if (HoldPoses.TryGetValue(name, out string poseName) && clips.TryGetValue(poseName, out var poseClip))
                {
                    var pose = machine.AddState(poseName);
                    pose.motion = poseClip;
                    var toPose = state.AddTransition(pose);
                    toPose.hasExitTime = true;
                    toPose.exitTime = 0.98f;
                    toPose.duration = 0.05f;
                }
                else if (!KayKitImporter.IsLoop(name))
                {
                    var back = state.AddTransition(locomotion);
                    back.hasExitTime = true;
                    back.exitTime = 0.92f;
                    back.hasFixedDuration = true;
                    back.duration = 0.15f;
                }
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddChild(BlendTree tree, Dictionary<string, AnimationClip> clips, string name, float threshold)
        {
            if (clips.TryGetValue(name, out var clip)) tree.AddChild(clip, threshold);
            else Debug.LogWarning($"[O Coveiro] Animação '{name}' não encontrada para a locomoção.");
        }

        // ================================================================ jogador

        private static void ApplyToPlayer(RuntimeAnimatorController controller)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                ReplaceBody(root.transform.Find("Visual_Coveiro"), "Characters/Rogue_Hooded.fbx", 1.9f, controller);
                ReplaceBody(root.transform.Find("Visual_Aprendiz"), "Characters/Ranger.fbx", 1.75f, controller);

                Ensure<CharacterAnimator>(root);

                // A pá vai para a mão direita; a lanterna, para a esquerda.
                var shovel = FindDeep(root.transform, "Pa");
                if (shovel != null) EnsureAttachment(root, shovel, "handslot.r", Vector3.zero, new Vector3(-90f, 0f, 0f));

                var lantern = FindDeep(root.transform, "Lanterna");
                if (lantern != null)
                {
                    EnsureLanternModel(lantern);
                    EnsureAttachment(root, lantern, "handslot.l", new Vector3(0f, -0.12f, 0f), Vector3.zero);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log("[O Coveiro] Jogador atualizado com os modelos.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ReplaceBody(Transform visual, string modelPath, float height, RuntimeAnimatorController controller)
        {
            if (visual == null) return;

            var existing = visual.GetComponentInChildren<Animator>(true);
            if (existing != null)
            {
                existing.runtimeAnimatorController = controller; // só atualiza o controller
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{KayKit}/{modelPath}");
            if (asset == null)
            {
                Debug.LogWarning($"[O Coveiro] Modelo {modelPath} não encontrado.");
                return;
            }

            // Tira a cápsula provisória.
            var meshRenderer = visual.GetComponent<MeshRenderer>();
            if (meshRenderer != null) Object.DestroyImmediate(meshRenderer);
            var meshFilter = visual.GetComponent<MeshFilter>();
            if (meshFilter != null) Object.DestroyImmediate(meshFilter);
            for (int i = visual.childCount - 1; i >= 0; i--) Object.DestroyImmediate(visual.GetChild(i).gameObject);
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, visual);
            model.name = "Modelo";
            FitModel(model.transform, height);
            SetupAnimator(model, controller);
        }

        private static void EnsureLanternModel(Transform lantern)
        {
            if (lantern.Find("ModeloLanterna") != null) return;

            var asset = FindModel("lantern_hanging", KayKit + "/Halloween") ?? FindModel("lantern", KayKit);
            if (asset == null) return;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, lantern);
            model.name = "ModeloLanterna";
            FitModel(model.transform, 0.35f, alignFeet: false, alignForward: false);
            model.transform.localPosition -= Vector3.up * 0.2f; // a luz fica dentro da lanterna
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = ShadowCastingMode.Off; // senão a lanterna faz sombra da própria luz
        }

        // ================================================================ inimigos

        private static void ApplyToEnemy(string prefabName, string modelPath, float height, string weaponName,
            string attackAnimation, bool ghost, RuntimeAnimatorController controller)
        {
            string path = $"{EnemyPrefabFolder}/{prefabName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = root.transform.Find("Visual");
                if (visual == null) return;

                var existing = visual.GetComponentInChildren<Animator>(true);
                if (existing != null)
                {
                    existing.runtimeAnimatorController = controller;
                }
                else
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{KayKit}/{modelPath}");
                    if (asset == null)
                    {
                        Debug.LogWarning($"[O Coveiro] Modelo {modelPath} não encontrado.");
                        return;
                    }

                    // Tira as formas provisórias (corpo, cabeça, braço, olhos).
                    for (int i = visual.childCount - 1; i >= 0; i--) Object.DestroyImmediate(visual.GetChild(i).gameObject);
                    visual.localRotation = Quaternion.identity;

                    var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, visual);
                    model.name = "Modelo";
                    FitModel(model.transform, height);
                    SetupAnimator(model, controller);

                    if (!string.IsNullOrEmpty(weaponName))
                    {
                        var weaponAsset = FindModel(weaponName, KayKit + "/Skeletons/Weapons");
                        var hand = FindDeep(model.transform, "handslot.r");
                        if (weaponAsset != null && hand != null)
                        {
                            var weapon = (GameObject)PrefabUtility.InstantiatePrefab(weaponAsset, hand);
                            weapon.name = "Arma";
                            weapon.transform.localPosition = Vector3.zero;
                            weapon.transform.localRotation = Quaternion.identity;
                        }
                    }

                    if (ghost)
                    {
                        var material = GetGhostMaterial();
                        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                        {
                            r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
                            r.shadowCastingMode = ShadowCastingMode.Off;
                        }
                    }
                }

                Ensure<CharacterAnimator>(root);

                var look = root.GetComponent<EnemyVisual>();
                if (look != null)
                {
                    SetRef(look, "body", visual);
                    SetRef(look, "arm", null);
                }

                // Animação de ataque no EnemyData (o impacto é sincronizado com o aviso).
                var brain = root.GetComponent<EnemyBrain>();
                if (brain != null && brain.Data != null)
                {
                    brain.Data.attackAnimation = attackAnimation;
                    EditorUtility.SetDirty(brain.Data);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[O Coveiro] {prefabName} atualizado com o modelo {Path.GetFileNameWithoutExtension(modelPath)}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Material translúcido e brilhante do Espectro.</summary>
        private static Material GetGhostMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(GhostMaterialPath);
            if (existing != null) return existing;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(KayKit + "/Skeletons/skeleton_texture.png");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", new Color(0.65f, 0.8f, 1f, 0.45f));

            // Transparência (URP Lit, modo "Transparent / Alpha").
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_SrcBlendAlpha")) material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (material.HasProperty("_DstBlendAlpha")) material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;

            // Brilho próprio azulado.
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetTexture("_EmissionMap", texture);
            material.SetColor("_EmissionColor", new Color(0.3f, 0.55f, 1.1f));

            EnsureFolder(Path.GetDirectoryName(GhostMaterialPath)?.Replace('\\', '/'));
            AssetDatabase.CreateAsset(material, GhostMaterialPath);
            return material;
        }

        // ================================================================ utilitários

        private static void SetupAnimator(GameObject model, RuntimeAnimatorController controller)
        {
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        /// <summary>
        /// Encaixa o modelo: vira para a frente do prefab (usando a direção do pé até os dedos),
        /// ajusta a altura e apoia os pés no chão.
        /// </summary>
        private static void FitModel(Transform model, float height, bool alignFeet = true, bool alignForward = true)
        {
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one;

            if (alignForward)
            {
                Vector3 forward = Vector3.zero;
                foreach (string side in new[] { "l", "r" })
                {
                    var foot = FindDeep(model, "foot." + side);
                    var toes = FindDeep(model, "toes." + side);
                    if (foot != null && toes != null) forward += toes.position - foot.position;
                }
                forward.y = 0f;
                if (forward.sqrMagnitude > 1e-6f && model.parent != null)
                {
                    float yaw = Vector3.SignedAngle(forward, model.parent.forward, Vector3.up);
                    model.localRotation = Quaternion.Euler(0f, yaw, 0f);
                }
            }

            var bounds = CombinedBounds(model);
            if (bounds.size.y > 0.01f) model.localScale = Vector3.one * (height / bounds.size.y);

            if (alignFeet && model.parent != null)
            {
                bounds = CombinedBounds(model);
                float floor = model.parent.position.y;
                model.position += Vector3.up * (floor - bounds.min.y);
            }
        }

        private static Bounds CombinedBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        private static void EnsureAttachment(GameObject root, Transform item, string bone, Vector3 position, Vector3 euler)
        {
            HandAttachment attachment = null;
            foreach (var existing in root.GetComponents<HandAttachment>())
                if (existing.Item == item) attachment = existing;
            if (attachment == null) attachment = root.AddComponent<HandAttachment>();

            var serialized = new SerializedObject(attachment);
            serialized.FindProperty("item").objectReferenceValue = item;
            serialized.FindProperty("boneName").stringValue = bone;
            serialized.FindProperty("localPosition").vector3Value = position;
            serialized.FindProperty("localEuler").vector3Value = euler;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject FindModel(string name, string folder)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{name} t:Model", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static void SetRef(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            var prop = serialized.FindProperty(property);
            if (prop == null) return;
            prop.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
