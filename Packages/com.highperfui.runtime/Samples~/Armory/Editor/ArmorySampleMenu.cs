using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace HighPerfUI.Reference.Editor
{
    internal static class ArmorySampleMenu
    {
        [MenuItem("Tools/HighPerfUI/Open Armory Sample")]
        private static void Open()
        {
            var scripts = AssetDatabase.FindAssets("ArmorySampleMenu t:MonoScript");
            if (scripts.Length != 1)
            {
                EditorUtility.DisplayDialog("HighPerfUI", "Import one version of the SLG Armory Business sample from Package Manager first.", "OK");
                return;
            }
            string script = AssetDatabase.GUIDToAssetPath(scripts[0]);
            string root = Path.GetDirectoryName(Path.GetDirectoryName(script));
            string scene = Path.Combine(root, "Scenes/Inventory.unity").Replace('\\', '/');
            if (!File.Exists(scene))
            {
                EditorUtility.DisplayDialog("HighPerfUI", "The imported sample scene is missing. Re-import the sample.", "OK");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(scene);
        }
    }
}
