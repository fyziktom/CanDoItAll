using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

internal static class ApiAdministrationTestSetup {
    internal static IReadOnlyList<ApiScopeChoice> Scopes => ApiScopeCatalog.All.Select(scope =>
        new ApiScopeChoice(scope.Name, scope.Label, scope.Description, scope.Section, scope.UserSelectable, scope.MachineSelectable, scope.Sensitive)).ToArray();

    internal static IServiceCollection EnableUi(this IServiceCollection services) => services.Configure<ApiAccessOptions>(options => {
        options.Authorization.Enabled = true;
        options.Authorization.SigningKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    });
}
