using System.Text;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.AgentFramework.Maf;

internal static class WorkspaceSearchSupport
{
        private static readonly HashSet<string> RagStopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "about",
            "above",
            "after",
            "again",
            "against",
            "also",
            "and",
            "answer",
            "are",
            "before",
            "being",
            "below",
            "between",
            "call",
            "can",
            "could",
            "exactly",
            "else",
            "for",
            "from",
            "give",
            "have",
            "into",
            "just",
            "more",
            "not",
            "nothing",
            "only",
            "please",
            "reply",
            "same",
            "should",
            "show",
            "some",
            "than",
            "that",
            "the",
            "their",
            "them",
            "then",
            "there",
            "these",
            "thing",
            "this",
            "those",
            "tool",
            "tools",
            "what",
            "when",
            "where",
            "which",
            "will",
            "with",
            "would",
            "you",
            "your"
        };

        public static IReadOnlyList<string> TokenizeQuery(string query)
        {
            return query
                .Split([' ', '\r', '\n', '\t', ',', '.', ';', ':', '(', ')', '[', ']', '{', '}', '/', '\\', '"', '\''], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => item.Length >= 3)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static IReadOnlyList<string> TokenizeRagQuery(string query)
        {
            return TokenizeQuery(ExtractUserRequestForRag(query))
                .Where(IsRagSignalTerm)
                .ToList();
        }

        public static string ExtractUserRequestForRag(string query)
        {
            const string marker = "User request:";

            var markerIndex = query.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return query;
            }

            var userRequest = query[(markerIndex + marker.Length)..].Trim();
            return string.IsNullOrWhiteSpace(userRequest) ? query : userRequest;
        }

        public static bool HasEnoughRagSignal(IReadOnlyCollection<string> terms, int minimumTermCount)
        {
            return terms.Count >= Math.Max(1, minimumTermCount);
        }

        public static IEnumerable<string> EnumerateSearchFiles(string rootPath, HashSet<string>? extensions, HashSet<string>? excludedPaths)
        {
            if (File.Exists(rootPath))
            {
                if (!IsExcludedSearchPath(rootPath, rootPath, excludedPaths))
                {
                    yield return rootPath;
                }

                yield break;
            }

            foreach (var file in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
            {
                if (extensions is not null && extensions.Count > 0 && !extensions.Contains(Path.GetExtension(file)))
                {
                    continue;
                }

                if (file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    file.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (IsExcludedSearchPath(rootPath, file, excludedPaths))
                {
                    continue;
                }

                yield return file;
            }
        }

        public static bool IsExcludedSearchPath(string rootPath, string filePath, HashSet<string>? excludedPaths)
        {
            var comparisonRoot = File.Exists(rootPath)
                ? Path.GetDirectoryName(rootPath) ?? Path.GetPathRoot(rootPath) ?? rootPath
                : rootPath;
            return WorkspaceRetrievalNoisePolicy.ShouldExcludeFromAmbientRetrieval(comparisonRoot, filePath, excludedPaths);
        }

        public static string NormalizeSearchPath(string path)
        {
            return WorkspaceRetrievalNoisePolicy.NormalizeRelativePath(path);
        }

        private static bool IsRagSignalTerm(string term)
        {
            return !RagStopWords.Contains(term);
        }

        public static int CountOccurrences(string text, string term)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                index += term.Length;
            }

            return count;
        }

        public static int CountWholeTermOccurrences(string text, string term)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                if (IsTermBoundary(text, index - 1) &&
                    IsTermBoundary(text, index + term.Length))
                {
                    count++;
                }

                index += term.Length;
            }

            return count;
        }

        private static bool IsTermBoundary(string text, int index)
        {
            return index < 0 ||
                   index >= text.Length ||
                   !char.IsLetterOrDigit(text[index]);
        }

        public static string BuildSearchSnippet(string text, IReadOnlyList<string> terms)
        {
            foreach (var term in terms)
            {
                var index = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    continue;
                }

                var start = Math.Max(0, index - 140);
                var length = Math.Min(text.Length - start, 320);
                return text.Substring(start, length).ReplaceLineEndings(" ").Trim();
            }

            return text.Length <= 320
                ? text.ReplaceLineEndings(" ").Trim()
                : text[..320].ReplaceLineEndings(" ").Trim();
        }
}
