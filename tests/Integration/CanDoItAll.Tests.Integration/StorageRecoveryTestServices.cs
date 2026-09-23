using System.Security.Claims;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

internal static class StorageRecoveryTestServices {
    internal static readonly string[] ReadScopes = [ApiAccessScopeNames.Api, ApiAccessScopeNames.ReadStoragePlacementRecovery];
    internal static readonly string[] ReconcileScopes = [.. ReadScopes, ApiAccessScopeNames.WriteProjectStructure,
        ApiAccessScopeNames.ReconcileStoragePlacement];
    internal static readonly string[] VerifyScopes = [.. ReconcileScopes, ApiAccessScopeNames.VerifyStorageExternalTermination];

    internal static void Add(IServiceCollection services, bool localOperator) {
        services.AddRouting();
        services.AddHttpContextAccessor();
        services.AddOptions<ApiAccessOptions>();
        services.AddAuthorization(options => {
            AddPolicy(options, ApiAuthorizationPolicies.GeneralApi, ApiAccessScopeNames.Api);
            AddPolicy(options, ApiAuthorizationPolicies.WriteProjectStructure, ApiAccessScopeNames.WriteProjectStructure);
            AddPolicy(options, ApiAuthorizationPolicies.ReadStoragePlacementRecovery, ApiAccessScopeNames.ReadStoragePlacementRecovery);
            AddPolicy(options, ApiAuthorizationPolicies.ReconcileStoragePlacement, ApiAccessScopeNames.ReconcileStoragePlacement);
            AddPolicy(options, ApiAuthorizationPolicies.VerifyStorageExternalTermination, ApiAccessScopeNames.VerifyStorageExternalTermination);
        });
        services.AddScoped<WebCurrentPrincipalResolver>();
        services.AddScoped<ProjectStoragePlacementRecoveryQuery>();
        services.AddScoped<AgentAssetCheckpointQuery>();
        services.AddScoped<WorkflowAssetCheckpointQuery>();
        services.AddScoped<IStoragePlacementRecoveryAccess, WebStoragePlacementRecoveryAccess>();
        services.AddScoped<StoragePlacementRecoveryService>();
        services.AddScoped<ProjectProcessAssetReceiptRecoveryQuery>();
        services.AddScoped<ProjectWorkflowAssetContinuationService>();
        services.AddScoped<IStoragePlacementOwnerContinuation, StoragePlacementOwnerContinuationService>();
        if (localOperator) {
            services.Replace(ServiceDescriptor.Scoped<IInteractiveAccessPrincipalProvider, ExplicitTestOperator>());
        }
    }

    private static void AddPolicy(AuthorizationOptions options, string policy, string scope) {
        if (options.GetPolicy(policy) is null) {
            options.AddPolicy(policy, builder => builder.RequireAuthenticatedUser().RequireAssertion(context =>
                ApiAuthorizationPolicies.HasScope(context.User, scope)));
        }
    }

    private sealed class ExplicitTestOperator : IInteractiveAccessPrincipalProvider {
        public bool IsAvailable => true;
        public ValueTask<ClaimsPrincipal> GetCurrentAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub", LocalOperatorAuthenticationStateProvider.ActorId),
                new Claim("scope", string.Join(' ', VerifyScopes))
            ], LocalOperatorAuthenticationStateProvider.AuthenticationType)));
        }
    }
}
