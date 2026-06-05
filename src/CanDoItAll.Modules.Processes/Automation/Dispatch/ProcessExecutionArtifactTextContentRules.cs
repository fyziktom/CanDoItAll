using CanDoItAll.AgentFramework.Models;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessExecutionArtifactTextContentRules
{
    internal static bool CanMatchArtifactByTextContent(ProcessAutomationExecutionArtifact artifact)
    {
        if (string.IsNullOrWhiteSpace(artifact.RelativePath))
        {
            return false;
        }

        return artifact.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
               artifact.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
               artifact.ContentType.Contains("xml", StringComparison.OrdinalIgnoreCase) ||
               artifact.ContentType.Contains("yaml", StringComparison.OrdinalIgnoreCase) ||
               ProcessManagedArtifactPathClassificationRules.IsTextReadableManagedArtifactPath(artifact.RelativePath);
    }

    internal static string? TryDecodeTextArtifactContent(
        ProcessAutomationExecutionArtifact artifact,
        string fullPath,
        byte[] content)
    {
        const int maxTextArtifactBytes = 512 * 1024;

        if (!CanMatchArtifactByTextContent(artifact) ||
            content.Length == 0 ||
            content.Length > maxTextArtifactBytes ||
            IsImageExtension(Path.GetExtension(fullPath)))
        {
            return null;
        }

        try
        {
            var text = Encoding.UTF8.GetString(content);
            return text.Contains('\0', StringComparison.Ordinal)
                ? null
                : text;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static bool IsImageExtension(string extension)
    {
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".svg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }
}
