using System;
using System.Collections.Generic;
using FigmaToUnity.Core;
using FigmaToUnity.Editor.SharedPipeline;
using FigmaToUnity.Editor.UguiPipeline;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace FigmaToUnity.Editor.ImportPipeline
{
    internal sealed class TmpTextApplier
    {
        private static readonly Dictionary<string, TMP_FontAsset?> FontCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> ReportedFontSubstitutions = new(StringComparer.Ordinal);
        private static List<TMP_FontAsset>? ProjectFontsCache;

        public static void ClearCaches()
        {
            FontCache.Clear();
            ReportedFontSubstitutions.Clear();
            ProjectFontsCache = null;
            DynamicTmpFontFactory.ClearCache();
        }

        public void Apply(IReadOnlyList<FigmaNode> nodes)
        {
            foreach (FigmaNode node in nodes)
            {
                if (!node.Tags.Contains(NodeTag.Text) || node.GameObject == null)
                {
                    continue;
                }

                RemoveLegacyTextComponent(node.GameObject);

                TextMeshProUGUI text = node.GameObject.GetComponent<TextMeshProUGUI>();
                if (text == null)
                {
                    text = node.GameObject.AddComponent<TextMeshProUGUI>();
                }

                TextStyle? effectiveStyle = TextStyleUtility.GetEffectiveTextStyle(node);
                text.text = node.Characters ?? string.Empty;
                text.fontSize = effectiveStyle?.FontSize > 0f ? effectiveStyle.FontSize : 14f;
                text.color = TextStyleUtility.GetTextColor(node);
                text.raycastTarget = false;
#if UNITY_2023_2_OR_NEWER
                text.textWrappingMode = TextWrappingModes.Normal;
#else
                text.enableWordWrapping = true;
#endif
                text.alignment = GetAlignment(effectiveStyle);
                text.fontStyle = GetFontStyle(effectiveStyle);

                TMP_FontAsset? resolvedFont = ResolveFont(effectiveStyle, text.text);
                if (resolvedFont != null)
                {
                    text.font = resolvedFont;
                }

                WarnIfFontSubstituted(node, effectiveStyle, resolvedFont);

                if (effectiveStyle?.LetterSpacing is float letterSpacing && text.fontSize > 0f)
                {
                    text.characterSpacing = letterSpacing / text.fontSize * 100f;
                }
                else
                {
                    text.characterSpacing = 0f;
                }

                if (effectiveStyle?.LineHeightPx is float lineHeightPx && lineHeightPx > 0f)
                {
                    text.lineSpacing = lineHeightPx - text.fontSize;
                }
                else
                {
                    text.lineSpacing = 0f;
                }

                TextLayoutUtility.ApplyContentSizeFitter(node, effectiveStyle);
            }
        }

        private static TMP_FontAsset? ResolveFont(TextStyle? style, string? textContent)
        {
            FigmaImportSettings settings = FigmaImportSettings.instance;
            TMP_FontAsset? preferredFont = PrepareFontForUse(ResolvePreferredFont(style, settings));
            List<TMP_FontAsset> configuredFallbacks = GetConfiguredFallbackFonts(settings, preferredFont);

            if (string.IsNullOrEmpty(textContent))
            {
                return preferredFont ?? PrepareFontForUse(settings.DefaultTmpFont);
            }

            string resolvedText = textContent ?? string.Empty;

            if (preferredFont != null)
            {
                if (HasCharacterCoverage(preferredFont, resolvedText) || CanResolveTextWithFallbacks(preferredFont, resolvedText, configuredFallbacks))
                {
                    EnsureFallbackFontsAttached(preferredFont, configuredFallbacks);
                    return preferredFont;
                }
            }

            TMP_FontAsset? defaultFont = ReferenceEquals(settings.DefaultTmpFont, preferredFont)
                ? preferredFont
                : PrepareFontForUse(settings.DefaultTmpFont);
            if (defaultFont != null)
            {
                List<TMP_FontAsset> defaultFallbacks = GetConfiguredFallbackFonts(settings, defaultFont);
                if (HasCharacterCoverage(defaultFont, resolvedText) || CanResolveTextWithFallbacks(defaultFont, resolvedText, defaultFallbacks))
                {
                    EnsureFallbackFontsAttached(defaultFont, defaultFallbacks);
                    return defaultFont;
                }
            }

            TMP_FontAsset? coverageFallback = FindCoverageFallbackFont(style, resolvedText, configuredFallbacks);
            if (coverageFallback != null)
            {
                EnsureFallbackFontsAttached(coverageFallback, GetConfiguredFallbackFonts(settings, coverageFallback));
                return coverageFallback;
            }

            if (settings.EnableAutoSystemFontFallback)
            {
                List<TMP_FontAsset> autoFallbacks = GetAutomaticFallbackFonts(settings, resolvedText);
                if (autoFallbacks.Count > 0)
                {
                    TMP_FontAsset? primary = preferredFont ?? defaultFont;
                    if (primary != null)
                    {
                        EnsureFallbackFontsAttached(primary, autoFallbacks);
                        return primary;
                    }

                    return PrepareFontForUse(autoFallbacks[0]);
                }
            }

            return preferredFont ?? defaultFont;
        }

        private static TMP_FontAsset? ResolvePreferredFont(TextStyle? style, FigmaImportSettings settings)
        {
            if (style == null || string.IsNullOrWhiteSpace(style.FontFamily))
            {
                return settings.DefaultTmpFont;
            }

            string cacheKey = BuildCacheKey(style);
            if (FontCache.TryGetValue(cacheKey, out TMP_FontAsset? cachedFont))
            {
                return cachedFont ?? settings.DefaultTmpFont;
            }

            TMP_FontAsset? resolved = FindProjectFont(style);
            FontCache[cacheKey] = resolved;
            return resolved ?? settings.DefaultTmpFont;
        }

        private static bool HasCharacterCoverage(TMP_FontAsset? font, string textContent)
        {
            if (!IsUsableFontAsset(font))
            {
                return false;
            }

            try
            {
                return font!.HasCharacters(textContent);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static List<TMP_FontAsset> GetConfiguredFallbackFonts(FigmaImportSettings settings, TMP_FontAsset? primaryFont)
        {
            List<TMP_FontAsset> fonts = new();
            foreach (TMP_FontAsset font in settings.FallbackTmpFonts)
            {
                if (!IsUsableFontAsset(font) || ReferenceEquals(font, primaryFont) || fonts.Contains(font))
                {
                    continue;
                }

                fonts.Add(font);
            }

            return fonts;
        }

        private static bool CanResolveTextWithFallbacks(TMP_FontAsset? primaryFont, string textContent, IReadOnlyList<TMP_FontAsset> fallbackFonts)
        {
            if (primaryFont == null)
            {
                return false;
            }

            foreach (char character in textContent)
            {
                if (char.IsWhiteSpace(character) || CanResolveCharacter(primaryFont, character, fallbackFonts))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static bool CanResolveCharacter(TMP_FontAsset primaryFont, char character, IReadOnlyList<TMP_FontAsset> fallbackFonts)
        {
            string value = character.ToString();
            if (primaryFont.HasCharacters(value))
            {
                return true;
            }

            foreach (TMP_FontAsset fallbackFont in fallbackFonts)
            {
                if (fallbackFont != null && fallbackFont.HasCharacters(value))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureFallbackFontsAttached(TMP_FontAsset? primaryFont, IReadOnlyList<TMP_FontAsset> fallbackFonts)
        {
            if (!IsUsableFontAsset(primaryFont))
            {
                return;
            }

            primaryFont!.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            bool changed = false;
            if (SanitizeFallbackTable(primaryFont))
            {
                changed = true;
            }

            foreach (TMP_FontAsset fallbackFont in fallbackFonts)
            {
                if (!IsUsableFontAsset(fallbackFont) || ReferenceEquals(fallbackFont, primaryFont) || primaryFont.fallbackFontAssetTable.Contains(fallbackFont))
                {
                    continue;
                }

                primaryFont.fallbackFontAssetTable.Add(fallbackFont);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(primaryFont);
            }
        }

        private static List<TMP_FontAsset> GetAutomaticFallbackFonts(FigmaImportSettings settings, string textContent)
        {
            List<TMP_FontAsset> fonts = new();

            if (SystemFontResolver.ContainsSymbol(textContent))
            {
                TMP_FontAsset? symbolFallback = DynamicTmpFontFactory.GetOrCreateFallback(settings.OutputFolder, DynamicTmpFontFactory.AutoFallbackKind.Symbol);
                if (symbolFallback == null)
                {
                    symbolFallback = DynamicTmpFontFactory.CreateFallbackFromSystemFont(settings.OutputFolder, DynamicTmpFontFactory.AutoFallbackKind.Symbol);
                }

                if (IsUsableFontAsset(symbolFallback))
                {
                    fonts.Add(symbolFallback!);
                }
            }

            if (SystemFontResolver.ContainsEmoji(textContent))
            {
                TMP_FontAsset? emojiFallback = DynamicTmpFontFactory.GetOrCreateFallback(settings.OutputFolder, DynamicTmpFontFactory.AutoFallbackKind.Emoji);
                if (emojiFallback == null)
                {
                    emojiFallback = DynamicTmpFontFactory.CreateFallbackFromSystemFont(settings.OutputFolder, DynamicTmpFontFactory.AutoFallbackKind.Emoji);
                }

                if (IsUsableFontAsset(emojiFallback) && !fonts.Contains(emojiFallback!))
                {
                    fonts.Add(emojiFallback!);
                }
            }

            if (SystemFontResolver.ContainsCjk(textContent))
            {
                TMP_FontAsset? cjkFallback = DynamicTmpFontFactory.GetOrCreateFallback(settings.OutputFolder, DynamicTmpFontFactory.AutoFallbackKind.Cjk);
                if (cjkFallback == null)
                {
                    cjkFallback = DynamicTmpFontFactory.CreateFallbackFromSystemFont(settings.OutputFolder, DynamicTmpFontFactory.AutoFallbackKind.Cjk);
                }

                if (IsUsableFontAsset(cjkFallback) && !fonts.Contains(cjkFallback!))
                {
                    fonts.Add(cjkFallback!);
                }
            }

            return fonts;
        }

        private static TMP_FontAsset? FindCoverageFallbackFont(TextStyle? style, string textContent, IReadOnlyList<TMP_FontAsset> prioritizedFonts)
        {
            string familyKey = style == null ? string.Empty : NormalizeTmpFontName(style.FontFamily);
            TMP_FontAsset? preferredMatch = FindCoverageMatch(prioritizedFonts, familyKey, textContent);
            if (preferredMatch != null)
            {
                return preferredMatch;
            }

            return FindCoverageMatch(GetProjectFonts(), familyKey, textContent);
        }

        private static TMP_FontAsset? FindCoverageMatch(IEnumerable<TMP_FontAsset> fonts, string familyKey, string textContent)
        {
            TMP_FontAsset? anyCoverageMatch = null;

            foreach (TMP_FontAsset font in fonts)
            {
                if (font == null || !HasCharacterCoverage(font, textContent))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(familyKey))
                {
                    foreach (string candidate in GetFontCandidates(font))
                    {
                        if (candidate == familyKey || candidate.IndexOf(familyKey, StringComparison.Ordinal) >= 0)
                        {
                            return font;
                        }
                    }
                }

                if (anyCoverageMatch == null)
                {
                    anyCoverageMatch = font;
                }
            }

            return anyCoverageMatch;
        }

        private static TMP_FontAsset? FindProjectFont(TextStyle style)
        {
            List<TMP_FontAsset> fonts = GetProjectFonts();
            if (fonts.Count == 0)
            {
                return null;
            }

            string familyKey = NormalizeTmpFontName(style.FontFamily);
            string familyWeightItalicKey = NormalizeTmpFontName(TextStyleUtility.BuildRawFontKey(style.FontFamily, style.FontWeight, style.Italic == true, includeWeight: true, includeItalic: true));
            string familyWeightKey = NormalizeTmpFontName(TextStyleUtility.BuildRawFontKey(style.FontFamily, style.FontWeight, italic: false, includeWeight: true, includeItalic: false));

            TMP_FontAsset? familyOnlyMatch = null;
            foreach (TMP_FontAsset font in fonts)
            {
                int score = ScoreFontAsset(font, familyWeightItalicKey, familyWeightKey, familyKey);
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

        private static int ScoreFontAsset(TMP_FontAsset font, string familyWeightItalicKey, string familyWeightKey, string familyKey)
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

        private static IEnumerable<string> GetFontCandidates(TMP_FontAsset font)
        {
            yield return NormalizeTmpFontName(font.name);

            string familyName = font.faceInfo.familyName;
            if (!string.IsNullOrWhiteSpace(familyName))
            {
                yield return NormalizeTmpFontName(familyName);
            }

            string styleName = font.faceInfo.styleName;
            if (!string.IsNullOrWhiteSpace(familyName) && !string.IsNullOrWhiteSpace(styleName))
            {
                yield return NormalizeTmpFontName($"{familyName}-{styleName}");
            }
        }

        private static List<TMP_FontAsset> GetProjectFonts()
        {
            if (ProjectFontsCache != null)
            {
                return ProjectFontsCache;
            }

            ProjectFontsCache = new List<TMP_FontAsset>();
            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                if (IsUsableFontAsset(font) && !ProjectFontsCache.Contains(font!))
                {
                    ProjectFontsCache.Add(font!);
                }
            }

            return ProjectFontsCache;
        }

        private static bool IsUsableFontAsset(TMP_FontAsset? font)
        {
            if (font == null)
            {
                return false;
            }

            try
            {
                Texture[] atlasTextures = font.atlasTextures;
                return atlasTextures != null && font.material != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static TMP_FontAsset? PrepareFontForUse(TMP_FontAsset? font)
        {
            if (!IsUsableFontAsset(font))
            {
                return null;
            }

            if (SanitizeFallbackTable(font!))
            {
                EditorUtility.SetDirty(font);
            }

            return font;
        }

        private static bool SanitizeFallbackTable(TMP_FontAsset primaryFont)
        {
            List<TMP_FontAsset>? currentTable = primaryFont.fallbackFontAssetTable;
            if (currentTable == null || currentTable.Count == 0)
            {
                return false;
            }

            List<TMP_FontAsset> cleaned = new(currentTable.Count);
            bool changed = false;
            foreach (TMP_FontAsset? fallback in currentTable)
            {
                if (!IsUsableFontAsset(fallback) || ReferenceEquals(fallback, primaryFont) || cleaned.Contains(fallback!))
                {
                    changed = true;
                    continue;
                }

                cleaned.Add(fallback!);
            }

            if (!changed)
            {
                return false;
            }

            primaryFont.fallbackFontAssetTable = cleaned;
            return true;
        }

        private static void WarnIfFontSubstituted(FigmaNode node, TextStyle? style, TMP_FontAsset? resolvedFont)
        {
            if (style == null || string.IsNullOrWhiteSpace(style.FontFamily) || resolvedFont == null || MatchesRequestedFont(resolvedFont, style))
            {
                return;
            }

            string requested = DescribeRequestedFont(style);
            string resolved = DescribeTmpFont(resolvedFont);
            string warningKey = $"{requested}|{resolved}";
            if (!ReportedFontSubstitutions.Add(warningKey))
            {
                return;
            }

            Debug.LogWarning($"[FigmaImporter] TMP font substitution: requested '{requested}', using '{resolved}'. Example node: '{node.Name}' ({node.Id}).");
        }

        private static bool MatchesRequestedFont(TMP_FontAsset font, TextStyle style)
        {
            string requestedKey = NormalizeTmpFontName(TextStyleUtility.BuildRawFontKey(style.FontFamily, style.FontWeight, style.Italic == true, includeWeight: true, includeItalic: true));
            string familyKey = NormalizeTmpFontName(style.FontFamily);
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

        private static string DescribeTmpFont(TMP_FontAsset font)
        {
            string family = string.IsNullOrWhiteSpace(font.faceInfo.familyName) ? font.name : font.faceInfo.familyName;
            string style = font.faceInfo.styleName;
            return string.IsNullOrWhiteSpace(style) ? family : $"{family} {style}";
        }

        private static string BuildCacheKey(TextStyle style)
        {
            return NormalizeTmpFontName(TextStyleUtility.BuildRawFontKey(style.FontFamily, style.FontWeight, style.Italic == true, includeWeight: true, includeItalic: true));
        }

        private static string NormalizeTmpFontName(string? value)
        {
            return TextStyleUtility.NormalizeFontName(value).Replace("sdf", string.Empty);
        }

        private static FontStyles GetFontStyle(TextStyle? style)
        {
            FontStyles fontStyle = FontStyles.Normal;
            if (style?.FontWeight >= 700)
            {
                fontStyle |= FontStyles.Bold;
            }

            if (style?.Italic == true)
            {
                fontStyle |= FontStyles.Italic;
            }

            fontStyle |= GetDecorationStyle(style?.TextDecoration);
            fontStyle |= GetCaseStyle(style?.TextCase);
            return fontStyle;
        }

        private static FontStyles GetDecorationStyle(string? textDecoration)
        {
            return textDecoration switch
            {
                "UNDERLINE" => FontStyles.Underline,
                "STRIKETHROUGH" => FontStyles.Strikethrough,
                _ => FontStyles.Normal,
            };
        }

        private static FontStyles GetCaseStyle(string? textCase)
        {
            return textCase switch
            {
                "UPPER" => FontStyles.UpperCase,
                "LOWER" => FontStyles.LowerCase,
                "SMALL_CAPS" => FontStyles.SmallCaps,
                _ => FontStyles.Normal,
            };
        }

        private static TextAlignmentOptions GetAlignment(TextStyle? style)
        {
            string h = style?.TextAlignHorizontal ?? "LEFT";
            string v = style?.TextAlignVertical ?? "TOP";

            return (v, h) switch
            {
                ("CENTER", "CENTER") => TextAlignmentOptions.Center,
                ("CENTER", "RIGHT") => TextAlignmentOptions.MidlineRight,
                ("CENTER", _) => TextAlignmentOptions.MidlineLeft,
                ("BOTTOM", "CENTER") => TextAlignmentOptions.Bottom,
                ("BOTTOM", "RIGHT") => TextAlignmentOptions.BottomRight,
                ("BOTTOM", _) => TextAlignmentOptions.BottomLeft,
                (_, "CENTER") => TextAlignmentOptions.Top,
                (_, "RIGHT") => TextAlignmentOptions.TopRight,
                _ => TextAlignmentOptions.TopLeft,
            };
        }

        private static void RemoveLegacyTextComponent(GameObject gameObject)
        {
            UnityEngine.UI.Text legacyText = gameObject.GetComponent<UnityEngine.UI.Text>();
            if (legacyText != null)
            {
                UnityEngine.Object.DestroyImmediate(legacyText);
            }
        }
    }
}
