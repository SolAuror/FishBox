using System;
using System.Collections.Generic;
using System.Text;

namespace Sol.Rpg
{
    public static class GameplayTagUtility
    {
        public static string NormalizePathOrEmpty(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
                return string.Empty;

            string[] segments = rawPath.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries);
            StringBuilder builder = new();
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = NormalizeSegment(segments[i]);
                if (string.IsNullOrEmpty(segment))
                    continue;

                if (builder.Length > 0)
                    builder.Append('.');
                builder.Append(segment);
            }

            return builder.ToString();
        }

        public static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string normalized = NormalizePathOrEmpty(path);
            if (!string.Equals(normalized, path.Trim(), StringComparison.Ordinal))
                return false;

            string[] segments = normalized.Split('.');
            if (segments.Length == 0)
                return false;

            for (int i = 0; i < segments.Length; i++)
            {
                if (!IsValidSegment(segments[i]))
                    return false;
            }

            return true;
        }

        public static bool IsChildOfOrEqual(string candidatePath, string parentPath)
        {
            string candidate = NormalizePathOrEmpty(candidatePath);
            string parent = NormalizePathOrEmpty(parentPath);
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(parent))
                return false;

            return string.Equals(candidate, parent, StringComparison.OrdinalIgnoreCase)
                || candidate.StartsWith(parent + ".", StringComparison.OrdinalIgnoreCase);
        }

        public static IEnumerable<string> EnumerateParentPaths(string tagPath)
        {
            string normalized = NormalizePathOrEmpty(tagPath);
            if (string.IsNullOrEmpty(normalized))
                yield break;

            int dot = normalized.LastIndexOf('.');
            while (dot > 0)
            {
                string parent = normalized.Substring(0, dot);
                yield return parent;
                dot = parent.LastIndexOf('.');
            }
        }

        private static string NormalizeSegment(string rawSegment)
        {
            if (string.IsNullOrWhiteSpace(rawSegment))
                return string.Empty;

            string trimmed = rawSegment.Trim();
            StringBuilder builder = new(trimmed.Length);
            bool capitalizeNext = true;
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
                    capitalizeNext = false;
                }
                else if (c == '_')
                {
                    builder.Append(c);
                    capitalizeNext = true;
                }
                else if (c == '-' || char.IsWhiteSpace(c))
                {
                    capitalizeNext = true;
                }
            }

            return builder.ToString();
        }

        private static bool IsValidSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
                return false;

            for (int i = 0; i < segment.Length; i++)
            {
                char c = segment[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }

            return true;
        }
    }
}
