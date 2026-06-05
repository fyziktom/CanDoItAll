using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactKindClassificationRules
{
    internal static string GuessContentTypeFromPath(string fullPath)
    {
        return Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".svg" => "image/svg+xml",
            ".yml" or ".yaml" => "text/yaml",
            ".log" or ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }

    internal static ProcessArtifactKind ResolveProcessArtifactKind(
        ProcessAutomationExecutionArtifact artifact,
        ProcessArtifactKind? matchedExpectationKind)
    {
        if (matchedExpectationKind.HasValue)
        {
            return matchedExpectationKind.Value;
        }

        if (artifact.RelativePath.EndsWith("/response.md", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactKind.Transcript;
        }

        var relativePath = artifact.RelativePath.Replace('\\', '/');
        var fileName = Path.GetFileName(relativePath);
        var extension = Path.GetExtension(fileName);

        if (artifact.ContentType.Contains("image", StringComparison.OrdinalIgnoreCase) ||
            IsImageExtension(extension))
        {
            return ProcessArtifactKind.Evidence;
        }

        if (ContainsArtifactHint(fileName, "checklist"))
        {
            return ProcessArtifactKind.Checklist;
        }

        if (ContainsArtifactHint(fileName, "decision"))
        {
            return ProcessArtifactKind.Decision;
        }

        if (ContainsArtifactHint(fileName, "brief"))
        {
            return ProcessArtifactKind.Brief;
        }

        if (ContainsArtifactHint(fileName, "prompt"))
        {
            return ProcessArtifactKind.Prompt;
        }

        if (ContainsArtifactHint(fileName, "dataset"))
        {
            return ProcessArtifactKind.Dataset;
        }

        if (ContainsArtifactHint(fileName, "log") ||
            ContainsArtifactHint(fileName, "transcript") ||
            ContainsArtifactHint(fileName, "stdout") ||
            ContainsArtifactHint(fileName, "stderr") ||
            extension.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactKind.Transcript;
        }

        return string.Equals(artifact.ArtifactKind, "generated-output", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               ProcessConcreteProductPathRules.IsCodeOrProjectExtension(extension)
            ? ProcessArtifactKind.Deliverable
            : ProcessArtifactKind.Evidence;
    }

    private static bool ContainsArtifactHint(string fileName, string hint)
    {
        return fileName.Contains(hint, StringComparison.OrdinalIgnoreCase);
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
