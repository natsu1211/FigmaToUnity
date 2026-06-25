using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FigmaToUnity.Editor.SharedPipeline
{
    // OS font discovery (CJK / symbol / emoji paths). Shared because both UGUI
    // (TMP fallback creation) and the future UIToolkit backend (USS unity-font src)
    // need the same paths.
    internal static class SystemFontResolver
    {
        // (fileName, displayName) — ordered by preference.
        private static readonly (string file, string name)[] WindowsCjkFonts =
        {
            ("msyh.ttc",    "Microsoft YaHei"),
            ("msjh.ttc",    "Microsoft JhengHei"),
            ("yugothm.ttc", "Yu Gothic Medium"),
            ("malgun.ttf",  "Malgun Gothic"),
            ("simsun.ttc",  "SimSun"),
        };

        private static readonly (string file, string name)[] MacCjkFonts =
        {
            ("PingFang.ttc",           "PingFang"),
            ("Hiragino Sans GB.ttc",   "Hiragino Sans GB"),
            ("AppleGothic.ttf",        "AppleGothic"),
            ("YuGothic-Medium.otf",    "Yu Gothic Medium"),
        };

        private static readonly string[] MacFontDirs =
        {
            "/System/Library/Fonts",
            "/Library/Fonts",
        };

        private static readonly string[] LinuxFontDirs =
        {
            "/usr/share/fonts",
            "/usr/local/share/fonts",
        };

        private static readonly string[] LinuxCjkFilePatterns =
        {
            "NotoSansCJK",
            "NotoSerifCJK",
            "wqy",
            "wenquanyi",
        };

        private static readonly (string file, string name)[] WindowsSymbolFonts =
        {
            ("seguisym.ttf", "Segoe UI Symbol"),
        };

        private static readonly (string file, string name)[] WindowsEmojiFonts =
        {
            ("seguiemj.ttf", "Segoe UI Emoji"),
        };

        private static readonly (string file, string name)[] MacSymbolFonts =
        {
            ("Apple Symbols.ttf", "Apple Symbols"),
        };

        private static readonly (string file, string name)[] MacEmojiFonts =
        {
            ("Apple Color Emoji.ttc", "Apple Color Emoji"),
        };

        private static readonly string[] LinuxSymbolFilePatterns =
        {
            "Symbola",
            "Symbols",
            "NotoSansSymbols",
            "SegoeUISymbol",
        };

        private static readonly string[] LinuxEmojiFilePatterns =
        {
            "NotoColorEmoji",
            "NotoEmoji",
            "Emoji",
        };

        /// <summary>
        /// Returns the absolute file path to a system CJK font, or null if none found.
        /// </summary>
        public static string? FindSystemCjkFontPath()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return FindWindowsCjkFont();
            }

            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                return FindMacCjkFont();
            }

            return FindLinuxCjkFont();
        }

        public static string? FindSystemSymbolFontPath()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return FindWindowsSymbolFont();
            }

            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                return FindMacSymbolFont();
            }

            return FindLinuxSymbolFont();
        }

        public static string? FindSystemEmojiFontPath()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                return FindWindowsEmojiFont();
            }

            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                return FindMacEmojiFont();
            }

            return FindLinuxEmojiFont();
        }

        private static string? FindWindowsCjkFont()
        {
            string fontsDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Fonts);
            if (string.IsNullOrEmpty(fontsDir))
            {
                fontsDir = @"C:\Windows\Fonts";
            }

            foreach ((string file, string name) in WindowsCjkFonts)
            {
                string path = Path.Combine(fontsDir, file);
                if (File.Exists(path))
                {
                    Debug.Log($"[FigmaImporter] Found system CJK font: {name} ({path})");
                    return path;
                }
            }

            return null;
        }

        private static string? FindMacCjkFont()
        {
            foreach (string dir in MacFontDirs)
            {
                foreach ((string file, string name) in MacCjkFonts)
                {
                    string path = Path.Combine(dir, file);
                    if (File.Exists(path))
                    {
                        Debug.Log($"[FigmaImporter] Found system CJK font: {name} ({path})");
                        return path;
                    }
                }
            }

            // macOS may also have fonts under ~/Library/Fonts.
            string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home))
            {
                string userFonts = Path.Combine(home, "Library/Fonts");
                foreach ((string file, string name) in MacCjkFonts)
                {
                    string path = Path.Combine(userFonts, file);
                    if (File.Exists(path))
                    {
                        Debug.Log($"[FigmaImporter] Found system CJK font: {name} ({path})");
                        return path;
                    }
                }
            }

            return null;
        }

        private static string? FindLinuxCjkFont()
        {
            foreach (string dir in LinuxFontDirs)
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                try
                {
                    string[] files = Directory.GetFiles(dir, "*.ttf", SearchOption.AllDirectories)
                        .Concat(Directory.GetFiles(dir, "*.ttc", SearchOption.AllDirectories))
                        .Concat(Directory.GetFiles(dir, "*.otf", SearchOption.AllDirectories))
                        .ToArray();

                    foreach (string file in files)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                        if (LinuxCjkFilePatterns.Any(p => fileName.Contains(p.ToLowerInvariant())))
                        {
                            Debug.Log($"[FigmaImporter] Found system CJK font: {file}");
                            return file;
                        }
                    }
                }
                catch (System.Exception)
                {
                    // Permission issues — skip this directory.
                }
            }

            return null;
        }

        private static string? FindWindowsSymbolFont()
        {
            string fontsDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Fonts);
            if (string.IsNullOrEmpty(fontsDir))
            {
                fontsDir = @"C:\Windows\Fonts";
            }

            foreach ((string file, string name) in WindowsSymbolFonts)
            {
                string path = Path.Combine(fontsDir, file);
                if (File.Exists(path))
                {
                    Debug.Log($"[FigmaImporter] Found system symbol font: {name} ({path})");
                    return path;
                }
            }

            return null;
        }

        private static string? FindMacSymbolFont()
        {
            foreach (string dir in MacFontDirs)
            {
                foreach ((string file, string name) in MacSymbolFonts)
                {
                    string path = Path.Combine(dir, file);
                    if (File.Exists(path))
                    {
                        Debug.Log($"[FigmaImporter] Found system symbol font: {name} ({path})");
                        return path;
                    }
                }
            }

            return null;
        }

        private static string? FindLinuxSymbolFont()
        {
            foreach (string dir in LinuxFontDirs)
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                try
                {
                    string[] files = Directory.GetFiles(dir, "*.ttf", SearchOption.AllDirectories)
                        .Concat(Directory.GetFiles(dir, "*.ttc", SearchOption.AllDirectories))
                        .Concat(Directory.GetFiles(dir, "*.otf", SearchOption.AllDirectories))
                        .ToArray();

                    foreach (string file in files)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                        if (LinuxSymbolFilePatterns.Any(p => fileName.Contains(p.ToLowerInvariant())))
                        {
                            Debug.Log($"[FigmaImporter] Found system symbol font: {file}");
                            return file;
                        }
                    }
                }
                catch (System.Exception)
                {
                    // Skip inaccessible directories.
                }
            }

            return null;
        }

        private static string? FindWindowsEmojiFont()
        {
            string fontsDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Fonts);
            if (string.IsNullOrEmpty(fontsDir))
            {
                fontsDir = @"C:\Windows\Fonts";
            }

            foreach ((string file, string name) in WindowsEmojiFonts)
            {
                string path = Path.Combine(fontsDir, file);
                if (File.Exists(path))
                {
                    Debug.Log($"[FigmaImporter] Found system emoji font: {name} ({path})");
                    return path;
                }
            }

            return null;
        }

        private static string? FindMacEmojiFont()
        {
            foreach (string dir in MacFontDirs)
            {
                foreach ((string file, string name) in MacEmojiFonts)
                {
                    string path = Path.Combine(dir, file);
                    if (File.Exists(path))
                    {
                        Debug.Log($"[FigmaImporter] Found system emoji font: {name} ({path})");
                        return path;
                    }
                }
            }

            return null;
        }

        private static string? FindLinuxEmojiFont()
        {
            foreach (string dir in LinuxFontDirs)
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                try
                {
                    string[] files = Directory.GetFiles(dir, "*.ttf", SearchOption.AllDirectories)
                        .Concat(Directory.GetFiles(dir, "*.ttc", SearchOption.AllDirectories))
                        .Concat(Directory.GetFiles(dir, "*.otf", SearchOption.AllDirectories))
                        .ToArray();

                    foreach (string file in files)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                        if (LinuxEmojiFilePatterns.Any(p => fileName.Contains(p.ToLowerInvariant())))
                        {
                            Debug.Log($"[FigmaImporter] Found system emoji font: {file}");
                            return file;
                        }
                    }
                }
                catch (System.Exception)
                {
                    // Skip inaccessible directories.
                }
            }

            return null;
        }

        /// <summary>
        /// Returns true if the character falls in a CJK Unicode range.
        /// </summary>
        internal static bool IsCjk(char c)
        {
            return c >= '\u4E00' && c <= '\u9FFF'
                || c >= '\u3400' && c <= '\u4DBF'
                || c >= '\u3000' && c <= '\u303F'
                || c >= '\u3040' && c <= '\u309F'
                || c >= '\u30A0' && c <= '\u30FF'
                || c >= '\uAC00' && c <= '\uD7AF'
                || c >= '\uFF00' && c <= '\uFFEF';
        }

        /// <summary>
        /// Returns true if the string contains at least one CJK character.
        /// </summary>
        internal static bool ContainsCjk(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (char c in text!)
            {
                if (IsCjk(c))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool ContainsEmoji(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (char c in text!)
            {
                if (char.IsSurrogate(c))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool ContainsSymbol(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (char c in text!)
            {
                if (char.IsSurrogate(c))
                {
                    continue;
                }

                if (c >= '\u2190' && c <= '\u21FF')
                {
                    return true;
                }

                if (c >= '\u2300' && c <= '\u23FF')
                {
                    return true;
                }

                if (c >= '\u2600' && c <= '\u27BF')
                {
                    return true;
                }
            }

            return false;
        }
    }
}
