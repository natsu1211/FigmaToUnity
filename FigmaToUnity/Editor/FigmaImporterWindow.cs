using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FigmaToUnity.Core;
using FigmaToUnity.Editor.State;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UIButton = UnityEngine.UIElements.Button;
using UIToggle = UnityEngine.UIElements.Toggle;

namespace FigmaToUnity.Editor
{
    internal sealed class FigmaImporterWindow : EditorWindow
    {
        private FigmaImportController? _controller;
        private FigmaFrameCacheStore? _frameCacheStore;
        private Dictionary<string, UIToggle>? _frameToggles;
        private FigmaImportSession? _session;

        private TextField _tokenField = null!;
        private TextField _urlField = null!;
        private TextField _outputField = null!;
        private TextField _prefabOutputField = null!;
        private IntegerField _frameDepthField = null!;
        private IntegerField _spriteScaleField = null!;
        private IntegerField _maxConcurrentDownloadsField = null!;
        private EnumField _importModeField = null!;
        private EnumField _backendField = null!;
        private FloatField _proceduralImageFalloffField = null!;
        private EnumField _textComponentField = null!;
        private VisualElement _tmpTextSettingsSection = null!;
        private ObjectField _defaultTmpFontField = null!;
        private IMGUIContainer _fallbackFontsContainer = null!;
        private List<TMP_FontAsset?>? _fallbackTmpFonts;
        private UIToggle _autoSystemFontFallbackToggle = null!;
        private VisualElement _legacyTextSettingsSection = null!;
        private ObjectField _defaultLegacyFontField = null!;
        private UIToggle _legacyBestFitToggle = null!;
        private EnumField _legacyHorizontalWrapField = null!;
        private EnumField _legacyVerticalWrapField = null!;
        private FloatField _legacyLineSpacingField = null!;
        private UIToggle _reuseSpritesToggle = null!;
        private UIToggle _enableAutoComponentPrefabsToggle = null!;
        private Label _statusLabel = null!;
        private Label _stageLabel = null!;
        private ProgressBar _progressBar = null!;
        private ScrollView _framesView = null!;
        private TextField _logsView = null!;
        // UI Toolkit's TextElement caps at 65535 vertices (~16K glyphs = 4 verts each).
        // Cap log buffer well below that and drop oldest lines on overflow.
        private const int MaxLogLines = 200;
        private const int MaxLogChars = 10000;
        private readonly Queue<string> _logEntries = new();
        private int _logsCharCount;
        private UIButton _verifyButton = null!;
        private UIButton _fetchFramesButton = null!;
        private UIButton _importButton = null!;
        private UIButton _cancelButton = null!;

        [MenuItem("LongGames/Figma Importer")]
        public static void ShowWindow()
        {
            FigmaImporterWindow window = GetWindow<FigmaImporterWindow>();
            window.titleContent = new GUIContent("Figma Importer");
            window.minSize = new Vector2(460f, 620f);
        }

        private void OnEnable()
        {
            EnsureRuntimeState();
            FigmaImportController controller = _controller!;
            controller.ProgressChanged += OnProgressChanged;
            controller.LogAdded += AddLog;
            controller.BusyChanged += OnBusyChanged;
            LoadSettingsIntoSession();
        }

        private void OnDisable()
        {
            if (_controller == null)
            {
                return;
            }

            _controller.ProgressChanged -= OnProgressChanged;
            _controller.LogAdded -= AddLog;
            _controller.BusyChanged -= OnBusyChanged;
            _controller.Cancel();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(FigmaImporterPaths.WindowStyleSheet);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogError($"[FigmaImporter] Failed to load stylesheet at '{FigmaImporterPaths.WindowStyleSheet}'. Window will render without styling.");
            }
            rootVisualElement.AddToClassList("root");

            ScrollView scrollView = new(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1f;
            scrollView.style.flexShrink = 1f;
            rootVisualElement.Add(scrollView);

            scrollView.Add(MakeHeader());
            scrollView.Add(MakeAuthSection());
            scrollView.Add(MakeProjectSection());
            scrollView.Add(MakeFramesSection());
            scrollView.Add(MakeSettingsSection());
            scrollView.Add(MakeActionsSection());
            scrollView.Add(MakeProgressSection());
            scrollView.Add(MakeLogsSection());

            RefreshFieldsFromSession();
            RestoreCachedFramesIfAvailable();
            RefreshActionState(false);
        }

