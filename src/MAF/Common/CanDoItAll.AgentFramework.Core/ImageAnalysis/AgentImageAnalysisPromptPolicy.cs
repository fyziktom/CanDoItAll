namespace CanDoItAll.AgentFramework.Core;

public static class AgentImageAnalysisPromptPolicy
{
    public static string NormalizeSingleImagePrompt(string prompt)
    {
        var userPrompt = string.IsNullOrWhiteSpace(prompt)
            ? "Analyze the attached image. Describe only evidence that is directly visible in the image."
            : prompt.Trim();

        return
            "You receive one image file. Analyze only visible evidence in the image. " +
            "Separate direct observations from uncertainty and avoid inferring context that is not visible. " +
            "Do not describe provider, model, token, or cost metadata; the tool wrapper returns that separately." +
            Environment.NewLine +
            Environment.NewLine +
            $"User question: {userPrompt}";
    }

    public static string NormalizeImageSetPrompt(
        string prompt,
        int imageCount,
        string deterministicEvidence)
    {
        var normalizedCount = Math.Max(2, imageCount);
        var userPrompt = string.IsNullOrWhiteSpace(prompt)
            ? "Describe visible similarities and differences across the attached images using only directly observable evidence."
            : prompt.Trim();
        var evidenceSection = string.IsNullOrWhiteSpace(deterministicEvidence)
            ? string.Empty
            : Environment.NewLine +
              Environment.NewLine +
              deterministicEvidence.Trim();

        return
            $"You receive {normalizedCount:N0} ordered image files. " +
            "Analyze only visible evidence in the images, using attachment order as the sequence order. " +
            "When comparing images, describe directly observable similarities, differences, positions, labels, colors, and coordinates without assuming the domain or purpose of the images. " +
            "Separate observations from uncertainty, and state when the images do not provide enough evidence for the requested claim. " +
            "Do not describe provider, model, token, or cost metadata; the tool wrapper returns that separately." +
            evidenceSection +
            Environment.NewLine +
            Environment.NewLine +
            $"User question: {userPrompt}";
    }
}
