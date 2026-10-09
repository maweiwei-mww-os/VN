namespace HighPerfUI
{
    /// <summary>
    /// One small, resumable unit of UI work.
    /// ExecuteStep must keep each step reasonably small. The scheduler cannot interrupt
    /// a single expensive Unity API call such as Instantiate midway through execution.
    /// </summary>
    public interface IUiWorkItem
    {
        UiPriority Priority { get; }
        bool IsValid { get; }
        string DebugName { get; }

        /// <returns>true when the item is fully complete; false to continue later.</returns>
        bool ExecuteStep();
    }
}
