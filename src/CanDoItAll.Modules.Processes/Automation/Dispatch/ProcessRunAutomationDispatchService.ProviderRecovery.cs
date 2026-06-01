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
        var builder = new StringBuilder();
        builder.Append("Infrastructure recovery: the previous attempt hit a provider failure. ");
        builder.Append("Assigned internal agents using provider '")
            .Append(repairOutcome.FailedProviderName)
            .Append("' were moved to '")
            .Append(repairOutcome.FallbackProviderName)
            .Append("' with model '")
            .Append(repairOutcome.FallbackModel)
            .Append("'. ");
        builder.AppendLine($"Failure summary: {repairOutcome.FailureSummary}");

        if (!string.IsNullOrWhiteSpace(recoveryDirective))
        {
            builder.AppendLine(recoveryDirective.Trim());
        }

        return builder.ToString().Trim();
    }

    private static IReadOnlyList<ProviderProfile> OrderFallbackProviders(
        IEnumerable<ProviderProfile> providers,
        Guid failedProviderId)
    {
        return providers
            .Where(item =>
                item.IsEnabled &&
                item.SupportsTools &&
                item.Id != failedProviderId &&
                item.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi)
            .OrderBy(item => item.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi ? 0 : 1)
            .ThenBy(item => item.Transport == ProviderTransportKind.Responses ? 0 : 1)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveFallbackProviderModel(
        ProviderProfile provider,
        ProviderHealthResult healthResult)
    {
        var suggestedModels = healthResult.SuggestedModels
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!string.IsNullOrWhiteSpace(provider.DefaultModel) &&
            (suggestedModels.Count == 0 || suggestedModels.Contains(provider.DefaultModel, StringComparer.OrdinalIgnoreCase)))
        {
            return provider.DefaultModel;
        }

        return suggestedModels.FirstOrDefault()
               ?? provider.DefaultModel;
    }

}
