using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

/// <summary>
/// Keeps .NET build output of a read-only external target out of that target. restore, build, test,
/// run and publish then write <c>bin</c> and <c>obj</c> to <c>.build/&lt;key&gt;</c> in the workspace.
/// The key identifies the read-only root, so every command and every run against the same root
/// shares one persistent folder: a later <c>test --no-build</c> finds the earlier build. Nothing
/// deletes these folders automatically.
/// </summary>
public static class WorkspaceReadOnlyBuildOutput
{
    public const string RootFolderName = ".build";
    public const int KeyLength = 12;

    /// <summary>
    /// Returns the workspace-relative output folder when the project location is an external target
    /// the agent may read but not write, otherwise null. The project location is the target path, or
    /// the working directory when there is no target.
    /// </summary>
    public static string? ResolveRelativePath(
        EffectiveExternalTargetAccessScope access,
        string? targetPath,
        string? workingDirectory,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (!enabled)
        {
            return null;
        }

        var projectLocation = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
            string.IsNullOrWhiteSpace(targetPath) ? workingDirectory : targetPath);
        if (string.IsNullOrWhiteSpace(projectLocation) ||
            !access.CanRead(projectLocation) ||
            access.CanWrite(projectLocation))
        {
            return null;
        }

        var readOnlyRoot = access.ReadOnlyAliases
            .Select(AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias)
            .Where(root => !string.IsNullOrWhiteSpace(root) &&
                           ExternalTargetAliasCodec.IsAliasWithinRoot(projectLocation, root))
            .OrderByDescending(root => root!.Length)
            .FirstOrDefault();
        return readOnlyRoot is null
            ? null
            : $"{RootFolderName}/{ComputeKey(readOnlyRoot)}";
    }

    public static string DescribeRedirect(string relativePath)
        => $"Build output for this read-only target is kept in '{relativePath}'; the target folder is not modified.";

    /// <summary>
    /// The first hexadecimal characters of the SHA-256 of the normalized root alias. A versioned alias
    /// carries the registry's identity of the bound physical root, so the key follows the folder and
    /// not the wording of a path. Legacy drive aliases compare case-insensitively and are lowercased.
    /// </summary>
    internal static string ComputeKey(string rootAlias)
    {
        var identity = ExternalTargetAliasCodec.NormalizeVersionedAlias(rootAlias)
            ?? rootAlias.ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexStringLower(hash)[..KeyLength];
    }
}
