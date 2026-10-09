using System;
using System.Collections.Generic;

namespace HighPerfUI
{
    public sealed class UiCommandQueue : IUiWorkItem
    {
        private struct Command { public UiBindingToken Binding; public Action Execute; public Func<bool> Ready; }
        private readonly Queue<Command> _commands = new Queue<Command>();
        private readonly FrameBudgetScheduler _scheduler;
        public UiCommandQueue(FrameBudgetScheduler scheduler) { _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler)); }
        public UiPriority Priority => UiPriority.Interaction;
        public bool IsValid => _commands.Count > 0;
        public string DebugName => "Ordered UI commands";
        public int PendingCount => _commands.Count;
        public void Enqueue(UiBindingToken binding, Action execute) { Enqueue(binding, execute, null); }
        public void Enqueue(UiBindingToken binding, Action execute, Func<bool> requiredStateReady)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            _commands.Enqueue(new Command { Binding = binding, Execute = execute, Ready = requiredStateReady });
            _scheduler.Enqueue(this);
        }
        public void Clear() { _commands.Clear(); _scheduler.Cancel(this); }
        public bool ExecuteStep()
        {
            if (_commands.Count == 0) return true;
            var command = _commands.Peek();
            if (!command.Binding.IsCurrent) _commands.Dequeue();
            else
            {
                try { if (command.Ready != null && !command.Ready()) return false; }
                catch
                {
                    _commands.Dequeue();
                    if (_commands.Count > 0) _scheduler.Enqueue(this);
                    throw;
                }
                _commands.Dequeue();
                try { command.Execute(); }
                finally { if (_commands.Count > 0) _scheduler.Enqueue(this); }
            }
            return _commands.Count == 0;
        }
    }
}
