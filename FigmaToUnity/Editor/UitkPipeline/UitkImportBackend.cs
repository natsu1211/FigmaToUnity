using System;
using System.IO;
using System.Threading.Tasks;
using FigmaToUnity.Core;
using FigmaToUnity.Editor.SharedPipeline;
using UnityEditor;

namespace FigmaToUnity.Editor.UitkPipeline
{
    // UI Toolkit backend: emits one UXML + one USS per imported root frame plus
    // the sprite assets the document references. Output layout follows plan §8:
    //
    //   Assets/UI/<sanitized-file>/
    //     Uxml/<sanitized-frame>.uxml
    //     Uss/<sanitized-frame>.uss
    //
    // Sprite assets land in the user-configured OutputFolder (same place UGUI
    // writes them); the UXML/USS only needs the asset path the SpriteImporter
    // already chose. For v1 the USS does not yet reference background-images
    // (#9), only NinePatch border widths.
    internal sealed class UitkImportBackend
    {
        private const string DefaultOutputRoot = "Assets/UI";

        private readonly SpriteImporter _spriteImporter;

        public UitkImportBackend(FigmaApiClient apiClient)
        {
            _spriteImporter = new SpriteImporter(
                apiClient,
                rasterizeUnsupportedText: true,
                omitMaskSprites: true,
                omitSpriteBorders: true);
        }

        public async Task ImportAsync(ImportContext context)
        {
            bool diffMode = context.ImportMode == ImportModeKind.DiffUpdate;
            FigmaImportSession session = context.Session;

            context.Report("Download Sprites", "Fetching image URLs and resolving sprite assets.", 0, 0, true, true);
            IProgress<SpriteImportProgress> spriteProgress = new Progress<SpriteImportProgress>(p =>
            {
                context.Report(p.Stage, $"{p.Stage} ({p.Current}/{p.Total}).", p.Current, p.Total, p.Total <= 0, true);
                if (!string.IsNullOrEmpty(p.Log))
                {
                    context.Log(p.Log!);
                }
            });

            int resolvedSprites = await _spriteImporter.ImportAsync(
                context.ImportedRootNodes,
                session.Token,
                session.FileKey,
                session.OutputFolder,
                context.CancellationToken,
                spriteProgress);
            context.Log($"Resolved {resolvedSprites} sprite asset(s).");

            string fileFolder = FigmaImporterUtils.ToAssetFolderPath(DefaultOutputRoot, session.FigmaFileName);
            string uxmlFolder = $"{fileFolder}/Uxml";
            string ussFolder = $"{fileFolder}/Uss";
            Directory.CreateDirectory(Path.GetFullPath(uxmlFolder));
            Directory.CreateDirectory(Path.GetFullPath(ussFolder));

            int total = context.ImportedRootNodes.Count;
            int written = 0;
            int diffUpdated = 0;
            int fullRewrites = 0;
            foreach (FigmaNode rootNode in context.ImportedRootNodes)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                // Filename = sanitized-frame-name + id slug. Frame names alone are not
                // unique across a Figma file, and a UniqueAssetNameRegistry suffix
                // would shift paths on every input-order change. The id slug is
                // globally unique within a file and stable across reimports.
                string sanitized = string.IsNullOrWhiteSpace(rootNode.Name)
                    ? UitkStyleMapper.ToUssId(rootNode.Id)
                    : FigmaNameSanitizer.Sanitize(rootNode.Name);
                string idSlug = UitkStyleMapper.ToUssId(rootNode.Id);
                string frameStem = sanitized == idSlug ? idSlug : $"{sanitized}-{idSlug}";

                string uxmlPath = FigmaImporterUtils.CombineAssetPath(uxmlFolder, $"{frameStem}.uxml");
                string wrapperUssPath = FigmaImporterUtils.CombineAssetPath(ussFolder, $"{frameStem}.uss");
                string generatedUssPath = FigmaImporterUtils.CombineAssetPath(ussFolder, $"{frameStem}.generated.uss");
                // UI Builder 2022.3 can mis-resolve relative Style src values when
                // the UXML/USS path contains non-ASCII characters, treating the
                // resolved Assets path as relative to the .uxml asset. Use an
                // absolute project path for the stylesheet include instead.
                string wrapperUssSrc = ToAbsoluteAssetReference(wrapperUssPath);

                string uxml;
                bool diffApplied = false;
                string absoluteUxmlPath = Path.GetFullPath(uxmlPath);
                if (diffMode && File.Exists(absoluteUxmlPath))
                {
                    string existingUxml = File.ReadAllText(absoluteUxmlPath);
                    string? updated = UxmlDiffUpdater.TryUpdate(existingUxml, rootNode, wrapperUssSrc);
                    if (updated != null)
                    {
                        uxml = updated;
                        diffApplied = true;
                    }
                    else
                    {
                        uxml = UxmlEmitter.Emit(rootNode, wrapperUssSrc);
                        context.Log($"Frame '{rootNode.Name}' existing UXML was incompatible with diff (root mismatch or malformed); rewrote from scratch.");
                    }
                }
                else
                {
                    uxml = UxmlEmitter.Emit(rootNode, wrapperUssSrc);
                    if (diffMode)
                    {
                        context.Log($"Frame '{rootNode.Name}' has no prior UXML; writing for the first time.");
                    }
                }

                string uss = UssEmitter.Emit(rootNode);

                // .generated.uss is overwritten on every import; .uss is the user
                // override wrapper, created once and never touched again so manual
                // tweaks survive reimports.
                File.WriteAllText(absoluteUxmlPath, uxml);
                File.WriteAllText(Path.GetFullPath(generatedUssPath), uss);
                EnsureWrapperUss(wrapperUssPath, frameStem, generatedUssPath);

                if (diffApplied)
                {
                    diffUpdated++;
                    context.Log($"Diff-updated UXML for frame '{rootNode.Name}' → {uxmlPath}");
                }
                else
                {
                    fullRewrites++;
                    context.Log($"Wrote UXML/USS for frame '{rootNode.Name}' → {uxmlPath}");
                }

                written++;
                context.Report("Emit UXML/USS", $"Emitting frame {written}/{total}.", written, total, false, true);

                await Task.Yield();
            }

