using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static CanDoItAll.Modules.Processes.ProcessAcceptanceCriteriaGate;
using static CanDoItAll.Modules.Processes.ProcessCompletionIssueResultFactory;
using static CanDoItAll.Modules.Processes.ProcessCompletionReceiptGate;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;
using static CanDoItAll.Modules.Processes.ProcessOutcomeGroundingValidator;
using static CanDoItAll.Modules.Processes.ProcessProductCompletionStateGate;
using static CanDoItAll.Modules.Processes.ProcessRequiredReceiptMatcher;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessCompletionGateFactory(
    ProcessToolReceiptPolicyCatalog toolReceiptPolicies,
    ProcessToolReceiptEvidenceGate toolReceiptEvidenceGate,
    IEnumerable<IProcessCompletionGateContribution> gateContributions,
    ProcessCompletionIssueResultFactory completionIssueResultFactory)
{
    private readonly IReadOnlyList<IProcessCompletionGateContribution> completionGateContributions =
        CreateCompletionGateContributions(gateContributions);

    internal ProcessCompletionGateEvaluator CreateCompletionGateEvaluator()
    {
        var earlyContributions = completionGateContributions
            .Where(contribution => contribution.Stage == ProcessCompletionGateContributionStage.BeforeToolReceiptEvidence)
            .ToArray();
        var lateContributions = completionGateContributions
            .Where(contribution => contribution.Stage == ProcessCompletionGateContributionStage.AfterToolReceiptEvidence)
            .ToArray();
        var gates = new List<Func<ProcessCompletionGateContext, ProcessCompletionIssue?>>
        {
            context => ValidateGroundedOutcomeReferences(
                context.Assignment,
                context.Output,
                context.ToolReceipts,
                context.VerifiedSubprocessOutcome),
            context => ValidateRequiredBranchOutcomeSelection(context.Assignment, context.Output),
            context => ValidateRuntimeRoutedBranchWasNotSelectedDirectly(context.Assignment, context.Output),
            context => ProcessProductMutationEvidenceGate.Validate(context.Assignment, context.Output),
            context => ValidateProductMutationWriteReceipt(
                context.Assignment,
                context.Output,
                context.ToolReceipts,
                toolReceiptPolicies),
            context =>
            {
                var applicableProductToolReceiptRules = ResolveApplicableProductCompletionRequiredToolReceiptRules(
                    context.Assignment,
                    context.Output.BranchOutcomeKey);
                return ValidateRequiredProductToolReceipts(
                    context.Assignment,
                    context.Output,
                    context.ToolReceipts,
                    applicableProductToolReceiptRules,
                    toolReceiptPolicies);
            },
            context =>
            {
                var applicableProductToolReceiptRules = ResolveApplicableProductCompletionRequiredToolReceiptRules(
                    context.Assignment,
                    context.Output.BranchOutcomeKey);
                return ValidateRequiredProcessToolReceipts(
                    context.Assignment,
                    context.Output,
                    context.ToolReceipts,
                    context.CurrentExecutionRunId,
                    ResolveEnforcedProductCoveredRuntimeToolNames(
                        context.Assignment,
                        applicableProductToolReceiptRules));
            },
        };

        gates.AddRange(earlyContributions.Select(
            contribution => (Func<ProcessCompletionGateContext, ProcessCompletionIssue?>)contribution.Validate));
        gates.Add(toolReceiptEvidenceGate.Validate);
        gates.AddRange(lateContributions.Select(
            contribution => (Func<ProcessCompletionGateContext, ProcessCompletionIssue?>)contribution.Validate));
        gates.AddRange(
        [
            context => completionIssueResultFactory.ValidateBranchOutcomeDefectEvidence(
                context.Assignment,
                context.Output,
                context.ToolReceipts,
                context.CurrentExecutionRunId),
            context => ValidateAcceptanceCriteriaCompletion(context.Assignment, context.Output),
            context => ValidateCompletedOutcomeDoesNotDeclareBlockers(
                context.Assignment,
                context.Output),
            context => ValidateManagedArtifactCompletion(context.Assignment, context.Output),
            context => ValidateManagedArtifactWriteReceipt(context.Assignment, context.ToolReceipts)
        ]);

        return new ProcessCompletionGateEvaluator(gates);
    }

    private static IReadOnlyList<IProcessCompletionGateContribution> CreateCompletionGateContributions(
        IEnumerable<IProcessCompletionGateContribution> gateContributions)
    {
        ArgumentNullException.ThrowIfNull(gateContributions);

        var contributions = gateContributions
            .OrderBy(contribution => contribution.Order)
            .ThenBy(contribution => contribution.ContributionKey, StringComparer.Ordinal)
            .ToArray();
        if (contributions.Any(contribution => string.IsNullOrWhiteSpace(contribution.ContributionKey)))
        {
            throw new InvalidOperationException(
                "A process completion gate contribution must declare a stable contribution key.");
        }

        var duplicate = contributions
            .GroupBy(contribution => contribution.ContributionKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate process completion gate contribution key '{duplicate.Key}' is registered.");
        }

        return contributions;
    }
}
