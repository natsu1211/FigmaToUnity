using System;
using UnityEditor;

namespace FigmaToUnity.Editor.State
{
    internal static class RateLimitDialog
    {
        public static bool Show(TimeSpan waitTime)
        {
            string message = $"Figma rate limit reached. Wait {Math.Ceiling(waitTime.TotalSeconds)} seconds and retry?";
            return EditorUtility.DisplayDialog("Figma Rate Limit", message, "Wait", "Cancel");
        }
    }
}