            AssetDatabase.Refresh();
            context.Log(diffMode
                ? $"Wrote {written} UXML + USS pair(s); diff-updated {diffUpdated}, full rewrites {fullRewrites}."
                : $"Wrote {written} UXML + USS pair(s) under {fileFolder}/.");
            // Backend-local stage; controller emits the global 'Import Complete'.
            context.Report("UIToolkit Backend Complete", "UXML/USS generated.", 1, 1, false, true);
        }

        private static void EnsureWrapperUss(string wrapperUssPath, string frameStem, string generatedUssPath)
        {
            string absolutePath = Path.GetFullPath(wrapperUssPath);
            string generatedUssSrc = ToAbsoluteAssetReference(generatedUssPath);
            string absoluteImport = $"@import url(\"{generatedUssSrc}\");";
            string legacyRelativeImport = $"@import url(\"{frameStem}.generated.uss\");";

            if (File.Exists(absolutePath))
            {
                string existing = File.ReadAllText(absolutePath);
                if (existing.Contains(legacyRelativeImport))
                {
                    File.WriteAllText(absolutePath, existing.Replace(legacyRelativeImport, absoluteImport));
                }

                return;
            }

            string content =
                $"/* Wrapper for {frameStem}. Generated by FigmaToUnity on first import.\n" +
                "   This file is never overwritten — add USS overrides below the @import. */\n" +
                absoluteImport + "\n";
            File.WriteAllText(absolutePath, content);
        }

        private static string ToAbsoluteAssetReference(string assetPath)
        {
            return assetPath.StartsWith("/", StringComparison.Ordinal)
                ? assetPath
                : "/" + assetPath;
        }
    }
}
