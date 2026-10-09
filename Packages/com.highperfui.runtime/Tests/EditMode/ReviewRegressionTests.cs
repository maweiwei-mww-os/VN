using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HighPerfUI.Tests
{
    public sealed class FailingDirtyView : IncrementalUiView
    {
        public ulong LastMask;
        public bool ThrowOnce = true;
        public void Change(ulong mask) { MarkDirty(mask); }
        protected override void ApplyDirty(ulong mask)
        { LastMask = mask; if (ThrowOnce) { ThrowOnce = false; throw new Exception("write"); } }
    }
    public sealed class ReviewRegressionTests
    {
        private sealed class Keyed : IKeyedUiWorkItem
        {
            public int Calls;
            public Func<bool> Step;
            public UiWorkKey Key => new UiWorkKey(1, 1, 1, 1);
            public UiPriority Priority => UiPriority.Visible;
            public bool IsValid => true;
            public string DebugName => "continuation";
            public bool ExecuteStep() { Calls++; return Step == null || Step(); }
        }
        [Test] public void OldContinuationCannotOverwriteNewKeyedRevision()
        {
            var scheduler = new FrameBudgetScheduler(); var newer = new Keyed();
            var old = new Keyed { Step = () => { scheduler.Enqueue(newer); return false; } };
            scheduler.Enqueue(old); scheduler.RunFrame(100); scheduler.RunFrame(100);
            Assert.That(newer.Calls, Is.EqualTo(1)); Assert.That(old.Calls, Is.EqualTo(1));
        }
        [Test] public void OldRegistrationCannotDisposeNewLifetimeRegistration()
        {
            var scope = new UiScope(); scope.Revive(); int releases = 0; Action release = () => releases++;
            var old = scope.Track(release); scope.Invalidate(); scope.Revive(); scope.Track(release);
            old.Dispose(); Assert.That(releases, Is.EqualTo(1));
            scope.Invalidate(); Assert.That(releases, Is.EqualTo(2));
        }
        [Test] public void FailedDirtyFieldsAreRetainedForExplicitRetry()
        {
            var go = new GameObject("dirty-test");
            try
            {
                var view = go.AddComponent<FailingDirtyView>(); view.OnRent(); view.BeginBind(1); view.Change(1);
                Assert.Throws<Exception>(() => view.ExecuteStep());
                view.Change(2); view.ExecuteStep();
                Assert.That(view.LastMask, Is.EqualTo(3));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test] public void ThrowingCommandReadinessDoesNotStrandFollowingCommands()
        {
            var go = new GameObject("commands");
            try
            {
                var view = go.AddComponent<ProbeView>(); view.OnRent(); view.BeginBind(1);
                var scheduler = new FrameBudgetScheduler(); var queue = new UiCommandQueue(scheduler); int completed = 0;
                queue.Enqueue(view.CaptureBinding(), () => { }, () => throw new Exception("ready"));
                queue.Enqueue(view.CaptureBinding(), () => completed++);
                scheduler.RunFrame(100); scheduler.RunFrame(100);
                Assert.That(completed, Is.EqualTo(1)); Assert.That(queue.PendingCount, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        private sealed class BadRequest : IUiSpriteRequest { public void Cancel() { throw new Exception("cancel"); } }
        [Test] public void CancellationErrorStillReleasesLeaseOnlyOnce()
        {
            var go = new GameObject("asset-cleanup");
            try
            {
                var slot = go.AddComponent<AsyncSpriteSlot>(); int released = 0;
                typeof(AsyncSpriteSlot).GetField("_request", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(slot, new BadRequest());
                typeof(AsyncSpriteSlot).GetField("_release", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(slot, (Action)(() => { released++; throw new Exception("release"); }));
                Assert.DoesNotThrow(slot.ResetSlot); Assert.DoesNotThrow(slot.ResetSlot);
                Assert.That(released, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
