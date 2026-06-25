using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FigmaToUnity.Core;
using FigmaToUnity.Editor.SharedPipeline;
using FigmaToUnity.Editor.State;
using FigmaToUnity.Editor.UguiPipeline;
using FigmaToUnity.Editor.UitkPipeline;
using UnityEngine;

namespace FigmaToUnity.Editor
{
    internal sealed class FigmaImportController
    {
        private readonly FigmaApiClient _apiClient;
        private readonly FigmaFrameCacheStore _frameCacheStore = FigmaFrameCacheStore.instance;
        private readonly NodeTagger _nodeTagger = new();
        private readonly UguiImportBackend _uguiBackend;
        private readonly UitkImportBackend _uitkBackend;
        private CancellationScope? _scope;

        public FigmaImportController()
        {
            _apiClient = new FigmaApiClient(new FigmaApiClientOptions
            {
                MaxAttempts = FigmaImportSettings.instance.MaxDownloadAttempts,
                RequestTimeoutSeconds = FigmaImportSettings.instance.RequestTimeoutSeconds
            });
            _apiClient.DiagnosticLog = msg => Log(msg);
            _uguiBackend = new UguiImportBackend(_apiClient);
            _uitkBackend = new UitkImportBackend(_apiClient);
        }

        public event Action<ImportProgress>? ProgressChanged;
        public event Action<string>? LogAdded;
        public event Action<bool>? BusyChanged;

        public bool IsBusy => _scope != null && !_scope.IsCancellationRequested;

        public bool TryRestoreCachedFrames(FigmaImportSession session)
        {
            if (!FigmaUrlParser.TryParseFileKey(session.FigmaUrl, out string fileKey))
            {
                return false;
            }

            int depth = Mathf.Max(1, FigmaImportSettings.instance.FrameListDepth);
            if (!_frameCacheStore.TryGet(fileKey, depth, out FrameCacheEntry? entry) || entry == null || entry.Frames.Count == 0)
            {
                return false;
            }

            session.FileKey = fileKey;
            session.FigmaFileName = entry.FileName;
            session.FileResponse = new FigmaFileResponse
            {
                Name = entry.FileName,
                LastModified = string.IsNullOrWhiteSpace(entry.LastModified) ? null : entry.LastModified,
                Version = string.IsNullOrWhiteSpace(entry.Version) ? null : entry.Version
            };

            ApplyCachedFrames(session, entry);
            return true;
        }

        public async Task<string> VerifyTokenAsync(FigmaImportSession session)
        {
            return await RunExclusiveAsync(
                "Verify Token",
                async () =>
                {
                    Log("Verifying Figma token.");
                    FigmaUserResponse user = await _apiClient.VerifyTokenAsync(session.Token, _scope!.Token);
                    string label = !string.IsNullOrWhiteSpace(user.Handle) ? user.Handle! : user.Email ?? "Unknown user";
                    Log($"Connected as {label}.");
                    return label;
                });
        }

        public async Task<IReadOnlyList<FrameSummary>> LoadFramesAsync(FigmaImportSession session)
        {
            return await RunExclusiveAsync(
                "Fetch Frames",
                async () =>
                {
                    if (!FigmaUrlParser.TryParseFileKey(session.FigmaUrl, out string fileKey))
                    {
                        throw new ArgumentException("Invalid Figma file URL.");
                    }

                    session.FileKey = fileKey;
                    int depth = Mathf.Max(1, FigmaImportSettings.instance.FrameListDepth);
                    if (_frameCacheStore.TryGet(fileKey, depth, out FrameCacheEntry? cachedEntry) && cachedEntry != null && cachedEntry.Frames.Count > 0)
                    {
                        Report("Fetch Frames", "Checking whether the Figma file changed.", 0, 0, true, true);
                        FigmaFileResponse metadataResponse = await _apiClient.FetchFileMetadataAsync(session.Token, fileKey, _scope!.Token);
                        if (IsCacheCurrent(cachedEntry, metadataResponse))
                        {
                            session.FigmaFileName = string.IsNullOrWhiteSpace(metadataResponse.Name) ? cachedEntry.FileName : metadataResponse.Name;
                            session.FileResponse = metadataResponse;
                            ApplyCachedFrames(session, cachedEntry);
                            Log($"Loaded {session.Frames.Count} cached frame(s) from {session.FigmaFileName}. No Figma changes detected.");
                            Report("Fetch Frames", $"Loaded {session.Frames.Count} cached frame(s).", session.Frames.Count, session.Frames.Count, false, false);
                            return session.Frames;
                        }
                    }

                    Report("Fetch Frames", "Downloading Figma file tree.", 0, 0, true, true);
                    FigmaFileResponse response = await _apiClient.FetchFileAsync(session.Token, fileKey, depth, _scope!.Token);
                    session.FileResponse = response;
                    session.FigmaFileName = response.Name;

                    List<FrameSummary> frames = new();
                    CollectFrames(response.Document, frames);
                    ApplyFrames(session, frames);
                    _frameCacheStore.Upsert(fileKey, depth, response.Name, response.LastModified, response.Version, session.Frames, session.SelectedFrameIds);

                    Log($"Loaded {frames.Count} frame(s) from {response.Name}.");
                    Report("Fetch Frames", $"Loaded {frames.Count} frame(s).", frames.Count, frames.Count, false, false);
                    return session.Frames;
                });
        }

