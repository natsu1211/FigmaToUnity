using System;
using System.Collections.Generic;
using System.Linq;
using FigmaToUnity.Core;
using UnityEditor;

namespace FigmaToUnity.Editor.State
{
    [FilePath("ProjectSettings/LongGames.FigmaImporter.FrameCache.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class FigmaFrameCacheStore : ScriptableSingleton<FigmaFrameCacheStore>
    {
        public List<FrameCacheEntry> Entries = new();

        public bool TryGet(string fileKey, int depth, out FrameCacheEntry? entry)
        {
            entry = Entries.FirstOrDefault(candidate =>
                string.Equals(candidate.FileKey, fileKey, StringComparison.Ordinal) &&
                candidate.Depth == depth);
            return entry != null;
        }

        public void Upsert(
            string fileKey,
            int depth,
            string fileName,
            string? lastModified,
            string? version,
            IReadOnlyList<FrameSummary> frames,
            IReadOnlyCollection<string> selectedFrameIds)
        {
            FrameCacheEntry entry = GetOrCreate(fileKey, depth);
            entry.FileName = fileName ?? string.Empty;
            entry.LastModified = lastModified ?? string.Empty;
            entry.Version = version ?? string.Empty;
            entry.CachedAtTicks = DateTime.UtcNow.Ticks;

            entry.Frames.Clear();
            foreach (FrameSummary frame in frames)
            {
                entry.Frames.Add(new CachedFrameSummary(frame));
            }

            entry.SelectedFrameIds.Clear();
            entry.SelectedFrameIds.AddRange(selectedFrameIds.Where(id => !string.IsNullOrWhiteSpace(id)));
            Save(true);
        }

        public void UpdateSelection(string fileKey, int depth, IReadOnlyCollection<string> selectedFrameIds)
        {
            if (!TryGet(fileKey, depth, out FrameCacheEntry? entry) || entry == null)
            {
                return;
            }

            entry.SelectedFrameIds.Clear();
            entry.SelectedFrameIds.AddRange(selectedFrameIds.Where(id => !string.IsNullOrWhiteSpace(id)));
            Save(true);
        }

        private FrameCacheEntry GetOrCreate(string fileKey, int depth)
        {
            if (TryGet(fileKey, depth, out FrameCacheEntry? entry) && entry != null)
            {
                return entry;
            }

            entry = new FrameCacheEntry
            {
                FileKey = fileKey,
                Depth = depth
            };
            Entries.Add(entry);
            return entry;
        }
    }

    [Serializable]
    internal sealed class FrameCacheEntry
    {
        public string FileKey = string.Empty;
        public int Depth = 1;
        public string FileName = string.Empty;
        public string LastModified = string.Empty;
        public string Version = string.Empty;
        public long CachedAtTicks;
        public List<CachedFrameSummary> Frames = new();
        public List<string> SelectedFrameIds = new();
    }

    [Serializable]
    internal sealed class CachedFrameSummary
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public float Width;
        public float Height;

        public CachedFrameSummary()
        {
        }

        public CachedFrameSummary(FrameSummary frame)
        {
            Id = frame.Id;
            Name = frame.Name;
            Width = frame.Width;
            Height = frame.Height;
        }

        public FrameSummary ToFrameSummary()
        {
            return new FrameSummary(Id, Name, Width, Height);
        }
    }
}
