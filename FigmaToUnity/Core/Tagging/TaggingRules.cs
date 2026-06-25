using System;

namespace FigmaToUnity.Core
{
    public static class TaggingRules
    {
        public static bool IsVisible(DesignNode node)
        {
            if (node.Visible == false)
            {
                return false;
            }

            return node.Opacity is null || node.Opacity.Value > 0f;
        }

        public static bool HasVisiblePaints(DesignNode node)
        {
            if (node.Fills != null)
            {
                foreach (Paint paint in node.Fills)
                {
                    if (paint.Visible != false)
                    {
                        return true;
                    }
                }
            }

            if (node.Strokes != null)
            {
                foreach (Paint stroke in node.Strokes)
                {
                    if (stroke.Visible != false)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool HasGraphicContent(DesignNode node)
        {
            if (HasVisiblePaints(node))
            {
                return true;
            }

            return node.Type is "VECTOR" or "LINE" or "ELLIPSE" or "BOOLEAN_OPERATION" or "STAR" or "REGULAR_POLYGON" or "RECTANGLE";
        }

        public static bool IsButtonStateName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string lowered = (name ?? string.Empty).Trim().ToLowerInvariant();
            return lowered is "default" or "hover" or "pressed" or "disabled" or "normal" or "selected";
        }

        public static bool HasButtonStateChildren(DesignNode node)
        {
            if (node.Children == null || node.Children.Count == 0)
            {
                return false;
            }

            int matchedStates = 0;
            foreach (DesignNode child in node.Children)
            {
                if (IsButtonStateName(child.Name))
                {
                    matchedStates++;
                }
            }

            return matchedStates >= 2;
        }

        public static bool IsLikelyButtonName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string lowered = (name ?? string.Empty).ToLowerInvariant();
            return lowered.Contains("button") || lowered.StartsWith("btn", StringComparison.Ordinal) || lowered.Contains("_btn") || lowered.Contains("-btn");
        }

        public static bool IsLikelyDecorativeGroupName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string lowered = name.Trim().ToLowerInvariant();
            return lowered.Contains("装飾") ||
                   lowered.Contains("decoration") ||
                   lowered.Contains("decor");
        }

        public static bool IsNineSlice(DesignNode node)
        {
            if (node.Children == null || node.Children.Count != 9)
            {
                return false;
            }

            return MatchesConstraint(node.Children[0], "LEFT", "TOP") &&
                   MatchesConstraint(node.Children[1], "LEFT_RIGHT", "TOP") &&
                   MatchesConstraint(node.Children[2], "RIGHT", "TOP") &&
                   MatchesConstraint(node.Children[3], "LEFT", "TOP_BOTTOM") &&
                   MatchesConstraint(node.Children[4], "LEFT_RIGHT", "TOP_BOTTOM") &&
                   MatchesConstraint(node.Children[5], "RIGHT", "TOP_BOTTOM") &&
                   MatchesConstraint(node.Children[6], "LEFT", "BOTTOM") &&
                   MatchesConstraint(node.Children[7], "LEFT_RIGHT", "BOTTOM") &&
                   MatchesConstraint(node.Children[8], "RIGHT", "BOTTOM");
        }

        private static bool MatchesConstraint(DesignNode node, string horizontal, string vertical)
        {
            return string.Equals(node.Constraints?.Horizontal, horizontal, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(node.Constraints?.Vertical, vertical, StringComparison.OrdinalIgnoreCase);
        }
    }
}
