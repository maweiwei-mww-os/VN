using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HighPerfUI.Tests
{
    public sealed class ImmediateFragmentTests
    {
        private sealed class Owner : PooledUiView { }
        [ExecuteAlways]
        private sealed class RebindOnDisable : MonoBehaviour
        {
            public PooledUiView Owner;
            public bool Armed;
            private void OnDisable() { if (Armed && Owner != null) Owner.BeginBind(99); }
        }
        private readonly List<GameObject> _objects = new List<GameObject>();
        private GameObject Make(string name)
        {
            var go = new GameObject(name); _objects.Add(go); return go;
        }
        private static void Apply(LazyFragmentHost host, IReadOnlyList<bool> flags)
        {
            var method = typeof(LazyFragmentHost).GetMethod("RequireVisibleState", new[] { typeof(IReadOnlyList<bool>) });
            Assert.That(method, Is.Not.Null, "Batched immediate demand must not enqueue work just to cancel it.");
            try { method.Invoke(host, new object[] { flags }); }
            catch (TargetInvocationException e) { throw e.InnerException; }
        }
        private LazyFragmentHost Host(out UiRuntime runtime, out Owner owner, out UiFragmentDescriptor[] descriptors)
        {
            runtime = Make("runtime").AddComponent<UiRuntime>();
            if (runtime.Scheduler == null) typeof(UiRuntime).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(runtime, null);
            owner = Make("owner").AddComponent<Owner>(); owner.OnRent(); owner.BeginBind(31);
            var first = Make("base-template"); first.SetActive(false);
            var second = Make("detail-template"); second.SetActive(false);
            descriptors = new[] {
                new UiFragmentDescriptor { Id = "details", Prefab = second, RequiredForInteraction = true, Dependencies = new[] { "base" } },
                new UiFragmentDescriptor { Id = "base", Prefab = first }
            };
            var host = owner.gameObject.AddComponent<LazyFragmentHost>(); host.Configure(runtime, owner, descriptors);
            return host;
        }
        [TearDown] public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) UnityEngine.Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }
        [Test] public void ImmediateDemandResolvesDependenciesAndReusesInstancesWithoutQueuedWork()
        {
            var host = Host(out var runtime, out var owner, out _);
            Apply(host, new[] { true, false });
            Assert.That(host.RealizedCount, Is.EqualTo(2)); Assert.That(runtime.Scheduler.PendingCount, Is.Zero);
            var original = host.GetIfCreated("details");
            Apply(host, new[] { false, false }); Assert.That(original.activeSelf, Is.False);
            Apply(host, new[] { true, false }); Assert.That(host.GetIfCreated("details"), Is.SameAs(original));
            owner.CommitBinding(); owner.MarkLayoutReady(); Assert.That(owner.IsCommitted, Is.True);
        }
        [Test] public void InvalidDemandIsRejectedBeforeChangingExistingVisibility()
        {
            var host = Host(out _, out _, out _);
            Apply(host, new[] { true, false });
            Assert.Throws<ArgumentException>(() => Apply(host, new[] { false }));
            Assert.Throws<ArgumentNullException>(() => Apply(host, null));
            Assert.That(host.GetIfCreated("details").activeSelf, Is.True);
        }
        [Test] public void FailedImmediateDemandRetiresQueuedWorkAndKeepsInteractionClosed()
        {
            var host = Host(out var runtime, out var owner, out var descriptors);
            host.SetVisible("details", true);
            UnityEngine.Object.DestroyImmediate(descriptors[0].Prefab);
            Assert.Throws<ArgumentException>(() => Apply(host, new[] { true, false }));
            Assert.That(runtime.Scheduler.PendingCount, Is.Zero);
            owner.CommitBinding(); owner.MarkLayoutReady(); Assert.That(owner.IsCommitted, Is.False);
            Assert.That(host.GetState("details"), Is.EqualTo(VirtualNodeState.Failed));
        }
        [Test] public void DisableRebindingCannotAdoptNewOwnerForAnOldDemandBatch()
        {
            var host = Host(out var runtime, out var owner, out var entries);
            var badge = Make("badge-template"); badge.SetActive(false);
            host.Configure(runtime, owner, new[] { entries[0], entries[1], new UiFragmentDescriptor { Id = "badge", Prefab = badge } });
            Apply(host, new[] { true, false, false });
            var callback = host.GetIfCreated("details").AddComponent<RebindOnDisable>();
            callback.Owner = owner; callback.Armed = true;
            Exception failure = null;
            try { Apply(host, new[] { false, false, true }); } catch (Exception e) { failure = e; }
            Assert.That(owner.DataId, Is.EqualTo(99), "The native OnDisable hook must actually execute in this fixture.");
            Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
            Assert.That(host.IsCreated("badge"), Is.False, "Old visibility must not be realized for the new binding.");
        }
    }
}
