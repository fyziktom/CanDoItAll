# Interface Contract Sketches

These sketches are not final code. They define the target seam Codex should preserve.

## Identifiers

```csharp
public readonly record struct PluginId(string Value);
public readonly record struct PluginConnectionId(Guid Value);
public readonly record struct PluginPackageId(string Value);
```

## Manifest

```csharp
[Flags]
public enum PluginCapabilityKind
{
    None = 0,
    WorkflowExecutor = 1 << 0,
    SettingsRenderer = 1 << 1,
    SecretReference = 1 << 2,
    WorkspaceFiles = 1 << 3,
    Storage = 1 << 4,
    ProjectStructure = 1 << 5,
    HttpClient = 1 << 6,
    OAuth2 = 1 << 7
}

public sealed record PluginDescriptor(
    PluginId Id,
    string DisplayName,
    string Description,
    string Version,
    string Vendor,
    PluginSourceKind SourceKind,
    PluginTrustLevel TrustLevel,
    string MinAppVersion,
    PluginCapabilityKind Capabilities,
    IReadOnlyList<PluginWorkflowExecutorDescriptor> WorkflowExecutors,
    PluginSettingsDescriptor Settings,
    IReadOnlyList<PluginConnectionDescriptor> Connections);
```

## Plugin Contract

```csharp
public interface ICanDoItAllPlugin
{
    PluginDescriptor Descriptor { get; }
}

public interface IBundledPlugin : ICanDoItAllPlugin
{
    void ConfigurePluginServices(IPluginServiceRegistry services);
}
```

## Workflow Executor Contract

```csharp
public interface IPluginWorkflowExecutor
{
    PluginWorkflowExecutorDescriptor Descriptor { get; }

    ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        PluginWorkflowExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken);
}

public sealed record PluginWorkflowExecutionContext(
    PluginDescriptor Plugin,
    PluginConnectionSnapshot? Connection,
    WorkflowDefinition Workflow,
    WorkflowNode Node,
    string NodeSettingsJson,
    IPluginCapabilityContext Capabilities);
```

The Plugins module adapts `IPluginWorkflowExecutor` to the existing `IWorkflowExecutor`. Plugins should not need to implement `IWorkflowExecutor` directly unless a later SDK decision deliberately allows that dependency.

## Capability Context

```csharp
public interface IPluginCapabilityContext
{
    IPluginSecretBroker Secrets { get; }
    IPluginWorkspaceFiles WorkspaceFiles { get; }
    IPluginProjectStructureGateway ProjectStructure { get; }
    IPluginHttpClientFactory Http { get; }
    IPluginOAuth2Broker? OAuth2 { get; }
    IPluginExecutionEvents Events { get; }
}
```

## Secret Broker

```csharp
public interface IPluginSecretBroker
{
    ValueTask<string> ResolveSecretAsync(
        PluginSecretReference reference,
        PluginSecretResolutionPurpose purpose,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<PluginSecretBindingSummary>> ListAllowedSecretsAsync(
        PluginConnectionId connectionId,
        CancellationToken cancellationToken);
}
```

## Settings Renderer

```csharp
public interface IPluginSettingsRendererDescriptor
{
    string RendererKey { get; }
    Type ComponentType { get; }
    PluginRendererTrustLevel TrustLevel { get; }
}

public interface IPluginSettingsRendererRegistry
{
    IReadOnlyList<IPluginSettingsRendererDescriptor> ListRenderers();

    bool TryResolve(string rendererKey, out IPluginSettingsRendererDescriptor descriptor);
}
```

## OAuth2 Broker Extension Point

```csharp
public interface IPluginOAuth2Broker
{
    ValueTask<PluginOAuth2AuthorizationStartResult> StartAuthorizationAsync(
        PluginOAuth2AuthorizationStartRequest request,
        CancellationToken cancellationToken);

    ValueTask<PluginOAuth2TokenLease> AcquireTokenAsync(
        PluginOAuth2TokenRequest request,
        CancellationToken cancellationToken);
}
```

## Do Not Add

```csharp
public interface IUnsafePluginRuntimeContext
{
    IServiceProvider Services { get; }
}
```

The plugin API must not include an arbitrary service-provider escape hatch.
