namespace CanDoItAll.Prompts.UiSandbox;

public enum PromptsAssetMode
{
    Parity,
    Fast
}

public static class PromptsSandboxAssets
{
#if PROMPTS_FAST_ASSETS
    public const PromptsAssetMode Mode = PromptsAssetMode.Fast;
    public const string ThemePath = "css/prompts-fast.css";
#else
    public const PromptsAssetMode Mode = PromptsAssetMode.Parity;
    public const string ThemePath = "css/output.css";
#endif

    public static void ValidateRequestedMode(string? requested)
    {
        if (requested is null)
        {
            return;
        }

        if (!Enum.TryParse<PromptsAssetMode>(requested, out var mode) || !Enum.IsDefined(mode) || mode != Mode)
        {
            throw new InvalidOperationException(
                "The requested asset mode does not match this build. Use --property:PromptsAssetMode=Parity or --property:PromptsAssetMode=Fast with the matching launch profile.");
        }
    }
}
