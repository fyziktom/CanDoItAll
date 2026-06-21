using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessAutomationDatabaseRequirementFailure(string Message);

internal sealed class ProcessAutomationDatabaseRequirementResolver(
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
    IOptions<ProcessRuntimeOptions> processRuntimeOptions)
{
    public ProcessAutomationDatabaseRequirementFailure? Resolve()
    {
        if (!processRuntimeOptions.Value.RequirePostgreSqlForAgentAutomation)
        {
            return null;
        }

        var profile = databaseProfileRuntimeAccessor.ResolveCurrentProfile();
        if (profile.Profile.ProviderKind == DatabaseProviderKind.PostgreSql)
        {
            return null;
        }

        return new ProcessAutomationDatabaseRequirementFailure(
            $"Governed process automation requires PostgreSQL, but the active database profile is '{profile.Profile.DisplayName}' ({profile.Profile.Id:D}, provider {profile.Profile.ProviderKind}, source {profile.Profile.SourceKind}, resolved by {profile.ResolutionSource}). Switch the active database profile to PostgreSQL before rerunning automation.");
    }
}
