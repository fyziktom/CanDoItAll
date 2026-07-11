using System.Text;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessStepContractPromptBuilder
{
    public static string Build(
        string prompt,
        ProcessStepExecutionContract stepContract,
        IReadOnlyDictionary<string, string>? launchVariables = null,
        string stepKey = "",
        ProcessSubprocessContract? resolvedSubprocessContract = null)
    {
        var subprocessContract = resolvedSubprocessContract;
        var hasSubprocessContract = subprocessContract is not null ||
            launchVariables is not null &&
            ProcessRuntimeLaunchVariables.TryReadProcessStepSubprocessContract(
                launchVariables,
                out subprocessContract);
        var requiresProductSourceInspection = launchVariables is not null &&
            ProcessProductSourceInspectionPolicy.IsConfiguredForStep(launchVariables, stepKey);
        if (stepContract.RequiredArtifacts.Count == 0 &&
            stepContract.ExpectedProducedArtifacts.Count == 0 &&
            stepContract.RequiredRuntimeToolNames.Count == 0 &&
            !requiresProductSourceInspection &&
            !hasSubprocessContract)
        {
            return prompt;
        }

        var builder = new StringBuilder(prompt.TrimEnd());
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine("Runtime step contract:");
        builder.AppendLine($"Contract hash: {stepContract.ContractHash}");
        builder.AppendLine("Required input artifacts:");
        if (stepContract.RequiredArtifacts.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var artifact in stepContract.RequiredArtifacts)
            {
                var descriptor = ResolveArtifactDescriptor(stepContract, artifact.SlotId);
                builder
                    .Append("- slot ")
                    .Append(artifact.SlotId)
                    .Append("; expectation ")
                    .Append(FormatArtifactExpectation(descriptor))
                    .Append("; primary ref ")
                    .Append(FormatPrimaryManagedRef(descriptor))
                    .Append("; availability ")
                    .Append(artifact.Availability)
                    .Append("; producer ")
                    .Append(artifact.ProducerStepId?.ToString() ?? "none")
                    .Append("; artifact ")
                    .Append(artifact.ArtifactId?.ToString() ?? "none")
                    .Append("; content ")
                    .AppendLine(string.IsNullOrWhiteSpace(artifact.ContentHash) ? "none" : artifact.ContentHash);
            }
        }

        builder.AppendLine("Expected output artifacts:");
        if (stepContract.ExpectedProducedArtifacts.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var artifact in stepContract.ExpectedProducedArtifacts)
            {
                var descriptor = ResolveArtifactDescriptor(stepContract, artifact.SlotId);
                builder
                    .Append("- slot ")
                    .Append(artifact.SlotId)
                    .Append("; expectation ")
                    .Append(FormatArtifactExpectation(descriptor))
                    .Append("; primary managed ref ")
                    .Append(FormatPrimaryManagedRef(descriptor))
                    .Append("; materialization ")
                    .AppendLine(descriptor?.MaterializationMode.ToString() ?? "unspecified");
            }
        }

        builder.AppendLine("Required runtime tools:");
        if (stepContract.RequiredRuntimeToolNames.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var toolName in stepContract.RequiredRuntimeToolNames)
            {
                builder
                    .Append("- ")
                    .AppendLine(toolName);
            }

            builder.AppendLine(
                "Required runtime tool receipt rule: each listed tool must produce a receipt whose execution id is this exact execution attempt before this step may submit Completed. Upstream receipts and receipts from earlier attempts of this step do not count. A managed artifact, markdown statement, upstream artifact, launch variable, or prior run log that names the tool is not a receipt. Invoke every missing tool now in this execution attempt. If a listed tool is unavailable, denied, or fails before a branch decision can be made, submit Blocked or the applicable repair branch with the concrete current-execution tool failure evidence instead of claiming Completed.");
            builder.AppendLine(
                "For validation tools, invoke the concrete runtime tools listed above in this step before writing final success evidence. Do not replace those invocations with manual shell commands, prose summaries, or upstream artifact readbacks.");
        }

        if (requiresProductSourceInspection)
        {
            var excludedPathFragments = ProcessProductSourceInspectionPolicy.ResolveExcludedPathFragments(
                launchVariables!,
                stepKey);
            builder.AppendLine("Required current-run product-source inspection:");
            builder.AppendLine(
                "- Before submitting Completed, call workspace_read_file in this exact execution attempt for at least one representative owning product source, configuration, style, or mapped test file under the grounded external product-root alias. Reading only managed process artifacts, upstream summaries, launch variables, or files from an earlier attempt does not satisfy this gate. Use the current product readback to justify the selected acceptance, repair, rejection, or escalation branch and cite the grounded product alias in the managed evidence artifact.");
            if (excludedPathFragments.Count > 0)
            {
                builder.Append("- For this step, reads matching these non-owning shell fragments do not satisfy the inspection gate by themselves: ")
                    .AppendLine(string.Join("; ", excludedPathFragments));
            }
        }

        if (subprocessContract is not null)
        {
            AppendSubprocessContract(builder, subprocessContract);
        }

        AppendSubprocessArtifactMappings(builder, stepContract.SubprocessArtifactMappings);

        builder.Append("Do not return Completed until every available required input is reflected in the work, every required runtime tool has a current execution-run receipt from invoking that exact tool, and every expected output artifact is produced.");
        return builder.ToString();
    }

    private static ProcessArtifactSlotDescriptor? ResolveArtifactDescriptor(
        ProcessStepExecutionContract stepContract,
        ArtifactSlotId slotId)
        => stepContract.ArtifactDescriptors.FirstOrDefault(descriptor => descriptor.SlotId == slotId);

    private static string FormatArtifactExpectation(ProcessArtifactSlotDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return "unspecified";
        }

        var title = string.IsNullOrWhiteSpace(descriptor.ArtifactTitle)
            ? descriptor.ArtifactExpectationKey
            : descriptor.ArtifactTitle;
        return $"{descriptor.ArtifactExpectationKey} - {title}";
    }

    private static string FormatPrimaryManagedRef(ProcessArtifactSlotDescriptor? descriptor)
        => string.IsNullOrWhiteSpace(descriptor?.PrimaryManagedRef)
            ? "unspecified"
            : descriptor.PrimaryManagedRef;

    private static void AppendSubprocessContract(
        StringBuilder builder,
        ProcessSubprocessContract contract)
    {
        builder.AppendLine("Subprocess parent bridge contract:");
        builder.AppendLine($"- child process: {contract.DefinitionKey}");
        builder.AppendLine($"- launch mode: {contract.LaunchMode}");
        builder.AppendLine($"- parent artifact expectation: {contract.ParentProducedArtifactExpectationKey}");
        builder.AppendLine("Accepted child outputs:");
        AppendChildOutputs(builder, contract.AcceptedChildOutputs);
        builder.AppendLine("No-go child outputs:");
        AppendChildOutputs(builder, contract.NoGoChildOutputs);
        if (contract.AlreadySatisfiedOutput is not null)
        {
            builder.AppendLine("Already-satisfied output:");
            AppendChildOutput(builder, contract.AlreadySatisfiedOutput);
        }

        if (contract.RequiredChildReceipts.Count > 0)
        {
            builder.AppendLine("Required child receipts:");
            foreach (var receipt in contract.RequiredChildReceipts)
            {
                builder
                    .Append("- tool ")
                    .Append(string.IsNullOrWhiteSpace(receipt.ToolName) ? "any" : receipt.ToolName)
                    .Append("; provider ")
                    .Append(string.IsNullOrWhiteSpace(receipt.RuntimeToolProviderKey) ? "any" : receipt.RuntimeToolProviderKey)
                    .Append("; ")
                    .AppendLine(receipt.Description);
            }
        }
    }

    private static void AppendSubprocessArtifactMappings(
        StringBuilder builder,
        IReadOnlyList<SubprocessArtifactMappingDescriptor> mappings)
    {
        if (mappings.Count == 0)
        {
            return;
        }

        builder.AppendLine("Subprocess artifact mappings:");
        foreach (var mapping in mappings)
        {
            builder
                .Append("- parent expectation ")
                .Append(mapping.ParentArtifactExpectationKey)
                .Append("; child process ")
                .Append(mapping.ChildProcessDefinitionKey)
                .Append("; parent slot ")
                .AppendLine(mapping.ParentSlotId.ToString());
            AppendMappingOutputs(builder, "accepted", mapping.AcceptedChildOutputs);
            AppendMappingOutputs(builder, "no-go", mapping.NoGoChildOutputs);
        }
    }

    private static void AppendMappingOutputs(
        StringBuilder builder,
        string label,
        IReadOnlyList<SubprocessChildArtifactMappingDescriptor> outputs)
    {
        if (outputs.Count == 0)
        {
            builder
                .Append("  - ")
                .Append(label)
                .AppendLine(": none");
            return;
        }

        foreach (var output in outputs)
        {
            builder
                .Append("  - ")
                .Append(label)
                .Append(": step ")
                .Append(output.StepKey)
                .Append("; artifact expectation ")
                .Append(output.ArtifactExpectationKey)
                .Append("; title ")
                .Append(output.ArtifactTitle);
            if (!string.IsNullOrWhiteSpace(output.BranchOutcomeKey))
            {
                builder
                    .Append("; branch ")
                    .Append(output.BranchOutcomeKey);
            }

            builder.AppendLine();
        }
    }

    private static void AppendChildOutputs(
        StringBuilder builder,
        IReadOnlyList<ProcessSubprocessChildOutputContract> outputs)
    {
        if (outputs.Count == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        foreach (var output in outputs)
        {
            AppendChildOutput(builder, output);
        }
    }

    private static void AppendChildOutput(
        StringBuilder builder,
        ProcessSubprocessChildOutputContract output)
    {
        builder
            .Append("- step ")
            .Append(output.StepKey)
            .Append("; artifact expectation ")
            .Append(string.IsNullOrWhiteSpace(output.ArtifactExpectationKey) ? "unspecified" : output.ArtifactExpectationKey)
            .Append("; title ")
            .Append(string.IsNullOrWhiteSpace(output.ArtifactTitle) ? "unspecified" : output.ArtifactTitle);
        if (!string.IsNullOrWhiteSpace(output.BranchOutcomeKey))
        {
            builder
                .Append("; branch ")
                .Append(output.BranchOutcomeKey);
        }

        if (!string.IsNullOrWhiteSpace(output.Description))
        {
            builder
                .Append("; ")
                .Append(output.Description);
        }

        builder.AppendLine();
    }
}
