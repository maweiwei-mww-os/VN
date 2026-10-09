using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace HighPerfUI.Tests
{
    public sealed class CommandAndFragmentTests
    {
        [Test] public void CommandsKeepOrderAndDropStaleBinding()
        {
            var type = typeof(UiRuntime).Assembly.GetType("HighPerfUI.UiCommandQueue");
            Assert.That(type, Is.Not.Null, "Ordered commands are a runtime contract");
            var scheduler = new FrameBudgetScheduler();
            var queue = Activator.CreateInstance(type, scheduler);
            var method = type.GetMethod("Enqueue", new[] { typeof(UiBindingToken), typeof(Action) });
            var go = new GameObject("command-owner");
            try
            {
                var view = go.AddComponent<ProbeView>(); view.OnRent(); view.BeginBind(1);
                var seen = new List<int>();
                method.Invoke(queue, new object[] { view.CaptureBinding(), (Action)(() => seen.Add(1)) });
                method.Invoke(queue, new object[] { view.CaptureBinding(), (Action)(() => seen.Add(2)) });
                scheduler.RunFrame(100); scheduler.RunFrame(100);
                CollectionAssert.AreEqual(new[] { 1, 2 }, seen);
                method.Invoke(queue, new object[] { view.CaptureBinding(), (Action)(() => seen.Add(3)) });
                view.BeginBind(2); scheduler.RunFrame(100);
                Assert.That(seen.Count, Is.EqualTo(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test] public void LazyFragmentsExposeBindingAwareConfiguration()
        {
            Assert.That(typeof(LazyFragmentHost).GetMethod("Configure"), Is.Not.Null);
            Assert.That(typeof(LazyFragmentHost).GetMethod("ResetBinding"), Is.Not.Null);
        }

        [Test] public void DifferentObjectsWithSameWorkKeyCoalesceToLatest()
        {
            var scheduler = new FrameBudgetScheduler();
            var old = new Keyed(); var latest = new Keyed();
            scheduler.Enqueue(old); scheduler.Enqueue(latest); scheduler.RunFrame(100);
            Assert.That(old.Calls, Is.Zero); Assert.That(latest.Calls, Is.EqualTo(1));
        }
        private sealed class Keyed : IKeyedUiWorkItem
        {
            public int Calls;
            public UiWorkKey Key => new UiWorkKey(1, 1, 1, 1);
            public UiPriority Priority => UiPriority.Visible;
            public bool IsValid => true;
            public string DebugName => "key";
            public bool ExecuteStep() { Calls++; return true; }
        }
    }
}
