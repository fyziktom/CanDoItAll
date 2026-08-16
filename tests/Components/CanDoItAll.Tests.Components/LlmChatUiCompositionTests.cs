using System.Security.Claims;
using CanDoItAll.Modules.LlmChats.Ui;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Composition;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Components;

public sealed class LlmChatUiCompositionTests
{
    [Fact]
    public void Focused_composition_registers_ui_boundary_and_assembly()
    {
        var services = new ServiceCollection();

        services.AddCanDoItAllLlmChatsUi();

        Assert.Contains(services, item => item.ServiceType == typeof(ILlmChatUiPolicyEvaluator));
        Assert.Contains(services, item => item.ServiceType == typeof(ILlmChatDefinitionUiGateway));
        Assert.Contains(
            CanDoItAll.Composition.ModuleAssemblies.All,
            assembly => assembly == typeof(LlmChatsUiAssemblyMarker).Assembly);
    }

    [Fact]
    public async Task Enabled_authorization_maps_each_ui_permission_to_existing_api_policy()
    {
        var authorization = new RecordingAuthorizationService();
        var evaluator = new WebLlmChatUiPolicyEvaluator(
            authorization,
            new FixedAuthenticationStateProvider(),
            Options.Create(new ApiAccessOptions
            {
                Authorization = new() { Enabled = true }
            }));

        Assert.True(await evaluator.IsAllowedAsync(LlmChatUiPermission.Read));
        Assert.True(await evaluator.IsAllowedAsync(LlmChatUiPermission.Manage));
        Assert.True(await evaluator.IsAllowedAsync(LlmChatUiPermission.Execute));
        Assert.Equal(
            [
                ApiAuthorizationPolicies.ReadLlmChats,
                ApiAuthorizationPolicies.ManageLlmChats,
                ApiAuthorizationPolicies.ExecuteLlmChats
            ],
            authorization.Policies);
    }

    [Fact]
    public async Task Disabled_authorization_allows_ui_without_evaluating_a_policy()
    {
        var authorization = new RecordingAuthorizationService();
        var evaluator = new WebLlmChatUiPolicyEvaluator(
            authorization,
            new FixedAuthenticationStateProvider(),
            Options.Create(new ApiAccessOptions
            {
                Authorization = new() { Enabled = false }
            }));

        Assert.True(await evaluator.IsAllowedAsync(LlmChatUiPermission.Manage));
        Assert.Empty(authorization.Policies);
    }

    private sealed class FixedAuthenticationStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "test-user")],
                "test"))));
    }

    private sealed class RecordingAuthorizationService : IAuthorizationService
    {
        public List<string> Policies { get; } = [];

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            IEnumerable<IAuthorizationRequirement> requirements)
            => throw new NotSupportedException("The UI evaluator must use named API policies.");

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            string policyName)
        {
            Policies.Add(policyName);
            return Task.FromResult(AuthorizationResult.Success());
        }
    }
}
