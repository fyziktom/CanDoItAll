using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.SharedProviders.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Composition;

internal sealed class SharedProviderRuntimeAccessContextHandler(
    IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains(SharedProviderHeaders.AccessContextReference) &&
            !request.Headers.Contains(SharedProviderHeaders.AccessContextReferenceType) &&
            ResolveAccessContext() is { } accessContext)
        {
            request.Headers.TryAddWithoutValidation(
                SharedProviderHeaders.AccessContextReference,
                accessContext.Reference.Value);
            if (accessContext.Type is { } type)
            {
                request.Headers.TryAddWithoutValidation(
                    SharedProviderHeaders.AccessContextReferenceType,
                    type.Value);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }

    private (AccessContextReference Reference, AccessContextReferenceType? Type)?
        ResolveAccessContext()
    {
        if (httpContextAccessor.HttpContext?.RequestServices
                .GetService<IAccessContextReferenceAccessor>() is
                { Current: { } explicitReference } accessor)
        {
            return (explicitReference, accessor.CurrentType);
        }

        var workspaceScope = WorkspaceExecutionAuditContext.Current?
            .ContextWorkspaceScope;
        if (workspaceScope is not { Kind: WorkspaceScopeKind.Project } ||
            !Guid.TryParse(workspaceScope.Key, out var projectId) ||
            projectId == Guid.Empty)
        {
            return null;
        }

        return (
            new AccessContextReference(projectId.ToString("D")),
            AccessContextReferenceTypes.Project);
    }
}

internal static class SharedProviderRuntimeAccessContextServiceCollectionExtensions
{
    public static IServiceCollection
        AddSharedProviderRuntimeAccessContextPropagation(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpContextAccessor();
        services.TryAddTransient<SharedProviderRuntimeAccessContextHandler>();
        AddHandler(
            services,
            SharedProviderCatalogClient.PublicClientName);
        AddHandler(
            services,
            SharedProviderCatalogClient.TrustedNetworkClientName);
        AddHandler(
            services,
            SharedProviderCatalogClient.PrivateHttpClientName);
        return services;
    }

    private static void AddHandler(
        IServiceCollection services,
        string clientName)
        => services.AddHttpClient(clientName)
            .AddHttpMessageHandler<SharedProviderRuntimeAccessContextHandler>();
}