        private VisualElement MakeHeader()
        {
            VisualElement section = MakeHeroSection();
            Label title = new("Figma Importer");
            title.AddToClassList("hero-title");
            section.Add(title);

            Label subtitle = new("Import frames from Figma into UGUI with prefab and diff-update support.");
            subtitle.AddToClassList("hero-subtitle");
            section.Add(subtitle);

            _statusLabel = new Label("Idle");
            _statusLabel.AddToClassList("status-pill");
            section.Add(_statusLabel);
            return section;
        }

        private VisualElement MakeAuthSection()
        {
            VisualElement section = MakeSection("Figma Auth", "Access token and account verification.");

            _tokenField = new TextField("Token")
            {
                isPasswordField = true
            };
            section.Add(_tokenField);

            _verifyButton = new UIButton(async () => await VerifyTokenAsync())
            {
                text = "Verify Token"
            };
            section.Add(_verifyButton);
            return section;
        }

        private VisualElement MakeProjectSection()
        {
            VisualElement section = MakeSection("Project", "Source file and frame discovery.");

            _urlField = new TextField("Figma URL");
            section.Add(_urlField);

            _fetchFramesButton = new UIButton(async () => await LoadFramesAsync())
            {
                text = "Fetch Frames"
            };
            section.Add(_fetchFramesButton);
            return section;
        }

        private VisualElement MakeFramesSection()
        {
            VisualElement section = MakeSection("Frames", "Select the frame roots to import.");
            _framesView = new ScrollView();
            _framesView.style.maxHeight = 180f;
            _framesView.AddToClassList("frames-box");
            section.Add(_framesView);
            return section;
        }

