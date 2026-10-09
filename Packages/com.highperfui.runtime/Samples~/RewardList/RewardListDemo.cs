using System.Collections.Generic;
using HighPerfUI;
using UnityEngine;

namespace HighPerfUI.Samples.RewardList
{
    public sealed class RewardListDemo : MonoBehaviour, IVirtualizedItemSource
    {
        private struct Reward
        {
            public long Id;
            public string Name;
            public int Count;
            public bool Claimed;
            public string IconKey;
        }

        [SerializeField] private FixedVirtualizedScrollList _list;
        [SerializeField, Min(1)] private int _itemCount = 10000;
        [SerializeField] private string _resourcesIconKey = "";

        private readonly List<Reward> _data = new List<Reward>();

        public int Count { get { return _data.Count; } }

        private void Start()
        {
            _data.Clear();
            _data.Capacity = Mathf.Max(_data.Capacity, _itemCount);
            for (int i = 0; i < _itemCount; ++i)
            {
                _data.Add(new Reward
                {
                    Id = i + 1,
                    Name = "Reward " + (i + 1),
                    Count = (i % 99) + 1,
                    Claimed = false,
                    IconKey = _resourcesIconKey
                });
            }
            if (_list != null) _list.SetSource(this);
        }

        public long GetStableId(int index)
        {
            return _data[index].Id;
        }

        public void Bind(PooledUiView view, int index)
        {
            RewardItemView item = view as RewardItemView;
            if (item == null) return;
            Reward data = _data[index];
            item.SetData(data.Name, data.Count, data.Claimed, data.IconKey);
        }

        public void Unbind(PooledUiView view, int index)
        {
            // View.OnReturn owns cleanup. Keep source unbind side-effect free.
        }

        public void IncrementVisibleDemoData(int stride = 3)
        {
            for (int i = 0; i < _data.Count; i += Mathf.Max(1, stride))
            {
                Reward r = _data[i];
                r.Count++;
                _data[i] = r;
            }
            if (_list != null) _list.RefreshVisible();
        }
    }
}
