using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class PluginWorkspaceFileLimits
{
    public const int MaxListResults = 250;
    public const int MaxSearchResults = 100;
    public const int MaxReadCharacters = 100_000;
    public const int MaxDiffLines = 500;
    public const int MaxTextWriteCharacters = 500_000;
}

public interface IPluginWorkspaceFiles
{
    WorkspaceFileListResult ListFiles(string? relativePath = null, string searchPattern = "*", int maxResults = 100);

    WorkspaceTextSearchResult SearchText(string query, string? relativePath = null, int maxResults = 20);

    WorkspaceTextFileReadResult ReadTextFile(string path, int maxCharacters = 12000);

    WorkspacePathStatResult StatPath(string path);

    WorkspaceFileMutationResult CreateDirectory(string path);

    WorkspaceFileMutationResult WriteTextFile(string path, string content, bool overwrite = true);

    WorkspaceFileMutationResult AppendTextFile(string path, string content);

    WorkspaceFileMutationResult CopyPath(string sourcePath, string destinationPath, bool overwrite = false);

    WorkspaceFileMutationResult MovePath(string sourcePath, string destinationPath, bool overwrite = false);

    WorkspaceFileMutationResult DeletePath(string path, bool recursive = false);

    WorkspaceTextDiffResult DiffTextFiles(string leftPath, string rightPath, int maxLines = 160);
}

public sealed class PluginWorkspaceFiles(IWorkspaceFileService files) : IPluginWorkspaceFiles
{
    public WorkspaceFileListResult ListFiles(string? relativePath = null, string searchPattern = "*", int maxResults = 100)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchPattern);
        return files.ListFiles(
            RequireOptionalPath(relativePath),
            searchPattern.Trim(),
            RequireWithinLimit(maxResults, PluginWorkspaceFileLimits.MaxListResults, nameof(maxResults)));
    }

    public WorkspaceTextSearchResult SearchText(string query, string? relativePath = null, int maxResults = 20)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        return files.SearchText(
            query,
            RequireOptionalPath(relativePath),
            RequireWithinLimit(maxResults, PluginWorkspaceFileLimits.MaxSearchResults, nameof(maxResults)));
    }

    public WorkspaceTextFileReadResult ReadTextFile(string path, int maxCharacters = 12000)
        => files.ReadTextFile(
            RequirePath(path),
            RequireWithinLimit(maxCharacters, PluginWorkspaceFileLimits.MaxReadCharacters, nameof(maxCharacters)));

    public WorkspacePathStatResult StatPath(string path)
        => files.StatPath(RequirePath(path));

    public WorkspaceFileMutationResult CreateDirectory(string path)
        => files.CreateDirectory(RequirePath(path));

    public WorkspaceFileMutationResult WriteTextFile(string path, string content, bool overwrite = true)
    {
        EnsureContentWithinLimit(content, nameof(content));
        return files.WriteTextFile(RequirePath(path), content, overwrite);
    }

    public WorkspaceFileMutationResult AppendTextFile(string path, string content)
    {
        EnsureContentWithinLimit(content, nameof(content));
        return files.AppendTextFile(RequirePath(path), content);
    }

    public WorkspaceFileMutationResult CopyPath(string sourcePath, string destinationPath, bool overwrite = false)
        => files.CopyPath(RequirePath(sourcePath), RequirePath(destinationPath), overwrite);

    public WorkspaceFileMutationResult MovePath(string sourcePath, string destinationPath, bool overwrite = false)
        => files.MovePath(RequirePath(sourcePath), RequirePath(destinationPath), overwrite);

    public WorkspaceFileMutationResult DeletePath(string path, bool recursive = false)
        => files.DeletePath(RequirePath(path), recursive);

    public WorkspaceTextDiffResult DiffTextFiles(string leftPath, string rightPath, int maxLines = 160)
        => files.DiffTextFiles(
            RequirePath(leftPath),
            RequirePath(rightPath),
            RequireWithinLimit(maxLines, PluginWorkspaceFileLimits.MaxDiffLines, nameof(maxLines)));

    private static string RequirePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = path.Trim();
        if (Path.IsPathRooted(normalized))
        {
            throw new InvalidOperationException("Plugin workspace file paths must be workspace-relative paths.");
        }

        return normalized;
    }

    private static string? RequireOptionalPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return RequirePath(path);
    }

    private static int RequireWithinLimit(int value, int maxValue, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be greater than zero.");
        }

        if (value > maxValue)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be less than or equal to {maxValue}.");
        }

        return value;
    }

    private static void EnsureContentWithinLimit(string content, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length > PluginWorkspaceFileLimits.MaxTextWriteCharacters)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                content.Length,
                $"Text writes are limited to {PluginWorkspaceFileLimits.MaxTextWriteCharacters} characters.");
        }
    }
}