        private VisualElement MakeSettingsSection()
        {
            VisualElement wrapper = new();

            // Output
            VisualElement outputSection = MakeSection("Output", "Sprite / Prefab asset output directories and emission policy.");
            _outputField = new TextField("Sprite Folder");
            outputSection.Add(_outputField);
            _prefabOutputField = new TextField("Prefab Folder");
            outputSection.Add(_prefabOutputField);
            _enableAutoComponentPrefabsToggle = new UIToggle("Auto Prefab for Top-Level Components");
            _enableAutoComponentPrefabsToggle.tooltip = "有効にすると、Figma の最上位 COMPONENT/INSTANCE ノードを独立した Prefab として出力します。ネストされたコンポーネントは最も外側の祖先にベイクされます。デザインが小さな Prefab に分割されすぎるのを防ぐため既定では無効です。ノード名に `#prefab` を含むものは、このトグルに関係なく常に Prefab 化されます。";
            outputSection.Add(_enableAutoComponentPrefabsToggle);
            wrapper.Add(outputSection);

            // Mode
            VisualElement modeSection = MakeSection("Mode", "Import strategy and asset reuse.");
            _importModeField = new EnumField("Import Mode", ImportModeKind.FullImport);
            _importModeField.tooltip = "FullImport: シーンをゼロから再構築します。DiffUpdate: 既存の FigmaSyncMarker 付き GameObject を新しい IR との差分でパッチします。";
            modeSection.Add(_importModeField);
            _backendField = new EnumField("Output Backend", OutputBackend.UGUI);
            _backendField.tooltip = "UGUI: GameObject + MonoBehaviour 階層を出力します（既定）。UIToolkit: Assets/UI/<file>/ 配下に UXML + USS を出力します（.generated.uss は毎回上書き、.uss ラッパーは初回のみ作成）。";
            modeSection.Add(_backendField);
            _reuseSpritesToggle = new UIToggle("Reuse Existing Sprites");
            _reuseSpritesToggle.tooltip = "Sprite Folder 配下に既存ファイルがあるスプライトの再ダウンロードをスキップします。再インポート時の帯域と Figma API クォータを節約できます。";
            modeSection.Add(_reuseSpritesToggle);
            wrapper.Add(modeSection);

            // Network
            VisualElement networkSection = MakeSection("Network", "Figma REST API parameters and download concurrency.");
            _frameDepthField = new IntegerField("Frame List Depth");
            _frameDepthField.tooltip = "Frames 一覧取得時に /files へ渡す depth パラメータ。値を大きくするとドキュメントツリーをより深くまで取得しますが、リクエストが遅くなります。";
            networkSection.Add(_frameDepthField);
            _spriteScaleField = new IntegerField("Sprite Scale");
            _spriteScaleField.tooltip = "/images エンドポイントに渡す scale パラメータ。1 = 等倍、2 = 2倍（Retina 相当）など。ラスタライズされる PNG の解像度とダウンロードサイズに影響します。";
            networkSection.Add(_spriteScaleField);
            _maxConcurrentDownloadsField = new IntegerField("Max Downloads");
            _maxConcurrentDownloadsField.tooltip = "Figma CDN からスプライト PNG を並列ダウンロードする際の最大同時実行数。";
            networkSection.Add(_maxConcurrentDownloadsField);
            wrapper.Add(networkSection);

            // Rendering
            VisualElement renderingSection = MakeSection("Rendering", "Runtime rendering parameters applied to imported assets.");
            _proceduralImageFalloffField = new FloatField("Procedural Falloff");
            _proceduralImageFalloffField.tooltip = "ProceduralRoundedImage が使用するエッジのアンチエイリアス幅（ピクセル）。値を大きくするとエッジが滑らかになりますが、わずかにぼけます。";
            renderingSection.Add(_proceduralImageFalloffField);
            wrapper.Add(renderingSection);

            // Text & Fonts
            VisualElement textSection = MakeSection("Text & Fonts", "Text component type and font resolution settings.");
            _textComponentField = new EnumField("Text Component", TextComponentKind.TextMeshPro);
            _textComponentField.RegisterValueChangedCallback(_ => RefreshTextSettingsVisibility());
            textSection.Add(_textComponentField);

            _tmpTextSettingsSection = MakeSubsection("TMP Settings");
            _defaultTmpFontField = new ObjectField("Default Font")
            {
                objectType = typeof(TMP_FontAsset),
                allowSceneObjects = false
            };
            _tmpTextSettingsSection.Add(_defaultTmpFontField);
            _tmpTextSettingsSection.Add(new Label("Fallback Fonts"));
            _fallbackFontsContainer = CreateListContainer(DrawFallbackFonts);
            _tmpTextSettingsSection.Add(_fallbackFontsContainer);
            _autoSystemFontFallbackToggle = new UIToggle("Auto System Font Fallback (CJK)");
            _tmpTextSettingsSection.Add(_autoSystemFontFallbackToggle);
            textSection.Add(_tmpTextSettingsSection);

            _legacyTextSettingsSection = MakeSubsection("Legacy Settings");
            _defaultLegacyFontField = new ObjectField("Default Font")
            {
                objectType = typeof(Font),
                allowSceneObjects = false
            };
            _legacyTextSettingsSection.Add(_defaultLegacyFontField);
            _legacyBestFitToggle = new UIToggle("Best Fit");
            _legacyTextSettingsSection.Add(_legacyBestFitToggle);
            _legacyHorizontalWrapField = new EnumField("Horizontal Wrap", HorizontalWrapMode.Wrap);
            _legacyTextSettingsSection.Add(_legacyHorizontalWrapField);
            _legacyVerticalWrapField = new EnumField("Vertical Wrap", VerticalWrapMode.Truncate);
            _legacyTextSettingsSection.Add(_legacyVerticalWrapField);
            _legacyLineSpacingField = new FloatField("Line Spacing");
            _legacyTextSettingsSection.Add(_legacyLineSpacingField);
            textSection.Add(_legacyTextSettingsSection);

            wrapper.Add(textSection);
            return wrapper;
        }

        private static IMGUIContainer CreateListContainer(Action onGuiHandler)
        {
            IMGUIContainer container = new(onGuiHandler);
            container.style.marginTop = 2f;
            container.style.marginBottom = 6f;
            return container;
        }

