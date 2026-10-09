using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace HighPerfUI.Reference
{
    [Serializable]
    public sealed class BenchmarkResult
    {
        public string Mode, UnityVersion, OS, CPU, GPU, Timestamp, Protocol, Status, SemanticHash;
        public int Items, Frames, WorkloadFrames, ActiveViews, NativeNodes, Width, Height;
        public double OpenMs, FrameP50Ms, FrameP95Ms, FrameP99Ms, MaxFrameMs, MaxSchedulerMs;
        public double OpeningBindingCpuMs, ScrollBindingCpuMs, UpdateBindingCpuMs, BusinessBindingCpuMs;
        public long BindingCalls, StateApplyCalls;
        public long GcAllocatedBytes, MonoBytes, TotalAllocatedBytes, PeakAllocatedBytes;
        public bool GcRecorderAvailable, VisibleStateCorrect;
        public bool RenderedPixelsValidated, GridPixelsValidated;
        public UiMetricsSnapshot Metrics;
        public string JsonPath;
    }

    public sealed class BenchmarkRunner : MonoBehaviour
    {
        private struct FrameSample { public float Ms; public double Scheduler; public long Gc; public int Queue; }
        private InventoryScreen _screen;
        private readonly List<BenchmarkResult> _results = new List<BenchmarkResult>();
        private bool _running;
        public string OutputOverride { get; set; }
        public string LastFailure { get; private set; }
        public void Initialize(InventoryScreen screen) { _screen = screen; }
        public void BeginComparison() { if (!_running) StartCoroutine(Compare()); }
        public void BeginCommandLine() { if (!_running) StartCoroutine(CommandLine()); }
        private string OutputDirectory => OutputOverride ?? InventoryScreen.Arg("--out", Path.GetFullPath(Path.Combine(Application.dataPath, Application.isEditor ? "../../Benchmarks" : "../../../Benchmarks")));

        private IEnumerator Compare()
        {
            var session = _screen.CaptureBusinessSession();
            _running = true; _screen.Measuring = true; _results.Clear(); LastFailure = null;
            int count = _screen.Model.Items.Count;
            try
            {
                for (int mode = 0; mode < 3; mode++)
                {
                    _screen.ResetDataset(count); _screen.SetMode(mode);
                    yield return GuardedCapture(360, "quick-same-process-shared-assets", false);
                    if (LastFailure != null) break;
                }
            }
            finally
            {
                try { _screen.RestoreBusinessSession(session); }
                finally { _screen.Measuring = false; _running = false; }
            }
            if (LastFailure != null) _screen.ShowReport("测试未完成，业务进度已恢复。\n\n" + LastFailure);
            else ShowReports();
        }
        private IEnumerator CommandLine()
        {
            _running = true; _screen.Measuring = true; LastFailure = null;
            try { yield return GuardedCapture(InventoryScreen.ArgInt("--frames", 1200), "independent-player-shared-art-prepared-v3", true); }
            finally { _screen.Measuring = false; _running = false; }
            if (LastFailure != null || _results.Count == 0 || _results[0].Status != "Passed") Application.Quit(2);
            else Application.Quit(0);
        }

        private IEnumerator GuardedCapture(int frames, string protocol, bool screenshot)
        {
            var capture = Capture(frames, protocol, screenshot);
            try
            {
                while (true)
                {
                    bool more; object current = null;
                    try { more = capture.MoveNext(); if (more) current = capture.Current; }
                    catch (Exception e) { LastFailure = e.Message; Debug.LogWarning("[HighPerfUI] Capture failed: " + e); break; }
                    if (!more) break;
                    yield return current;
                }
            }
            finally { (capture as IDisposable)?.Dispose(); }
        }

        private IEnumerator Capture(int sampleFrames, string protocol, bool screenshot)
        {
            var frames = new List<FrameSample>(sampleFrames + 600);
            var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            var result = new BenchmarkResult {
                Mode = ((char)('A' + _screen.Mode)).ToString(), Items = _screen.Model.Items.Count,
                UnityVersion = Application.unityVersion, OS = SystemInfo.operatingSystem, CPU = SystemInfo.processorType,
                GPU = SystemInfo.graphicsDeviceName, Width = Screen.width, Height = Screen.height,
                Timestamp = DateTime.UtcNow.ToString("O"), Protocol = protocol,
                WorkloadFrames = sampleFrames,
                Status = "Passed", GcRecorderAvailable = recorder.Valid, FrameP99Ms = -1
            };
            double maxScheduler = 0; long peak = Profiler.GetTotalAllocatedMemoryLong(), gcTotal = 0;
            double openingCpu = 0, scrollingCpu = 0, updateCpu = 0, businessCpu = 0;
            int openingFrames = 0;
            try
            {
                while (!_screen.Ready && _screen.AbortReason == null && openingFrames++ < 1200)
                {
                    yield return null;
                    Record(frames, recorder, ref maxScheduler, ref peak, ref gcTotal);
                }
                if (_screen.AbortReason != null || !_screen.Ready)
                    result.Status = _screen.AbortReason ?? "Open timed out";
                _screen.RecordOpeningIfReady(); result.OpenMs = _screen.OpenMs;
                openingCpu = BindingCpu();
                if (result.Status == "Passed")
                {
                    for (int i = 0; i < sampleFrames; i++)
                    {
                        double beforeCpu = BindingCpu();
                        if (i < sampleFrames / 2)
                            _screen.Collection.ScrollTo(Mathf.PingPong(i / 95f, 1));
                        if (i >= sampleFrames / 2 && i < sampleFrames * 5 / 6 && i % 30 == 0) _screen.BatchUpdate();
                        if (i == sampleFrames * 5 / 6) { _screen.SelectItem(8); _screen.EquipSelected(); _screen.UpgradeSelected(); _screen.Model.ClaimReward(RewardCatalog.Entries[0].Id); }
                        yield return null;
                        double elapsedCpu = BindingCpu() - beforeCpu;
                        if (i < sampleFrames / 2) scrollingCpu += elapsedCpu;
                        else if (i < sampleFrames * 5 / 6) updateCpu += elapsedCpu;
                        else businessCpu += elapsedCpu;
                        Record(frames, recorder, ref maxScheduler, ref peak, ref gcTotal);
                    }
                    _screen.Collection.ScrollTo(0);
                    for (int i = 0; i < 240 && (!_screen.Ready || _screen.Provider.PendingRequests > 0 || i < 12); i++)
                    { yield return null; Record(frames, recorder, ref maxScheduler, ref peak, ref gcTotal); }
                    result.VisibleStateCorrect = ValidateVisible();
                    if (!result.VisibleStateCorrect) result.Status = "Visible state mismatch";
                }
            }
            finally { recorder.Dispose(); }

            result.Frames = frames.Count;
            var sorted = new double[frames.Count];
            for (int i = 0; i < frames.Count; i++) sorted[i] = frames[i].Ms;
            Array.Sort(sorted);
            result.FrameP50Ms = Percentile(sorted, .5); result.FrameP95Ms = Percentile(sorted, .95);
            if (sorted.Length >= 1000) result.FrameP99Ms = Percentile(sorted, .99);
            result.MaxFrameMs = sorted.Length > 0 ? sorted[sorted.Length - 1] : -1;
            result.MaxSchedulerMs = maxScheduler; result.GcAllocatedBytes = result.GcRecorderAvailable ? gcTotal : -1;
            result.OpeningBindingCpuMs = openingCpu; result.ScrollBindingCpuMs = scrollingCpu;
            result.UpdateBindingCpuMs = updateCpu; result.BusinessBindingCpuMs = businessCpu;
            result.BindingCalls = EquipmentCell.BindCalls; result.StateApplyCalls = EquipmentCell.ApplyCalls;
            result.PeakAllocatedBytes = peak; result.MonoBytes = Profiler.GetMonoUsedSizeLong();
            result.TotalAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong(); result.Metrics = UiMetrics.Snapshot();
            result.ActiveViews = _screen.Collection != null ? _screen.Collection.ActiveCount : 0;
            result.NativeNodes = _screen.Collection != null ? _screen.Collection.GetComponentsInChildren<Transform>(true).Length : 0;
            result.SemanticHash = HashModel(_screen.Model);
            Directory.CreateDirectory(OutputDirectory);
            string prefix = "mode-" + result.Mode + "-" + result.Items + "-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff");
            result.JsonPath = Path.Combine(OutputDirectory, prefix + ".json");
            File.WriteAllText(result.JsonPath, JsonUtility.ToJson(result, true), Encoding.UTF8);
            var csv = new StringBuilder("frame,frame_ms,scheduler_ms,gc_alloc_bytes,pending_work\n");
            for (int i = 0; i < frames.Count; i++)
            {
                var f = frames[i]; csv.Append(i).Append(',').Append(f.Ms.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
                    .Append(f.Scheduler.ToString("F4", CultureInfo.InvariantCulture)).Append(',').Append(f.Gc).Append(',').Append(f.Queue).Append('\n');
            }
            File.WriteAllText(Path.Combine(OutputDirectory, prefix + ".csv"), csv.ToString(), Encoding.UTF8);
            _results.Add(result);
            _screen.ShowMeasurement(result);
            _screen.SetStatus(result.Mode + "  " + result.Status + "  /  " + result.Frames + " 帧  /  报告已导出");
            Debug.Log("[HighPerfUI] Benchmark " + result.JsonPath + " " + result.Status);
            if (screenshot && !Application.isBatchMode)
            {
                yield return new WaitForEndOfFrame();
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                var pixels = texture.GetPixels32(); int nonBlack = 0; var colors = new HashSet<uint>();
                for (int i = 0; i < pixels.Length; i += 137)
                {
                    var p = pixels[i];
                    if (p.r + p.g + p.b > 60) nonBlack++;
                    colors.Add((uint)(p.r << 16 | p.g << 8 | p.b));
                }
                result.RenderedPixelsValidated = nonBlack > 100 && colors.Count > 20;
                result.GridPixelsValidated = HasGridPixels(texture, _screen.GridPixelRect());
                if (!result.RenderedPixelsValidated || !result.GridPixelsValidated) result.Status = "Invalid rendering: blank capture or grid";
                File.WriteAllBytes(Path.Combine(OutputDirectory, prefix + ".png"), texture.EncodeToPNG());
                Destroy(texture);
                File.WriteAllText(result.JsonPath, JsonUtility.ToJson(result, true), Encoding.UTF8);
                if (InventoryScreen.HasArg("--capture-rewards"))
                {
                    _screen.Measuring = false; _screen.OpenRewardCenter();
                    for (int i = 0; i < 20; i++) yield return null;
                    yield return new WaitForEndOfFrame();
                    var rewardTexture = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(OutputDirectory, "rewards-" + Screen.width + "x" + Screen.height + ".png"), rewardTexture.EncodeToPNG());
                    Destroy(rewardTexture); _screen.CloseModal(); _screen.Measuring = true;
                }
            }
        }
        private void Record(List<FrameSample> frames, ProfilerRecorder recorder, ref double maxScheduler, ref long peak, ref long gcTotal)
        {
            long gc = recorder.Valid ? recorder.LastValue : -1;
            double scheduler = _screen.Runtime.Scheduler.LastFrameMs;
            frames.Add(new FrameSample { Ms = Time.unscaledDeltaTime * 1000, Scheduler = scheduler, Gc = gc, Queue = _screen.Runtime.Scheduler.PendingCount });
            maxScheduler = Math.Max(maxScheduler, scheduler);
            if (gc >= 0) gcTotal += gc;
            peak = Math.Max(peak, Profiler.GetTotalAllocatedMemoryLong());
        }
        public bool ValidateVisible()
        {
            if (!_screen.Ready) return false;
            var list = _screen.Collection;
            int expectedCount = _screen.Count == 0 ? 0 : list.LastRequestedIndex - list.FirstRequestedIndex + 1;
            if (list.ActiveCount != expectedCount) return false;
            var ids = new HashSet<long>();
            foreach (var pair in list.ActiveBindings)
            {
                if (pair.Key < list.FirstRequestedIndex || pair.Key > list.LastRequestedIndex || pair.Key >= _screen.Count) return false;
                long id = _screen.GetStableId(pair.Key);
                var cell = (EquipmentCell)pair.Value; var item = _screen.Model.Find(id);
                if (!ids.Add(id) || item == null || !cell.Matches(item, id == _screen.SelectedId,
                    _screen.IsEquipped(item), _screen.Provider.Icons[item.Kind])) return false;
            }
            return true;
        }
        public static bool HasGridPixels(Texture2D texture, Rect rect)
        {
            if (rect.xMin < 0 || rect.yMin < 0 || rect.xMax > texture.width + 1 || rect.yMax > texture.height + 1) return false;
            var colors = new HashSet<Color32>(); int bright = 0;
            var pixels = texture.GetPixels32();
            for (int y = Mathf.Max(0, (int)rect.yMin); y < Mathf.Min(texture.height, (int)rect.yMax); y += 7)
                for (int x = Mathf.Max(0, (int)rect.xMin); x < Mathf.Min(texture.width, (int)rect.xMax); x += 7)
                {
                    var p = pixels[y * texture.width + x]; colors.Add(p);
                    if (p.r + p.g + p.b > 240) bright++;
                }
            return bright > 80 && colors.Count > 30;
        }
        public static double Percentile(double[] sorted, double quantile)
        { return sorted.Length == 0 ? -1 : sorted[Mathf.Clamp((int)Math.Ceiling(sorted.Length * quantile) - 1, 0, sorted.Length - 1)]; }
        private static string HashModel(InventoryModel model)
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                foreach (var item in model.Items)
                {
                    hash ^= (ulong)(item.Id * 397 + item.Power * 17 + item.Level + item.Kind * 13 + item.Rarity * 7 + item.Mastery * 31 + item.SetId * 43 + item.Quantity * 47 + item.ExpiresAtUtc);
                    hash *= 1099511628211UL;
                    hash ^= item.Locked ? 1UL : 0UL; hash *= 1099511628211UL;
                    foreach (char c in item.Name) { hash ^= c; hash *= 1099511628211UL; }
                }
                foreach (long id in model.Equipped) { hash ^= (ulong)id; hash *= 1099511628211UL; }
                hash ^= (ulong)model.Gold; hash *= 1099511628211UL;
                hash ^= (ulong)model.Ore; hash *= 1099511628211UL;
                hash ^= (ulong)model.Revision; hash *= 1099511628211UL;
                foreach (var reward in RewardCatalog.Entries) { hash ^= model.IsRewardClaimed(reward.Id) ? (ulong)reward.Id : 0; hash *= 1099511628211UL; }
                return hash.ToString("x16");
            }
        }
        private static double BindingCpu() => EquipmentCell.BindCpuMs + EquipmentCell.ApplyCpuMs - EquipmentCell.SynchronousApplyCpuMs;
        public void ShowReports()
        {
            if (_running) return;
            if (_results.Count == 0) { _screen.ShowReport("尚未运行对比。\n\n报告目录：\n" + OutputDirectory); return; }
            var text = new StringBuilder("同进程快速对比 / 共享素材缓存 / 非独立进程冷启动\n\n");
            foreach (var result in _results)
                text.Append(result.Mode).Append("   ").Append(result.Status).Append("    ").Append(result.Items).Append(" 项\n")
                    .Append("首屏可交互 ").Append(result.OpenMs.ToString("F1")).Append(" ms    整帧 P95 ").Append(result.FrameP95Ms.ToString("F2"))
                    .Append(" ms    Item ").Append(result.ActiveViews).Append("    节点 ").Append(result.NativeNodes).Append('\n')
                    .Append("创建 ").Append(result.Metrics.Instantiations).Append("    托管内存 ").Append((result.MonoBytes / 1048576.0).ToString("F1"))
                    .Append(" MB    业务摘要 ").Append(result.SemanticHash).Append("\n\n");
            text.Append("原始 JSON / CSV 已导出；不足 1000 帧不报告 P99。\n").Append(OutputDirectory);
            _screen.ShowReport(text.ToString());
        }
    }
}
