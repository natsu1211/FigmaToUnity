using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FigmaToUnity.Editor
{
    [FilePath("ProjectSettings/LongGames.FigmaImporter.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class FigmaImportSettings : ScriptableSingleton<FigmaImportSettings>
    {
        private const string TokenKey = "LongGames.FigmaImporter.Token";

        public string LastFileUrl = string.Empty;
        public string OutputFolder = FigmaImporterPaths.DefaultSpriteOutputRoot;
        public string PrefabOutputFolder = FigmaImporterPaths.DefaultPrefabOutputRoot;
        public int FrameListDepth = 2;
        public int ApiDepth = 100;
        public int SpriteScale = 2;
        public int MaxConcurrentDownloads = 8;
        public int MaxDownloadAttempts = 3;
        public int RequestTimeoutSeconds = 60;
        public int LongRateLimitDialogThresholdSeconds = 20;
        public bool ReuseExistingSprites = true;
        public bool EnableAutoComponentPrefabs;
        public ImportModeKind ImportMode = ImportModeKind.FullImport;
        public OutputBackend Backend = OutputBackend.UGUI;
        public float ProceduralImageFalloff = 0.5f;
        public TextComponentKind TextComponent = TextComponentKind.TextMeshPro;
        public TMP_FontAsset? DefaultTmpFont;
        public List<TMP_FontAsset> FallbackTmpFonts = new();
        public bool EnableAutoSystemFontFallback = true;
        public Font? DefaultLegacyFont;
        public bool LegacyBestFit;
        public HorizontalWrapMode LegacyHorizontalWrapMode = HorizontalWrapMode.Wrap;
        public VerticalWrapMode LegacyVerticalWrapMode = VerticalWrapMode.Truncate;
        public float LegacyLineSpacing = 1f;

        public string GetToken()
        {
            return EditorPrefs.GetString(TokenKey, string.Empty);
        }

        public void SetToken(string token)
        {
            EditorPrefs.SetString(TokenKey, token ?? string.Empty);
        }

        public void SaveSettings()
        {
            Save(true);
        }
    }
}
