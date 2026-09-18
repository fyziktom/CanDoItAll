namespace CanDoItAll.CrmHr.UiSandbox;

public enum CrmHrAssetMode
{
    Parity,
    Fast
}

public static class CrmHrSandboxAssets
{
#if CRMHR_FAST_ASSETS
    public const CrmHrAssetMode Mode = CrmHrAssetMode.Fast;
    public const string ThemePath = "css/crmhr-fast.css";
#else
    public const CrmHrAssetMode Mode = CrmHrAssetMode.Parity;
    public const string ThemePath = "css/output.css";
#endif

    public static void ValidateRequestedMode(string? requested)
    {
        if (requested is null)
        {
            return;
        }

        if (!Enum.TryParse<CrmHrAssetMode>(requested, out var mode) || !Enum.IsDefined(mode) || mode != Mode)
        {
            throw new InvalidOperationException(
                "The requested asset mode does not match this build. Use --property:CrmHrAssetMode=Parity or --property:CrmHrAssetMode=Fast with the matching launch profile.");
        }
    }
}
