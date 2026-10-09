using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEngine;

namespace HighPerfUI.Tests
{
    public sealed class ProductRuntimeTests
    {
        private sealed class BindRecord
        {
            public long Id;
            public int Index;
            public PooledUiView View;
        }

        private sealed class Source : IVirtualizedItemSource
        {
            public readonly List<long> Ids = new List<long>();
            public readonly List<BindRecord> Binds = new List<BindRecord>();
            public int Unbinds;
            public int StableIdReads;
            public int Count => Ids.Count;
            public long GetStableId(int index) { StableIdReads++; return Ids[index]; }
            public void Bind(PooledUiView view, int index)
            {
                Binds.Add(new BindRecord { Id = Ids[index], Index = index, View = view });
                view.CommitBinding();
            }
            public void Unbind(PooledUiView view, int index) { Unbinds++; }
        }

        private GameObject _root;
        private UiRuntime _runtime;
        private FixedVirtualizedScrollList _list;
        private RectTransform _viewport;
        private RectTransform _content;
        private Source _source;

        [SetUp] public void Setup()
        {
            _root = new GameObject("product-runtime-tests", typeof(RectTransform));
        }

        [TearDown] public void Cleanup()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        private GameObject Make(string name, params Type[] components)
        {
            var go = new GameObject(name, components);
            go.transform.SetParent(_root.transform, false);
            return go;
        }

        private UiRuntime Runtime()
        {
            if (_runtime != null) return _runtime;
            _runtime = _root.AddComponent<UiRuntime>();
            if (_runtime.Scheduler == null) InvokeLifecycle(_runtime, "Awake");
            Assert.That(_runtime.Scheduler, Is.Not.Null, "The fixture must initialize a real scheduler.");
            return _runtime;
        }

        private static MethodInfo Api(Type owner, string name, params Type[] parameters)
        {
            var method = owner.GetMethod(name, BindingFlags.Instance | BindingFlags.Public,
                null, parameters, null);
            Assert.That(method, Is.Not.Null, owner.Name + "." + name + " is a required public runtime API.");
            return method;
        }

        private static PropertyInfo Property(Type owner, string name, Type valueType)
        {
            var property = owner.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, owner.Name + "." + name + " is a required public runtime property.");
            Assert.That(property.PropertyType, Is.EqualTo(valueType));
            Assert.That(property.GetGetMethod(), Is.Not.Null, "The property must have a public getter.");
            return property;
        }

        private static object Call(MethodInfo method, object owner, params object[] arguments)
        {
            try { return method.Invoke(owner, arguments); }
            catch (TargetInvocationException error)
            {
                ExceptionDispatchInfo.Capture(error.InnerException ?? error).Throw();
                throw;
            }
        }

        private static void InvokeLifecycle(object owner, string name)
        {
            var method = owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing deterministic lifecycle hook " + name);
            Call(method, owner);
        }

        private static void Set(object owner, string field, object value)
        {
            var member = owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(member, Is.Not.Null, "Missing fixture configuration field " + field);
            member.SetValue(owner, value);
        }

        private static bool IsCurrent(object token)
        {
            Assert.That(token, Is.Not.Null);
            return (bool)Property(token.GetType(), "IsCurrent", typeof(bool)).GetValue(token);
        }

