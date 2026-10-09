using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HighPerfUI.Tests.PlayMode
{
    public sealed class FragmentOwner : PooledUiView { }
    public sealed class FragmentLifecycleTests
    {
        [UnityTest] public IEnumerator RequiredFragmentsAndDependenciesSurviveOverload()
        {
            var root = new GameObject("required-test"); var runtime = root.AddComponent<UiRuntime>();
            var owner = root.AddComponent<FragmentOwner>(); owner.OnRent(); owner.BeginBind(1);
            var prefab = new GameObject("template"); prefab.SetActive(false); prefab.transform.SetParent(root.transform);
            var host = root.AddComponent<LazyFragmentHost>();
            host.Configure(runtime, owner, new[] {
                new UiFragmentDescriptor { Id = "Dependency", Prefab = prefab, Priority = UiPriority.Decorative },
                new UiFragmentDescriptor { Id = "Critical", Prefab = prefab, Priority = UiPriority.Preload, RequiredForInteraction = true, Dependencies = new[] { "Dependency" } }
            });
            typeof(UiRuntime).GetProperty("State").SetValue(runtime, UiRuntimeState.Degraded);
            try
            {
                host.SetVisible("Critical", true);
                for (int i = 0; i < 5; i++) yield return null;
                Assert.That(host.IsCreated("Critical"), Is.True);
                Assert.That(host.IsCreated("Dependency"), Is.True);
            }
            finally { Object.Destroy(root); }
            yield return null;
        }
        [UnityTest] public IEnumerator RebindDoesNotResurrectOldFragmentDemand()
        {
            var root = new GameObject("fragment-test");
            var runtime = root.AddComponent<UiRuntime>();
            var owner = root.AddComponent<FragmentOwner>(); owner.OnRent(); owner.BeginBind(1);
            var prefab = new GameObject("badge-prefab"); prefab.SetActive(false); prefab.transform.SetParent(root.transform);
            var host = root.AddComponent<LazyFragmentHost>();
            host.Configure(runtime, owner, new[] { new UiFragmentDescriptor { Id = "Badge", Prefab = prefab } });
            try
            {
                host.SetVisible("Badge", true); owner.BeginBind(2);
                for (int i = 0; i < 4; i++) yield return null;
                Assert.That(host.IsCreated("Badge"), Is.False);
                host.SetVisible("Badge", true);
                for (int i = 0; i < 4; i++) yield return null;
                Assert.That(host.IsCreated("Badge"), Is.True);
                owner.OnReturn();
                Assert.That(host.GetIfCreated("Badge").activeSelf, Is.False);
            }
            finally { Object.Destroy(root); }
            yield return null;
        }
        [UnityTest] public IEnumerator HiddenAndQueriedFragmentNeverCreates()
        {
            var root = new GameObject("hidden-test"); var runtime = root.AddComponent<UiRuntime>();
            var owner = root.AddComponent<FragmentOwner>(); owner.OnRent(); owner.BeginBind(1);
            var prefab = new GameObject("template"); prefab.SetActive(false); prefab.transform.SetParent(root.transform);
            var host = root.AddComponent<LazyFragmentHost>();
            host.Configure(runtime, owner, new[] { new UiFragmentDescriptor { Id = "Hidden", Prefab = prefab } });
            try
            {
                host.SetVisible("Hidden", false);
                for (int i = 0; i < 3; i++) { Assert.That(host.GetIfCreated("Hidden"), Is.Null); yield return null; }
                Assert.That(host.RealizedCount, Is.Zero);
            }
            finally { Object.Destroy(root); }
            yield return null;
        }
    }
}
