using System.Net;
using System.Net.Http;
using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;

namespace CanDoItAll.AgentFramework.Tools.Abstractions;

public enum ToolDescriptorKind
{
    Internal,
    ExternalProcess,
    ExternalHttp,
    ProviderNative
}

public abstract record ToolDescriptor(
    ToolDescriptorKind DescriptorKind,
    CapabilityIdentity Identity,
    RuntimeToolName RuntimeToolName,
    ImplementationKey ImplementationKey,
    IReadOnlySet<CapabilityTag> Tags,
    IReadOnlySet<CapabilityOperationClassification> OperationClassifications,
    CapabilitySideEffectProfile SideEffectProfile);

public sealed record InternalToolDescriptor(
    CapabilityIdentity Identity,
    RuntimeToolName RuntimeToolName,
    ImplementationKey ImplementationKey,
    IReadOnlySet<CapabilityTag> Tags,
    IReadOnlySet<CapabilityOperationClassification> OperationClassifications,
    CapabilitySideEffectProfile SideEffectProfile)
    : ToolDescriptor(
        ToolDescriptorKind.Internal,
        Identity,
        RuntimeToolName,
        ImplementationKey,
        Tags,
        OperationClassifications,
        SideEffectProfile);

public sealed record ExternalProcessToolDescriptor(
    CapabilityIdentity Identity,
    RuntimeToolName RuntimeToolName,
    ImplementationKey ImplementationKey,
    IReadOnlySet<CapabilityTag> Tags,
    IReadOnlySet<CapabilityOperationClassification> OperationClassifications,
    CapabilitySideEffectProfile SideEffectProfile,
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    TimeSpan Timeout,
    int MaxOutputBytes,
    IReadOnlySet<string> AllowedExecutableNames,
    IReadOnlySet<string> RequiredOutputProperties)
    : ToolDescriptor(
        ToolDescriptorKind.ExternalProcess,
        Identity,
        RuntimeToolName,
        ImplementationKey,
        Tags,
        OperationClassifications,
        SideEffectProfile);

public sealed record ExternalHttpToolDescriptor(
    CapabilityIdentity Identity,
    RuntimeToolName RuntimeToolName,
    ImplementationKey ImplementationKey,
    IReadOnlySet<CapabilityTag> Tags,
    IReadOnlySet<CapabilityOperationClassification> OperationClassifications,
    CapabilitySideEffectProfile SideEffectProfile,
    HttpMethod Method,
    Uri Endpoint,
    IReadOnlyDictionary<string, string> Headers,
    TimeSpan Timeout,
    int MaxResponseBytes,
    IReadOnlySet<string> RequiredOutputProperties)
    : ToolDescriptor(
        ToolDescriptorKind.ExternalHttp,
        Identity,
        RuntimeToolName,
        ImplementationKey,
        Tags,
        OperationClassifications,
        SideEffectProfile);

public sealed record ProviderNativeToolDescriptor(
    CapabilityIdentity Identity,
    RuntimeToolName RuntimeToolName,
    ImplementationKey ImplementationKey,
    IReadOnlySet<CapabilityTag> Tags,
    IReadOnlySet<CapabilityOperationClassification> OperationClassifications,
    CapabilitySideEffectProfile SideEffectProfile)
    : ToolDescriptor(
        ToolDescriptorKind.ProviderNative,
        Identity,
        RuntimeToolName,
        ImplementationKey,
        Tags,
        OperationClassifications,
        SideEffectProfile);

public sealed record ToolInvocationRequest(
    CapabilityIdentity Identity,
    ImplementationKey ImplementationKey,
    JsonElement Input,
    string CorrelationId)
{
    public static ToolInvocationRequest Create(
        CapabilityIdentity identity,
        ImplementationKey implementationKey,
        string jsonInput,
        string correlationId)
    {
        using var document = JsonDocument.Parse(jsonInput);
        return new ToolInvocationRequest(identity, implementationKey, document.RootElement.Clone(), correlationId);
    }
}

public sealed record ToolInvocationResult(
    bool IsSuccess,
    string CorrelationId,
    JsonElement Output,
    IReadOnlyList<CapabilityDiagnostic> Diagnostics)
{
    public static ToolInvocationResult Success(string correlationId, JsonElement output)
        => new(true, correlationId, output.Clone(), []);

    public static ToolInvocationResult Failure(string correlationId, IReadOnlyList<CapabilityDiagnostic> diagnostics)
        => new(false, correlationId, default, diagnostics);
}

public interface IInternalTool
{
    InternalToolDescriptor Descriptor { get; }

    ValueTask<ToolInvocationResult> InvokeAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken);
}

public interface IInternalToolRegistry
{
    void Register(IInternalTool tool);

    IInternalTool Resolve(ImplementationKey implementationKey);

    IReadOnlyList<IInternalTool> List();
}

public sealed record ExternalProcessRunRequest(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    TimeSpan Timeout,
    string StandardInput,
    int MaxOutputBytes,
    string CorrelationId);

public sealed record ExternalProcessRunResult(
    bool Started,
    int ExitCode,
    string Stdout,
    string Stderr,
    TimeSpan Elapsed);

public interface IExternalProcessRunner
{
    Task<ExternalProcessRunResult> RunAsync(
        ExternalProcessRunRequest request,
        CancellationToken cancellationToken);
}

public sealed record ExternalHttpRequest(
    HttpMethod Method,
    Uri Endpoint,
    IReadOnlyDictionary<string, string> Headers,
    string JsonBody,
    TimeSpan Timeout,
    int MaxResponseBytes,
    string CorrelationId);

public sealed record ExternalHttpResponse(
    HttpStatusCode StatusCode,
    string Body);

public interface IExternalHttpTransport
{
    Task<ExternalHttpResponse> SendAsync(
        ExternalHttpRequest request,
        CancellationToken cancellationToken);
}

public interface IExternalProcessToolInvoker
{
    Task<ToolInvocationResult> InvokeAsync(
        ExternalProcessToolDescriptor descriptor,
        ToolInvocationRequest request,
        CancellationToken cancellationToken);
}

public interface IExternalHttpToolInvoker
{
    Task<ToolInvocationResult> InvokeAsync(
        ExternalHttpToolDescriptor descriptor,
        ToolInvocationRequest request,
        CancellationToken cancellationToken);
}

public interface IToolSetupTestService
{
    Task<CapabilitySetupTestResult> TestProcessToolAsync(
        ExternalProcessToolDescriptor descriptor,
        string jsonInput,
        string correlationId,
        CancellationToken cancellationToken);

    Task<CapabilitySetupTestResult> TestHttpToolAsync(
        ExternalHttpToolDescriptor descriptor,
        string jsonInput,
        string correlationId,
        CancellationToken cancellationToken);
}
