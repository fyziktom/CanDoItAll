using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Memory.Abstractions;
using MafMemorySourceKind = CanDoItAll.AgentFramework.Core.MemorySourceKind;
using MafMemorySourceProvenance = CanDoItAll.AgentFramework.Core.MemorySourceProvenance;
using MafMemorySourceSnapshot = CanDoItAll.AgentFramework.Core.MemorySourceSnapshot;
using MemorySourceSnapshotId = CanDoItAll.AgentFramework.Core.MemorySourceSnapshotId;

namespace CanDoItAll.Memory.Application;

public sealed class ManualMemorySourceGatewayAdapter(
    IManualSourceSnapshotProvider sourceSnapshotProvider) : IMemorySourceGatewayAdapter
{
    public MemorySourceGatewayAdapterDescriptor Descriptor { get; } = new(
        MemorySourceModuleId.Parse("memory.manual-input"),
        MafMemorySourceKind.ManualInput,
        MemorySourceSnapshotProviderVersions.ManualInput,
        MemorySourceScope.Manual,
        RequiresPermissionCheck: true);

    public async Task<MafMemorySourceSnapshot> ReadSnapshotAsync(
        MemorySourceGatewayRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SourceKind != Descriptor.SourceKind)
        {
            throw new InvalidOperationException(
                $"Manual source adapter cannot read source kind '{request.SourceKind}'.");
        }

        if (request.RequestedScope != Descriptor.RequiredScope)
        {
            throw new InvalidOperationException(
                $"Manual source adapter requires scope '{Descriptor.RequiredScope}' but received '{request.RequestedScope}'.");
        }

        var manualRequest = ManualSourceSnapshotRequestFactory.Create(request);
        return await sourceSnapshotProvider.ReadSnapshotAsync(manualRequest, cancellationToken);
    }
}

public sealed class ManualMemorySourceSnapshotProvider : IManualSourceSnapshotProvider
{
    public Task<MafMemorySourceSnapshot> ReadSnapshotAsync(
        ManualSourceSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var item = CreateItem(request);
        var page = MemorySourceSnapshotPage.Apply(
            [item],
            request.Cursor,
            request.Take,
            MafMemorySourceKind.ManualInput,
            request.SourceId,
            MemorySourceSnapshotProviderVersions.ManualInput,
            out var nextCursor,
            out var hasMore);
        var snapshotHash = MemorySourceSnapshotHasher.Compute(page.Select(pageItem => pageItem.ContentHash).ToArray());
        return Task.FromResult(new MafMemorySourceSnapshot(
            new MemorySourceSnapshotManifest(
                MemorySourceSnapshotId.Create(MafMemorySourceKind.ManualInput, request.SourceId, snapshotHash),
                MafMemorySourceKind.ManualInput,
                request.SourceId,
                DateTimeOffset.UtcNow,
                TotalItemCount: 1,
                nextCursor,
                hasMore,
                hasMore ? MemorySourceSnapshotPageStatus.PageReturned : MemorySourceSnapshotPageStatus.EndOfSource,
                MemorySourceSnapshotHashScope.FullSnapshot,
                MemorySourceSnapshotProviderVersions.ManualInput),
            page));
    }

