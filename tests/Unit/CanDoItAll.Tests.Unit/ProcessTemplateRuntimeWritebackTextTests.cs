namespace CanDoItAll.Tests.Unit;

public sealed class ProcessTemplateRuntimeWritebackTextTests
{
    [Fact]
    public void Runtime_command_writeback_requires_launcher_compatible_metadata()
    {
        var root = FindRepositoryRoot();
        var resolve = File.ReadAllText(Path.Combine(root, "Templates", "Processes", "processes", "dotnet-runtime-command-writeback", "steps", "resolve-dotnet-run-commands.md"));
        var write = File.ReadAllText(Path.Combine(root, "Templates", "Processes", "processes", "dotnet-runtime-command-writeback", "steps", "write-run-command-nodes.md"));
        var handoff = File.ReadAllText(Path.Combine(root, "Templates", "Processes", "processes", "dotnet-runtime-command-writeback", "steps", "runtime-command-handoff.md"));
        var definitionJson = File.ReadAllText(Path.Combine(root, "Templates", "Processes", "processes", "dotnet-runtime-command-writeback", "definition.json"));

        Assert.Contains("ProjectStructureRuntimeLauncher", resolve, StringComparison.Ordinal);
        Assert.Contains("launcher-compatible", write, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("project_structure_node_create", write, StringComparison.Ordinal);
        Assert.Contains("project_structure_read", write, StringComparison.Ordinal);
        Assert.Contains("do not return `Completed` with only intended node payloads", write, StringComparison.Ordinal);
        Assert.Contains("created node ids", write, StringComparison.Ordinal);
        Assert.Contains("verified with project_structure_read", write, StringComparison.Ordinal);
        Assert.Contains("return `Blocked` with the missing field", write, StringComparison.Ordinal);
        Assert.Contains("metadataJson", write, StringComparison.Ordinal);
        Assert.Contains("\"environment\":{\"projectPath\"", write, StringComparison.Ordinal);
        Assert.Contains("\"script\":{\"command\":\"dotnet\"", write, StringComparison.Ordinal);
        Assert.Contains("steps/run-command-node-receipts.md", write, StringComparison.Ordinal);
        Assert.Contains("do not use `runtime-command-handoff.md` for this step", write, StringComparison.Ordinal);
        Assert.Contains("launcher-compatibility receipts", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("metadata.environment.projectPath", definitionJson, StringComparison.Ordinal);
        Assert.Contains("metadata.environment.workingDirectory", definitionJson, StringComparison.Ordinal);
        Assert.Contains("metadataJson", definitionJson, StringComparison.Ordinal);
        Assert.Contains("\\\"environment\\\":{\\\"projectPath\\\"", definitionJson, StringComparison.Ordinal);
        Assert.Contains("\\\"script\\\":{\\\"command\\\":\\\"dotnet\\\"", definitionJson, StringComparison.Ordinal);
        Assert.Contains("ProjectStructureRuntimeLauncher.Resolve", definitionJson, StringComparison.Ordinal);
        Assert.Contains("project_structure_node_create", definitionJson, StringComparison.Ordinal);
        Assert.Contains("project_structure_read", definitionJson, StringComparison.Ordinal);
        Assert.Contains("do not return Completed with only intended node payloads", definitionJson, StringComparison.Ordinal);
        Assert.Contains("created node ids", definitionJson, StringComparison.Ordinal);
        Assert.Contains("verified-with-project_structure_read text", definitionJson, StringComparison.Ordinal);
        Assert.Contains("steps/run-command-node-receipts.md", definitionJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Dotnet_solution_setup_handoff_accepts_canonical_first_build_artifact()
    {
        var root = FindRepositoryRoot();
        var handoff = File.ReadAllText(Path.Combine(root, "Templates", "Processes", "processes", "dotnet-solution-setup", "steps", "setup-handoff.md"));
        var definitionJson = File.ReadAllText(Path.Combine(root, "Templates", "Processes", "processes", "dotnet-solution-setup", "definition.json"));

        Assert.Contains("steps/validate-first-build.md", handoff, StringComparison.Ordinal);
        Assert.Contains("successful `workspace_stat_path` or `workspace_read_file` receipt", handoff, StringComparison.Ordinal);
        Assert.Contains("Do not require or probe a sibling path", handoff, StringComparison.Ordinal);
        Assert.Contains("steps/setup-handoff.md", handoff, StringComparison.Ordinal);
        Assert.Contains("setup-repair-required", File.ReadAllText(Path.Combine(root, "Templates", "Processes", "processes", "dotnet-solution-setup", "steps", "validate-first-build.md")), StringComparison.Ordinal);
        Assert.Contains("steps/setup-handoff-after-repair.md", definitionJson, StringComparison.Ordinal);
        Assert.Contains("steps/validate-first-build.md", definitionJson, StringComparison.Ordinal);
        Assert.Contains("non-canonical sibling evidence path", definitionJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Software_delivery_parent_steps_require_runtime_and_screenshot_compatibility()
    {
        var root = FindRepositoryRoot();
        var recordRuntimeCommands = File.ReadAllText(Path.Combine(root, "Templates", "Processes", "processes", "software-delivery", "steps", "record-runtime-commands.md"));
        var recordRuntimeCommandsAfterRepair = File.ReadAllText(Path.Combine(root, "Templates", "Processes", "processes", "software-delivery", "steps", "record-runtime-commands-after-repair.md"));
        var captureScreenshots = File.ReadAllText(Path.Combine(root, "Templates", "Processes", "processes", "software-delivery", "steps", "capture-ui-screenshots.md"));
        var captureScreenshotsAfterRepair = File.ReadAllText(Path.Combine(root, "Templates", "Processes", "processes", "software-delivery", "steps", "capture-ui-screenshots-after-repair.md"));
        var definitionJson = File.ReadAllText(Path.Combine(root, "Templates", "Processes", "processes", "software-delivery", "definition.json"));

        Assert.Contains("launcher-compatible metadata receipts", recordRuntimeCommands, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("launcher-compatible metadata receipts", recordRuntimeCommandsAfterRepair, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("launcher-compatible metadata receipts", definitionJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("verify the runtime command handoff includes a launcher-compatible Run app node", captureScreenshots, StringComparison.Ordinal);
        Assert.Contains("verify the repaired runtime command handoff includes a launcher-compatible Run app node", captureScreenshotsAfterRepair, StringComparison.Ordinal);
        Assert.Contains("why screenshots cannot be captured", captureScreenshots, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Templates", "Processes")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located.");
    }
}
