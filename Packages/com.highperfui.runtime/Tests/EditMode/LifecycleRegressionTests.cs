using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace HighPerfUI.Tests
{
    [ExecuteAlways]
    public sealed class ProbeView : PooledUiView
    {
        public bool AliveOnEnable;
        private void OnEnable() { AliveOnEnable = Scope.IsAlive; }
    }

    public sealed class ControlledSpriteProvider : MonoBehaviour, IUiSpriteProvider
    {
        public sealed class Request : IUiSpriteRequest { public int Cancels; public void Cancel() { Cancels++; } }
        public readonly List<Action<UiSpriteLoadResult>> Callbacks = new List<Action<UiSpriteLoadResult>>();
        public readonly List<Request> Requests = new List<Request>();
        public IUiSpriteRequest Load(string key, Action<UiSpriteLoadResult> completed)
        { var request = new Request(); Requests.Add(request); Callbacks.Add(completed); return request; }
    }

    public sealed class LifecycleRegressionTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private GameObject Make(string name)
        { var go = new GameObject(name); _objects.Add(go); return go; }
        [TearDown] public void Clean()
        { foreach (var go in _objects) if (go != null) UnityEngine.Object.DestroyImmediate(go); _objects.Clear(); }

        [Test] public void ScopeOwnsCleanupAndDisposesOnlyOnce()
        {
            var scope = new UiScope(); scope.Revive(); int cleaned = 0;
            MethodInfo track = typeof(UiScope).GetMethod("Track", new[] { typeof(Action) });
            Assert.That(track, Is.Not.Null, "Scope must own cleanup, not only a validity bit");
            track.Invoke(scope, new object[] { (Action)(() => cleaned++) });
            scope.Invalidate(); scope.Invalidate();
            Assert.That(cleaned, Is.EqualTo(1));
        }

        [Test] public void RevivedScopeNeverRevivesOldToken()
        {
            var scope = new UiScope(); scope.Revive(); var token = scope.Capture();
            scope.Invalidate(); scope.Revive();
            Assert.That(token.IsCurrent, Is.False);
        }

        [Test] public void PoolRejectsDuplicateReturn()
        {
            var prefab = Make("prefab"); prefab.SetActive(false);
            var view = prefab.AddComponent<ProbeView>();
            var root = Make("pool"); root.SetActive(false);
            var pool = new UiViewPool(view, root.transform, 2);
            var rented = pool.Rent(Make("parent").transform);
            pool.Return(rented);
            Assert.Throws<InvalidOperationException>(() => pool.Return(rented));
        }

        [Test] public void PoolRestoresScopeBeforeEnable()
        {
            var prefab = Make("prefab"); prefab.SetActive(false);
            var view = prefab.AddComponent<ProbeView>();
            var root = Make("pool"); root.SetActive(false);
            var pool = new UiViewPool(view, root.transform, 2);
            var rented = (ProbeView)pool.Rent(Make("parent").transform);
            Assert.That(rented.AliveOnEnable, Is.True);
            pool.Return(rented);
        }

        [Test] public void RebindInvalidatesPreviousBinding()
        {
            var view = Make("view").AddComponent<ProbeView>();
            view.OnRent(); view.BeginBind(42); var token = view.CaptureBinding();
            view.BeginBind(43);
            Assert.That(token.IsCurrent, Is.False);
        }

        [Test] public void CommitWaitsForExplicitLayoutReadiness()
        {
            var go = Make("gated");
            var group = go.AddComponent<CanvasGroup>();
            var view = go.AddComponent<ProbeView>();
            typeof(PooledUiView).GetField("_interactionGate", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, group);
            view.OnRent(); view.BeginBind(1); view.CommitBinding();
            Assert.That(group.interactable, Is.False, "State commit alone is not layout readiness");
        }

        [Test] public void CriticalUpdateInvalidatesPreviousLayoutReadiness()
        {
            var go = Make("gated-update"); var gate = go.AddComponent<CanvasGroup>();
            var view = go.AddComponent<ProbeView>(); view.ConfigureInteraction(gate);
            view.OnRent(); view.BeginBind(1); view.MarkLayoutReady(); view.CommitBinding();
            Assert.That(gate.interactable, Is.True);
            view.InvalidateState(); view.CommitBinding();
            Assert.That(gate.interactable, Is.False);
        }

        [Test] public void ScopeCleanupContinuesAfterOneCleanupThrows()
        {
            var scope = new UiScope(); scope.Revive(); int cleaned = 0;
            scope.Track(() => cleaned++); scope.Track(() => throw new InvalidOperationException("cleanup"));
            Assert.DoesNotThrow(scope.Invalidate);
            Assert.That(cleaned, Is.EqualTo(1)); Assert.That(scope.LastCleanupError, Is.Not.Null);
        }

        [Test] public void GlobalPoolCapacityIsBoundedAndReleased()
        {
            var prefab = Make("prefab"); prefab.SetActive(false); var view = prefab.AddComponent<ProbeView>();
            var inactive = Make("inactive"); inactive.SetActive(false);
            var budget = new UiPoolBudget(1); var pool = new UiViewPool(view, inactive.transform, 4, budget);
            var parent = Make("parent"); var a = pool.Rent(parent.transform); var b = pool.Rent(parent.transform);
            pool.Return(a); pool.Return(b);
            Assert.That(pool.FreeCount, Is.EqualTo(1)); Assert.That(budget.Retained, Is.EqualTo(1));
            pool.Clear(); Assert.That(budget.Retained, Is.Zero);
        }

        [Test] public void StaleAssetCallbackDoesNotClearNewRequest()
        {
            var owner = Make("owner").AddComponent<ProbeView>(); owner.OnRent(); owner.BeginBind(1);
            var provider = Make("provider").AddComponent<ControlledSpriteProvider>();
            var slot = Make("slot").AddComponent<AsyncSpriteSlot>();
            typeof(AsyncSpriteSlot).GetField("_provider", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(slot, provider);
            slot.Bind("A", owner.CaptureBinding());
            slot.Bind("B", owner.CaptureBinding());
            int released = 0;
            provider.Callbacks[0](new UiSpriteLoadResult(null, () => released++));
            slot.ResetSlot();
            Assert.That(released, Is.EqualTo(1));
            Assert.That(provider.Requests[1].Cancels, Is.EqualTo(1));
        }
    }
}
