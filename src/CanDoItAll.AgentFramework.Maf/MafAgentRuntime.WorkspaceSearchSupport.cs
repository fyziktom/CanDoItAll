using System.Text;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private static class WorkspaceSearchSupport
    {
        public static IReadOnlyList<string> TokenizeQuery(string query)
        {
            return query
                .Split([' ', '\r', '\n', '\t', ',', '.', ';', ':', '(', ')', '[', ']', '{', '}', '/', '\\', '"', '\''], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => item.Length >= 3)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
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
}