        private void MakeList(bool scheduled)
        {
            var runtime = Runtime();
            _viewport = Make("viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            _viewport.anchorMin = _viewport.anchorMax = new Vector2(0, 1);
            _viewport.pivot = new Vector2(0, 1);
            _viewport.sizeDelta = new Vector2(600, 300);
            _content = Make("content", typeof(RectTransform)).GetComponent<RectTransform>();
            _content.SetParent(_viewport, false);
            _content.anchorMin = _content.anchorMax = new Vector2(0, 1);
            _content.pivot = new Vector2(0, 1);
            var prefab = Make("item-prefab", typeof(RectTransform));
            prefab.SetActive(false);
            var view = prefab.AddComponent<ProbeView>();
            _list = _root.AddComponent<FixedVirtualizedScrollList>();
            Set(_list, "_bufferRows", 0);
            _list.ScheduleWork = scheduled;
            _list.Configure(null, _viewport, _content, view, runtime, 1, 100, 0);
            InvokeLifecycle(_list, "Awake");
            _source = new Source();
            for (int i = 0; i < 20; i++) _source.Ids.Add(101 + i * 13);
            _list.SetSource(_source);
            FlushList();
            Assert.That(_list.ActiveCount, Is.EqualTo(4), "The fixture has four visible/buffered rows.");
            _source.Binds.Clear();
            _source.Unbinds = 0;
        }

        private void FlushList()
        {
            InvokeLifecycle(_list, "LateUpdate");
            _runtime.Scheduler.RunFrame(1000);
            InvokeLifecycle(_list, "MarkLayoutReady");
            Assert.That(_list.PendingCount, Is.Zero);
            Assert.That(_runtime.Scheduler.FaultCount, Is.Zero);
        }

        private Dictionary<long, PooledUiView> ActiveById()
        {
            var views = new Dictionary<long, PooledUiView>();
            foreach (var view in _list.ActiveViews) views.Add(view.DataId, view);
            return views;
        }

        private static Dictionary<long, UiBindingToken> Bindings(Dictionary<long, PooledUiView> views)
        {
            var tokens = new Dictionary<long, UiBindingToken>();
            foreach (var pair in views) tokens.Add(pair.Key, pair.Value.CaptureBinding());
            return tokens;
        }

        private static void AssertBindingsCurrent(Dictionary<long, UiBindingToken> bindings)
        {
            foreach (var pair in bindings)
                Assert.That(pair.Value.IsCurrent, Is.True, "Stable-ID binding was invalidated: " + pair.Key);
        }

        private LazyFragmentHost MakeHost(string name, UiFragmentDescriptor[] descriptors, out ProbeView owner)
        {
            var go = Make(name);
            var gate = go.AddComponent<CanvasGroup>();
            owner = go.AddComponent<ProbeView>();
            owner.ConfigureInteraction(gate);
            owner.OnRent();
            owner.BeginBind(42);
            owner.CommitBinding();
            owner.MarkLayoutReady();
            var host = go.AddComponent<LazyFragmentHost>();
            host.Configure(Runtime(), owner, descriptors);
            InvokeLifecycle(host, "Awake");
            return host;
        }

        private UiFragmentDescriptor Descriptor(string id, bool required = false, params string[] dependencies)
        {
            var prefab = Make(id + "-prefab");
            prefab.SetActive(false);
            return new UiFragmentDescriptor
            {
                Id = id, Prefab = prefab, RequiredForInteraction = required, Dependencies = dependencies
            };
        }

        private UiFragmentDescriptor[] Branches()
        {
            // Descriptor order deliberately differs from dependency order and alphabetical order.
            return new[]
            {
                Descriptor("details", false, "base"), Descriptor("idle"),
                Descriptor("base"), Descriptor("required", true, "base")
            };
        }

        private static MethodInfo BatchApi()
        {
            var method = Api(typeof(LazyFragmentHost), "SetVisibleState", typeof(IReadOnlyList<bool>));
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));
            return method;
        }

        private static MethodInfo RequireApi()
        {
            var method = Api(typeof(LazyFragmentHost), "Require", typeof(string));
            Assert.That(method.ReturnType, Is.EqualTo(typeof(GameObject)));
            return method;
        }

        private void DrainFragments(params LazyFragmentHost[] hosts)
        {
            foreach (var host in hosts) InvokeLifecycle(host, "LateUpdate");
            for (int frame = 0; frame < 8 && _runtime.Scheduler.PendingCount > 0; frame++)
                _runtime.Scheduler.RunFrame(1000);
            Assert.That(_runtime.Scheduler.PendingCount, Is.Zero, "Dependencies must complete in bounded deterministic steps.");
            Assert.That(_runtime.Scheduler.FaultCount, Is.Zero);
        }

        [Test] public void LeaseId_AdvancesOnRentAndReturn_ButNotOnBind()
        {
            var leaseId = Property(typeof(PooledUiView), "LeaseId", typeof(long));
            var view = Make("lease-owner").AddComponent<ProbeView>();
            long initial = (long)leaseId.GetValue(view);
            view.OnRent();
            Assert.That((long)leaseId.GetValue(view), Is.EqualTo(initial + 1));
            view.BeginBind(101);
            view.BeginBind(114);
            Assert.That((long)leaseId.GetValue(view), Is.EqualTo(initial + 1));
            view.OnReturn();
            Assert.That((long)leaseId.GetValue(view), Is.EqualTo(initial + 2));
            view.OnRent();
            Assert.That((long)leaseId.GetValue(view), Is.EqualTo(initial + 3));
        }

        [Test] public void CaptureLease_ReturnAndSameIdRerent_NeverReviveOldToken()
        {
            var capture = Api(typeof(PooledUiView), "CaptureLease");
            Assert.That(capture.ReturnType.FullName, Is.EqualTo("HighPerfUI.UiLeaseToken"));
            var prefab = Make("lease-prefab");
            prefab.SetActive(false);
            var inactive = Make("inactive-pool");
            inactive.SetActive(false);
            var pool = new UiViewPool(prefab.AddComponent<ProbeView>(), inactive.transform, 1);
            var parent = Make("leased-parent");
            var view = pool.Rent(parent.transform, 101);
            var lease = Call(capture, view);
            var binding = view.CaptureBinding();
            Assert.That(IsCurrent(lease), Is.True);
            pool.Return(view);
            Assert.That(IsCurrent(lease), Is.False);
            Assert.That(binding.IsCurrent, Is.False);
            var rerented = pool.Rent(parent.transform, 101);
            Assert.That(rerented, Is.SameAs(view), "Exercise reuse of the same MonoBehaviour, not a replacement.");
            Assert.That(IsCurrent(lease), Is.False);
            Assert.That(binding.IsCurrent, Is.False, "Existing CaptureBinding semantics must survive lease support.");
            Assert.That(IsCurrent(Call(capture, rerented)), Is.True);
            pool.Return(rerented);
        }

        [Test] public void CaptureLease_RebindKeepsLeaseCurrent_WhileBindingBecomesStale()
        {
            var capture = Api(typeof(PooledUiView), "CaptureLease");
            var view = Make("rebound-lease-owner").AddComponent<ProbeView>();
            view.OnRent();
            view.BeginBind(101);
            var lease = Call(capture, view);
            var binding = view.CaptureBinding();
            view.BeginBind(114);
            Assert.That(IsCurrent(lease), Is.True);
            Assert.That(binding.IsCurrent, Is.False);
            Assert.That(IsCurrent(Activator.CreateInstance(capture.ReturnType)), Is.False);
            UnityEngine.Object.DestroyImmediate(view.gameObject);
            Assert.That(IsCurrent(lease), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void RefreshItem_BindsOnlyTargetedSameIdView(bool scheduled)
        {
            var refresh = Api(typeof(FixedVirtualizedScrollList), "RefreshItem", typeof(long));
            MakeList(scheduled);
            var views = ActiveById();
            var bindings = Bindings(views);
            int generation = views[114].BindingGeneration;
            var metrics = UiMetrics.Snapshot();
            Call(refresh, _list, 114L);
            FlushList();
            Assert.That(_source.Binds.Count, Is.EqualTo(1));
            Assert.That(_source.Binds[0].Id, Is.EqualTo(114));
            Assert.That(_source.Binds[0].Index, Is.EqualTo(1));
            Assert.That(_source.Binds[0].View, Is.SameAs(views[114]));
            Assert.That(views[114].BindingGeneration, Is.EqualTo(generation));
            Assert.That(_source.Unbinds, Is.Zero);
            Assert.That(UiMetrics.Snapshot().PoolRents, Is.EqualTo(metrics.PoolRents));
            AssertBindingsCurrent(bindings);
        }

        [TestCase(false)] [TestCase(true)]
        public void RefreshItem_InactiveAndUnknownIds_DoNotBindOrRealizeVisibleItems(bool scheduled)
        {
            var refresh = Api(typeof(FixedVirtualizedScrollList), "RefreshItem", typeof(long));
            MakeList(scheduled);
            var bindings = Bindings(ActiveById());
            var metrics = UiMetrics.Snapshot();
            Call(refresh, _list, 348L);
            Call(refresh, _list, 99999L);
            FlushList();
            Assert.That(_source.Binds, Is.Empty);
            Assert.That(_source.Unbinds, Is.Zero);
            Assert.That(_list.ActiveCount, Is.EqualTo(4));
            Assert.That(UiMetrics.Snapshot().Instantiations, Is.EqualTo(metrics.Instantiations));
            AssertBindingsCurrent(bindings);
        }

        [TestCase(false)] [TestCase(true)]
        public void RefreshItems_BindsDistinctActiveTargetsOnly(bool scheduled)
        {
            var refresh = Api(typeof(FixedVirtualizedScrollList), "RefreshItems", typeof(IEnumerable<long>));
            MakeList(scheduled);
            var views = ActiveById();
            var bindings = Bindings(views);
            Call(refresh, _list, new long[] { 114, 140, 114, 348, 99999 });
            FlushList();
            Assert.That(_source.Binds.Count, Is.EqualTo(2), "Duplicate IDs must not duplicate binding work.");
            var ids = new List<long>();
            foreach (var bind in _source.Binds)
            {
                ids.Add(bind.Id);
                Assert.That(bind.View, Is.SameAs(views[bind.Id]));
            }
            CollectionAssert.AreEquivalent(new long[] { 114, 140 }, ids);
            Assert.That(_source.Unbinds, Is.Zero);
            AssertBindingsCurrent(bindings);
        }

        [TestCase(false, true)] [TestCase(true, true)] [TestCase(false, false)]
        public void RefreshItems_Bulk333Of1000_BindsEachMatchingVisibleIdOnce(bool scheduled, bool virtualize)
        {
            var refresh = Api(typeof(FixedVirtualizedScrollList), "RefreshItems", typeof(IEnumerable<long>));
            MakeList(scheduled);
            for (int i = 20; i < 1000; i++) _source.Ids.Add(101 + i * 13);
            _list.Virtualize = virtualize;
            _list.RefreshStructure();
            FlushList();
            var bindings = Bindings(ActiveById());
            var changed = new List<long>();
            for (int i = 0; i < 999; i += 3) changed.Add(_source.Ids[i]);
            Assert.That(changed.Count, Is.EqualTo(333));
            changed.Add(101);
            changed.Add(99999);
            _source.Binds.Clear();
            _source.Unbinds = 0;
            _source.StableIdReads = 0;
            var metrics = UiMetrics.Snapshot();
            Call(refresh, _list, changed);
            Assert.That(_source.StableIdReads, Is.Zero, "Content refresh must not search the entire source for IDs.");
            FlushList();
            Assert.That(_source.Binds.Count, Is.EqualTo(virtualize ? 2 : 333));
            var seen = new HashSet<long>();
            foreach (var bind in _source.Binds)
            {
                Assert.That(seen.Add(bind.Id), Is.True, "Each matching stable ID binds exactly once.");
                Assert.That(changed.Contains(bind.Id), Is.True);
                Assert.That(bind.Index % 3, Is.Zero);
            }
            if (virtualize)
            {
                CollectionAssert.AreEquivalent(new long[] { 101, 140 }, seen);
                Assert.That(_source.StableIdReads, Is.LessThan(32), "Only the four active rows may be inspected.");
            }
            Assert.That(_source.Unbinds, Is.Zero);
            Assert.That(UiMetrics.Snapshot().Instantiations, Is.EqualTo(metrics.Instantiations));
            AssertBindingsCurrent(bindings);
        }

        [TestCase(false)] [TestCase(true)]
        public void RefreshStructure_ReorderPreservesSameIdViewsAndBindingGeneration(bool scheduled)
        {
            var refresh = Api(typeof(FixedVirtualizedScrollList), "RefreshStructure");
            MakeList(scheduled);
            var views = ActiveById();
            var bindings = Bindings(views);
            var metrics = UiMetrics.Snapshot();
            _source.Ids.Reverse(0, 4);
            Call(refresh, _list);
            FlushList();
            Assert.That(_source.Binds, Is.Empty, "Moving an existing stable ID is not a data rebind.");
            Assert.That(_source.Unbinds, Is.Zero);
            Assert.That(_list.ActiveCount, Is.EqualTo(4));
            foreach (var pair in _list.ActiveBindings)
            {
                long id = _source.Ids[pair.Key];
                Assert.That(pair.Value, Is.SameAs(views[id]), "View identity must follow the stable ID, not the old index.");
                Assert.That(pair.Value.DataId, Is.EqualTo(id));
                Assert.That(pair.Value.IsCommitted, Is.True);
                var rect = (RectTransform)pair.Value.transform;
                Assert.That(rect.anchoredPosition.y, Is.EqualTo(-pair.Key * 100).Within(0.01f));
            }
            Assert.That(UiMetrics.Snapshot().PoolReturns, Is.EqualTo(metrics.PoolReturns));
            Assert.That(UiMetrics.Snapshot().PoolRents, Is.EqualTo(metrics.PoolRents));
            AssertBindingsCurrent(bindings);
        }

        [Test] public void RefreshStructure_ResizesContentWhilePreservingNormalizedScroll()
        {
            var refresh = Api(typeof(FixedVirtualizedScrollList), "RefreshStructure");
            MakeList(false);
            _list.ScrollTo(0.35f);
            FlushList();
            Assert.That(_content.anchoredPosition.y, Is.EqualTo(595).Within(0.01f));
            for (int i = 0; i < 20; i++) _source.Ids.Add(1000 + i * 17);
            Call(refresh, _list);
            FlushList();
            Assert.That(_content.rect.height, Is.EqualTo(4000).Within(0.01f));
            Assert.That(_content.anchoredPosition.y, Is.EqualTo(1295).Within(0.01f));
        }

        [Test] public void RefreshItem_AfterReorderTargetsNewIndexWithoutChangingBinding()
        {
            var structure = Api(typeof(FixedVirtualizedScrollList), "RefreshStructure");
            var item = Api(typeof(FixedVirtualizedScrollList), "RefreshItem", typeof(long));
            MakeList(true);
            var view = ActiveById()[101];
            var binding = view.CaptureBinding();
            _source.Ids.Reverse(0, 4);
            Call(structure, _list);
            FlushList();
            _source.Binds.Clear();
            Call(item, _list, 101L);
            FlushList();
            Assert.That(_source.Binds.Count, Is.EqualTo(1));
            Assert.That(_source.Binds[0].Index, Is.EqualTo(3));
            Assert.That(_source.Binds[0].Id, Is.EqualTo(101));
            Assert.That(_source.Binds[0].View, Is.SameAs(view));
            Assert.That(binding.IsCurrent, Is.True);
        }

        [Test] public void SetVisibleState_UsesDescriptorOrderAndMatchesLegacyDependencyVisibility()
        {
            var batch = BatchApi();
            var descriptors = Branches();
            var batched = MakeHost("batched", descriptors, out var batchOwner);
            var legacy = MakeHost("legacy", descriptors, out var legacyOwner);
            var state = new[] { true, false, false, true };
            Call(batch, batched, state);
            for (int i = 0; i < descriptors.Length; i++) legacy.SetVisible(descriptors[i].Id, state[i]);
            Assert.That(batchOwner.IsCommitted, Is.False, "Required pending fragments must close the interaction gate.");
            DrainFragments(batched, legacy);
            Assert.That(batched.RealizedCount, Is.EqualTo(3));
            Assert.That(legacy.RealizedCount, Is.EqualTo(3));
            foreach (string id in new[] { "details", "base", "required" })
            {
                Assert.That(batched.GetIfCreated(id).activeSelf, Is.True);
                Assert.That(legacy.GetIfCreated(id).activeSelf, Is.True);
            }
            Assert.That(batched.IsCreated("idle"), Is.False);
            Assert.That(batchOwner.IsCommitted, Is.True);
            Assert.That(legacyOwner.IsCommitted, Is.True);
            Call(batch, batched, new[] { false, false, false, false });
            DrainFragments(batched);
            Assert.That(batched.RealizedCount, Is.EqualTo(3), "Hiding retains reusable instances until explicitly trimmed.");
            foreach (string id in new[] { "details", "base", "required" })
                Assert.That(batched.GetIfCreated(id).activeSelf, Is.False);
        }

        [Test] public void SetVisibleState_SwapsBranchesInOneReconciliationWithoutRequeueingSharedDependency()
        {
            var batch = BatchApi();
            var descriptors = new[]
            {
                Descriptor("old", false, "common"), Descriptor("new", false, "common"), Descriptor("common")
            };
            var host = MakeHost("swap-host", descriptors, out _);
            Call(batch, host, new[] { true, false, false });
            Assert.That(_runtime.Scheduler.PendingCount, Is.EqualTo(2));
            var before = UiMetrics.Snapshot();
            Call(batch, host, new[] { false, true, false });
            var after = UiMetrics.Snapshot();
            Assert.That(after.Scheduled - before.Scheduled, Is.EqualTo(1),
                "The shared dependency must not be cancelled and requested again between descriptor updates.");
            Assert.That(after.Coalesced - before.Coalesced, Is.Zero);
            Assert.That(_runtime.Scheduler.PendingCount, Is.EqualTo(2));
            DrainFragments(host);
            Assert.That(host.IsCreated("old"), Is.False);
            Assert.That(host.GetIfCreated("new").activeSelf, Is.True);
            Assert.That(host.GetIfCreated("common").activeSelf, Is.True);
        }

        [Test] public void SetVisibleState_RepeatedRealizedStateAllocatesNoManagedBytes()
        {
            var batch = BatchApi();
            var host = MakeHost("allocation-host", new[] { Descriptor("visible") }, out _);
            var apply = (Action<IReadOnlyList<bool>>)Delegate.CreateDelegate(typeof(Action<IReadOnlyList<bool>>), host, batch);
            IReadOnlyList<bool> state = new[] { true };
            apply(state);
            DrainFragments(host);
            var counter = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Static | BindingFlags.Public);
            if (counter == null) Assert.Ignore("This Unity scripting runtime does not expose a per-thread allocation counter.");
            var allocated = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), counter);
            for (int i = 0; i < 16; i++) apply(state);
            allocated();
            long before = allocated();
            for (int i = 0; i < 64; i++) apply(state);
            long bytes = allocated() - before;
            Assert.That(bytes, Is.Zero, "Measure the bound API delegate, not allocating reflection invocation arguments.");
            Assert.That(host.RealizedCount, Is.EqualTo(1));
        }

        [Test] public void SetVisibleState_CancelBeforeCreationLeavesNoInstancesOrPendingWork()
        {
            var batch = BatchApi();
            var host = MakeHost("cancel-host", Branches(), out var owner);
            Call(batch, host, new[] { false, false, false, true });
            Assert.That(owner.IsCommitted, Is.False);
            Call(batch, host, new[] { false, false, false, false });
            DrainFragments(host);
            Assert.That(host.RealizedCount, Is.Zero);
            Assert.That(host.transform.childCount, Is.Zero);
            Assert.That(owner.IsCommitted, Is.True);
            Assert.That(host.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
        }

        [Test] public void SetVisibleState_OwnerReturnAndRebindDropsPreviouslyQueuedCreation()
        {
            var batch = BatchApi();
            var host = MakeHost("rebound-host", Branches(), out var owner);
            Call(batch, host, new[] { false, false, false, true });
            owner.OnReturn();
            owner.OnRent();
            owner.BeginBind(99);
            owner.CommitBinding();
            owner.MarkLayoutReady();
            Call(batch, host, new[] { false, true, false, false });
            DrainFragments(host);
            Assert.That(host.RealizedCount, Is.EqualTo(1));
            Assert.That(host.GetIfCreated("idle").activeSelf, Is.True);
            Assert.That(host.IsCreated("required"), Is.False);
            Assert.That(host.IsCreated("base"), Is.False);
            Assert.That(owner.IsCommitted, Is.True);
        }

        [Test] public void Require_RealizesNecessaryTransitiveDependenciesSynchronouslyEvenWhenDegraded()
        {
            var require = RequireApi();
            var descriptors = new[]
            {
                Descriptor("leaf", true, "middle"), Descriptor("unrelated"),
                Descriptor("middle", false, "base"), Descriptor("base")
            };
            foreach (var descriptor in descriptors) descriptor.Priority = UiPriority.Preload;
            var host = MakeHost("require-host", descriptors, out var owner);
            typeof(UiRuntime).GetProperty("State").SetValue(_runtime, UiRuntimeState.Degraded);
            var instance = (GameObject)Call(require, host, "leaf");
            Assert.That(instance, Is.Not.Null, "Require must return a real object before any scheduler frame runs.");
            Assert.That(instance, Is.SameAs(host.GetIfCreated("leaf")));
            Assert.That(host.RealizedCount, Is.EqualTo(3));
            foreach (string id in new[] { "base", "middle", "leaf" })
            {
                Assert.That(host.GetIfCreated(id).activeSelf, Is.True);
                Assert.That(host.GetState(id), Is.EqualTo(VirtualNodeState.Real));
            }
            Assert.That(host.IsCreated("unrelated"), Is.False);
            Assert.That(_runtime.Scheduler.PendingCount, Is.Zero);
            Assert.That(owner.IsCommitted, Is.True);
            Assert.That(host.GetComponent<CanvasGroup>().interactable, Is.True);
            Assert.That(Call(require, host, "leaf"), Is.SameAs(instance));
            Assert.That(host.RealizedCount, Is.EqualTo(3));
        }

        [Test] public void Require_CancelsQueuedCreationAndDoesNotCreateDuplicateInstances()
        {
            var require = RequireApi();
            var host = MakeHost("queued-require-host", Branches(), out var owner);
            host.SetVisible("required", true);
            Assert.That(_runtime.Scheduler.PendingCount, Is.EqualTo(2));
            Assert.That(owner.IsCommitted, Is.False);
            var instance = (GameObject)Call(require, host, "required");
            Assert.That(host.RealizedCount, Is.EqualTo(2));
            Assert.That(_runtime.Scheduler.PendingCount, Is.Zero, "Synchronous fulfillment must retire its queued work.");
            var before = UiMetrics.Snapshot().Instantiations;
            DrainFragments(host);
            Assert.That(host.GetIfCreated("required"), Is.SameAs(instance));
            Assert.That(UiMetrics.Snapshot().Instantiations, Is.EqualTo(before));
            Assert.That(owner.IsCommitted, Is.True);
        }

        [Test] public void Require_FailedCreationRetiresQueuedWorkAndKeepsReadinessClosedUntilRetry()
        {
            var require = RequireApi();
            var descriptor = Descriptor("required", true);
            var host = MakeHost("failed-require-host", new[] { descriptor }, out var owner);
            host.SetVisible("required", true);
            UnityEngine.Object.DestroyImmediate(descriptor.Prefab);
            Assert.Throws<ArgumentException>(() => Call(require, host, "required"));
            Assert.That(host.IsCreated("required"), Is.False);
            Assert.That(host.transform.childCount, Is.Zero);
            Assert.That(_runtime.Scheduler.PendingCount, Is.Zero);
            Assert.That(host.GetState("required"), Is.EqualTo(VirtualNodeState.Failed));
            Assert.That(owner.IsCommitted, Is.False);
            Assert.That(host.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            descriptor.Prefab = Make("replacement-required-prefab");
            descriptor.Prefab.SetActive(false);
            Assert.That(Call(require, host, "required"), Is.Not.Null);
            Assert.That(host.RealizedCount, Is.EqualTo(1));
            Assert.That(owner.IsCommitted, Is.True);
            DrainFragments(host);
        }

        [Test] public void TrimHidden_BoundsIdleResidencyWithoutRemovingActiveOrRequiredFragments()
        {
            var batch = BatchApi();
            var trim = Api(typeof(LazyFragmentHost), "TrimHidden", typeof(int));
            var descriptors = new[]
            {
                Descriptor("idle-a"), Descriptor("idle-b"), Descriptor("idle-c"),
                Descriptor("active"), Descriptor("required", true)
            };
            var host = MakeHost("trim-host", descriptors, out var owner);
            Call(batch, host, new[] { true, true, true, true, true });
            DrainFragments(host);
            var active = host.GetIfCreated("active");
            var required = host.GetIfCreated("required");
            Call(batch, host, new[] { false, false, false, true, false });
            Call(trim, host, 1);
            int retainedIdle = 0;
            foreach (string id in new[] { "idle-a", "idle-b", "idle-c" })
                if (host.IsCreated(id)) retainedIdle++;
            Assert.That(retainedIdle, Is.EqualTo(1));
            Assert.That(host.RealizedCount, Is.EqualTo(3));
            Assert.That(host.GetIfCreated("active"), Is.SameAs(active));
            Assert.That(active.activeSelf, Is.True);
            Assert.That(host.GetIfCreated("required"), Is.SameAs(required), "Required descriptors are not idle trim candidates.");
            Assert.That(required.activeSelf, Is.False);
            Assert.That(owner.IsCommitted, Is.True);
            Call(trim, host, 0);
            Assert.That(host.RealizedCount, Is.EqualTo(2));
            Assert.That(host.GetIfCreated("active"), Is.SameAs(active));
            Assert.That(host.GetIfCreated("required"), Is.SameAs(required));
            DrainFragments(host);
        }
    }
}
