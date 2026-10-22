using UnityEditor;

namespace OCoveiro.EditorTools
{
    /// <summary>
    /// Configura automaticamente a importação dos modelos KayKit (Assets/ThirdParty/KayKit).
    /// - Personagens e animações: rig "Generic". Todos usam o mesmo esqueleto (Rig_Medium),
    ///   então as animações servem para qualquer personagem sem conversão.
    /// - O "root motion" fica no osso "root" e é descartado: quem move o personagem é o código.
    /// - Animações de ciclo (parado, andar, correr...) ficam em loop.
    /// - Cenário e armas: sem animação.
    /// </summary>
    public class KayKitImporter : AssetPostprocessor
    {
        private const string Root = "Assets/ThirdParty/KayKit/";

        private bool IsKayKit => assetPath.StartsWith(Root);
        private bool IsAnimationFile => assetPath.StartsWith(Root + "Animations/");
        private bool IsCharacter =>
            assetPath.StartsWith(Root + "Characters/") ||
            (assetPath.StartsWith(Root + "Skeletons/") && !assetPath.StartsWith(Root + "Skeletons/Weapons/"));

        private void OnPreprocessModel()
        {
            if (!IsKayKit) return;
            var importer = (ModelImporter)assetImporter;
            importer.materialSearch = ModelImporterMaterialSearch.Everywhere;

            if (IsCharacter || IsAnimationFile)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.bakeAxisConversion = true; // sem rotação de -90° vinda do Blender
                importer.motionNodeName = "root";
                importer.importAnimation = IsAnimationFile;
                if (IsAnimationFile) importer.materialImportMode = ModelImporterMaterialImportMode.None;
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
            }
        }

        private void OnPreprocessAnimation()
        {
            if (!IsAnimationFile) return;
            var importer = (ModelImporter)assetImporter;

            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                bool loop = IsLoop(clip.name);
                clip.loopTime = loop;
                clip.loopPose = loop;
            }
            importer.clipAnimations = clips;
        }

        /// <summary>Animações que repetem: parado, andar, correr, poses mantidas e ações contínuas ("Digging").</summary>
        public static bool IsLoop(string name) =>
            name.StartsWith("Idle") || name.StartsWith("Walking") || name.StartsWith("Running") ||
            name.Contains("_Idle") || name.EndsWith("_Pose") || name.EndsWith("ing") ||
            name == "Skeletons_Idle" || name == "Lie_Idle";
    }
}