        private VisualElement MakeActionsSection()
        {
            VisualElement section = MakeSection("Actions", "Run import, cancel in-flight work, and watch progress below.");
            VisualElement row = new();
            row.AddToClassList("toolbar-row");

            _importButton = new UIButton(async () => await StartImportAsync())
            {
                text = "Import Selected"
            };
            _importButton.AddToClassList("import-button");

            _cancelButton = new UIButton(() => _controller!.Cancel())
            {
                text = "Cancel"
            };
            _cancelButton.AddToClassList("cancel-button");

            row.Add(_importButton);
            row.Add(_cancelButton);
            section.Add(row);
            return section;
        }

        private VisualElement MakeProgressSection()
        {
            VisualElement section = MakeSection("Progress", "Current stage and import progress.");
            _stageLabel = new Label("No task running.");
            _stageLabel.AddToClassList("stage-label");
            section.Add(_stageLabel);
            _progressBar = new ProgressBar();
            section.Add(_progressBar);
            return section;
        }

        private VisualElement MakeLogsSection()
        {
            VisualElement section = MakeSection("Logs", "Recent importer activity.");
            _logsView = new TextField
            {
                multiline = true,
                isReadOnly = true
            };
            _logsView.AddToClassList("log-box");
            _logsView.style.minHeight = 140f;
            _logsView.style.maxHeight = 260f;
            _logsView.style.whiteSpace = WhiteSpace.Normal;
            section.Add(_logsView);
            return section;
        }

        private static VisualElement MakeHeroSection()
        {
            VisualElement section = new();
            section.AddToClassList("hero-section");
            return section;
        }

        private static VisualElement MakeSection(string title, string description)
        {
            VisualElement section = new();
            section.AddToClassList("section");

            Label titleLabel = new(title);
            titleLabel.AddToClassList("section-title");
            section.Add(titleLabel);

            if (!string.IsNullOrWhiteSpace(description))
            {
                Label descriptionLabel = new(description);
                descriptionLabel.AddToClassList("section-description");
                section.Add(descriptionLabel);
            }

            VisualElement divider = new();
            divider.AddToClassList("section-divider");
            section.Add(divider);
            return section;
        }

        private static VisualElement MakeSubsection(string title)
        {
            VisualElement section = new();
            section.AddToClassList("subsection");

            Label titleLabel = new(title);
            titleLabel.AddToClassList("subsection-title");
            section.Add(titleLabel);

            VisualElement divider = new();
            divider.AddToClassList("subsection-divider");
            section.Add(divider);
            return section;
        }

        private void LoadSettingsIntoSession()
        {
            FigmaImportSettings settings = FigmaImportSettings.instance;
            FigmaImportSession session = _session!;
            session.Token = settings.GetToken();
            session.FigmaUrl = settings.LastFileUrl;
            session.OutputFolder = settings.OutputFolder;
            session.PrefabOutputFolder = settings.PrefabOutputFolder;
        }

        private void RefreshFieldsFromSession()
        {
            if (_tokenField == null)
            {
                return;
            }

            FigmaImportSettings settings = FigmaImportSettings.instance;
            FigmaImportSession session = _session!;
            List<TMP_FontAsset?> fallbackTmpFonts = _fallbackTmpFonts!;
            _tokenField.value = session.Token;
            _urlField.value = session.FigmaUrl;
            _outputField.value = session.OutputFolder;
            _prefabOutputField.value = session.PrefabOutputFolder;
            _frameDepthField.value = settings.FrameListDepth;
            _spriteScaleField.value = settings.SpriteScale;
            _maxConcurrentDownloadsField.value = settings.MaxConcurrentDownloads;
            _importModeField.value = settings.ImportMode;
            _backendField.value = settings.Backend;
            _proceduralImageFalloffField.value = settings.ProceduralImageFalloff;
            _textComponentField.value = settings.TextComponent;
            _defaultTmpFontField.value = settings.DefaultTmpFont;
            fallbackTmpFonts.Clear();
            foreach (TMP_FontAsset font in settings.FallbackTmpFonts.OfType<TMP_FontAsset>().Distinct())
            {
                fallbackTmpFonts.Add(font);
            }
            _fallbackFontsContainer?.MarkDirtyRepaint();
            _defaultLegacyFontField.value = settings.DefaultLegacyFont;
            _legacyBestFitToggle.value = settings.LegacyBestFit;
            _legacyHorizontalWrapField.value = settings.LegacyHorizontalWrapMode;
            _legacyVerticalWrapField.value = settings.LegacyVerticalWrapMode;
            _legacyLineSpacingField.value = settings.LegacyLineSpacing;
            _reuseSpritesToggle.value = settings.ReuseExistingSprites;
            _enableAutoComponentPrefabsToggle.value = settings.EnableAutoComponentPrefabs;
            _autoSystemFontFallbackToggle.value = settings.EnableAutoSystemFontFallback;
            RefreshTextSettingsVisibility();
        }

