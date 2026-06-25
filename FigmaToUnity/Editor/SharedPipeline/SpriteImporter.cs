using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FigmaToUnity.Core;
using FigmaToUnity.Editor.ImportPipeline;
using UnityEditor;
using UnityEngine;

namespace FigmaToUnity.Editor.SharedPipeline
{
    // Figma image-ref → Unity sprite asset pipeline. Lives in SharedPipeline because
    // both UGUI and (future) UIToolkit backends consume the same node-id → asset-path
    // mapping; UGUI binds the Sprite onto Image components, UIToolkit will reference
    // the same .png via USS background-image / Image elements.
    internal readonly struct SpriteImportProgress
    {
        public SpriteImportProgress(string stage, int current, int total, string? log = null)
        {
            Stage = stage;
            Current = current;
            Total = total;
            Log = log;
        }

        public string Stage { get; }
        public int Current { get; }
        public int Total { get; }
        public string? Log { get; }
    }

    internal sealed class SpriteImporter
    {
        private const int ImageUrlChunkSize = 50;

        private readonly FigmaApiClient _apiClient;
        private readonly bool _rasterizeUnsupportedText;
        private readonly bool _omitMaskSprites;
        private readonly bool _omitSpriteBorders;
        private readonly float? _spriteBorderScaleOverride;

        public SpriteImporter(
            FigmaApiClient apiClient,
            bool rasterizeUnsupportedText = false,
            bool omitMaskSprites = false,
            bool omitSpriteBorders = false,
            float? spriteBorderScaleOverride = null)
        {
            _apiClient = apiClient;
            _rasterizeUnsupportedText = rasterizeUnsupportedText;
            _omitMaskSprites = omitMaskSprites;
            _omitSpriteBorders = omitSpriteBorders;
            _spriteBorderScaleOverride = spriteBorderScaleOverride;
        }

        public async Task<int> ImportAsync(
            IReadOnlyList<FigmaNode> rootNodes,
            string token,
            string fileKey,
            string outputRoot,
            CancellationToken cancellationToken,
            IProgress<SpriteImportProgress>? progress = null)
        {
            List<FigmaNode> spriteNodes = CollectDownloadableNodes(rootNodes);
            return await ImportNodesAsync(spriteNodes, token, fileKey, outputRoot, cancellationToken, null, progress);
        }

        public async Task<int> ImportNodesAsync(
            IReadOnlyList<FigmaNode> spriteNodes,
            string token,
            string fileKey,
            string outputRoot,
            CancellationToken cancellationToken,
            HashSet<string>? forceRefreshNodeIds = null,
            IProgress<SpriteImportProgress>? progress = null)
        {
            if (spriteNodes.Count == 0)
            {
                progress?.Report(new SpriteImportProgress("Download Sprites", 0, 0, "No sprites required."));
                return 0;
            }

            bool reuseExistingSprites = FigmaImportSettings.instance.ReuseExistingSprites;
            List<FigmaNode> unresolvedNodes = PrepareCachedSprites(spriteNodes, outputRoot, reuseExistingSprites, forceRefreshNodeIds);
            int reusedCount = spriteNodes.Count - unresolvedNodes.Count;
            if (reusedCount > 0)
            {
                progress?.Report(new SpriteImportProgress("Download Sprites", reusedCount, spriteNodes.Count, $"Reused {reusedCount} cached sprite(s)."));
            }
            if (unresolvedNodes.Count == 0)
            {
                return spriteNodes.Count;
            }

            List<FigmaNode> nodesToImportFromDisk = new();
            List<FigmaNode> nodesToDownload = new();
            foreach (FigmaNode node in unresolvedNodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool forceRefresh = forceRefreshNodeIds != null && forceRefreshNodeIds.Contains(node.Id);
                if (reuseExistingSprites && !forceRefresh && File.Exists(Path.GetFullPath(node.AssetPath)))
                {
                    nodesToImportFromDisk.Add(node);
                }
                else
                {
                    nodesToDownload.Add(node);
                }
            }

            if (nodesToDownload.Count > 0)
            {
                Dictionary<string, string> imageUrlMap = await FetchImageUrlsAsync(nodesToDownload, token, fileKey, cancellationToken, progress);
                await DownloadSpritesAsync(nodesToDownload, imageUrlMap, cancellationToken, progress);
            }

            List<FigmaNode> nodesToImport = new(nodesToImportFromDisk.Count + nodesToDownload.Count);
            nodesToImport.AddRange(nodesToImportFromDisk);
            nodesToImport.AddRange(nodesToDownload);
            if (nodesToImport.Count > 0)
            {
                await ImportSpritesAsync(nodesToImport, cancellationToken, progress);
            }

            return spriteNodes.Count;
        }

