using System.Text;

namespace CanDoItAll.Modules.Factory.Pages;

public partial class PromptFactoryPage
{
    private string promptPreviewKicker = "Build result";
    private string promptPreviewTitle = "Final prompt";
    private string promptPreviewDescription = "Build uses the selected flow and opens the assembled prompt here so it can be copied immediately.";
    private string promptPreviewText = string.Empty;

    private Task OpenSelectionPromptPreviewAsync()
    {
        var preview = ResolveSelectionPromptPreview();
        if (string.IsNullOrWhiteSpace(preview.Text))
        {
            SetMessage("Nothing is available to preview for the selected item yet.");
            return Task.CompletedTask;
        }

        OpenPromptPreview(preview);
        return Task.CompletedTask;
    }

    private void OpenFinalPromptPreview()
    {
        if (string.IsNullOrWhiteSpace(editor.GeneratedPrompt))
        {
            showPromptPreviewDialog = false;
            return;
        }

        OpenPromptPreview(new PromptPreviewModel(
            "Build result",
            "Final prompt",
            "Build uses the selected flow and opens the assembled prompt here so it can be copied immediately.",
            editor.GeneratedPrompt));
    }

    private void OpenPromptPreview(PromptPreviewModel preview)
    {
        promptPreviewKicker = preview.Kicker;
        promptPreviewTitle = preview.Title;
        promptPreviewDescription = preview.Description;
        promptPreviewText = preview.Text;
        showPromptPreviewDialog = !string.IsNullOrWhiteSpace(promptPreviewText);
    }

    private PromptPreviewModel ResolveSelectionPromptPreview()
    {
        if (selectedComponentNode is not null)
        {
            return new PromptPreviewModel(
                "Selection preview",
                selectedComponentNode.Name,
                "This preview uses the effective session override for the selected component. Editing the inspector changes what build uses.",
                BuildComponentPreviewText(selectedComponentNode));
        }

        if (selectedPromptNode is not null)
        {
            return new PromptPreviewModel(
                "Branch preview",
                selectedPromptNode.Title,
                "This preview contains the selected prompt step and every descendant step under it.",
                BuildPromptNodePreviewText(selectedPromptNode));
        }

        if (selectedBranchLabel is not null)
        {
            return new PromptPreviewModel(
                "Branch preview",
                selectedBranchLabel,
                "This preview contains the currently selected branch only.",
                BuildBranchPreviewText(selectedBranchLabel, selectedBranchNodes));
        }

        if (selectedAttachmentNode is not null)
        {
            return new PromptPreviewModel(
                "Selection preview",
                selectedAttachmentNode.Title,
                "This preview shows the attached input exactly as it will be described to the prompt session.",
                BuildAttachmentPreviewText(selectedAttachmentNode));
        }

        if (selectedFlowNode is not null)
        {
            return new PromptPreviewModel(
                "Selection preview",
                selectedFlowNode.Name,
                "This preview summarizes the selected flow template and its current agent sequence.",
                BuildFlowPreviewText(selectedFlowNode));
        }

        if (selectedBlueprintNode is not null)
        {
            return new PromptPreviewModel(
                "Selection preview",
                selectedBlueprintNode.Name,
                "This preview summarizes the selected blueprint framing that guides the full prompt build.",
                BuildBlueprintPreviewText(selectedBlueprintNode));
        }

        if (selectedComponentGroupNode is not null)
        {
            return new PromptPreviewModel(
                "Selection preview",
                selectedComponentGroupNode.Name,
                "This preview summarizes the currently selected component group.",
                BuildComponentGroupPreviewText(selectedComponentGroupNode));
        }

        if (selectedSetupNode is not null)
        {
            return new PromptPreviewModel(
                "Selection preview",
                "Session setup",
                "This preview captures the current first-step wizard values pinned into the prompt session.",
                BuildSetupPreviewText());
        }

        if (!string.IsNullOrWhiteSpace(editor.GeneratedPrompt))
        {
            return new PromptPreviewModel(
                "Session preview",
                "Current prompt",
                "This preview shows the current built prompt stored for the session root.",
                editor.GeneratedPrompt);
        }

        return new PromptPreviewModel(
            "Session preview",
            string.IsNullOrWhiteSpace(editor.SessionName) ? "Prompt session" : editor.SessionName,
            "No full prompt has been built yet, so this preview shows the current session frame instead.",
            BuildSessionPreviewText());
    }

