using System.Text;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessStepContractPromptBuilder
{
    public static string Build(
        string prompt,
        ProcessStepExecutionContract stepContract)
    {
        if (stepContract.RequiredArtifacts.Count == 0 &&
            stepContract.ExpectedProducedArtifacts.Count == 0 &&
            stepContract.RequiredRuntimeToolNames.Count == 0)
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
                builder
                    .Append("- slot ")
                    .Append(artifact.SlotId)
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
                builder
                    .Append("- slot ")
                    .AppendLine(artifact.SlotId.ToString());
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
        }

        builder.Append("Do not return Completed until every available required input is reflected in the work, every required runtime tool has the needed receipt, and every expected output artifact is produced.");
        return builder.ToString();
    }
}
