using NUnit.Framework;

namespace HighPerfUI.Tests
{
    public sealed class SchedulerTests
    {
        private sealed class Work : IUiWorkItem
        {
            public UiPriority Priority { get; set; }
            public bool IsValid { get; set; } = true;
            public string DebugName { get { return "test"; } }
            public int Remaining;
            public int Calls;

            public bool ExecuteStep()
            {
                Calls++;
                Remaining--;
                return Remaining <= 0;
            }
        }

        [Test]
        public void EnqueueSameItem_Coalesces()
        {
            var scheduler = new FrameBudgetScheduler();
            var work = new Work { Priority = UiPriority.Visible, Remaining = 1 };
            Assert.IsTrue(scheduler.Enqueue(work));
            Assert.IsFalse(scheduler.Enqueue(work));
            Assert.AreEqual(1, scheduler.PendingCount);
        }

        [Test]
        public void IncompleteItem_IsRescheduledUntilDone()
        {
            var scheduler = new FrameBudgetScheduler();
            var work = new Work { Priority = UiPriority.Visible, Remaining = 3 };
            scheduler.Enqueue(work);
            scheduler.RunFrame(100.0, 16);
            Assert.AreEqual(3, work.Calls);
            Assert.AreEqual(0, scheduler.PendingCount);
        }

        [Test]
        public void InvalidItem_IsDropped()
        {
            var scheduler = new FrameBudgetScheduler();
            var work = new Work { Priority = UiPriority.Visible, Remaining = 1 };
            scheduler.Enqueue(work);
            work.IsValid = false;
            scheduler.RunFrame(100.0, 16);
            Assert.AreEqual(0, work.Calls);
            Assert.AreEqual(0, scheduler.PendingCount);
        }
    }
}