    private string BuildComponentPreviewText(PromptBlockSummary block)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Component: {block.Name}");
        builder.AppendLine($"Kind: {block.BlockKind}");
        builder.AppendLine($"Group: {ResolveLibraryGroupLabel(block.GroupKey)}");
        builder.AppendLine();
        builder.AppendLine(SelectedComponentRenderedContent);
        return builder.ToString().Trim();
    }

    private string BuildPromptNodePreviewText(PromptRunNodeSummary rootNode)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Prompt subtree: {rootNode.Title}");
        builder.AppendLine($"Branch: {rootNode.BranchLabel}");
        builder.AppendLine();

        foreach (var entry in EnumeratePromptNodeTree(rootNode))
        {
            var headingLevel = Math.Min(6, entry.Depth + 2);
            builder.AppendLine($"{new string('#', headingLevel)} Step {entry.Node.Sequence}: {entry.Node.Title}");
            builder.AppendLine($"State: {entry.Node.State}");
            builder.AppendLine($"Branch: {entry.Node.BranchLabel}");
            if (entry.Node.PromptArtifactId.HasValue)
            {
                builder.AppendLine("Artifact: linked");
            }

            builder.AppendLine();
            builder.AppendLine(string.IsNullOrWhiteSpace(entry.Node.Notes) ? "No step notes yet." : entry.Node.Notes.Trim());
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private string BuildBranchPreviewText(string branchLabel, IReadOnlyList<PromptRunNodeSummary> branchNodes)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Branch: {branchLabel}");
        builder.AppendLine();

        foreach (var node in branchNodes.OrderBy(item => item.Sequence).ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"## Step {node.Sequence}: {node.Title}");
            builder.AppendLine($"State: {node.State}");
            if (node.ParentNodeId.HasValue)
            {
                builder.AppendLine("Origin: derived branch");
            }

            builder.AppendLine();
            builder.AppendLine(string.IsNullOrWhiteSpace(node.Notes) ? "No step notes yet." : node.Notes.Trim());
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private string BuildAttachmentPreviewText(PromptSessionAttachmentSummary attachment)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Input: {attachment.Title}");
        builder.AppendLine($"Kind: {attachment.Kind}");

        if (!string.IsNullOrWhiteSpace(attachment.MediaOriginalFileName))
        {
            builder.AppendLine($"File: {attachment.MediaOriginalFileName}");
        }

        if (!string.IsNullOrWhiteSpace(attachment.LinkUrl))
        {
            builder.AppendLine($"Link: {attachment.LinkUrl}");
        }

        builder.AppendLine();
        builder.AppendLine(string.IsNullOrWhiteSpace(attachment.Notes)
            ? string.IsNullOrWhiteSpace(attachment.Subtitle)
                ? "No additional extraction note supplied."
                : attachment.Subtitle.Trim()
            : attachment.Notes.Trim());
        return builder.ToString().Trim();
    }

    private string BuildFlowPreviewText(PromptFlowTemplateSummary flow)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Flow: {flow.Name}");
        builder.AppendLine();
        builder.AppendLine(flow.Summary);
        builder.AppendLine();

        foreach (var step in flow.AgentSequence.OrderBy(item => item.Order))
        {
            builder.AppendLine($"## Step {step.Order}: {(string.IsNullOrWhiteSpace(step.Goal) ? step.Phase : step.Goal)}");
            builder.AppendLine($"Phase: {(string.IsNullOrWhiteSpace(step.Phase) ? "general" : step.Phase)}");
            if (!string.IsNullOrWhiteSpace(step.BlueprintKey))
            {
                builder.AppendLine($"Blueprint: {step.BlueprintKey}");
            }

            if (step.BlockKeys.Count > 0)
            {
                builder.AppendLine($"Blocks: {string.Join(", ", step.BlockKeys)}");
            }

            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private string BuildBlueprintPreviewText(PromptBlueprintSummary blueprint)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Blueprint: {blueprint.Name}");
        builder.AppendLine($"Prompt type: {blueprint.PromptType}");
        builder.AppendLine();
        builder.AppendLine(blueprint.Guidance);

        if (blueprint.RecommendedBlockKeys.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Recommended blocks:");
            foreach (var blockKey in blueprint.RecommendedBlockKeys)
            {
                builder.AppendLine($"- {blockKey}");
            }
        }

        return builder.ToString().Trim();
    }

    private string BuildComponentGroupPreviewText(PromptLibraryGroupSummary group)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Component group: {group.Name}");
        builder.AppendLine();
        builder.AppendLine(group.Purpose);
        builder.AppendLine();

        foreach (var component in group.Components
                     .Where(item => editor.SelectedBlockIds.Contains(item.Id))
                     .OrderBy(item => item.OrderIndex)
                     .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- {component.Name}: {component.Summary}");
        }

        return builder.ToString().Trim();
    }

    private string BuildSetupPreviewText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Session setup");
        builder.AppendLine($"Intent: {(string.IsNullOrWhiteSpace(sessionSetup.IntentCategory) ? "Not set" : sessionSetup.IntentCategory)}");
        builder.AppendLine($"Main language: {(string.IsNullOrWhiteSpace(sessionSetup.MainLanguage) ? "Not set" : sessionSetup.MainLanguage)}");
        builder.AppendLine($"Application state: {(string.IsNullOrWhiteSpace(sessionSetup.ApplicationState) ? "Not set" : sessionSetup.ApplicationState)}");
        builder.AppendLine($"Work repository: {(string.IsNullOrWhiteSpace(sessionSetup.WorkRepository) ? "Missing" : sessionSetup.WorkRepository)}");
        builder.AppendLine($"Sources: {(string.IsNullOrWhiteSpace(sessionSetup.SourceRepositories) ? "Not set" : sessionSetup.SourceRepositories)}");
        if (!string.IsNullOrWhiteSpace(sessionSetup.GuidanceNotes))
        {
            builder.AppendLine();
            builder.AppendLine("Guidance notes:");
            builder.AppendLine(sessionSetup.GuidanceNotes.Trim());
        }

        return builder.ToString().Trim();
    }

    private string BuildSessionPreviewText()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {(string.IsNullOrWhiteSpace(editor.SessionName) ? "Prompt session" : editor.SessionName)}");
        builder.AppendLine($"Project: {ResolveSelectedProjectName()}");
        builder.AppendLine($"Blueprint: {ResolveSelectedBlueprintName()}");
        builder.AppendLine($"Flow: {ResolveSelectedFlowName()}");
        builder.AppendLine($"Provider: {ResolveSelectedProviderName()}");
        builder.AppendLine($"Components: {editor.SelectedBlockIds.Count}");
        builder.AppendLine($"Inputs: {VisibleSessionAttachments.Count}");
        builder.AppendLine($"Warnings: {editor.Warnings.Count}");
        return builder.ToString().Trim();
    }

    private IEnumerable<(PromptRunNodeSummary Node, int Depth)> EnumeratePromptNodeTree(PromptRunNodeSummary rootNode)
    {
        foreach (var entry in Enumerate(rootNode, 0))
        {
            yield return entry;
        }

        IEnumerable<(PromptRunNodeSummary Node, int Depth)> Enumerate(PromptRunNodeSummary node, int depth)
        {
            yield return (node, depth);

            var children = editor.Nodes
                .Where(item => item.ParentNodeId == node.Id)
                .OrderBy(item => item.Sequence)
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var child in children)
            {
                foreach (var descendant in Enumerate(child, depth + 1))
                {
                    yield return descendant;
                }
            }
        }
    }

    private sealed record PromptPreviewModel(string Kicker, string Title, string Description, string Text);
}