        private async Task VerifyTokenAsync()
        {
            try
            {
                SaveFieldsToSession();
                string userName = await _controller!.VerifyTokenAsync(_session!);
                _statusLabel.text = $"Connected: {userName}";
                SaveSessionToSettings();
            }
            catch (Exception ex)
            {
                ShowFailureState("Verification failed", ex.Message);
            }
        }

        private async Task LoadFramesAsync()
        {
            try
            {
                SaveFieldsToSession();
                SaveSessionToSettings();
                IReadOnlyList<FrameSummary> frames = await _controller!.LoadFramesAsync(_session!);
                RebuildFrameList(frames);
            }
            catch (Exception ex)
            {
                ShowFailureState("Fetch frames failed", ex.Message);
            }
        }

        private async Task StartImportAsync()
        {
            try
            {
                SaveFieldsToSession();
                SaveSelectedFrames();
                SaveSessionToSettings();
                await _controller!.StartImportAsync(_session!);
            }
            catch (Exception ex)
            {
                ShowFailureState("Import failed", ex.Message);
            }
        }

        private void SaveFieldsToSession()
        {
            FigmaImportSession session = _session!;
            session.Token = _tokenField.value?.Trim() ?? string.Empty;
            session.FigmaUrl = _urlField.value?.Trim() ?? string.Empty;
            session.OutputFolder = _outputField.value?.Trim() ?? FigmaImporterPaths.DefaultSpriteOutputRoot;
            session.PrefabOutputFolder = _prefabOutputField.value?.Trim() ?? FigmaImporterPaths.DefaultPrefabOutputRoot;
        }

        private void SaveSessionToSettings()
        {
            FigmaImportSettings settings = FigmaImportSettings.instance;
            FigmaImportSession session = _session!;
            List<TMP_FontAsset?> fallbackTmpFonts = _fallbackTmpFonts!;
            settings.SetToken(session.Token);
            settings.LastFileUrl = session.FigmaUrl;
            settings.OutputFolder = session.OutputFolder;
            settings.PrefabOutputFolder = session.PrefabOutputFolder;
            settings.FrameListDepth = Mathf.Max(1, _frameDepthField.value);
            settings.SpriteScale = Mathf.Max(1, _spriteScaleField.value);
            settings.MaxConcurrentDownloads = Mathf.Max(1, _maxConcurrentDownloadsField.value);
            settings.ImportMode = (ImportModeKind)_importModeField.value;
            settings.Backend = (OutputBackend)_backendField.value;
            settings.ProceduralImageFalloff = Mathf.Max(0.01f, _proceduralImageFalloffField.value);
            settings.TextComponent = (TextComponentKind)_textComponentField.value;
            settings.DefaultTmpFont = _defaultTmpFontField.value as TMP_FontAsset;
            settings.FallbackTmpFonts = fallbackTmpFonts.OfType<TMP_FontAsset>().Distinct().ToList();
            settings.ReuseExistingSprites = _reuseSpritesToggle.value;
            settings.EnableAutoComponentPrefabs = _enableAutoComponentPrefabsToggle.value;
            settings.EnableAutoSystemFontFallback = _autoSystemFontFallbackToggle.value;
            settings.DefaultLegacyFont = _defaultLegacyFontField.value as Font;
            settings.LegacyBestFit = _legacyBestFitToggle.value;
            settings.LegacyHorizontalWrapMode = (HorizontalWrapMode)_legacyHorizontalWrapField.value;
            settings.LegacyVerticalWrapMode = (VerticalWrapMode)_legacyVerticalWrapField.value;
            settings.LegacyLineSpacing = Mathf.Max(0.1f, _legacyLineSpacingField.value);
            settings.SaveSettings();
        }

