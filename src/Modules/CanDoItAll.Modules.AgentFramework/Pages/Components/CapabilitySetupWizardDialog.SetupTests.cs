using CanDoItAll.AgentFramework.Models;
using CapabilityDiagnostic = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityDiagnostic;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class CapabilitySetupWizardDialog
{
    private async Task TestSetupAsync()
    {
        if (isBusy || editorModel.Kind is not (CapabilityKind.Tool or CapabilityKind.McpServer))
        {
            return;
        }

        isBusy = true;
        try
        {
            var errors = PrepareEditorForSetupTest();
            if (errors.Count > 0)
            {
                NotificationService.Warning("Setup test was not started", string.Join(" ", errors));
                return;
            }

            if (editorModel.Kind == CapabilityKind.Tool)
            {
                toolSetupResult = await CapabilitySetupFlowService.TestToolSetupAsync(new CapabilityToolSetupTestRequest
                {
                    Capability = editorModel,
                    JsonInput = string.IsNullOrWhiteSpace(toolState.TestInputJson) ? "{}" : toolState.TestInputJson
                });
                NotifySetupResult(toolSetupResult.IsSuccess, "Tool setup test");
            }
            else
            {
                mcpSetupResult = await CapabilitySetupFlowService.TestMcpSetupAsync(new CapabilityMcpSetupTestRequest
                {
                    Capability = editorModel
                });
                NotifySetupResult(mcpSetupResult.IsSuccess, "MCP setup test");
            }
        }
        catch (Exception exception)
        {
            NotificationService.Error("Setup test failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private IReadOnlyList<string> PrepareEditorForSetupTest()
    {
        var errors = ValidateStep(0).Concat(ValidateStep(1)).ToList();
        editorModel.Key = string.IsNullOrWhiteSpace(editorModel.Key)
            ? CapabilityConfigurationEditorSupport.NormalizeKey(editorModel.Name)
            : CapabilityConfigurationEditorSupport.NormalizeKey(editorModel.Key);
        editorModel.Tags = NormalizeTags(capabilityTags).ToList();

        if (editorModel.Kind == CapabilityKind.Tool)
        {
            errors.AddRange(CapabilityConfigurationEditorSupport.WriteTool(editorModel, toolState));
        }
        else if (editorModel.Kind == CapabilityKind.McpServer)
        {
            errors.AddRange(CapabilityConfigurationEditorSupport.WriteMcp(editorModel, mcpState));
        }

        return errors
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<CapabilityDiagnostic> CurrentSetupDiagnostics
    {
        get
        {
            if (editorModel.Kind == CapabilityKind.Tool)
            {
                return toolSetupResult?.Diagnostics ?? [];
            }

            return mcpSetupResult?.Diagnostics ?? [];
        }
    }

    private bool? CurrentSetupSucceeded
    {
        get
        {
            return editorModel.Kind switch
            {
                CapabilityKind.Tool => toolSetupResult?.IsSuccess,
                CapabilityKind.McpServer => mcpSetupResult?.IsSuccess,
                _ => null
            };
        }
    }

    private IReadOnlyList<string> CurrentMcpDiscoveredTools
        => mcpSetupResult?.DiscoveredTools.Select(tool => tool.Name.Value).ToList() ?? [];

    private void NotifySetupResult(bool isSuccess, string label)
    {
        if (isSuccess)
        {
            NotificationService.Success(label, "Setup test completed successfully.");
        }
        else
        {
            NotificationService.Warning(label, "Setup test returned diagnostics.");
        }
    }
}