        public async Task StartImportAsync(FigmaImportSession session)
        {
            await RunExclusiveAsync(
                "Import",
                async () =>
                {
                    if (session.SelectedFrameIds.Count == 0)
                    {
                        throw new ArgumentException("Select at least one frame before importing.");
                    }

                    if (string.IsNullOrWhiteSpace(session.FileKey))
                    {
                        if (!FigmaUrlParser.TryParseFileKey(session.FigmaUrl, out string fileKey))
                        {
                            throw new ArgumentException("Invalid Figma file URL.");
                        }

                        session.FileKey = fileKey;
                    }

                    if (session.FileResponse == null || string.IsNullOrWhiteSpace(session.FileResponse.Name) || session.FileResponse.Components == null)
                    {
                        session.FileResponse = await _apiClient.FetchFileMetadataAsync(session.Token, session.FileKey, _scope!.Token);
                        session.FigmaFileName = session.FileResponse.Name;
                    }

                    Report("Fetch Selected Nodes", "Downloading selected frame trees.", 0, session.SelectedFrameIds.Count, false, true);
                    List<string> selectedIds = new(session.SelectedFrameIds);
                    FigmaFileResponse nodesResponse = await _apiClient.FetchNodesAsync(session.Token, session.FileKey, selectedIds, _scope!.Token);

                    session.DesignRootNodes.Clear();
                    if (nodesResponse.Nodes != null)
                    {
                        foreach ((string _, FigmaNodeContainer container) in nodesResponse.Nodes)
                        {
                            session.DesignRootNodes.Add(container.Document);
                        }
                    }

                    if (session.DesignRootNodes.Count == 0)
                    {
                        throw new InvalidOperationException("Figma returned no root nodes for the selected frames.");
                    }

                    Report("Tag Nodes", "Applying manual, figma, and smart tags.", 0, session.DesignRootNodes.Count, true, true);
                    HashOptions hashOptions = new() { ProceduralImageFalloff = FigmaImportSettings.instance.ProceduralImageFalloff };
                    bool rootAsPrefab = session.RootAsPrefab;
                    NodeTagger nodeTagger = _nodeTagger;
                    List<DesignNode> designRoots = session.DesignRootNodes;
                    CancellationToken token = _scope!.Token;

                    // Pure-CPU stages run on a background thread. They never touch
                    // Unity APIs (only DesignNode POCOs), so this is safe and frees
                    // the main thread to repaint the editor.
                    List<FigmaNode> mappedRoots = await Task.Run(() =>
                    {
                        foreach (DesignNode rootNode in designRoots)
                        {
                            token.ThrowIfCancellationRequested();
                            nodeTagger.TagTree(rootNode);
                        }

                        // --root-as-prefab is the CLI equivalent of adding a #prefab
                        // suffix to every root frame name. Applied here so the hasher
                        // sees the Prefab tag and the prefab pipeline emits an asset.
                        if (rootAsPrefab)
                        {
                            foreach (DesignNode rootNode in designRoots)
                            {
                                rootNode.Tags.Add(NodeTag.Prefab);
                                rootNode.ExplicitPrefab = true;
                            }
                        }

                        token.ThrowIfCancellationRequested();
                        FigmaNodeHasher.ComputeHashes(designRoots, hashOptions);

                        token.ThrowIfCancellationRequested();
                        return FigmaNodeMapper.MapAll(designRoots);
                    }, token);

                    Report("Tag Nodes", $"Tagged & hashed {designRoots.Count} root frame(s).", designRoots.Count, designRoots.Count, false, true);
                    session.ImportedRootNodes.Clear();
                    session.ImportedRootNodes.AddRange(mappedRoots);

                    ImportModeKind importMode = FigmaImportSettings.instance.ImportMode;
                    OutputBackend backend = FigmaImportSettings.instance.Backend;

                    ImportContext context = new(
                        session,
                        session.ImportedRootNodes,
                        importMode,
                        progress => ProgressChanged?.Invoke(progress),
                        Log,
                        _scope!.Token);

                    if (backend == OutputBackend.UGUI)
                    {
                        await _uguiBackend.ImportAsync(context);
                    }

                    if (backend == OutputBackend.UIToolkit)
                    {
                        await _uitkBackend.ImportAsync(context);
                    }

                    string finalStatus = backend switch
                    {
                        OutputBackend.UGUI => "UGUI import finished.",
                        OutputBackend.UIToolkit => "UIToolkit import finished.",
                        _ => "Import finished.",
                    };
                    Report("Import Complete", finalStatus, 1, 1, false, false);
                });
        }

