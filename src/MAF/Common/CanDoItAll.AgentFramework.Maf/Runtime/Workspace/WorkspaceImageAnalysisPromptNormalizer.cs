using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.AgentFramework.Maf;

internal static class WorkspaceImageAnalysisPromptNormalizer
{
    public static string NormalizeSingleImagePrompt(string prompt) =>
        AgentImageAnalysisPromptPolicy.NormalizeSingleImagePrompt(prompt);

    public static string NormalizeImageSetPrompt(
        string prompt,
        int imageCount,
        string deterministicEvidence) =>
        AgentImageAnalysisPromptPolicy.NormalizeImageSetPrompt(prompt, imageCount, deterministicEvidence);
}
