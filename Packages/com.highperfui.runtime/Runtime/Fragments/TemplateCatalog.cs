using System;
using System.Collections.Generic;
using UnityEngine;

namespace HighPerfUI
{
    public enum VirtualNodeState { Virtual, Queued, Realizing, Real, Recycled, Destroyed, Failed }
    [Serializable]
    public sealed class UiFragmentDescriptor
    {
        public string Id;
        public GameObject Prefab;
        public Transform Parent;
        public UiPriority Priority = UiPriority.Visible;
        public bool RequiredForInteraction;
        public int TemplateVersion = 1;
        public string[] Dependencies = new string[0];
    }
    public static class TemplateCatalog
    {
        public static void Validate(UiFragmentDescriptor[] descriptors)
        {
            if (descriptors == null) throw new ArgumentNullException(nameof(descriptors));
            var map = new Dictionary<string, UiFragmentDescriptor>();
            foreach (var descriptor in descriptors)
            {
                if (descriptor == null || string.IsNullOrEmpty(descriptor.Id) || descriptor.Prefab == null)
                    throw new ArgumentException("Fragments require an ID and prefab.");
                if (map.ContainsKey(descriptor.Id)) throw new ArgumentException("Duplicate fragment " + descriptor.Id);
                map.Add(descriptor.Id, descriptor);
            }
            var done = new HashSet<string>(); var visiting = new HashSet<string>();
            foreach (var id in map.Keys) Visit(id, map, done, visiting);
        }
        private static void Visit(string id, Dictionary<string, UiFragmentDescriptor> map, HashSet<string> done, HashSet<string> visiting)
        {
            if (done.Contains(id)) return;
            if (!map.TryGetValue(id, out var descriptor)) throw new ArgumentException("Unknown dependency " + id);
            if (!visiting.Add(id)) throw new ArgumentException("Cyclic fragment dependency " + id);
            foreach (var dependency in descriptor.Dependencies ?? Array.Empty<string>()) Visit(dependency, map, done, visiting);
            visiting.Remove(id); done.Add(id);
        }
    }
}
