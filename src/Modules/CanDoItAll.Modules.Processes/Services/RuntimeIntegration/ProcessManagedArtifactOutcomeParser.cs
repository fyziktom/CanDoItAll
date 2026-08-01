using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Core.Execution;
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

using static CanDoItAll.Modules.Processes.ProcessAgentRightsDiagnosticPolicy;
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionMetadataBuilder;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessManagedArtifactOutcomeParser
{
    internal static bool HasAllManagedArtifactEvidence(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<string> evidenceRefs)
    {
        var normalizedEvidenceRefs = evidenceRefs
            .Where(evidenceRef => !string.IsNullOrWhiteSpace(evidenceRef))
            .Select(NormalizeManagedArtifactRef)
            .Where(evidenceRef => evidenceRef.Length > 0)
            .ToArray();
        return assignment.ProducedArtifactSlotIds.All(slotId =>
            HasManagedArtifactEvidence(assignment, slotId, normalizedEvidenceRefs));
    }

    internal static ProcessStepOutcomeResult CopyWithEvidenceRef(
        ProcessStepOutcomeResult output,
        string evidenceRef)
    {
        var evidenceRefs = output.EvidenceRefs
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Append(evidenceRef)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return CopyWithEvidenceRefs(output, evidenceRefs);
    }

    internal static ProcessStepOutcomeResult CopyWithEvidenceRefs(
        ProcessStepOutcomeResult output,
        IReadOnlyList<string> evidenceRefs)
    {
        return new ProcessStepOutcomeResult
        {
            Status = output.Status,
            Reason = output.Reason,
            BranchOutcomeKey = output.BranchOutcomeKey,
            BranchOutcomeTitle = output.BranchOutcomeTitle,
            EvidenceRefs = evidenceRefs,
            AcceptanceCriteriaEvidence = output.AcceptanceCriteriaEvidence,
            NextActions = output.NextActions,
            HumanReadableSummaryMarkdown = output.HumanReadableSummaryMarkdown
        };
    }

}
