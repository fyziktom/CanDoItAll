using System.Globalization;
using System.ComponentModel;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.ApiAccess;

namespace CanDoItAll.Web.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Description("Replacement workspace business defaults; contains no authentication or deployment configuration.")]
internal sealed record WorkspaceSettingsApiRequest(
    [property: Description("Nonempty workspace display name, at most 200 characters without control characters.")] string WorkspaceName,
    [property: Description("Default provider profile GUID; must exist and be enabled, or null to leave no default.")] Guid? DefaultProviderProfileId,
    [property: Description("Nonempty default prompt output format, at most 40 characters.")] string DefaultPromptOutputFormat,
    [property: Description("Three-letter currency code, normalized to uppercase when saved.")] string CurrencyCode,
    [property: Description("Valid .NET culture name used for currency formatting, at most 40 characters.")] string CurrencyCultureName,
    [property: Description("Workspace notes, at most 8192 characters; may be empty.")] string Notes);

internal static class WorkspaceSettingsApi {
    public static void MapWorkspaceSettings(this RouteGroupBuilder api) {
        var settings = api.MapGroup("/settings/workspace")
            .WithApiSection(ApiAccessScopeNames.ReadWorkspaceSettings, ApiAccessScopeNames.WriteWorkspaceSettings);
        settings.MapGet("", (WorkspaceService workspace, CancellationToken cancellationToken) => workspace.GetSettingsAsync(cancellationToken))
            .WithName("GetWorkspaceSettings").Produces<WorkspaceSettingsModel>().ProducesApiErrors(401, 403, 503)
            .DescribeApi("Read workspace settings", "Read business defaults from the currently selected workspace database. Requires workspace-settings read capability.", "Persisted workspace settings, or defaults when none have been saved.");
        settings.MapPut("", SaveAsync).WithName("SaveWorkspaceSettings").Produces<WorkspaceSettingsModel>().ProducesApiErrors(400, 401, 403, 503)
            .DescribeApi("Replace workspace settings", "Validate and save business defaults in the current workspace database. Requires workspace-settings write capability. Returns authoritative read-back; if that read fails after commit, returns the saved snapshot with X-CanDoItAll-Read-Back: pending. Never repeat a committed write merely to refresh the read.", "Committed workspace settings snapshot.", "Complete replacement of the six workspace business settings fields; unknown members are rejected.");
    }

    private static async Task<IResult> SaveAsync(WorkspaceSettingsApiRequest request, WorkspaceService workspace,
        IProviderProfileRegistry providers, HttpContext context, ILoggerFactory loggerFactory, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(request.WorkspaceName) || request.WorkspaceName.Length > 200 || request.WorkspaceName.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(request.DefaultPromptOutputFormat) || request.DefaultPromptOutputFormat.Length > 40 ||
            request.CurrencyCode is null || request.CurrencyCode.Length != 3 || !request.CurrencyCode.All(char.IsAsciiLetter) ||
            string.IsNullOrWhiteSpace(request.CurrencyCultureName) || request.CurrencyCultureName.Length > 40 || request.Notes is null || request.Notes.Length > 8192) {
            return ApiEndpointResults.BadRequest("Workspace settings contain invalid fields.", "settings.workspace-invalid");
        }
        try {
            CultureInfo.GetCultureInfo(request.CurrencyCultureName);
        } catch (CultureNotFoundException) {
            return ApiEndpointResults.BadRequest("The currency culture is invalid.", "settings.workspace-invalid");
        }
        if (request.DefaultProviderProfileId is { } providerId &&
            (await providers.GetProviderAsync(providerId, cancellationToken))?.IsEnabled != true) {
            return ApiEndpointResults.BadRequest("The default provider must exist and be enabled.", "settings.workspace-provider-invalid");
        }
        var model = new WorkspaceSettingsModel {
            WorkspaceName = request.WorkspaceName, DefaultProviderProfileId = request.DefaultProviderProfileId,
            DefaultPromptOutputFormat = request.DefaultPromptOutputFormat, CurrencyCode = request.CurrencyCode,
            CurrencyCultureName = request.CurrencyCultureName, Notes = request.Notes
        };
        var saved = await workspace.SaveSettingsAsync(model, cancellationToken);
        try {
            return Results.Ok(await workspace.GetSettingsAsync(cancellationToken));
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            loggerFactory.CreateLogger(nameof(WorkspaceSettingsApi)).LogWarning("Workspace settings committed, but read-back failed: {ErrorType}.", exception.GetType().Name);
            context.Response.Headers["X-CanDoItAll-Read-Back"] = "pending";
            return Results.Ok(saved);
        }
    }
}
