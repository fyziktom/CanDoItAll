using CanDoItAll.Conversations.Components.Presentation;

namespace CanDoItAll.Conversations.Components;

public static class PresentationToneResolver
{
    public static string Resolve(PresentationTone tone)
    {
        return tone switch
        {
            PresentationTone.Accent => "primary",
            PresentationTone.Info => "info",
            PresentationTone.Success => "success",
            PresentationTone.Warning => "warning",
            PresentationTone.Danger => "danger",
            PresentationTone.Promo => "primary",
            PresentationTone.Rank => "info",
            _ => "neutral"
        };
    }
}