        private async Task<Dictionary<string, string>> FetchImageUrlsAsync(
            IReadOnlyList<FigmaNode> nodes,
            string token,
            string fileKey,
            CancellationToken cancellationToken,
            IProgress<SpriteImportProgress>? progress)
        {
            Dictionary<string, string> imageUrls = new();
            List<string> nodeIds = new(nodes.Count);
            foreach (FigmaNode node in nodes)
            {
                nodeIds.Add(node.Id);
            }

            int totalChunks = (nodeIds.Count + ImageUrlChunkSize - 1) / ImageUrlChunkSize;
            int chunkIndex = 0;
            int resolvedUrls = 0;
            // Progress for this stage advances per *batch* — Figma's /images
            // endpoint can take 10-30s per call, so per-node granularity would
            // sit at 0/N forever. Batch-level lets the bar move immediately
            // when each request starts.
            progress?.Report(new SpriteImportProgress("Fetch Sprite URLs", 0, totalChunks, $"Requesting {nodeIds.Count} sprite URL(s) in {totalChunks} batch(es)."));

            foreach (List<string> chunk in FigmaImporterUtils.Chunk(nodeIds, ImageUrlChunkSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                chunkIndex++;

                // Optimistic pre-advance so the progress bar visibly ticks the
                // moment a request leaves; will be reaffirmed on completion.
                progress?.Report(new SpriteImportProgress(
                    "Fetch Sprite URLs",
                    chunkIndex,
                    totalChunks,
                    $"Requesting URL batch {chunkIndex}/{totalChunks} ({chunk.Count} id(s))... resolved so far: {resolvedUrls}/{nodeIds.Count}"));

                FigmaImageUrlsResponse response = await _apiClient.FetchImageUrlsAsync(token, fileKey, chunk, FigmaImportSettings.instance.SpriteScale, cancellationToken);
                if (response.Images != null)
                {
                    foreach ((string nodeId, string url) in response.Images)
                    {
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            imageUrls[nodeId] = url;
                            resolvedUrls++;
                        }
                    }
                }

                progress?.Report(new SpriteImportProgress(
                    "Fetch Sprite URLs",
                    chunkIndex,
                    totalChunks,
                    $"Fetched URL batch {chunkIndex}/{totalChunks} ({resolvedUrls}/{nodeIds.Count} resolved)."));
            }

            return imageUrls;
        }