        public void Cancel()
        {
            _scope?.Cancel();
            Log("Cancellation requested.");
        }

        private async Task RunExclusiveAsync(string stageName, Func<Task> action)
        {
            await RunExclusiveAsync<object?>(
                stageName,
                async () =>
                {
                    await action();
                    return null;
                });
        }

        private async Task<T> RunExclusiveAsync<T>(string stageName, Func<Task<T>> action)
        {
            if (_scope != null)
            {
                throw new InvalidOperationException("Another Figma import task is already running.");
            }

            _scope = new CancellationScope();
            BusyChanged?.Invoke(true);

            try
            {
                Report(stageName, "Running.", 0, 0, true, true);
                return await action();
            }
            finally
            {
                _scope.Dispose();
                _scope = null;
                BusyChanged?.Invoke(false);
            }
        }

        private void CollectFrames(DesignNode node, List<FrameSummary> frames)
        {
            if (node.Type == "FRAME" && node.AbsoluteBoundingBox != null)
            {
                BoundingBox box = node.AbsoluteBoundingBox;
                frames.Add(new FrameSummary(node.Id, node.Name, box.Width, box.Height));
            }

            if (node.Children == null)
            {
                return;
            }

            foreach (DesignNode child in node.Children)
            {
                CollectFrames(child, frames);
            }
        }

        private void Report(string stage, string status, int current, int total, bool indeterminate, bool working)
        {
            ProgressChanged?.Invoke(new ImportProgress(stage, status, current, total, indeterminate, working));
        }

        private void Log(string message)
        {
            LogAdded?.Invoke(message);
        }

        private void ApplyCachedFrames(FigmaImportSession session, FrameCacheEntry entry)
        {
            List<FrameSummary> frames = new(entry.Frames.Count);
            foreach (CachedFrameSummary cachedFrame in entry.Frames)
            {
                frames.Add(cachedFrame.ToFrameSummary());
            }

            ApplyFrames(session, frames);
            RestoreSelectedFrames(session, entry.SelectedFrameIds);
        }

        private static void ApplyFrames(FigmaImportSession session, IReadOnlyList<FrameSummary> frames)
        {
            session.Frames.Clear();
            session.Frames.AddRange(frames);

            HashSet<string> validFrameIds = new(StringComparer.Ordinal);
            foreach (FrameSummary frame in frames)
            {
                validFrameIds.Add(frame.Id);
            }

            session.SelectedFrameIds.RemoveWhere(selectedId => !validFrameIds.Contains(selectedId));
        }

        private static void RestoreSelectedFrames(FigmaImportSession session, IReadOnlyCollection<string> selectedFrameIds)
        {
            session.SelectedFrameIds.Clear();
            HashSet<string> validFrameIds = new(StringComparer.Ordinal);
            foreach (FrameSummary frame in session.Frames)
            {
                validFrameIds.Add(frame.Id);
            }

            foreach (string selectedFrameId in selectedFrameIds)
            {
                if (validFrameIds.Contains(selectedFrameId))
                {
                    session.SelectedFrameIds.Add(selectedFrameId);
                }
            }
        }

        private static bool IsCacheCurrent(FrameCacheEntry cachedEntry, FigmaFileResponse metadataResponse)
        {
            if (!string.IsNullOrWhiteSpace(cachedEntry.Version) &&
                !string.IsNullOrWhiteSpace(metadataResponse.Version))
            {
                return string.Equals(cachedEntry.Version, metadataResponse.Version, StringComparison.Ordinal);
            }

            if (!string.IsNullOrWhiteSpace(cachedEntry.LastModified) &&
                !string.IsNullOrWhiteSpace(metadataResponse.LastModified))
            {
                return string.Equals(cachedEntry.LastModified, metadataResponse.LastModified, StringComparison.Ordinal);
            }

            return false;
        }
    }
}
