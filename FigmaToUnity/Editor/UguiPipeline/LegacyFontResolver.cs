using System;
using System.Collections.Generic;
using System.IO;
using FigmaToUnity.Core;
using FigmaToUnity.Editor.ImportPipeline;
using UnityEditor;
using UnityEngine;

namespace FigmaToUnity.Editor.UguiPipeline
{
    // Legacy UnityEngine.UI.Text font lookup (UGUI's pre-TMP component).
    // Lives in UguiPipeline because it returns UnityEngine.Font; UIToolkit's
    // font resolver operates on different types.
    internal static class LegacyFontResolver
    {
        private static readonly Dictionary<string, Font?> FontCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> ReportedFontSubstitutions = new(StringComparer.Ordinal);
        private static List<Font>? ProjectFontsCache;

        public static void ClearCaches()
        {
            FontCache.Clear();
            ReportedFontSubstitutions.Clear();
            ProjectFontsCache = null;
        }

        public static Font? ResolveFont(TextStyle? style)
        {
            FigmaImportSettings settings = FigmaImportSettings.instance;
            Font? resolved = ResolvePreferredFont(style, settings);
            return resolved ?? settings.DefaultLegacyFont ?? GetBuiltInFallbackFont();
        }

        public static void WarnIfFontSubstituted(FigmaNode node, TextStyle? style, Font? resolvedFont)
        {
            if (style == null || string.IsNullOrWhiteSpace(style.FontFamily) || resolvedFont == null || MatchesRequestedFont(resolvedFont, style))
            {
                return;
            }

            string requested = DescribeRequestedFont(style);
            string resolved = DescribeLegacyFont(resolvedFont);
            string warningKey = $"{requested}|{resolved}";
            if (!ReportedFontSubstitutions.Add(warningKey))
            {
                return;
            }

            Debug.LogWarning($"[FigmaImporter] Legacy font substitution: requested '{requested}', using '{resolved}'. Example node: '{node.Name}' ({node.Id}).");
        }

        private static Font? ResolvePreferredFont(TextStyle? style, FigmaImportSettings settings)
        {
            if (style == null || string.IsNullOrWhiteSpace(style.FontFamily))
            {
                return settings.DefaultLegacyFont;
            }

            string cacheKey = BuildCacheKey(style);
            if (FontCache.TryGetValue(cacheKey, out Font? cachedFont))
            {
                return cachedFont ?? settings.DefaultLegacyFont;
            }

            Font? resolved = FindProjectFont(style);
            FontCache[cacheKey] = resolved;
            return resolved ?? settings.DefaultLegacyFont;
        }

        private static Font? FindProjectFont(TextStyle style)
        {
            List<Font> fonts = GetProjectFonts();
            if (fonts.Count == 0)
            {
                return null;
            }

            string familyKey = NormalizeLegacyFontName(style.FontFamily);
            string familyWeightItalicKey = NormalizeLegacyFontName(TextStyleUtility.BuildRawFontKey(style.FontFamily, style.FontWeight, style.Italic == true, includeWeight: true, includeItalic: true));
            string familyWeightKey = NormalizeLegacyFontName(TextStyleUtility.BuildRawFontKey(style.FontFamily, style.FontWeight, italic: false, includeWeight: true, includeItalic: false));

            Font? familyOnlyMatch = null;
            foreach (Font font in fonts)
            {
                int score = ScoreFont(font, familyWeightItalicKey, familyWeightKey, familyKey);
                if (score >= 100)
                {
                    return font;
                }

                if (score >= 80 && familyOnlyMatch == null)
                {
                    familyOnlyMatch = font;
                }
            }

            return familyOnlyMatch;
        }

        private static int ScoreFont(Font font, string familyWeightItalicKey, string familyWeightKey, string familyKey)
        {
            foreach (string candidate in GetFontCandidates(font))
            {
                if (candidate == familyWeightItalicKey)
                {
                    return 100;
                }

                if (candidate == familyWeightKey)
                {
                    return 90;
                }

                if (candidate == familyKey || candidate.IndexOf(familyKey, StringComparison.Ordinal) >= 0)
                {
                    return 80;
                }
            }

            return 0;
        }

        private static IEnumerable<string> GetFontCandidates(Font font)
        {
            yield return NormalizeLegacyFontName(font.name);

            string assetPath = AssetDatabase.GetAssetPath(font);
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                string fileName = Path.GetFileNameWithoutExtension(assetPath);
                if (!string.Equals(fileName, font.name, StringComparison.OrdinalIgnoreCase))
                {
                    yield return NormalizeLegacyFontName(fileName);
                }
            }
        }

        private static List<Font> GetProjectFonts()
        {
            if (ProjectFontsCache != null)
            {
                return ProjectFontsCache;
            }

            ProjectFontsCache = new List<Font>();
            string[] guids = AssetDatabase.FindAssets("t:Font");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                Font font = AssetDatabase.LoadAssetAtPath<Font>(assetPath);
                if (font != null && !ProjectFontsCache.Contains(font))
                {
                    ProjectFontsCache.Add(font);
                }
            }

            return ProjectFontsCache;
        }

        private static string BuildCacheKey(TextStyle style)
        {
            return NormalizeLegacyFontName(TextStyleUtility.BuildRawFontKey(style.FontFamily, style.FontWeight, style.Italic == true, includeWeight: true, includeItalic: true));
        }

        private static string NormalizeLegacyFontName(string? value)
        {
            return TextStyleUtility.NormalizeFontName(value);
        }

        private static bool MatchesRequestedFont(Font font, TextStyle style)
        {
            string requestedKey = NormalizeLegacyFontName(TextStyleUtility.BuildRawFontKey(style.FontFamily, style.FontWeight, style.Italic == true, includeWeight: true, includeItalic: true));
            string familyKey = NormalizeLegacyFontName(style.FontFamily);
            bool allowFamilyOnlyMatch = (style.FontWeight ?? 400f) <= 499f && style.Italic != true;

            foreach (string candidate in GetFontCandidates(font))
            {
                if (candidate == requestedKey)
                {
                    return true;
                }

                if (allowFamilyOnlyMatch && candidate == familyKey)
                {
                    return true;
                }
            }

            return false;
        }

        private static string DescribeRequestedFont(TextStyle style)
        {
            string family = string.IsNullOrWhiteSpace(style.FontFamily) ? "Unknown" : style.FontFamily!.Trim();
            string weight = TextStyleUtility.FontWeightToString(style.FontWeight);
            return style.Italic == true ? $"{family} {weight} Italic" : $"{family} {weight}";
        }

        private static string DescribeLegacyFont(Font font)
        {
            string assetPath = AssetDatabase.GetAssetPath(font);
            string fileName = string.IsNullOrWhiteSpace(assetPath) ? string.Empty : Path.GetFileNameWithoutExtension(assetPath);
            return string.IsNullOrWhiteSpace(fileName) ? font.name : fileName;
        }

        private static Font GetBuiltInFallbackFont()
        {
#if UNITY_2022_1_OR_NEWER
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }
    }
}
