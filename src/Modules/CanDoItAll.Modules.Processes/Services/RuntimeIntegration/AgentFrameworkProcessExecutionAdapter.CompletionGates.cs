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

namespace CanDoItAll.Modules.Processes;

internal sealed partial class AgentFrameworkProcessExecutionAdapter
{
    private static readonly ProcessCompletionGateEvaluator CompletionGateEvaluator = CreateCompletionGateEvaluator();

    private static ProcessCompletionGateEvaluator CreateCompletionGateEvaluator()
        => new(
        [
            context => ValidateGroundedOutcomeReferences(context.Assignment, context.Output, context.ToolReceipts),
            context => ValidateProductMutationCompletion(context.Assignment, context.Output),
            context => ValidateProductMutationWriteReceipt(context.Assignment, context.Output, context.ToolReceipts),
            context =>
            {
                var applicableProductToolReceiptRules = ResolveApplicableProductCompletionRequiredToolReceiptRules(
                    context.Assignment,
                    context.Output.BranchOutcomeKey);
                return ValidateRequiredProductToolReceipts(
                    context.Assignment,
                    context.Output,
                    context.ToolReceipts,
                    applicableProductToolReceiptRules);
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
            context =>
            {
                var applicableProductToolReceiptRules = ResolveApplicableProductCompletionRequiredToolReceiptRules(
                    context.Assignment,
                    context.Output.BranchOutcomeKey);
                return ValidateRuntimeLifecycleReceipts(
                    context.Assignment,
                    context.Output,
                    context.ToolReceipts,
                    context.CurrentExecutionRunId,
                    applicableProductToolReceiptRules);
            },
            context => ValidateBranchOutcomeDefectEvidence(context.Assignment, context.Output, context.ToolReceipts),
            context => ValidateAcceptanceCriteriaCompletion(context.Assignment, context.Output),
            context => ValidateRequiredProductStateCompletion(context.Assignment, context.Output),
            context => ValidateCompletedOutcomeDoesNotDeclareBlockers(context.Assignment, context.Output),
            context => ValidateManagedArtifactCompletion(context.Assignment, context.Output),
            context => ValidateManagedArtifactWriteReceipt(context.Assignment, context.ToolReceipts)
        ]);
}
