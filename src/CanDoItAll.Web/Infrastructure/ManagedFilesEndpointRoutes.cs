using CanDoItAll.Infrastructure.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;

namespace CanDoItAll.Web.Infrastructure;

public static class ManagedFilesEndpointRoutes
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    public static IEndpointRouteBuilder MapCanDoItAllManagedFiles(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/storage/objects/preview", HandleStorageObjectPreviewRequest);
        endpoints.MapGet("/storage/objects/download", HandleStorageObjectDownloadRequest);
        endpoints.MapGet("/managed-files/{**path}", HandleManagedFileRequest);

        return endpoints;
    }

    private static Task<IResult> HandleStorageObjectPreviewRequest(
        HttpContext httpContext,
        IStorageAccessService accessService,
        IStorageCatalogService catalogService,
        IStorageDriverRegistry driverRegistry)
    {
        return HandleStorageObjectRequestAsync(
            httpContext,
            requireInlinePreview: true,
            accessService,
            catalogService,
            driverRegistry);
    }

    private static Task<IResult> HandleStorageObjectDownloadRequest(
        HttpContext httpContext,
        IStorageAccessService accessService,
        IStorageCatalogService catalogService,
        IStorageDriverRegistry driverRegistry)
    {
        return HandleStorageObjectRequestAsync(
            httpContext,
            requireInlinePreview: false,
            accessService,
            catalogService,
            driverRegistry);
    }

    private static async Task<IResult> HandleStorageObjectRequestAsync(
        HttpContext httpContext,
        bool requireInlinePreview,
        IStorageAccessService accessService,
        IStorageCatalogService catalogService,
        IStorageDriverRegistry driverRegistry)
    {
        var token = httpContext.Request.Query["ref"].ToString();
        if (!StorageJson.TryDecodeReferenceToken(token, out var reference) || reference is null)
        {
            return TypedResults.BadRequest("The storage reference is invalid.");
        }

        var descriptor = await accessService.DescribeAsync(reference, httpContext.RequestAborted);
        if (requireInlinePreview && !descriptor.SupportsInlinePreview)
        {
            return TypedResults.BadRequest(string.IsNullOrWhiteSpace(descriptor.ReasonWhenUnavailable)
                ? "Inline preview is not available for this storage object."
                : descriptor.ReasonWhenUnavailable);
        }

        if (!requireInlinePreview && !descriptor.SupportsDownload)
        {
            return TypedResults.BadRequest(string.IsNullOrWhiteSpace(descriptor.ReasonWhenUnavailable)
                ? "Download is not available for this storage object."
                : descriptor.ReasonWhenUnavailable);
        }

        var storageResolution = await ResolveStorageAsync(reference, catalogService, httpContext.RequestAborted);
        if (storageResolution.Error is not null)
        {
            return storageResolution.Error;
        }

        var storage = storageResolution.Storage!;
        var driver = driverRegistry.Resolve(storage.ProviderKind);
        var stream = await driver.OpenReadAsync(storage, reference, httpContext.RequestAborted);

        return TypedResults.File(
            stream,
            descriptor.ContentType,
            requireInlinePreview ? null : descriptor.DisplayFileName,
            enableRangeProcessing: true);
    }

    private static IResult HandleManagedFileRequest(
        HttpContext httpContext,
        string? path,
        IWorkspacePathAccessGuard pathAccessGuard)
    {
        if (ContainsTraversalSegments(httpContext.Request.Path.Value))
        {
            return TypedResults.BadRequest("The resolved path is outside the active managed files root.");
        }

        var resolution = pathAccessGuard.ResolveManagedFilePath(path ?? string.Empty);
        if (!resolution.IsSuccess)
        {
            return TypedResults.BadRequest(resolution.Message);
        }

        if (!File.Exists(resolution.FullPath))
        {
            return TypedResults.NotFound();
        }

        var contentType = ContentTypeProvider.TryGetContentType(resolution.FullPath, out var resolvedContentType)
            ? resolvedContentType
            : "application/octet-stream";

        return TypedResults.PhysicalFile(resolution.FullPath, contentType, enableRangeProcessing: true);
    }

    private static bool ContainsTraversalSegments(string? requestPath)
    {
        if (string.IsNullOrWhiteSpace(requestPath))
        {
            return false;
        }

        var unescapedPath = Uri.UnescapeDataString(requestPath)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var segments = unescapedPath.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return segments.Any(segment => segment is "." or "..");
    }

    private static async Task<(StorageCatalogRecord? Storage, IResult? Error)> ResolveStorageAsync(
        StorageObjectReference reference,
        IStorageCatalogService catalogService,
        CancellationToken cancellationToken)
    {
        if (reference.StorageId.HasValue)
        {
            var storage = await catalogService.GetAsync(reference.StorageId.Value, cancellationToken);
            return storage is null
                ? (null, TypedResults.NotFound())
                : (storage, null);
        }

        return reference.ProviderKind switch
        {
            StorageProviderKind.FileSystem => (await catalogService.EnsureBootstrapFileSystemStorageAsync(cancellationToken), null),
            StorageProviderKind.Ipfs when !string.IsNullOrWhiteSpace(reference.Route) &&
                                         Uri.TryCreate(reference.Route, UriKind.Absolute, out var routeUri)
                => (CreateAdHocStorageRecord(StorageProviderKind.Ipfs, routeUri.GetLeftPart(UriPartial.Authority)), null),
            StorageProviderKind.Ipfs => (null, TypedResults.BadRequest("IPFS storage references require a catalog record or an absolute route.")),
            StorageProviderKind.Ftp => (null, TypedResults.BadRequest("FTP storage references require a catalog record.")),
            _ => (null, TypedResults.BadRequest("The storage provider is not supported."))
        };
    }

    private static StorageCatalogRecord CreateAdHocStorageRecord(StorageProviderKind providerKind, string endpointOrRoot)
    {
        return new StorageCatalogRecord
        {
            Id = Guid.Empty,
            Name = $"{providerKind} ad hoc route",
            ProviderKind = providerKind,
            ConnectionMode = StorageConnectionMode.Remote,
            EndpointOrRoot = endpointOrRoot,
            CapabilityMask = providerKind switch
            {
                StorageProviderKind.Ipfs => StorageCapability.Read |
                                            StorageCapability.Download |
                                            StorageCapability.InlinePreview |
                                            StorageCapability.DirectUrl,
                _ => StorageCapability.Read
            },
            HealthStatus = StorageHealthStatus.Healthy,
            IsEnabled = true
        };
    }
}