    private static MemorySourceItem CreateItem(ManualSourceSnapshotRequest request)
    {
        var payloadKind = ParsePayloadKind(request.PayloadKind);
        var itemId = MemorySourceItemId.Create(
            MafMemorySourceKind.ManualInput,
            request.SourceId,
            ResolveEntityKind(payloadKind),
            request.SourceId.ToString("D"));
        var title = NormalizeTitle(request.Title, payloadKind, request.Locator);
        var content = BuildContent(request, payloadKind);
        var contentHash = MemorySourceSnapshotHasher.Compute(
            request.SourceId.ToString("D"),
            payloadKind.ToString(),
            request.Title,
            request.ContentText,
            request.Locator,
            request.ContentType,
            request.SourceCategory,
            string.Join("|", request.Tags));
        return new MemorySourceItem(
            itemId,
            MafMemorySourceKind.ManualInput,
            ResolveEntityKind(payloadKind),
            title,
            content,
            contentHash,
            CreatedAtUtc: null,
            DateTimeOffset.UtcNow,
            new MafMemorySourceProvenance(
                MafMemorySourceKind.ManualInput,
                request.SourceId,
                ResolveEntityKind(payloadKind),
                request.SourceId.ToString("D"),
                $"/memory/manual/{request.SourceId:D}"),
            MemorySourceSnapshotSecurity.CreatePermission(
                containsSensitivePayload: false,
                "Manual source policy rejects credential-shaped text and sensitive URL query parameters before snapshot capture.",
                "User-supplied source evidence for selected memory provider ingestion."),
            Layout: null,
            Links: [],
            References: BuildReferences(request),
            StorageReference: BuildStorageReference(request, payloadKind),
            Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["payloadKind"] = payloadKind.ToString(),
                ["sourceCategory"] = request.SourceCategory,
                ["tags"] = string.Join(",", request.Tags)
            })
        {
            HashPolicy = MemorySourceHashPolicy.PublicRedactedContent
        };
    }

    private static ManualMemorySourcePayloadKind ParsePayloadKind(string value)
    {
        return Enum.TryParse<ManualMemorySourcePayloadKind>(value, ignoreCase: true, out var payloadKind)
            ? payloadKind
            : throw new InvalidOperationException($"Manual source payload kind '{value}' is not supported.");
    }

    private static MemorySourceEntityKind ResolveEntityKind(ManualMemorySourcePayloadKind payloadKind)
        => payloadKind switch
        {
            ManualMemorySourcePayloadKind.Text => MemorySourceEntityKind.ManualText,
            ManualMemorySourcePayloadKind.FileReference => MemorySourceEntityKind.ManualFileReference,
            ManualMemorySourcePayloadKind.LinkReference => MemorySourceEntityKind.ManualLinkReference,
            _ => throw new InvalidOperationException($"Manual source payload kind '{payloadKind}' is not supported.")
        };

    private static string NormalizeTitle(
        string title,
        ManualMemorySourcePayloadKind payloadKind,
        string locator)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        return payloadKind switch
        {
            ManualMemorySourcePayloadKind.FileReference or ManualMemorySourcePayloadKind.LinkReference =>
                ManualMemorySourceSafetyPolicy.SafeLocatorForTitle(locator),
            _ => "Manual memory source"
        };
    }

    private static string BuildContent(
        ManualSourceSnapshotRequest request,
        ManualMemorySourcePayloadKind payloadKind)
    {
        return payloadKind switch
        {
            ManualMemorySourcePayloadKind.Text => ManualMemorySourceSafetyPolicy.RedactText(request.ContentText),
            ManualMemorySourcePayloadKind.FileReference => string.Join(
                Environment.NewLine,
                $"File reference: {ManualMemorySourceSafetyPolicy.SafeLocatorForTitle(request.Locator)}",
                $"Content type: {request.ContentType}"),
            ManualMemorySourcePayloadKind.LinkReference => $"Link reference: {ManualMemorySourceSafetyPolicy.EnsureUriAllowed(request.Locator)}",
            _ => throw new InvalidOperationException($"Manual source payload kind '{payloadKind}' is not supported.")
        };
    }

    private static IReadOnlyList<MemorySourceReference> BuildReferences(ManualSourceSnapshotRequest request)
        => request.Tags
            .Select((tag, index) => new MemorySourceReference("manual-tag", tag, index))
            .ToArray();

    private static MemorySourceStorageReference? BuildStorageReference(
        ManualSourceSnapshotRequest request,
        ManualMemorySourcePayloadKind payloadKind)
        => payloadKind switch
        {
            ManualMemorySourcePayloadKind.FileReference => new MemorySourceStorageReference(
                "manual-source",
                "file-path",
                request.Locator,
                request.ContentType,
                ManualMemorySourceSafetyPolicy.SafeLocatorForTitle(request.Locator)),
            ManualMemorySourcePayloadKind.LinkReference => new MemorySourceStorageReference(
                "manual-source",
                "url",
                ManualMemorySourceSafetyPolicy.EnsureUriAllowed(request.Locator).ToString(),
                request.ContentType,
                ManualMemorySourceSafetyPolicy.SafeLocatorForTitle(request.Locator)),
            _ => null
        };
}