        private void DrawFallbackFonts()
        {
            int removeIndex = -1;
            List<TMP_FontAsset?> fallbackTmpFonts = _fallbackTmpFonts!;

            for (int index = 0; index < fallbackTmpFonts.Count; index++)
            {
                EditorGUILayout.BeginHorizontal();
                TMP_FontAsset? updatedFont = (TMP_FontAsset?)EditorGUILayout.ObjectField(fallbackTmpFonts[index], typeof(TMP_FontAsset), false);
                if (!ReferenceEquals(updatedFont, fallbackTmpFonts[index]))
                {
                    fallbackTmpFonts[index] = updatedFont;
                }

                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    removeIndex = index;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (removeIndex >= 0)
            {
                fallbackTmpFonts.RemoveAt(removeIndex);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Fallback Font", GUILayout.Width(160f)))
            {
                fallbackTmpFonts.Add(null);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void RebuildFrameList(IReadOnlyList<FrameSummary> frames)
        {
            _framesView.Clear();
            FigmaImportSession session = _session!;
            Dictionary<string, UIToggle> frameToggles = _frameToggles!;
            frameToggles.Clear();

            foreach (FrameSummary frame in frames)
            {
                VisualElement row = new();
                row.AddToClassList("frame-row");

                UIToggle toggle = new(frame.Name)
                {
                    value = session.SelectedFrameIds.Contains(frame.Id)
                };
                toggle.RegisterValueChangedCallback(evt => OnFrameSelectionChanged(frame.Id, evt.newValue));
                Label sizeLabel = new($"({frame.Width:0} x {frame.Height:0})");
                sizeLabel.AddToClassList("muted");

                row.Add(toggle);
                row.Add(sizeLabel);
                _framesView.Add(row);
                frameToggles[frame.Id] = toggle;
            }
        }

        private void SaveSelectedFrames()
        {
            FigmaImportSession session = _session!;
            Dictionary<string, UIToggle> frameToggles = _frameToggles!;
            session.SelectedFrameIds.Clear();
            foreach ((string frameId, UIToggle toggle) in frameToggles)
            {
                if (toggle.value)
                {
                    session.SelectedFrameIds.Add(frameId);
                }
            }

            PersistFrameSelection();
        }

        private void OnProgressChanged(ImportProgress progress)
        {
            _stageLabel.text = $"{progress.StageName}: {progress.Message}";
            _progressBar.lowValue = 0f;
            _progressBar.highValue = 100f;
            _progressBar.value = progress.IsIndeterminate ? 0f : progress.Percentage * 100f;
            _progressBar.title = progress.IsIndeterminate ? "Working..." : $"{progress.CompletedItems}/{progress.TotalItems}";
        }

        private void OnBusyChanged(bool isBusy)
        {
            RefreshActionState(isBusy);

            if (!isBusy && _progressBar != null && _progressBar.title == "Working...")
            {
                _progressBar.title = string.Empty;
            }
        }

        private void RefreshActionState(bool isBusy)
        {
            if (_verifyButton == null)
            {
                return;
            }

            _verifyButton.SetEnabled(!isBusy);
            _fetchFramesButton.SetEnabled(!isBusy);
            _importButton.SetEnabled(!isBusy);
            _cancelButton.SetEnabled(isBusy);
            _tokenField.SetEnabled(!isBusy);
            _urlField.SetEnabled(!isBusy);
            _outputField.SetEnabled(!isBusy);
            _prefabOutputField.SetEnabled(!isBusy);
            _frameDepthField.SetEnabled(!isBusy);
            _spriteScaleField.SetEnabled(!isBusy);
            _maxConcurrentDownloadsField.SetEnabled(!isBusy);
            _importModeField.SetEnabled(!isBusy);
            _backendField.SetEnabled(!isBusy);
            _proceduralImageFalloffField.SetEnabled(!isBusy);
            _textComponentField.SetEnabled(!isBusy);
            _tmpTextSettingsSection.SetEnabled(!isBusy);
            _defaultTmpFontField.SetEnabled(!isBusy);
            _fallbackFontsContainer.SetEnabled(!isBusy);
            _legacyTextSettingsSection.SetEnabled(!isBusy);
            _defaultLegacyFontField.SetEnabled(!isBusy);
            _legacyBestFitToggle.SetEnabled(!isBusy);
            _legacyHorizontalWrapField.SetEnabled(!isBusy);
            _legacyVerticalWrapField.SetEnabled(!isBusy);
            _legacyLineSpacingField.SetEnabled(!isBusy);
            _reuseSpritesToggle.SetEnabled(!isBusy);
            _enableAutoComponentPrefabsToggle.SetEnabled(!isBusy);
            _autoSystemFontFallbackToggle.SetEnabled(!isBusy);
        }

        private void RefreshTextSettingsVisibility()
        {
            if (_textComponentField == null)
            {
                return;
            }

            bool useTmp = (TextComponentKind)_textComponentField.value == TextComponentKind.TextMeshPro;
            _tmpTextSettingsSection.style.display = useTmp ? DisplayStyle.Flex : DisplayStyle.None;
            _legacyTextSettingsSection.style.display = useTmp ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void AddLog(string message)
        {
            if (_logsView == null)
            {
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string entry = $"[{timestamp}] {message}";
            _logEntries.Enqueue(entry);
            _logsCharCount += entry.Length + 1; // +1 for the joining '\n'

            while (_logEntries.Count > MaxLogLines || _logsCharCount > MaxLogChars)
            {
                if (_logEntries.Count == 0)
                {
                    break;
                }

                string dropped = _logEntries.Dequeue();
                _logsCharCount -= dropped.Length + 1;
            }

            _logsView.SetValueWithoutNotify(string.Join("\n", _logEntries));
            _statusLabel.text = message;
            // Park the caret at the end so the TextField auto-scrolls to the
            // newest line. SetValueWithoutNotify avoids re-triggering change events.
            _logsView.schedule.Execute(() =>
            {
                int end = _logsView.value.Length;
                _logsView.cursorIndex = end;
                _logsView.selectIndex = end;
            }).ExecuteLater(0);
        }

        private void ShowFailureState(string title, string message)
        {
            _statusLabel.text = title;
            _stageLabel.text = title;
            _progressBar.lowValue = 0f;
            _progressBar.highValue = 100f;
            _progressBar.value = 0f;
            _progressBar.title = "Failed";
            AddLog(message);
        }

        private void RestoreCachedFramesIfAvailable()
        {
            EnsureRuntimeState();
            if (!_controller!.TryRestoreCachedFrames(_session!) || _session!.Frames.Count == 0)
            {
                return;
            }

            RebuildFrameList(_session.Frames);
            _statusLabel.text = $"Restored {_session.Frames.Count} cached frame(s).";
            _stageLabel.text = "Frames: Restored cached frame list.";
        }

        private void OnFrameSelectionChanged(string frameId, bool isSelected)
        {
            EnsureRuntimeState();
            if (isSelected)
            {
                _session!.SelectedFrameIds.Add(frameId);
            }
            else
            {
                _session!.SelectedFrameIds.Remove(frameId);
            }

            PersistFrameSelection();
        }

        private void PersistFrameSelection()
        {
            EnsureRuntimeState();
            if (string.IsNullOrWhiteSpace(_session!.FileKey))
            {
                string url = _urlField?.value ?? string.Empty;
                if (!FigmaUrlParser.TryParseFileKey(url, out string parsedFileKey))
                {
                    return;
                }

                _session.FileKey = parsedFileKey;
            }

            int depth = Mathf.Max(1, _frameDepthField?.value ?? FigmaImportSettings.instance.FrameListDepth);
            _frameCacheStore!.UpdateSelection(_session.FileKey, depth, _session.SelectedFrameIds);
        }

        private void EnsureRuntimeState()
        {
            _controller ??= new FigmaImportController();
            _frameCacheStore ??= FigmaFrameCacheStore.instance;
            _frameToggles ??= new Dictionary<string, UIToggle>();
            _session ??= new FigmaImportSession();
            _fallbackTmpFonts ??= new List<TMP_FontAsset?>();
        }
    }
}
