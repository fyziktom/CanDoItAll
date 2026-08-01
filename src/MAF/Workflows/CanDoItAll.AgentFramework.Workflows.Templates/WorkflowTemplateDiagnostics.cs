namespace CanDoItAll.AgentFramework.Workflows.Templates;

public enum WorkflowTemplateFailureKind
{
    ManifestLoadFailed,
    WorkflowLoadFailed,
    MissingRequiredField,
    DuplicateWorkflowKey,
    InputParameterInvalid,
    GraphMaterializationFailed,
    DescriptorValidationFailed,
    SemanticValidationFailed,
    PreviewSimulationInvalid
}

public sealed record WorkflowTemplateDiagnostic(
    string TemplateFilePath,
    string TemplateKey,
    string WorkflowKey,
    string YamlPath,
    string NodeId,
    string ExecutorId,
    string RepairHint,
    string RedactedTechnicalDetail);

public sealed class WorkflowTemplatePackException : InvalidOperationException
{
    public WorkflowTemplatePackException(
        WorkflowTemplateFailureKind failureKind,
        string message,
        WorkflowTemplateDiagnostic diagnostic,
        Exception? innerException = null)
        : base(FormatMessage(message, diagnostic), innerException)
    {
        FailureKind = failureKind;
        Diagnostic = diagnostic;
    }

    public WorkflowTemplateFailureKind FailureKind { get; }

    public WorkflowTemplateDiagnostic Diagnostic { get; }

    private static string FormatMessage(
        string message,
        WorkflowTemplateDiagnostic diagnostic)
    {
        var parts = new List<string> { message };
        AddPart(parts, "template file", diagnostic.TemplateFilePath);
        AddPart(parts, "template key", diagnostic.TemplateKey);
        AddPart(parts, "workflow key", diagnostic.WorkflowKey);
        AddPart(parts, "YAML path", diagnostic.YamlPath);
        AddPart(parts, "node id", diagnostic.NodeId);
        AddPart(parts, "executor id", diagnostic.ExecutorId);
        AddPart(parts, "repair hint", diagnostic.RepairHint);
        AddPart(parts, "detail", diagnostic.RedactedTechnicalDetail);
        return string.Join(" ", parts);
    }

    private static void AddPart(List<string> parts, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{label}: {value}.");
        }
    }
}

internal readonly record struct WorkflowTemplateContext(
    string TemplateFilePath,
    string TemplateKey,
    string YamlPath,
    string NodeId = "",
    string ExecutorId = "")
{
    public string WorkflowKey => TemplateKey;

    public WorkflowTemplateContext WithYamlPath(string yamlPath)
        => this with { YamlPath = yamlPath };

    public WorkflowTemplateContext WithNode(string nodeId, string yamlPath)
        => this with { NodeId = nodeId, YamlPath = yamlPath };

    public WorkflowTemplateContext WithExecutor(string nodeId, string executorId, string yamlPath)
        => this with { NodeId = nodeId, ExecutorId = executorId, YamlPath = yamlPath };

    public WorkflowTemplateDiagnostic ToDiagnostic(
        string repairHint,
        string redactedTechnicalDetail = "")
        => new(
            TemplateFilePath,
            TemplateKey,
            WorkflowKey,
            YamlPath,
            NodeId,
            ExecutorId,
            repairHint,
            redactedTechnicalDetail);
}

internal static class WorkflowTemplateDiagnostics
{
    public static WorkflowTemplatePackException CreateException(
        WorkflowTemplateFailureKind failureKind,
        string message,
        WorkflowTemplateContext context,
        string repairHint,
        Exception? innerException = null)
        => new(
            failureKind,
            message,
            context.ToDiagnostic(repairHint, innerException?.Message ?? string.Empty),
            innerException);

    public static string Require(
        string? value,
        string fieldName,
        WorkflowTemplateContext context,
        string repairHint)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        throw CreateException(
            WorkflowTemplateFailureKind.MissingRequiredField,
            $"Workflow template is missing required field '{fieldName}'.",
            context,
            repairHint);
    }
}
