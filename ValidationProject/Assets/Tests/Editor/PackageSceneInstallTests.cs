using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HighPerfUI.Reference.Tests
{
    public sealed class PackageSceneInstallTests
    {
        [Test]
        public void ImportedArmorySceneHasItsScriptAndBundledPortrait()
        {
            var paths = AssetDatabase.FindAssets("Inventory t:Scene", new[] { "Assets/Samples" });
            Assert.That(paths.Length, Is.EqualTo(1));
            var scene = EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(paths[0]));
            try
            {
                Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<InventoryScreen>(true)).Count(), Is.EqualTo(1));
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var child in root.GetComponentsInChildren<Transform>(true))
                        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero);
                Assert.That(Resources.Load<Texture2D>("ArmoryArt/Commander"), Is.Not.Null);
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }

        [Test]
        public void AllOptionalSamplesCompileWithoutBecomingRuntimeDependencies()
        {
            string[] assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).ToArray();
            foreach (var sample in new[] { "HighPerfUI.MinimalIntegration", "HighPerfUI.RewardListSample", "HighPerfUI.Reference" })
                Assert.That(assemblies, Does.Contain(sample));
            Assert.That(typeof(UiRuntime).Assembly.GetReferencedAssemblies().Select(a => a.Name), Does.Not.Contain("HighPerfUI.Reference"));
        }
    }
}
