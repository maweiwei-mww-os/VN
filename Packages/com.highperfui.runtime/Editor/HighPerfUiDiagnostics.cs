using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace HighPerfUI.Editor
{
    public sealed class HighPerfUiDiagnostics : EditorWindow
    {
        [MenuItem("Window/HighPerfUI/Runtime Diagnostics")]
        private static void Open() { GetWindow<HighPerfUiDiagnostics>("HighPerfUI"); }
        private void OnInspectorUpdate() { Repaint(); }
        private void OnGUI()
        {
            var runtime = Object.FindObjectOfType<UiRuntime>();
            if (runtime == null || runtime.Scheduler == null) { EditorGUILayout.LabelField("Enter Play mode with a UiRuntime."); return; }
            EditorGUILayout.LabelField("State", runtime.State.ToString());
            EditorGUILayout.LabelField("Frame budget (ms)", runtime.FrameBudgetMs.ToString("F2"));
            EditorGUILayout.LabelField("Scheduler last (ms)", runtime.Scheduler.LastFrameMs.ToString("F3"));
            EditorGUILayout.LabelField("Pending work", runtime.Scheduler.PendingCount.ToString());
            EditorGUILayout.LabelField("Oldest wait (frames)", runtime.Scheduler.OldestWaitFrames.ToString());
            var metrics = UiMetrics.Snapshot();
            EditorGUILayout.LabelField("Faults", metrics.Faults.ToString());
            EditorGUILayout.LabelField("Pool hits / misses", metrics.PoolHits + " / " + metrics.PoolMisses);
            EditorGUILayout.LabelField("Stale results", metrics.AsyncStaleDropped.ToString());
            if (GUILayout.Button("Validate fragment descriptors")) ValidateFragments();
        }
        [MenuItem("Tools/HighPerfUI/Validate Fragment Descriptors")]
        private static void ValidateFragments()
        {
            int count = 0;
            foreach (var host in Resources.FindObjectsOfTypeAll<LazyFragmentHost>())
            {
                var field = typeof(LazyFragmentHost).GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
                try { TemplateCatalog.Validate((UiFragmentDescriptor[])field.GetValue(host)); count++; }
                catch (System.Exception error) { Debug.LogError(host.name + ": " + error.Message, host); }
            }
            Debug.Log("[HighPerfUI] Validated " + count + " fragment hosts.");
        }
    }
}
