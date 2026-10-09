using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HighPerfUI.Tests
{
    public sealed class CollectionRegressionTests
    {
        private sealed class Source : IVirtualizedItemSource
        {
            public int Count { get; set; } = 20;
            public int Binds;
            public bool ThrowUnbind;
            public readonly List<PooledUiView> Seen = new List<PooledUiView>();
            public long GetStableId(int index) => index + 1;
            public void Bind(PooledUiView view, int index) { Binds++; if (!Seen.Contains(view)) Seen.Add(view); }
            public void Unbind(PooledUiView view, int index) { if (ThrowUnbind) throw new System.Exception("unbind"); }
        }
        private GameObject _root;
        private FixedVirtualizedScrollList _list;
        private UiRuntime _runtime;
        private Source _source;
        private static void Set(object owner, string field, object value)
        { owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value); }
        private static void Invoke(object owner, string method)
        { owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null); }
        [SetUp] public void Setup()
        {
            _root = new GameObject("test-root", typeof(RectTransform));
            _runtime = _root.AddComponent<UiRuntime>();
            if (_runtime.Scheduler == null) Invoke(_runtime, "Awake");
            var viewport = new GameObject("viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            viewport.SetParent(_root.transform); viewport.sizeDelta = new Vector2(600, 300);
            var content = new GameObject("content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport);
            var prefab = new GameObject("prefab", typeof(RectTransform));
            prefab.transform.SetParent(_root.transform); prefab.SetActive(false);
            var view = prefab.AddComponent<ProbeView>();
            _list = _root.AddComponent<FixedVirtualizedScrollList>();
            Set(_list, "_viewport", viewport); Set(_list, "_content", content); Set(_list, "_itemPrefab", view);
            Invoke(_list, "Awake");
            _source = new Source(); _list.SetSource(_source);
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(_root); }
        [Test] public void PoolMissCreationWaitsForScheduler()
        {
            Invoke(_list, "LateUpdate");
            Assert.That(_source.Binds, Is.Zero);
            _runtime.Scheduler.RunFrame(1000);
            Assert.That(_source.Binds, Is.GreaterThan(0));
        }
        [Test] public void SameIdContentRefreshRebindsVisibleViews()
        {
            Invoke(_list, "LateUpdate"); _runtime.Scheduler.RunFrame(1000);
            _source.Binds = 0;
            _list.RefreshVisible(); Invoke(_list, "LateUpdate"); _runtime.Scheduler.RunFrame(1000);
            Assert.That(_source.Binds, Is.GreaterThan(0));
        }
        [Test] public void OneUnbindFailureDoesNotLeaveOtherScopesAlive()
        {
            Invoke(_list, "LateUpdate"); _runtime.Scheduler.RunFrame(1000);
            _source.ThrowUnbind = true;
            Assert.Throws<System.AggregateException>(() => _list.SetSource(null));
            _source.ThrowUnbind = false;
            foreach (var view in _source.Seen) Assert.That(view.Scope.IsAlive, Is.False);
            Assert.That(_list.ActiveCount, Is.Zero);
        }
        [Test] public void DroppedRequestCanBeRequestedAfterCountRecovers()
        {
            Invoke(_list, "LateUpdate"); _source.Count = 0;
            _runtime.Scheduler.RunFrame(1000);
            _source.Count = 20; _list.RefreshVisible(); Invoke(_list, "LateUpdate");
            _runtime.Scheduler.RunFrame(1000);
            Assert.That(_list.ActiveCount, Is.GreaterThan(0));
        }
    }
}
