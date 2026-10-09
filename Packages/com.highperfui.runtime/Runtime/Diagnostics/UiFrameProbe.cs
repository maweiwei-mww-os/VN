using System;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

namespace HighPerfUI
{
    public sealed class UiFrameProbe : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public int Frames;
            public float P50Ms;
            public float P95Ms;
            public float P99Ms;
            public float MaxMs;
            public long MemoryStart;
            public long MemoryEnd;
            public UiMetricsSnapshot MetricsStart;
            public UiMetricsSnapshot MetricsEnd;
        }

        [SerializeField, Min(30)] private int _sampleFrames = 600;
        [SerializeField] private bool _writeJson = true;
        [SerializeField] private string _fileName = "high_perf_ui_probe.json";

        private float[] _samples;
        private int _index;
        private long _memoryStart;
        private UiMetricsSnapshot _metricsStart;

        private void OnEnable()
        {
            _samples = new float[_sampleFrames];
            _index = 0;
            _memoryStart = Profiler.GetTotalAllocatedMemoryLong();
            _metricsStart = UiMetrics.Snapshot();
        }

        private void Update()
        {
            if (_samples == null || _index >= _samples.Length) return;
            _samples[_index++] = Time.unscaledDeltaTime * 1000f;
            if (_index == _samples.Length) Finish();
        }

        private void Finish()
        {
            float[] sorted = new float[_samples.Length];
            Array.Copy(_samples, sorted, _samples.Length);
            Array.Sort(sorted);

            Result result = new Result
            {
                Frames = sorted.Length,
                P50Ms = Percentile(sorted, 0.50f),
                P95Ms = Percentile(sorted, 0.95f),
                P99Ms = Percentile(sorted, 0.99f),
                MaxMs = sorted[sorted.Length - 1],
                MemoryStart = _memoryStart,
                MemoryEnd = Profiler.GetTotalAllocatedMemoryLong(),
                MetricsStart = _metricsStart,
                MetricsEnd = UiMetrics.Snapshot()
            };

            string json = JsonUtility.ToJson(result, true);
            Debug.Log("[HighPerfUI] Probe result\n" + json);
            if (_writeJson)
            {
                string path = Path.Combine(Application.persistentDataPath, _fileName);
                File.WriteAllText(path, json);
                Debug.Log("[HighPerfUI] Probe written to " + path);
            }
        }

        private static float Percentile(float[] sorted, float percentile)
        {
            int index = Mathf.Clamp(Mathf.CeilToInt(sorted.Length * percentile) - 1, 0, sorted.Length - 1);
            return sorted[index];
        }
    }
}
