using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static string BuildProviderRepairRecoveryDirective(
        string recoveryDirective,
        ProviderRepairOutcome repairOutcome)
    {
        return ProcessProviderRecoveryDirectiveBuilder.BuildDirective(recoveryDirective, repairOutcome);
    }

    private static IReadOnlyList<ProviderProfile> OrderFallbackProviders(
        IEnumerable<ProviderProfile> providers,
        Guid failedProviderId)
    {
        return ProcessProviderFallbackSelectionRules.OrderFallbackProviders(providers, failedProviderId);
    }

    private static string ResolveFallbackProviderModel(
        ProviderProfile provider,
        ProviderHealthResult healthResult)
    {
        return ProcessProviderFallbackSelectionRules.ResolveFallbackProviderModel(provider, healthResult);
    }

}
