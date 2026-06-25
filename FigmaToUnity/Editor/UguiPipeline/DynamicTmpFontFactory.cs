using System.IO;
using FigmaToUnity.Editor.SharedPipeline;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace FigmaToUnity.Editor.UguiPipeline
{
    // TMP_FontAsset construction from system fonts. UGUI-only: TMP coverage,
    // SDF atlas creation, FallbackKind enum. UIToolkit's path imports raw Font
    // assets and references them from USS instead.
    internal static class DynamicTmpFontFactory
    {
        internal enum AutoFallbackKind
        {
            Cjk,
            Symbol,
            Emoji
        }

        private const int SamplingPointSize = 48;
        private const int AtlasPadding = 5;
        private const int AtlasWidth = 1024;
        private const int AtlasHeight = 1024;

        private static readonly System.Collections.Generic.Dictionary<AutoFallbackKind, TMP_FontAsset?> CachedFallbacks = new();

        /// <summary>
        /// Returns a previously created CJK fallback font asset (from cache or disk), or null.
        /// </summary>
        public static TMP_FontAsset? GetOrCreateFallback(string outputFolder, AutoFallbackKind kind)
        {
            if (CachedFallbacks.TryGetValue(kind, out TMP_FontAsset? cached) && IsUsableFontAsset(cached))
            {
                return cached;
            }

            string assetPath = GetFallbackAssetPath(outputFolder, kind);
            TMP_FontAsset? existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (IsUsableFontAsset(existing))
            {
                CachedFallbacks[kind] = existing;
                return existing;
            }

            return null;
        }

        /// <summary>
        /// Copies a system CJK font into the project, imports it, and creates a Dynamic-mode
        /// TMP_FontAsset from the imported Font asset. The font file stays in the project
        /// because Dynamic TMP assets need the source font for on-demand glyph rasterization.
        /// </summary>
        public static TMP_FontAsset? CreateFallbackFromSystemFont(string outputFolder, AutoFallbackKind kind)
        {
            string? systemFontPath = kind == AutoFallbackKind.Cjk
                ? SystemFontResolver.FindSystemCjkFontPath()
                : kind == AutoFallbackKind.Symbol
                    ? SystemFontResolver.FindSystemSymbolFontPath()
                    : SystemFontResolver.FindSystemEmojiFontPath();
            if (systemFontPath == null)
            {
                string label = kind == AutoFallbackKind.Cjk
                    ? "CJK"
                    : kind == AutoFallbackKind.Symbol
                        ? "symbol"
                        : "emoji";
                Debug.LogWarning($"[FigmaImporter] No system {label} font found. Some characters may not render correctly.");
                return null;
            }

            string fontsFolder = GetFontsFolder(outputFolder);
            EnsureFolderExists(fontsFolder);

            string extension = Path.GetExtension(systemFontPath);
            string importedFontPath = $"{fontsFolder}/{GetImportedFontFileName(kind)}{extension}";
            try
            {
                string absoluteImportPath = Path.GetFullPath(importedFontPath);
                File.Copy(systemFontPath, absoluteImportPath, overwrite: true);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FigmaImporter] Failed to copy system font: {ex.Message}");
                return null;
            }

            AssetDatabase.ImportAsset(importedFontPath, ImportAssetOptions.ForceUpdate);
            Font? importedFont = AssetDatabase.LoadAssetAtPath<Font>(importedFontPath);
            if (importedFont == null)
            {
                Debug.LogWarning($"[FigmaImporter] Failed to import font asset from: {importedFontPath}");
                return null;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                importedFont,
                SamplingPointSize,
                AtlasPadding,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                AtlasWidth,
                AtlasHeight,
                AtlasPopulationMode.Dynamic);

            if (fontAsset == null)
            {
                Debug.LogWarning($"[FigmaImporter] Failed to create TMP_FontAsset from imported font: {importedFontPath}");
                return null;
            }

            fontAsset.name = GetFallbackAssetName(kind);

            string tmpAssetPath = GetFallbackAssetPath(outputFolder, kind);
            TMP_FontAsset? existingAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(tmpAssetPath);
            if (existingAsset != null)
            {
                AssetDatabase.DeleteAsset(tmpAssetPath);
            }

            AssetDatabase.CreateAsset(fontAsset, tmpAssetPath);
            EmbedFontAssetSubObjects(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(tmpAssetPath, ImportAssetOptions.ForceUpdate);

            TMP_FontAsset? created = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(tmpAssetPath);
            if (!IsUsableFontAsset(created))
            {
                CachedFallbacks.Remove(kind);
                AssetDatabase.DeleteAsset(tmpAssetPath);
                Debug.LogWarning($"[FigmaImporter] Created TMP fallback font was unusable and has been removed: {tmpAssetPath}");
                return null;
            }

            CachedFallbacks[kind] = created;
            Debug.Log($"[FigmaImporter] Created dynamic {kind} fallback font: {tmpAssetPath} (source: {systemFontPath})");
            return created;
        }

        public static void ClearCache()
        {
            CachedFallbacks.Clear();
        }

        private static string GetFontsFolder(string outputFolder)
        {
            string folder = string.IsNullOrWhiteSpace(outputFolder)
                ? "Assets/FigmaImporterOutput"
                : outputFolder.TrimEnd('/');
            return $"{folder}/Fonts";
        }

        private static string GetFallbackAssetPath(string outputFolder, AutoFallbackKind kind)
        {
            return $"{GetFontsFolder(outputFolder)}/{GetFallbackAssetName(kind)}.asset";
        }

        private static string GetImportedFontFileName(AutoFallbackKind kind)
        {
            return kind switch
            {
                AutoFallbackKind.Cjk => "CJK-System-Font",
                AutoFallbackKind.Symbol => "Symbol-System-Font",
                _ => "Emoji-System-Font",
            };
        }

        private static string GetFallbackAssetName(AutoFallbackKind kind)
        {
            return kind switch
            {
                AutoFallbackKind.Cjk => "CJK-Dynamic-Fallback",
                AutoFallbackKind.Symbol => "Symbol-Dynamic-Fallback",
                _ => "Emoji-Dynamic-Fallback",
            };
        }

        // A TMP_FontAsset created via CreateFontAsset carries its atlas texture
        // and material as live in-memory references. AssetDatabase.CreateAsset
        // only persists the font asset itself — unless we explicitly add the
        // atlas texture and material as sub-assets, they are dropped on the
        // next domain reload / asset load, and anything that touches the font
        // (prefab isolation, runtime rendering) throws UnassignedReferenceException
        // on m_AtlasTextures.
        private static void EmbedFontAssetSubObjects(TMP_FontAsset fontAsset)
        {
            Texture[]? atlasTextures = fontAsset.atlasTextures;
            if (atlasTextures != null)
            {
                foreach (Texture atlas in atlasTextures)
                {
                    if (atlas != null && !AssetDatabase.IsSubAsset(atlas))
                    {
                        atlas.name = fontAsset.name + " Atlas";
                        AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                    }
                }
            }

            Material? material = fontAsset.material;
            if (material != null && !AssetDatabase.IsSubAsset(material))
            {
                material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(material, fontAsset);
            }
        }

        private static bool IsUsableFontAsset(TMP_FontAsset? fontAsset)
        {
            if (fontAsset == null)
            {
                return false;
            }

            try
            {
                Texture[] atlasTextures = fontAsset.atlasTextures;
                return atlasTextures != null && fontAsset.material != null;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath)!.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolderExists(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
        }
    }
}
