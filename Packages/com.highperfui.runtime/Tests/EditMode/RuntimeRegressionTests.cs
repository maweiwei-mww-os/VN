using System;
using NUnit.Framework;

namespace HighPerfUI.Tests
{
    public sealed class RuntimeRegressionTests
    {
        private sealed class Work : IUiWorkItem
        {
            public UiPriority Priority { get; set; } = UiPriority.Visible;
            public bool IsValid { get; set; } = true;
            public string DebugName => "regression";
            public int Calls;
            public Func<bool> Step;
            public bool ExecuteStep() { Calls++; return Step == null || Step(); }
        }

        [Test] public void DuplicateItemCoalesces()
        {
            var scheduler = new FrameBudgetScheduler();
            var work = new Work();
            Assert.That(scheduler.Enqueue(work), Is.True);
            Assert.That(scheduler.Enqueue(work), Is.False);
            Assert.That(scheduler.PendingCount, Is.EqualTo(1));
        }

        [Test] public void NewlyEnqueuedWorkWaitsUntilNextFrame()
        {
            var scheduler = new FrameBudgetScheduler();
            var child = new Work();
            scheduler.Enqueue(new Work { Step = () => { scheduler.Enqueue(child); return true; } });
            scheduler.RunFrame(100);
            Assert.That(child.Calls, Is.Zero);
            scheduler.RunFrame(100);
            Assert.That(child.Calls, Is.EqualTo(1));
        }

        [Test] public void ContinuationExecutesOncePerFrame()
        {
            var scheduler = new FrameBudgetScheduler();
            var work = new Work { Step = () => false };
            scheduler.Enqueue(work);
            scheduler.RunFrame(100, 16);
            Assert.That(work.Calls, Is.EqualTo(1));
            Assert.That(scheduler.PendingCount, Is.EqualTo(1));
        }

        [Test] public void ThrowingWorkDoesNotPoisonQueue()
        {
            var scheduler = new FrameBudgetScheduler();
            var bad = new Work { Step = () => throw new InvalidOperationException("injected") };
            var good = new Work();
            scheduler.Enqueue(bad);
            scheduler.Enqueue(good);
            Assert.DoesNotThrow(() => scheduler.RunFrame(100));
            Assert.That(good.Calls, Is.EqualTo(1));
            Assert.That(scheduler.PendingCount, Is.Zero);
            Assert.That(scheduler.Enqueue(bad), Is.True);
        }

        [Test] public void WorkRequeuedDuringCompletionIsNotLost()
        {
            var scheduler = new FrameBudgetScheduler();
            Work work = null;
            work = new Work { Step = () => { if (work.Calls == 1) scheduler.Enqueue(work); return true; } };
            scheduler.Enqueue(work);
            scheduler.RunFrame(100);
            scheduler.RunFrame(100);
            Assert.That(work.Calls, Is.EqualTo(2));
        }

        [Test] public void InvalidWorkNeverExecutes()
        {
            var scheduler = new FrameBudgetScheduler();
            var work = new Work();
            scheduler.Enqueue(work);
            work.IsValid = false;
            scheduler.RunFrame(100);
            Assert.That(work.Calls, Is.Zero);
            Assert.That(scheduler.PendingCount, Is.Zero);
        }

        [Test] public void ThrowingValidityCheckDoesNotStopOtherWork()
        {
            var scheduler = new FrameBudgetScheduler(); var bad = new BadValidity(); var good = new Work();
            scheduler.Enqueue(bad); scheduler.Enqueue(good); bad.Throw = true;
            Assert.DoesNotThrow(() => scheduler.RunFrame(100));
            Assert.That(good.Calls, Is.EqualTo(1)); Assert.That(scheduler.PendingCount, Is.Zero);
        }
        private sealed class BadValidity : IUiWorkItem
        {
            public bool Throw;
            public UiPriority Priority => UiPriority.Visible;
            public bool IsValid => Throw ? throw new InvalidOperationException("validity") : true;
            public string DebugName => "bad validity";
            public bool ExecuteStep() => true;
        }
    }
}
