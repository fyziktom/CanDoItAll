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

        public static IEnumerable<string> EnumerateSearchFiles(
            string rootPath,
            HashSet<string>? extensions,
            HashSet<string>? excludedPaths,
            Func<string, IReadOnlyList<string>>? enumerateDirectoryEntries = null)
        {
            if (File.Exists(rootPath))
            {
                if (IsRegularFile(rootPath) &&
                    !IsExcludedSearchPath(rootPath, rootPath, excludedPaths))
                {
                    yield return rootPath;
                }

                yield break;
            }

            if (!Directory.Exists(rootPath))
            {
                yield break;
            }

            var enumerate = enumerateDirectoryEntries ?? EnumerateDirectoryEntries;
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(rootPath);
            while (pendingDirectories.TryPop(out var directory))
            {
                IReadOnlyList<string> entries;
                try
                {
                    entries = enumerate(directory);
                }
                catch (Exception exception) when (
                    exception is UnauthorizedAccessException or IOException)
                {
                    continue;
                }

                foreach (var entry in entries.Order(StringComparer.OrdinalIgnoreCase))
                {
                    FileAttributes attributes;
                    try
                    {
                        attributes = File.GetAttributes(entry);
                    }
                    catch (Exception exception) when (
                        exception is UnauthorizedAccessException or IOException)
                    {
                        continue;
                    }

                    if (attributes.HasFlag(FileAttributes.ReparsePoint) ||
                        IsExcludedSearchPath(rootPath, entry, excludedPaths))
                    {
                        continue;
                    }

                    if (attributes.HasFlag(FileAttributes.Directory))
                    {
                        if (!IsBuildNoiseDirectory(entry))
                        {
                            pendingDirectories.Push(entry);
                        }

                        continue;
                    }

                    if (extensions is not null &&
                        extensions.Count > 0 &&
                        !extensions.Contains(Path.GetExtension(entry)))
                    {
                        continue;
                    }

                    yield return entry;
                }
            }
        }

        private static IReadOnlyList<string> EnumerateDirectoryEntries(string directory)
            => Directory.EnumerateFileSystemEntries(
                    directory,
                    "*",
                    new EnumerationOptions
                    {
                        RecurseSubdirectories = false,
                        IgnoreInaccessible = false,
                        AttributesToSkip = FileAttributes.ReparsePoint
                    })
                .ToArray();

        private static bool IsRegularFile(string path)
        {
            try
            {
                var attributes = File.GetAttributes(path);
                return !attributes.HasFlag(FileAttributes.Directory) &&
                       !attributes.HasFlag(FileAttributes.ReparsePoint);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException or IOException)
            {
                return false;
            }
        }

        private static bool IsBuildNoiseDirectory(string path)
        {
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            return name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals(".git", StringComparison.OrdinalIgnoreCase);
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
