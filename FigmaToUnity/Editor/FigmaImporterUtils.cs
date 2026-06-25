using System.Collections.Generic;
using System.IO;
using FigmaToUnity.Core;

namespace FigmaToUnity.Editor
{
    internal static class FigmaImporterUtils
    {
        public static string ToAssetFolderPath(string rootFolder, string subfolderName)
        {
            string sanitized = FigmaNameSanitizer.Sanitize(subfolderName);
            return $"{rootFolder.TrimEnd('/', '\\')}/{sanitized}";
        }

        public static FigmaNode GetRootFrame(FigmaNode node)
        {
            FigmaNode current = node;
            while (current.Parent != null)
            {
                current = current.Parent;
            }

            return current;
        }

        public static string NormalizeAssetPath(string path)
        {
            return path.Replace('\\', '/');
        }

        public static string CombineAssetPath(string left, string right)
        {
            return NormalizeAssetPath(Path.Combine(left, right));
        }

        public static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> items, int chunkSize)
        {
            if (chunkSize <= 0)
            {
                chunkSize = 1;
            }

            for (int i = 0; i < items.Count; i += chunkSize)
            {
                List<T> chunk = new();
                int end = i + chunkSize;
                if (end > items.Count)
                {
                    end = items.Count;
                }

                for (int j = i; j < end; j++)
                {
                    chunk.Add(items[j]);
                }

                yield return chunk;
            }
        }
    }
}
