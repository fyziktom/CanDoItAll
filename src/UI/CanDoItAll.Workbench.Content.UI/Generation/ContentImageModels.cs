namespace CanDoItAll.Workbench.Content.UI.Generation;

public enum ContentImageSize { Auto, Square, Portrait, Landscape }
public enum ContentImageQuality { Auto, Low, Medium, High }
public enum ContentImageFormat { Png, Jpeg, Webp }
public sealed record ContentImageModel(string Id, string Label);
public sealed record ContentImageProvider(Guid Id, string Name, string DefaultModelLabel,
    IReadOnlyList<ContentImageModel> Models, bool AllowsCustomModel);
public sealed record ContentImageDraft(string Title, string Usage, string Prompt, Guid? ProviderId,
    string Model, ContentImageSize Size = ContentImageSize.Square, ContentImageQuality Quality = ContentImageQuality.Low,
    ContentImageFormat Format = ContentImageFormat.Png);
public enum ContentImagePhase { Ready, Preparing, PlaceholderSaved, Queued, ProviderCompleted, MediaSaved, Rejected, Unconfirmed }
public sealed record ContentImageReceipt(Guid OperationId, ContentImagePhase Phase, string Message,
    string? NodeId = null, Guid? RecordId = null, bool ProviderInvoked = false, bool ProviderCompleted = false, string? ContentSha256 = null);
public sealed record ContentImageSetup(Guid OpeningId, IReadOnlyList<ContentImageProvider> Providers,
    bool IsLoading, bool IsBusy, bool CanSubmit, string Message = "", ContentImageReceipt? Receipt = null);
