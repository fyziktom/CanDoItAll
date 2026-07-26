using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class AgentPackageImportApi
{
    private const long MaximumRequestBytes = AgentPackageReadOptions.DefaultMaximumPackageBytes + (1024 * 1024);

    public static RouteGroupBuilder MapAgentPackageImportApi(this RouteGroupBuilder agents)
    {
        agents.MapPost("/import-package", ImportAsync)
            .WithName("ImportAgentPackage")
            .Accepts<AgentPackageImportApiForm>("multipart/form-data")
            .Produces<AgentPackageImportReceipt>(StatusCodes.Status200OK)
            .Produces<AgentPackageImportReceipt>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status412PreconditionFailed)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden)
            .WithMetadata(
                new RequestSizeLimitAttribute(MaximumRequestBytes),
                new RequestFormLimitsAttribute
                {
                    MultipartBodyLengthLimit = MaximumRequestBytes
                });

        return agents;
    }

    private static async Task<IResult> ImportAsync(
        [FromForm] AgentPackageImportApiForm request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        if (request.Package is null || request.Package.Length == 0)
        {
            return ApiEndpointResults.BadRequest(
                "A non-empty package file is required.",
                "agent-package.file-required");
        }

        if (request.Package.Length > AgentPackageReadOptions.DefaultMaximumPackageBytes)
        {
            return ApiEndpointResults.BadRequest(
                $"The package exceeds the {AgentPackageReadOptions.DefaultMaximumPackageBytes}-byte limit.",
                "agent-package.too-large");
        }

        if (!TryParseMode(request.Mode, out var mode))
        {
            return ApiEndpointResults.BadRequest(
                "Mode must be create, replace-exact-version, or clone.",
                "agent-package.mode-invalid");
        }

        try
        {
            await using var package = request.Package.OpenReadStream();
            var receipt = await workspaceService.ImportAgentPackageAsync(
                package,
                new AgentPackageImportCommand(
                    mode,
                    idempotencyKey ?? string.Empty,
                    request.ExternalKey ?? string.Empty,
                    request.ExpectedPackageSha256,
                    request.ExpectedAgentVersion,
                    request.ExternalNamespace ?? AgentExternalIdentityNormalizer.PackageImportNamespace),
                cancellationToken);

            return receipt.Replayed || mode == AgentPackageImportMode.ReplaceExactVersion
                ? Results.Ok(receipt)
                : Results.Created($"/api/agents/{receipt.AgentId:D}", receipt);
        }
        catch (AgentPackageValidationException exception)
        {
            return Error(StatusCodes.Status400BadRequest, exception.Code, exception.Message);
        }
        catch (AgentPackageImportException exception)
        {
            var statusCode = exception.Kind switch
            {
                AgentPackageImportFailureKind.Conflict => StatusCodes.Status409Conflict,
                AgentPackageImportFailureKind.PreconditionFailed => StatusCodes.Status412PreconditionFailed,
                _ => StatusCodes.Status400BadRequest
            };
            return Error(statusCode, exception.Code, exception.Message);
        }
    }

    private static bool TryParseMode(string? value, out AgentPackageImportMode mode)
    {
        mode = value?.Trim().ToLowerInvariant() switch
        {
            "create" => AgentPackageImportMode.Create,
            "replace-exact-version" => AgentPackageImportMode.ReplaceExactVersion,
            "clone" => AgentPackageImportMode.Clone,
            _ => (AgentPackageImportMode)(-1)
        };
        return Enum.IsDefined(mode);
    }

    private static IResult Error(int statusCode, string code, string message)
    {
        return Results.Json(
            new ApiErrorResponse([new ApiErrorItem(code, message, ErrorSeverity.Error)]),
            statusCode: statusCode);
    }
}

internal sealed class AgentPackageImportApiForm
{
    public IFormFile? Package { get; set; }
    public string? Mode { get; set; }
    public string? ExternalKey { get; set; }
    public string? ExternalNamespace { get; set; }
    public string? ExpectedPackageSha256 { get; set; }
    public DateTimeOffset? ExpectedAgentVersion { get; set; }
}
