using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Readiness;
using CanDoItAll.Web.Api;
using CanDoItAll.Modules.Workspace.ApiAccess;

namespace CanDoItAll.Web;

/// <summary>
/// Operational runtime routes under <c>/api/runtime</c> that report the host profile, host capabilities and readiness.
/// </summary>
internal static class RuntimeEndpoints
{
    private const string Tag = "Runtime";

    public static IEndpointRouteBuilder MapRuntimeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // The snapshots describe the host and each read probes the purpose roots on disk, so with API authorization
        // enabled they need a bearer token like the rest of the API; the anonymous liveness probe is /health.
        endpoints.MapGet("/api/runtime/capabilities", GetCapabilities)
            .WithTags(Tag)
            .Produces<HostCapabilitySnapshot>()
            .ProducesApiErrors(StatusCodes.Status401Unauthorized)
            .ApplyApiAuthorization(endpoints, ApiAccessScopeNames.ReadRuntime);
        endpoints.MapGet("/api/runtime/operations", GetOperations)
            .WithTags(Tag)
            .Produces<RuntimeOperationsSnapshot>()
            .ProducesApiErrors(StatusCodes.Status401Unauthorized)
            .ApplyApiAuthorization(endpoints, ApiAccessScopeNames.ReadRuntime);

        return endpoints;
    }

    /// <summary>
    /// Read the runtime host profile and the availability of each host capability.
    /// </summary>
    /// <remarks>
    /// Returns a fresh snapshot of the resolved host profile (for example <c>WindowsHeadless</c>), the operating
    /// system, whether the host is interactive, the readiness of each application purpose root and one entry per host
    /// capability. <c>isReady</c> is true when every mandatory capability is available; optional capabilities, such as
    /// desktop file opening on a headless host, can be unavailable on a ready host. For the deployment support manifest
    /// and the database readiness in one response, use <c>GET /api/runtime/operations</c>.
    ///
    /// Each read probes the application purpose roots again by creating and deleting a small temporary file in each
    /// root; it changes no application data. The response contains no paths, connection strings or secret values.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open. It is mapped even when <c>Api:Enabled</c> is false. For
    /// an anonymous liveness check use <c>GET /health</c>.
    /// </remarks>
    /// <response code="200">The current host capability snapshot.</response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    internal static IResult GetCapabilities(
        IHostCapabilitySnapshotProvider hostCapabilities) =>
            Results.Ok(hostCapabilities.GetSnapshot());

    /// <summary>
    /// Read the operational state of the host: readiness, deployment support and host capabilities.
    /// </summary>
    /// <remarks>
    /// Use this read after an installation, upgrade or restart to confirm that the host finished starting. The
    /// response combines <c>state</c> (<c>Starting</c>, <c>Ready</c> or <c>Unavailable</c>), whether the database
    /// and its migrations are ready, the deployment support manifest embedded in this build (supported publish
    /// targets, host profiles, prerequisites and pending validations) and the host capability snapshot also returned
    /// by <c>GET /api/runtime/capabilities</c>. A successful HTTP response does not mean the host is ready: check
    /// <c>state</c> and <c>databaseAndMigrationsReady</c>.
    ///
    /// Each read probes the application purpose roots again by creating and deleting a small temporary file in each
    /// root; it changes no application data. The response contains no paths, connection strings or secret values.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open. It is mapped even when <c>Api:Enabled</c> is false. For
    /// an anonymous liveness check use <c>GET /health</c>.
    /// </remarks>
    /// <response code="200">The current operational snapshot; read <c>state</c> for readiness.</response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    internal static IResult GetOperations(
        IRuntimeDeploymentSupportProvider deploymentSupport,
        IRuntimeReadinessService readiness,
        IHostCapabilitySnapshotProvider hostCapabilities) =>
            Results.Ok(RuntimeOperationsSnapshotProjector.Create(
                deploymentSupport.GetManifest(),
                readiness.GetSnapshot(),
                hostCapabilities.GetSnapshot()));
}
