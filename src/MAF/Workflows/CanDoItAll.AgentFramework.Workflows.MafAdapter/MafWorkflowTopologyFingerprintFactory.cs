using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafWorkflowTopologyFingerprintFactory
{
    public static WorkflowCompilerContractVersion CompilerContractVersion { get; } = new(1);

    public static WorkflowTopologyFingerprint Create(
        WorkflowDefinition definition,
        IReadOnlyDictionary<WorkflowNodeId, MafCompiledNodeBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(bindings);

        var canonical = new StringBuilder();
        Append(canonical, CompilerContractVersion.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(canonical, definition.VersionId.ToString());
        Append(canonical, definition.Graph.StartNodeId.Value);
        Append(canonical, definition.SourceHash);

        foreach (var node in definition.Graph.Nodes.OrderBy(node => node.Id.Value, StringComparer.Ordinal))
        {
            if (!bindings.TryGetValue(node.Id, out var binding))
            {
                throw new InvalidOperationException($"MAF workflow topology is missing a compiled binding for node '{node.Id}'.");
            }

            Append(canonical, node.Id.Value);
            Append(canonical, node.Kind.ToString());
            AppendNodeSettings(canonical, node.Settings);
            foreach (var port in node.Ports.OrderBy(port => port.Id.Value, StringComparer.Ordinal))
            {
                Append(canonical, port.Id.Value);
                Append(canonical, port.Name);
                Append(canonical, port.Direction.ToString());
                AppendShape(canonical, port.Shape);
                Append(canonical, port.Required.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            Append(canonical, binding.Entry.Id);
            Append(canonical, binding.Exit.Id);
            foreach (var component in binding.Components
                .OrderBy(component => component.Role)
                .ThenBy(component => component.Binding.Id, StringComparer.Ordinal))
            {
                Append(canonical, component.Role.ToString());
                Append(canonical, component.Binding.Id);
            }

            foreach (var edge in binding.InternalEdges
                .OrderBy(edge => edge.Source.Id, StringComparer.Ordinal)
                .ThenBy(edge => edge.Target.Id, StringComparer.Ordinal))
            {
                Append(canonical, edge.Source.Id);
                Append(canonical, edge.Target.Id);
            }
        }

        foreach (var edge in definition.Graph.Edges
            .OrderBy(edge => edge.Id.Value, StringComparer.Ordinal))
        {
            Append(canonical, edge.Id.Value);
            Append(canonical, edge.SourceNodeId.Value);
            Append(canonical, edge.SourcePortId?.Value ?? string.Empty);
            Append(canonical, edge.TargetNodeId.Value);
            Append(canonical, edge.TargetPortId?.Value ?? string.Empty);
            Append(canonical, edge.Kind.ToString());
            Append(canonical, edge.ConditionExpression);
            Append(canonical, edge.Routing.Kind.ToString());
            Append(canonical, edge.Routing.Label);
            Append(canonical, edge.Routing.JsonPath);
            Append(canonical, edge.Routing.Operator.ToString());
            Append(canonical, edge.Routing.ExpectedValueJson);
            Append(canonical, edge.Routing.ExpectedValueKind.ToString());
            Append(canonical, edge.Routing.CaseSensitive.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(canonical, edge.Routing.FanOutTargetIndex?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
            Append(canonical, edge.Routing.RoutingLanguage);
        }

        return WorkflowTopologyFingerprint.Create(canonical.ToString());
    }

    private static void AppendNodeSettings(StringBuilder builder, WorkflowNodeSettings settings)
    {
        Append(builder, settings.ComponentId?.ToString() ?? string.Empty);
        Append(builder, settings.AgentId?.ToString("D") ?? string.Empty);
        Append(builder, settings.SubworkflowId?.ToString() ?? string.Empty);
        Append(builder, settings.ExternalRequestKind?.ToString() ?? string.Empty);
        Append(builder, settings.ProviderProfileId?.ToString("D") ?? string.Empty);
        Append(builder, settings.Model);
        Append(builder, settings.ExecutorId?.Value ?? string.Empty);
        Append(builder, Hash(settings.Instructions));
        Append(builder, Hash(settings.ExecutorSettingsJson));
        AppendShape(builder, settings.InputShape);
        AppendShape(builder, settings.ResultShape);
        Append(builder, settings.ExecutionPolicy?.TimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        Append(builder, settings.ExecutionPolicy?.MaxRetryAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        Append(builder, settings.ExecutionPolicy?.RetryDelayMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        Append(builder, settings.ExecutionPolicy?.CaptureOutputArtifact.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
    }

    private static void AppendShape(StringBuilder builder, WorkflowValueShape? shape)
    {
        Append(builder, shape?.Kind.ToString() ?? string.Empty);
        Append(builder, shape is null ? string.Empty : Hash(shape.SchemaJson));
        Append(builder, shape?.Description ?? string.Empty);
    }

    private static string Hash(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void Append(StringBuilder builder, string value)
    {
        builder.Append(value.Length);
        builder.Append(':');
        builder.Append(value);
        builder.Append('|');
    }
}