        private async Task DownloadSpritesAsync(
            IReadOnlyList<FigmaNode> nodes,
            IReadOnlyDictionary<string, string> imageUrlMap,
            CancellationToken cancellationToken,
            IProgress<SpriteImportProgress>? progress)
        {
            int maxConcurrency = Math.Max(1, FigmaImportSettings.instance.MaxConcurrentDownloads);
            using SemaphoreSlim semaphore = new(maxConcurrency);
            ConcurrentBag<Exception> exceptions = new();
            List<Task> tasks = new();

            int totalDownloads = 0;
            foreach (FigmaNode node in nodes)
            {
                if (imageUrlMap.TryGetValue(node.Id, out string? url) && !string.IsNullOrWhiteSpace(url))
                {
                    totalDownloads++;
                }
            }

            int completed = 0;
            int reportInterval = Math.Max(1, totalDownloads / 50);
            progress?.Report(new SpriteImportProgress("Download Sprites", 0, totalDownloads, $"Starting {totalDownloads} download(s) (concurrency={maxConcurrency})."));

            foreach (FigmaNode node in nodes)
            {
                if (!imageUrlMap.TryGetValue(node.Id, out string? url) || string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                await semaphore.WaitAsync(cancellationToken);
                tasks.Add(DownloadSingleSpriteAsync(node, url, semaphore, exceptions, cancellationToken, () =>
                {
                    int done = Interlocked.Increment(ref completed);
                    if (done == totalDownloads || done % reportInterval == 0)
                    {
                        progress?.Report(new SpriteImportProgress("Download Sprites", done, totalDownloads));
                    }
                }));
            }

            await Task.WhenAll(tasks);
            progress?.Report(new SpriteImportProgress("Download Sprites", totalDownloads, totalDownloads, $"Downloaded {totalDownloads} sprite(s)."));
            if (!exceptions.IsEmpty)
            {
                throw new AggregateException(exceptions);
            }
        }

        private async Task DownloadSingleSpriteAsync(
            FigmaNode node,
            string url,
            SemaphoreSlim semaphore,
            ConcurrentBag<Exception> exceptions,
            CancellationToken cancellationToken,
            Action onCompleted)
        {
            try
            {
                byte[] bytes = await _apiClient.DownloadBytesAsync(url, cancellationToken);
                string fullPath = Path.GetFullPath(node.AssetPath);
                string? directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
            finally
            {
                semaphore.Release();
                onCompleted();
            }
        }

        private async Task ImportSpritesAsync(
            IReadOnlyList<FigmaNode> nodes,
            CancellationToken cancellationToken,
            IProgress<SpriteImportProgress>? progress)
        {
            int total = nodes.Count;
            int reportInterval = Math.Max(1, total / 50);
            progress?.Report(new SpriteImportProgress("Import Sprites", 0, total, $"Importing {total} sprite asset(s)."));

            for (int i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FigmaNode node = nodes[i];
                if (string.IsNullOrWhiteSpace(node.AssetPath))
                {
                    continue;
                }

                AssetDatabase.ImportAsset(node.AssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                TextureImporter? importer = AssetImporter.GetAtPath(node.AssetPath) as TextureImporter;
                if (importer != null)
                {
                    ApplyTextureImporterSettings(importer, node);
                }

                if ((i + 1) == total || (i + 1) % reportInterval == 0)
                {
                    progress?.Report(new SpriteImportProgress("Import Sprites", i + 1, total));
                    await Task.Yield();
                }
            }

            AssetDatabase.Refresh();
            for (int i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FigmaNode node = nodes[i];
                if (!string.IsNullOrWhiteSpace(node.AssetPath))
                {
                    node.Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(node.AssetPath);
                    node.SpriteBorder = node.Sprite != null ? node.Sprite.border : Vector4.zero;
                }
            }

            progress?.Report(new SpriteImportProgress("Import Sprites", total, total, $"Imported {total} sprite asset(s)."));
        }

        private List<FigmaNode> PrepareCachedSprites(
            IReadOnlyList<FigmaNode> nodes,
            string outputRoot,
            bool reuseExistingSprites,
            HashSet<string>? forceRefreshNodeIds)
        {
            List<FigmaNode> unresolvedNodes = new();
            // Per-frame registry so two sprites with the same name in different
            // frames don't fight over a -1/-2 suffix that's purely scoped to
            // their own frame folder.
            Dictionary<string, UniqueAssetNameRegistry> registriesByFolder = new();
            foreach (FigmaNode node in nodes)
            {
                string frameFolder = FigmaImporterUtils.ToAssetFolderPath(outputRoot, FigmaImporterUtils.GetRootFrame(node).Name);
                Directory.CreateDirectory(frameFolder);
                if (!registriesByFolder.TryGetValue(frameFolder, out UniqueAssetNameRegistry? nameRegistry))
                {
                    nameRegistry = new UniqueAssetNameRegistry();
                    registriesByFolder[frameFolder] = nameRegistry;
                }

                string assetPath = BuildAssetPath(frameFolder, node, nameRegistry);
                node.AssetPath = assetPath;
                bool forceRefresh = forceRefreshNodeIds != null && forceRefreshNodeIds.Contains(node.Id);
                node.Sprite = reuseExistingSprites && !forceRefresh ? AssetDatabase.LoadAssetAtPath<Sprite>(assetPath) : null;
                node.SpriteBorder = node.Sprite != null ? node.Sprite.border : Vector4.zero;
                if (node.Sprite == null || HasMismatchedNinePatchBorder(node))
                {
                    unresolvedNodes.Add(node);
                }
            }

            return unresolvedNodes;
        }

        private List<FigmaNode> CollectDownloadableNodes(IReadOnlyList<FigmaNode> rootNodes)
        {
            List<FigmaNode> result = new();
            foreach (FigmaNode rootNode in rootNodes)
            {
                CollectDownloadableNodesRecursive(rootNode, result, _rasterizeUnsupportedText, _omitMaskSprites);
            }

            return result;
        }

        public static void CollectDownloadableNodesRecursive(FigmaNode node, List<FigmaNode> result, bool rasterizeUnsupportedText = false, bool omitMaskSprites = false)
        {
            if (node.IgnoreNode)
            {
                return;
            }

            if (ShouldDownloadSprite(node, rasterizeUnsupportedText, omitMaskSprites))
            {
                result.Add(node);
                IgnoreDescendants(node);
                return;
            }

            if (node.Children == null)
            {
                return;
            }

            foreach (FigmaNode child in node.Children)
            {
                CollectDownloadableNodesRecursive(child, result, rasterizeUnsupportedText, omitMaskSprites);
            }
        }

        private static void IgnoreDescendants(FigmaNode node)
        {
            if (node.Children == null)
            {
                return;
            }

            foreach (FigmaNode child in node.Children)
            {
                child.IgnoreNode = true;
                IgnoreDescendants(child);
            }
        }

        public static bool ShouldDownloadSprite(FigmaNode node, bool rasterizeUnsupportedText = false, bool omitMaskSprites = false)
        {
            if (omitMaskSprites && node.Tags.Contains(NodeTag.Mask))
            {
                return false;
            }

            if (rasterizeUnsupportedText && UitkRenderPolicy.ShouldRenderTextAsRasterImage(node.Design))
            {
                return true;
            }

            if (!node.Tags.Contains(NodeTag.Image) || node.Tags.Contains(NodeTag.Text))
            {
                return false;
            }

            if (node.ForceImage)
            {
                return true;
            }

            if (node.Type is "VECTOR" or "BOOLEAN_OPERATION" or "LINE" or "ELLIPSE" or "STAR" or "REGULAR_POLYGON")
            {
                return true;
            }

            if (HasNonIgnoredTextDescendant(node))
            {
                return false;
            }

            if (node.Fills != null)
            {
                foreach (Paint fill in node.Fills)
                {
                    if (fill.Visible == false)
                    {
                        continue;
                    }

                    if (!string.Equals(fill.Type, "SOLID", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasNonIgnoredTextDescendant(FigmaNode node)
        {
            if (node.Children == null || node.Children.Count == 0)
            {
                return false;
            }

            foreach (FigmaNode child in node.Children)
            {
                if (child.IgnoreNode)
                {
                    continue;
                }

                if (child.Tags.Contains(NodeTag.Text) || HasNonIgnoredTextDescendant(child))
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyTextureImporterSettings(TextureImporter importer, FigmaNode node)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.spriteBorder = CalculateSpriteBorder(node);

            TextureImporterSettings settings = new();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private Vector4 CalculateSpriteBorder(FigmaNode node)
        {
            if (_omitSpriteBorders)
            {
                return Vector4.zero;
            }

            if (!node.Tags.Contains(NodeTag.NinePatch) || node.Children == null || node.Children.Count != 9 || node.AbsoluteBoundingBox == null)
            {
                return Vector4.zero;
            }

            BoundingBox? topLeft = node.Children[0].AbsoluteBoundingBox;
            BoundingBox? topRight = node.Children[2].AbsoluteBoundingBox;
            BoundingBox? bottomLeft = node.Children[6].AbsoluteBoundingBox;
            if (topLeft == null || topRight == null || bottomLeft == null)
            {
                return Vector4.zero;
            }

            float scale = Mathf.Max(0.01f, _spriteBorderScaleOverride ?? FigmaImportSettings.instance.SpriteScale);
            int textureWidth = Mathf.Max(1, Mathf.RoundToInt(node.AbsoluteBoundingBox.Width * scale));
            int textureHeight = Mathf.Max(1, Mathf.RoundToInt(node.AbsoluteBoundingBox.Height * scale));

            float left = Mathf.Round(topLeft.Width * scale);
            float right = Mathf.Round(topRight.Width * scale);
            float top = Mathf.Round(topLeft.Height * scale);
            float bottom = Mathf.Round(bottomLeft.Height * scale);

            left = Mathf.Clamp(left, 0f, textureWidth);
            right = Mathf.Clamp(right, 0f, textureWidth - left);
            bottom = Mathf.Clamp(bottom, 0f, textureHeight);
            top = Mathf.Clamp(top, 0f, textureHeight - bottom);

            return new Vector4(left, bottom, right, top);
        }

        private bool HasMismatchedNinePatchBorder(FigmaNode node)
        {
            if (node.Sprite == null || !node.Tags.Contains(NodeTag.NinePatch))
            {
                return false;
            }

            return node.SpriteBorder != CalculateSpriteBorder(node);
        }

        private static string BuildAssetPath(string outputFolder, FigmaNode node, UniqueAssetNameRegistry registry)
        {
            string baseName = ResolveSpriteBaseName(node);
            string uniqueName = registry.GetUnique(baseName);
            return FigmaImporterUtils.CombineAssetPath(outputFolder, $"{uniqueName}.png");
        }

        private static string ResolveSpriteBaseName(FigmaNode node)
        {
            if (!FigmaNameResolver.IsPlaceholderName(node.Name))
            {
                return FigmaNameSanitizer.Sanitize(node.Name);
            }

            return FigmaNameResolver.TypeLabel(node.Type);
        }
    }
}
