using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactProviderNativeVisualValidationRules
{
    private const string BrowserScreenshotToolName = "browser_take_screenshot";

    public static int ScoreProviderNativeVisualArtifactExpectation(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        ProcessAutomationExecutionArtifact artifact,
        string relativePath,
        string displayName)
    {
        if (!IsProviderNativeBrowserOutputArtifact(artifact) ||
            !IsImageArtifact(artifact) ||
            expectedArtifact.ArtifactKind != ProcessArtifactKind.Evidence)
        {
            return 0;
        }

        var expectedText = CollapsePromptWhitespace(
            $"{expectedArtifact.Title} {expectedArtifact.ValidationRequirementSummary}");
        if (!ContainsVisualArtifactSignal(expectedText))
        {
            return 0;
        }

        var expectedTokens = ProcessArtifactTextMatchRules.TokenizeVisualArtifactMatchText(expectedText);
        var observedTokens = ProcessArtifactTextMatchRules.TokenizeVisualArtifactMatchText($"{relativePath} {displayName}")
            .ToHashSet(StringComparer.Ordinal);
        var matchedTokenCount = expectedTokens.Count(observedTokens.Contains);
        var score = 10 + matchedTokenCount * 10;
        if (ContainsScreenshotArtifactSignal(expectedText))
        {
            score += 8;
        }

        if (string.Equals(NormalizeToolToken(artifact.ProducedBy), BrowserScreenshotToolName, StringComparison.Ordinal))
        {
            score += 8;
        }

        return score;
    }

    public static bool IsProviderNativeBrowserOutputArtifact(ProcessAutomationExecutionArtifact artifact)
    {
        var producedBy = NormalizeToolToken(artifact.ProducedBy);
        if (producedBy.StartsWith("browser_", StringComparison.Ordinal))
        {
            return true;
        }

        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(artifact.RelativePath);
        return IsProviderNativeBrowserEvidenceReferencePath(normalizedPath);
    }

    public static string ResolveProviderNativeBrowserToolName(string expectedRelativePath)
    {
        var extension = Path.GetExtension(expectedRelativePath).ToLowerInvariant();
        if (string.Equals(extension, ".md", StringComparison.Ordinal))
        {
            var fileName = Path.GetFileName(expectedRelativePath);
            return fileName.Contains("browser", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Contains("snapshot", StringComparison.OrdinalIgnoreCase)
                ? "browser_snapshot"
                : string.Empty;
        }

        return extension switch
        {
            ".png" => BrowserScreenshotToolName,
            ".yml" or ".yaml" => "browser_snapshot",
            ".log" or ".txt" => "browser_console_messages",
            ".json" => "browser_evaluate",
            _ => string.Empty
        };
    }

    public static bool IsProviderNativeBrowserEvidenceReferencePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
        if (normalizedPath.StartsWith(".playwright-mcp/", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveProviderNativeBrowserToolName(normalizedPath).Length > 0;
        }

        var comparablePath = ProcessArtifactPathValidationRules.NormalizeManagedRelativePathForComparison(normalizedPath);
        return comparablePath.StartsWith("artifacts/process-runs/", StringComparison.OrdinalIgnoreCase) &&
               IsManagedBrowserEvidenceReferencePath(comparablePath) &&
               ResolveProviderNativeBrowserToolName(comparablePath).Length > 0;
    }

    public static bool IsManagedBrowserEvidenceReferencePath(string comparablePath)
    {
        var segments = comparablePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length >= 5 &&
               string.Equals(segments[0], "artifacts", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[1], "process-runs", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[3], "browser", StringComparison.OrdinalIgnoreCase);
    }

    public static bool MatchesExpectedBrowserOutputFile(string expectedRelativePath, string outputFileName)
    {
        var normalizedExpectedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(expectedRelativePath);
        var normalizedOutputPath = WorkspaceScopeDescriptor.NormalizeRelativePath(outputFileName);
        if (string.Equals(normalizedExpectedPath, normalizedOutputPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var expectedFileName = Path.GetFileName(normalizedExpectedPath);
        var outputFileNameOnly = Path.GetFileName(normalizedOutputPath);
        if (!string.Equals(expectedFileName, outputFileNameOnly, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expectedDirectoryName = Path.GetFileName(Path.GetDirectoryName(normalizedExpectedPath) ?? string.Empty);
        var outputDirectoryName = Path.GetFileName(Path.GetDirectoryName(normalizedOutputPath) ?? string.Empty);
        return string.Equals(expectedDirectoryName, outputDirectoryName, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsProviderNativeBrowserArtifactPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
        var comparablePath = ProcessArtifactPathValidationRules.NormalizeManagedRelativePathForComparison(normalizedPath);
        if (comparablePath.StartsWith("artifacts/process-runs/", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveProviderNativeBrowserToolName(comparablePath).Length > 0;
        }

        return normalizedPath.StartsWith(".playwright-mcp/", StringComparison.OrdinalIgnoreCase) &&
               ResolveProviderNativeBrowserToolName(normalizedPath).Length > 0;
    }

    public static bool ContainsVisualArtifactSignal(string text)
    {
        var normalizedText = text.ToLowerInvariant();
        return ContainsScreenshotArtifactSignal(normalizedText) ||
               normalizedText.Contains("image", StringComparison.Ordinal) ||
               normalizedText.Contains("visual", StringComparison.Ordinal) ||
               normalizedText.Contains("render", StringComparison.Ordinal) ||
               normalizedText.Contains("layout", StringComparison.Ordinal);
    }

    public static bool ContainsScreenshotArtifactSignal(string text)
    {
        var normalizedText = text.ToLowerInvariant();
        return normalizedText.Contains("screenshot", StringComparison.Ordinal) ||
               normalizedText.Contains("screen shot", StringComparison.Ordinal);
    }

    private static bool IsImageArtifact(ProcessAutomationExecutionArtifact artifact)
    {
        var extension = Path.GetExtension(artifact.RelativePath);
        return artifact.ContentType.Contains("image", StringComparison.OrdinalIgnoreCase) ||
               IsImageExtension(extension);
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

    private static string NormalizeToolToken(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('-', '_').Trim().ToLowerInvariant();
    }

    private static string CollapsePromptWhitespace(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
