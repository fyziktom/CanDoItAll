using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Mcp;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CapabilityOperationClassification = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityOperationClassification;
using CapabilityKey = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKey;
using McpServerKey = CanDoItAll.AgentFramework.Capabilities.Abstractions.McpServerKey;
using McpToolName = CanDoItAll.AgentFramework.Capabilities.Abstractions.McpToolName;
using CapabilityTag = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityTag;
using McpRuntimeDiscoveredTool = CanDoItAll.AgentFramework.Mcp.Abstractions.DiscoveredMcpTool;
using RuntimeToolName = CanDoItAll.AgentFramework.Capabilities.Abstractions.RuntimeToolName;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class McpCapabilityBuilder(
    IMcpClientFactory mcpClientFactory,
    ISecretRuntimeResolver? secretRuntimeResolver,
    string workspaceRoot,
    WorkspaceScopeDescriptor workspaceScope,
    WorkspaceRuntimeServices workspaceRuntimeServices,
    IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
    IMafProviderCredentialService providerCredentialService,
    Func<CapabilityCatalogItem, RuntimeToolName?, IReadOnlySet<CapabilityOperationClassification>> resolveCatalogOperationClassifications,
    Func<CapabilityCatalogItem, McpCapabilityConfiguration, McpServerKey> resolveMcpServerKey,
    Func<CapabilityCatalogItem, string> resolveCapabilityDisplayName,
    Func<CapabilityCatalogItem, string> resolveCapabilityDescription,
    Func<McpCapabilityConfiguration, IReadOnlySet<McpToolName>> resolveMcpAllowedTools,
    Func<McpCapabilityConfiguration, McpApprovalMode> resolveMcpApprovalMode,
    Func<McpCapabilityConfiguration, TimeSpan> resolveMcpTimeout,
    Func<CapabilityCatalogItem, RuntimeToolName?, IReadOnlySet<CapabilityOperationClassification>, IReadOnlySet<CapabilityTag>> resolveCatalogTags,
    Func<string?, CapabilityCatalogItem, McpStdioMessageFraming> resolveMcpMessageFraming)
{
        private static readonly ProviderProfileService ProviderFeatureService = new();
        private const int MaxBrowserMcpToolResultCharacters = 12000;
        private const int MaxScreenshotResultCharacters = 2000;
        private static readonly JsonElement DefaultMcpToolInputSchema = JsonDocument
            .Parse("""{"type":"object","additionalProperties":true}""")
            .RootElement
            .Clone();

        public async Task AddMcpToolsAsync(
            RuntimeCapabilityState state,
            CapabilityCatalogItem capability,
            AgentDefinition agent,
            ProviderProfile provider,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken,
            bool suppressApprovalRequirements = false)
        {
            var configuration = MafRuntimeJson.DeserializeConfiguration<McpCapabilityConfiguration>(capability.ConfigurationJson) ?? new McpCapabilityConfiguration();
            ValidateMcpConfigurationValues(capability, configuration);
            var approvalRequired = agent.Permissions.RequiresApprovalForExternalCalls;
            var hostedApprovalRequired = !suppressApprovalRequirements
                && (approvalRequired || string.Equals(configuration.ApprovalMode, "AlwaysRequire", StringComparison.OrdinalIgnoreCase));
            var wrappedToolApprovalRequired = !suppressApprovalRequirements
                && (approvalRequired || string.Equals(configuration.ApprovalMode, "AlwaysRequire", StringComparison.OrdinalIgnoreCase));
            var allowedTools = configuration.AllowedTools is { Count: > 0 }
                ? resolveMcpAllowedTools(configuration)
                : null;

            if (IsLogicalMcpSeam(capability, configuration))
            {
                await progressCallback(
                    ExecutionState.Preparing,
                    "MCP",
                    $"Skipped logical MCP seam '{capability.Name}' because it does not expose a runnable endpoint yet.");
                return;
            }

            if (configuration.Hosted == true)
            {
                EnsureHostedMcpSupported(capability, provider);
                var hostedTool = await CreateHostedMcpToolAsync(
                    capability,
                    configuration,
                    agent,
                    approvalRequired,
                    suppressApprovalRequirements,
                    cancellationToken);
                state.Tools.Add(hostedTool);
                state.HasApprovalTools |= hostedApprovalRequired;
                await progressCallback(ExecutionState.Preparing, "MCP", $"Attached hosted MCP server '{capability.Name}' through Microsoft Agent Framework.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(configuration.Command))
            {
                await AddLocalMcpToolsAsync(
                    state,
                    capability,
                    configuration,
                    agent,
                    provider,
                    allowedTools,
                    wrappedToolApprovalRequired,
                    progressCallback,
                    cancellationToken);
                return;
            }

            var mcpClient = await CreateMcpClientAsync(capability, configuration, agent, provider, cancellationToken);
            state.AsyncDisposables.Add(mcpClient);

            var tools = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
            foreach (var tool in tools.Where(tool =>
                         allowedTools is null ||
                         McpToolName.TryCreate(tool.Name, out var toolName) && allowedTools.Contains(toolName)))
            {
                var boundedTool = CreateModelContextBoundedMcpTool(tool);
                state.Tools.Add(wrappedToolApprovalRequired ? new ApprovalRequiredAIFunction(boundedTool) : boundedTool);
            }

            state.HasApprovalTools |= wrappedToolApprovalRequired;
            await progressCallback(ExecutionState.Preparing, "MCP", $"Attached {tools.Count} MCP tool(s) from '{capability.Name}'.");
        }

        private async Task AddLocalMcpToolsAsync(
            RuntimeCapabilityState state,
            CapabilityCatalogItem capability,
            McpCapabilityConfiguration configuration,
            AgentDefinition agent,
            ProviderProfile provider,
            IReadOnlySet<McpToolName>? allowedTools,
            bool wrappedToolApprovalRequired,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken)
        {
            var mcpClient = await CreateLocalMcpRuntimeClientAsync(
                    capability,
                    configuration,
                    agent,
                    provider,
                    cancellationToken)
                .ConfigureAwait(false);
            state.AsyncDisposables.Add(new LocalMcpRuntimeClientLease(mcpClient));

            var tools = await mcpClient.ListToolsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var tool in tools.Where(tool => allowedTools is null || allowedTools.Contains(tool.Name)))
            {
                var boundedTool = CreateModelContextBoundedMcpTool(CreateLocalMcpRuntimeTool(mcpClient, tool));
                state.Tools.Add(wrappedToolApprovalRequired ? new ApprovalRequiredAIFunction(boundedTool) : boundedTool);
            }

            state.HasApprovalTools |= wrappedToolApprovalRequired;
            await progressCallback(ExecutionState.Preparing, "MCP", $"Attached {tools.Count} MCP tool(s) from '{capability.Name}'.");
        }

        private AIFunction CreateModelContextBoundedMcpTool(AIFunction tool)
        {
            return IsBrowserMcpToolName(tool.Name)
                ? new BrowserMcpModelContextBoundedAIFunction(tool, workspaceRoot, workspaceScope)
                : tool;
        }

        private static AIFunction CreateLocalMcpRuntimeTool(
            IMcpRuntimeClient mcpClient,
            McpRuntimeDiscoveredTool tool)
        {
            var innerFunction = AIFunctionFactory.Create(
                InvokeToolAsync,
                new AIFunctionFactoryOptions
                {
                    Name = tool.Name.Value,
                    Description = string.IsNullOrWhiteSpace(tool.Description)
                        ? tool.Name.Value
                        : tool.Description
                });

            return new LocalMcpRuntimeAIFunction(innerFunction, ResolveMcpToolInputSchema(tool));

            async Task<string> InvokeToolAsync(
                AIFunctionArguments arguments,
                CancellationToken cancellationToken)
            {
                var jsonArguments = SerializeMcpToolArguments(arguments);
                return await mcpClient.CallToolAsync(
                        tool.Name,
                        jsonArguments,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private static JsonElement ResolveMcpToolInputSchema(McpRuntimeDiscoveredTool tool)
            => tool.InputSchema?.Clone() ?? DefaultMcpToolInputSchema.Clone();

        private static string SerializeMcpToolArguments(AIFunctionArguments arguments)
        {
            var payload = new JsonObject();
            foreach (var (key, value) in arguments)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                payload[key] = value is null
                    ? null
                    : JsonSerializer.SerializeToNode(value, MafRuntimeJson.SerializerOptions);
            }

            return payload.ToJsonString(MafRuntimeJson.SerializerOptions);
        }

        private static bool IsBrowserMcpToolName(string? toolName)
            => !string.IsNullOrWhiteSpace(toolName) &&
               toolName.StartsWith("browser_", StringComparison.OrdinalIgnoreCase);

        private static object? CompactBrowserMcpToolResultForModelContext(
            string toolName,
            AIFunctionArguments arguments,
            object? result,
            IReadOnlyList<string>? importedArtifactPaths = null)
        {
            if (!IsBrowserMcpToolName(toolName))
            {
                return result;
            }

            var fileName = TryGetStringArgument(arguments, "filename");
            var maxCharacters = string.Equals(toolName, "browser_take_screenshot", StringComparison.OrdinalIgnoreCase)
                ? MaxScreenshotResultCharacters
                : MaxBrowserMcpToolResultCharacters;
            var text = ExtractCompactText(result, maxCharacters);
            var summary = new StringBuilder();
            summary.Append("Browser MCP tool ");
            summary.Append(toolName);
            summary.Append(" completed.");
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                summary.Append(" Saved artifact: ");
                summary.Append(fileName.Trim());
                summary.Append('.');
            }

            if (importedArtifactPaths is { Count: > 0 })
            {
                summary.Append(" Managed artifact");
                summary.Append(importedArtifactPaths.Count == 1 ? ": " : "s: ");
                summary.Append(string.Join(", ", importedArtifactPaths));
                summary.Append('.');
            }

            if (string.Equals(toolName, "browser_take_screenshot", StringComparison.OrdinalIgnoreCase))
            {
                summary.Append(" Screenshot image content was omitted from model context.");
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                summary.AppendLine();
                summary.Append(text);
            }

            return summary.ToString();
        }

        private static string? TryGetStringArgument(
            AIFunctionArguments arguments,
            string key)
        {
            if (!arguments.TryGetValue(key, out var value) || value is null)
            {
                return null;
            }

            return value switch
            {
                string text => text,
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                _ => value.ToString()
            };
        }

        private static string ExtractCompactText(
            object? value,
            int maxCharacters)
        {
            var builder = new StringBuilder(Math.Min(maxCharacters, 4096));
            AppendCompactText(value, builder, maxCharacters);
            return builder.ToString().Trim();
        }

        private static void AppendCompactText(
            object? value,
            StringBuilder builder,
            int maxCharacters)
        {
            if (value is null || builder.Length >= maxCharacters)
            {
                return;
            }

            switch (value)
            {
                case string text:
                    AppendBoundedText(builder, text, maxCharacters);
                    return;
                case JsonElement element:
                    AppendJsonElementText(element, builder, maxCharacters);
                    return;
                case System.Collections.IEnumerable enumerable when value is not string:
                    foreach (var item in enumerable)
                    {
                        AppendCompactText(item, builder, maxCharacters);
                        if (builder.Length >= maxCharacters)
                        {
                            break;
                        }
                    }

                    return;
            }

            var type = value.GetType();
            if (TryAppendStringProperty(value, type, "Text", builder, maxCharacters))
            {
                return;
            }

            TryAppendStringProperty(value, type, "Message", builder, maxCharacters);
            TryAppendStringProperty(value, type, "Error", builder, maxCharacters);

            var isError = type.GetProperty("IsError")?.GetValue(value);
            if (isError is bool isErrorValue && isErrorValue)
            {
                AppendBoundedLine(builder, "isError=true", maxCharacters);
            }

            var content = type.GetProperty("Content")?.GetValue(value);
            if (content is not null && !ReferenceEquals(content, value))
            {
                AppendCompactText(content, builder, maxCharacters);
            }
        }

        private static bool TryAppendStringProperty(
            object value,
            Type type,
            string propertyName,
            StringBuilder builder,
            int maxCharacters)
        {
            if (type.GetProperty(propertyName)?.GetValue(value) is not string text ||
                string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            AppendBoundedLine(builder, text, maxCharacters);
            return true;
        }

        private static void AppendJsonElementText(
            JsonElement element,
            StringBuilder builder,
            int maxCharacters)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    AppendBoundedLine(builder, element.GetString() ?? string.Empty, maxCharacters);
                    return;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        AppendJsonElementText(item, builder, maxCharacters);
                        if (builder.Length >= maxCharacters)
                        {
                            break;
                        }
                    }

                    return;
                case JsonValueKind.Object:
                    if (element.TryGetProperty("text", out var textElement))
                    {
                        AppendJsonElementText(textElement, builder, maxCharacters);
                    }

                    if (element.TryGetProperty("message", out var messageElement))
                    {
                        AppendJsonElementText(messageElement, builder, maxCharacters);
                    }

                    if (element.TryGetProperty("error", out var errorElement))
                    {
                        AppendJsonElementText(errorElement, builder, maxCharacters);
                    }

                    if (element.TryGetProperty("isError", out var isErrorElement) &&
                        isErrorElement.ValueKind == JsonValueKind.True)
                    {
                        AppendBoundedLine(builder, "isError=true", maxCharacters);
                    }

                    if (element.TryGetProperty("content", out var contentElement))
                    {
                        AppendJsonElementText(contentElement, builder, maxCharacters);
                    }

                    return;
                default:
                    return;
            }
        }

        private static void AppendBoundedLine(
            StringBuilder builder,
            string text,
            int maxCharacters)
        {
            if (string.IsNullOrWhiteSpace(text) || builder.Length >= maxCharacters)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            AppendBoundedText(builder, text, maxCharacters);
        }

        private static void AppendBoundedText(
            StringBuilder builder,
            string text,
            int maxCharacters)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var remaining = maxCharacters - builder.Length;
            if (remaining <= 0)
            {
                return;
            }

            if (text.Length <= remaining)
            {
                builder.Append(text);
                return;
            }

            builder.Append(text.AsSpan(0, Math.Max(0, remaining - 25)));
            builder.Append("... [truncated]");
        }

        private async Task<HostedMcpServerTool> CreateHostedMcpToolAsync(
            CapabilityCatalogItem capability,
            McpCapabilityConfiguration configuration,
            AgentDefinition agent,
            bool approvalRequired,
            bool suppressApprovalRequirements,
            CancellationToken cancellationToken)
        {
            ThrowIfPersistedSecretsConfigured(capability, configuration);
            var endpoint = ResolveConfiguredEndpoint(capability, configuration);
            var hostedTool = new HostedMcpServerTool(configuration.ServerName ?? capability.Key, endpoint)
            {
                ApprovalMode = suppressApprovalRequirements
                    ? HostedMcpServerToolApprovalMode.NeverRequire
                    : approvalRequired
                    ? HostedMcpServerToolApprovalMode.AlwaysRequire
                    : ResolveHostedApprovalMode(configuration),
                ServerDescription = capability.Description
            };

            var allowedTools = hostedTool.AllowedTools;
            foreach (var allowedTool in configuration.AllowedTools?.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!) ?? [])
            {
                allowedTools?.Add(allowedTool);
            }

            var hostedHeaders = hostedTool.Headers;
            foreach (var header in await ResolveSecretBindingsAsync(
                         configuration.HeaderBindings,
                         agent,
                         capability.Name,
                         "header",
                         SecretRuntimePurposes.AgentMcpHeader,
                         StringComparer.OrdinalIgnoreCase,
                         cancellationToken))
            {
                if (hostedHeaders is not null)
                {
                    hostedHeaders[header.Key] = header.Value;
                }
            }

            return hostedTool;
        }

        private static HostedMcpServerToolApprovalMode ResolveHostedApprovalMode(McpCapabilityConfiguration configuration)
        {
            return string.Equals(configuration.ApprovalMode, "AlwaysRequire", StringComparison.OrdinalIgnoreCase)
                ? HostedMcpServerToolApprovalMode.AlwaysRequire
                : HostedMcpServerToolApprovalMode.NeverRequire;
        }

        private static bool IsLogicalMcpSeam(CapabilityCatalogItem capability, McpCapabilityConfiguration configuration)
        {
            if (string.Equals(configuration.Transport, "logical", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.IsNullOrWhiteSpace(configuration.Endpoint)
                && string.IsNullOrWhiteSpace(configuration.Command)
                && capability.EndpointOrPath.Contains('.', StringComparison.Ordinal)
                && !Uri.TryCreate(capability.EndpointOrPath, UriKind.Absolute, out _);
        }

        private static string ResolveConfiguredEndpoint(CapabilityCatalogItem capability, McpCapabilityConfiguration configuration)
        {
            if (!string.IsNullOrWhiteSpace(configuration.Endpoint))
            {
                return configuration.Endpoint;
            }

            if (Uri.TryCreate(capability.EndpointOrPath, UriKind.Absolute, out var uri))
            {
                return uri.ToString();
            }

            throw new InvalidOperationException($"Capability '{capability.Name}' is missing a valid MCP endpoint.");
        }

        private async Task<IMcpRuntimeClient> CreateLocalMcpRuntimeClientAsync(
            CapabilityCatalogItem capability,
            McpCapabilityConfiguration configuration,
            AgentDefinition agent,
            ProviderProfile provider,
            CancellationToken cancellationToken)
        {
            ValidateLocalMcpConfiguration(capability, configuration);
            ThrowIfPersistedSecretsConfigured(capability, configuration);
            var pathResolver = new WorkspacePathResolutionService(
                workspaceRoot,
                physicalPathPolicyFactory,
                workspaceScope,
                workspaceRuntimeServices.ExternalTargetPathRegistry);
            var workingDirectoryResolution = string.IsNullOrWhiteSpace(configuration.WorkingDirectory)
                ? null
                : pathResolver.ResolveDirectoryPath(
                    configuration.WorkingDirectory,
                    allowMissing: false,
                    configuration.AllowedWorkingDirectories);
            var workingDirectory = workingDirectoryResolution?.FullPath;
            var workingDirectoryDisplayPath = workingDirectoryResolution?.RelativePath;
            ValidateLocalEnvironmentBindingIdentifiers(capability, configuration);
            ValidateProviderCredentialTargetCollisions(capability, configuration, provider);
            var environmentVariableNames = ResolveLocalEnvironmentVariableNames(
                configuration,
                provider);
            var commandExecutionService = workspaceRuntimeServices.CommandExecutionService;
            var launchDescriptor = commandExecutionService.PrepareLocalMcpServerLaunch(
                capability.Name,
                configuration.Command!,
                configuration.Arguments?.ToArray(),
                workingDirectory,
                environmentVariables: null,
                approvalRequired: string.Equals(configuration.ApprovalMode, "AlwaysRequire", StringComparison.OrdinalIgnoreCase),
                workingDirectoryDisplayPath: workingDirectoryDisplayPath,
                environmentVariableNames: environmentVariableNames);
            if (!launchDescriptor.IsAllowed)
            {
                throw new InvalidOperationException(launchDescriptor.Message);
            }

            launchDescriptor = await TryUseCachedPlaywrightMcpLaunchAsync(
                    launchDescriptor,
                    configuration,
                    cancellationToken)
                .ConfigureAwait(false);

            var environmentNameComparer = new WorkspaceCommandEnvironmentPolicy().EnvironmentNameComparer;
            var environmentVariables = (await ResolveSecretBindingsAsync(
                    configuration.EnvironmentVariableBindings,
                    agent,
                    capability.Name,
                    "environment variable",
                    SecretRuntimePurposes.AgentMcpEnvironmentVariable,
                    environmentNameComparer,
                    cancellationToken))
                .ToDictionary(
                    pair => pair.Key,
                    pair => (string?)pair.Value,
                    environmentNameComparer);
            AttachProviderCredentialForLocalMcp(capability, configuration, provider, environmentVariables);
            launchDescriptor = launchDescriptor with
            {
                EnvironmentVariables = new WorkspaceCommandEnvironmentPolicy()
                    .MergeEnvironmentVariables(environmentVariables, "local_mcp")
            };

            var classifications = resolveCatalogOperationClassifications(capability, null);
            var descriptor = McpDescriptorFactory.LocalStdio(
                key: CapabilityKey.Create(capability.Key),
                serverKey: resolveMcpServerKey(capability, configuration),
                displayName: resolveCapabilityDisplayName(capability),
                description: resolveCapabilityDescription(capability),
                command: launchDescriptor.Command,
                arguments: launchDescriptor.Arguments,
                workingDirectory: string.IsNullOrWhiteSpace(launchDescriptor.WorkingDirectory) ? "." : launchDescriptor.WorkingDirectory,
                allowedWorkingDirectories: configuration.AllowedWorkingDirectories ?? [],
                allowedTools: resolveMcpAllowedTools(configuration),
                environmentVariableBindings: new Dictionary<string, string>(),
                rawEnvironmentVariables: ToRawEnvironmentVariables(launchDescriptor.EnvironmentVariables),
                approvalMode: resolveMcpApprovalMode(configuration),
                timeout: resolveMcpTimeout(configuration),
                tags: resolveCatalogTags(capability, null, classifications),
                operationClassifications: classifications,
                messageFraming: resolveMcpMessageFraming(configuration.MessageFraming, capability));
            var client = await mcpClientFactory.CreateAsync(
                    descriptor,
                    $"agent-{agent.Id:D}-{capability.Key}",
                    cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await client.StartAsync(cancellationToken).ConfigureAwait(false);
                return client;
            }
            catch
            {
                await client.StopAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }

        private async Task<McpClient> CreateMcpClientAsync(
            CapabilityCatalogItem capability,
            McpCapabilityConfiguration configuration,
            AgentDefinition agent,
            ProviderProfile provider,
            CancellationToken cancellationToken)
        {
            var endpoint = ResolveConfiguredEndpoint(capability, configuration);
            var httpTransportOptions = new HttpClientTransportOptions
            {
                Endpoint = new Uri(endpoint, UriKind.Absolute),
                Name = configuration.ServerName ?? capability.Name
            };

            var additionalHeaders = httpTransportOptions.AdditionalHeaders;
            ThrowIfPersistedSecretsConfigured(capability, configuration);
            foreach (var header in await ResolveSecretBindingsAsync(
                         configuration.HeaderBindings,
                         agent,
                         capability.Name,
                         "header",
                         SecretRuntimePurposes.AgentMcpHeader,
                         StringComparer.OrdinalIgnoreCase,
                         cancellationToken))
            {
                if (additionalHeaders is not null)
                {
                    additionalHeaders[header.Key] = header.Value;
                }
            }

            var httpTransport = new HttpClientTransport(httpTransportOptions);
            return await McpClient.CreateAsync(httpTransport, cancellationToken: cancellationToken);
        }

        private static void ValidateLocalMcpConfiguration(CapabilityCatalogItem capability, McpCapabilityConfiguration configuration)
        {
            if (configuration.AllowedTools is null || configuration.AllowedTools.Count == 0)
            {
                throw new InvalidOperationException($"Local MCP capability '{capability.Name}' must declare AllowedTools before it can launch a stdio server.");
            }

            if (configuration.AllowedTools.Any(tool => !McpToolName.TryCreate(tool, out _)))
            {
                throw new InvalidOperationException(
                    $"Local MCP capability '{capability.Name}' contains an invalid AllowedTools entry.");
            }

            if (!LocalMcpCommandPolicy.IsAllowed(configuration.Command))
            {
                throw new InvalidOperationException(
                    $"Local MCP capability '{capability.Name}' uses a command outside the approved interpreter policy. Allowed commands: {LocalMcpCommandPolicy.DescribeAllowedCommands()}.");
            }

            if (string.IsNullOrWhiteSpace(configuration.ApprovalMode))
            {
                throw new InvalidOperationException($"Local MCP capability '{capability.Name}' must declare ApprovalMode before it can launch a stdio server.");
            }
        }

        private static void ValidateMcpConfigurationValues(
            CapabilityCatalogItem capability,
            McpCapabilityConfiguration configuration)
        {
            if (!string.IsNullOrWhiteSpace(configuration.ApprovalMode) &&
                !Enum.GetNames<McpApprovalMode>()
                    .Contains(configuration.ApprovalMode, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"MCP capability '{capability.Name}' has an unsupported ApprovalMode value.");
            }

            if (configuration.AllowedTools?.Any(tool => !McpToolName.TryCreate(tool, out _)) == true)
            {
                throw new InvalidOperationException(
                    $"MCP capability '{capability.Name}' contains an invalid AllowedTools entry.");
            }
        }

        private static IReadOnlyDictionary<string, string> ToRawEnvironmentVariables(IReadOnlyDictionary<string, string?> environmentVariables)
        {
            var comparer = new WorkspaceCommandEnvironmentPolicy().EnvironmentNameComparer;
            return environmentVariables
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value is not null)
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value!,
                    comparer);
        }

        private async Task<WorkspaceLocalMcpLaunchDescriptor> TryUseCachedPlaywrightMcpLaunchAsync(
            WorkspaceLocalMcpLaunchDescriptor launchDescriptor,
            McpCapabilityConfiguration configuration,
            CancellationToken cancellationToken)
        {
            var processHost = workspaceRuntimeServices.ProcessHost as IWorkspaceLongRunningProcessHost
                ?? throw new InvalidOperationException(
                    "The configured workspace process host does not support owned MCP sessions.");
            var resolution = await PlaywrightMcpLaunchResolver.TryResolveAsync(
                    workspaceRoot,
                    launchDescriptor.Command,
                    configuration.Arguments ?? [],
                    processHost,
                    cancellationToken)
                .ConfigureAwait(false);
            if (resolution is null)
            {
                return launchDescriptor;
            }

            return launchDescriptor with
            {
                Command = resolution.Command,
                Arguments = resolution.Arguments
            };
        }

        private static void ThrowIfPersistedSecretsConfigured(
            CapabilityCatalogItem capability,
            McpCapabilityConfiguration configuration)
        {
            if (configuration.EnvironmentVariables?.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Local MCP capability '{capability.Name}' persists raw environmentVariables. Use environmentVariableBindings to resolve secrets at runtime instead.");
            }

            if (configuration.Headers?.Count > 0)
            {
                throw new InvalidOperationException(
                    $"MCP capability '{capability.Name}' persists raw headers. Use headerBindings to resolve secrets at runtime instead.");
            }

            if (SensitiveTextRedactor.ContainsSecretBearingArguments(configuration.Arguments ?? []))
            {
                throw new InvalidOperationException(
                    $"Local MCP capability '{capability.Name}' persists a secret-bearing command argument. Use an environment-variable or stored-secret binding instead.");
            }
        }

        private async Task<IReadOnlyDictionary<string, string>> ResolveSecretBindingsAsync(
            IDictionary<string, string>? bindings,
            AgentDefinition agent,
            string capabilityName,
            string bindingKind,
            string purpose,
            StringComparer nameComparer,
            CancellationToken cancellationToken)
        {
            var resolved = new Dictionary<string, string>(nameComparer);
            if (bindings is null)
            {
                return resolved;
            }

            foreach (var binding in bindings)
            {
                if (string.IsNullOrWhiteSpace(binding.Key))
                {
                    throw new InvalidOperationException($"MCP capability '{capabilityName}' contains a {bindingKind} binding with an empty target name.");
                }

                if (string.IsNullOrWhiteSpace(binding.Value))
                {
                    throw new InvalidOperationException(
                        $"MCP capability '{capabilityName}' contains a {bindingKind} binding for '{binding.Key}' without an environment variable name.");
                }

                ValidateBindingTargetName(purpose, binding.Key, capabilityName, bindingKind);
                if (resolved.ContainsKey(binding.Key))
                {
                    throw new InvalidOperationException(
                        $"MCP capability '{capabilityName}' contains ambiguous {bindingKind} target names for this host.");
                }

                if (TryParseSecretBinding(binding.Value, out var secretId))
                {
                    var secretValue = await ResolveAllowedAgentSecretAsync(
                        agent,
                        secretId,
                        purpose,
                        capabilityName,
                        binding.Key,
                        cancellationToken);
                    if (string.IsNullOrWhiteSpace(secretValue))
                    {
                        throw new InvalidOperationException(
                            $"MCP capability '{capabilityName}' requires stored secret '{secretId:D}' to resolve {bindingKind} '{binding.Key}', but the secret is missing or empty.");
                    }

                    resolved.Add(binding.Key, secretValue);
                    continue;
                }

                if (!McpEnvironmentVariableNamePolicy.IsValid(binding.Value))
                {
                    throw new InvalidOperationException(
                        $"MCP capability '{capabilityName}' contains a {bindingKind} binding with an invalid environment variable source name.");
                }

                var resolvedValue = AgentProviderEnvironmentCredential.Resolve(binding.Value);
                if (string.IsNullOrWhiteSpace(resolvedValue))
                {
                    throw new InvalidOperationException(
                        $"MCP capability '{capabilityName}' requires environment variable '{binding.Value}' to resolve {bindingKind} '{binding.Key}'.");
                }

                resolved.Add(binding.Key, resolvedValue);
            }

            return resolved;
        }

        private static void ValidateBindingTargetName(
            string purpose,
            string targetName,
            string capabilityName,
            string bindingKind)
        {
            var isValid = purpose switch
            {
                SecretRuntimePurposes.AgentMcpEnvironmentVariable =>
                    McpEnvironmentVariableNamePolicy.IsValid(targetName),
                SecretRuntimePurposes.AgentMcpHeader =>
                    McpEnvironmentVariableNamePolicy.IsValidHttpHeaderName(targetName),
                _ => false
            };
            if (!isValid)
            {
                throw new InvalidOperationException(
                    $"MCP capability '{capabilityName}' contains a {bindingKind} binding with an invalid target name.");
            }
        }

        private async Task<string?> ResolveAllowedAgentSecretAsync(
            AgentDefinition agent,
            Guid secretId,
            string purpose,
            string capabilityName,
            string bindingName,
            CancellationToken cancellationToken)
        {
            var resolver = secretRuntimeResolver;
            if (resolver is null)
            {
                throw new InvalidOperationException(
                    $"MCP capability '{capabilityName}' requires stored secret '{secretId:D}' for binding '{bindingName}', but the secret runtime resolver is not registered.");
            }

            var allowedSecretIds = agent.Permissions.NormalizedAllowedSecrets
                .Select(item => item.SecretId)
                .ToHashSet();
            return await resolver.ResolveValueAsync(
                new SecretRuntimeRequest(
                    secretId,
                    purpose,
                    allowedSecretIds,
                    ConsumerType: SecretRuntimeConsumerTypes.AgentMcp,
                    ConsumerId: SecretRuntimeConsumerIds.AgentMcp(agent.Id, capabilityName, bindingName)),
                cancellationToken);
        }

        private static bool TryParseSecretBinding(string value, out Guid secretId)
        {
            secretId = Guid.Empty;
            const string prefix = "secret:";
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim();
            return Guid.TryParse(normalized, out secretId) ||
                   (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    Guid.TryParse(normalized[prefix.Length..], out secretId));
        }

        private void AttachProviderCredentialForLocalMcp(
            CapabilityCatalogItem capability,
            McpCapabilityConfiguration configuration,
            ProviderProfile provider,
            IDictionary<string, string?> environmentVariables)
        {
            if (provider.Kind != ProviderKind.OpenAi ||
                !PlaywrightMcpLaunchResolver.IsPinnedVisionLaunch(
                    configuration.Command ?? string.Empty,
                    configuration.Arguments ?? []))
            {
                return;
            }

            EnsureValidCredentialEnvironmentVariableName(provider.ApiKeyEnvironmentVariable, capability.Name);
            EnsureValidCredentialEnvironmentVariableName(MafProviderRuntimeSettings.OpenAiApiKeyEnvironmentVariable, capability.Name);

            var credential = providerCredentialService.Resolve(provider);
            if (!credential.IsResolved)
            {
                throw new InvalidOperationException(
                    $"Local MCP capability '{capability.Name}' requires provider credential for '{provider.Name}' to enable Playwright vision. {credential.FailureMessage}");
            }

            var comparer = new WorkspaceCommandEnvironmentPolicy().EnvironmentNameComparer;
            foreach (var targetName in new[]
                     {
                         provider.ApiKeyEnvironmentVariable,
                         MafProviderRuntimeSettings.OpenAiApiKeyEnvironmentVariable
                     }.Distinct(comparer))
            {
                LocalMcpCredentialEnvironmentPolicy.Add(
                    environmentVariables,
                    targetName,
                    credential.ApiKey);
            }
        }

        private static void ValidateProviderCredentialTargetCollisions(
            CapabilityCatalogItem capability,
            McpCapabilityConfiguration configuration,
            ProviderProfile provider)
        {
            if (provider.Kind != ProviderKind.OpenAi ||
                !PlaywrightMcpLaunchResolver.IsPinnedVisionLaunch(
                    configuration.Command ?? string.Empty,
                    configuration.Arguments ?? []))
            {
                return;
            }

            var comparer = new WorkspaceCommandEnvironmentPolicy().EnvironmentNameComparer;
            var explicitTargets = (configuration.EnvironmentVariableBindings is { } bindings
                    ? bindings.Keys
                    : Enumerable.Empty<string>())
                .ToHashSet(comparer);
            foreach (var targetName in new[]
                     {
                         provider.ApiKeyEnvironmentVariable,
                         MafProviderRuntimeSettings.OpenAiApiKeyEnvironmentVariable
                     }.Distinct(comparer))
            {
                EnsureValidCredentialEnvironmentVariableName(targetName, capability.Name);
                if (explicitTargets.Contains(targetName))
                {
                    throw new InvalidOperationException(
                        $"Local MCP capability '{capability.Name}' explicitly binds an environment variable reserved for automatic provider credential injection.");
                }
            }
        }

        private static void EnsureValidCredentialEnvironmentVariableName(
            string variableName,
            string capabilityName)
        {
            if (!McpEnvironmentVariableNamePolicy.IsValid(variableName))
            {
                throw new InvalidOperationException(
                    $"Local MCP capability '{capabilityName}' has an invalid provider credential environment variable target name.");
            }
        }

        private static void ValidateLocalEnvironmentBindingIdentifiers(
            CapabilityCatalogItem capability,
            McpCapabilityConfiguration configuration)
        {
            var targetNames = new HashSet<string>(
                new WorkspaceCommandEnvironmentPolicy().EnvironmentNameComparer);
            foreach (var binding in configuration.EnvironmentVariableBindings ?? [])
            {
                ValidateBindingTargetName(
                    SecretRuntimePurposes.AgentMcpEnvironmentVariable,
                    binding.Key,
                    capability.Name,
                    "environment variable");
                if (!targetNames.Add(binding.Key))
                {
                    throw new InvalidOperationException(
                        $"MCP capability '{capability.Name}' contains ambiguous environment variable target names for this host.");
                }

                if (!TryParseSecretBinding(binding.Value, out _) &&
                    !McpEnvironmentVariableNamePolicy.IsValid(binding.Value))
                {
                    throw new InvalidOperationException(
                        $"MCP capability '{capability.Name}' contains an environment variable binding with an invalid source name.");
                }
            }
        }

        private static IReadOnlyCollection<string> ResolveLocalEnvironmentVariableNames(
            McpCapabilityConfiguration configuration,
            ProviderProfile provider)
        {
            var comparer = new WorkspaceCommandEnvironmentPolicy().EnvironmentNameComparer;
            var names = (configuration.EnvironmentVariableBindings is { } bindings
                    ? bindings.Keys
                    : Enumerable.Empty<string>())
                .ToHashSet(comparer);
            if (provider.Kind == ProviderKind.OpenAi &&
                PlaywrightMcpLaunchResolver.IsPinnedVisionLaunch(
                    configuration.Command ?? string.Empty,
                    configuration.Arguments ?? []))
            {
                EnsureValidCredentialEnvironmentVariableName(provider.ApiKeyEnvironmentVariable, "Playwright MCP");
                EnsureValidCredentialEnvironmentVariableName(MafProviderRuntimeSettings.OpenAiApiKeyEnvironmentVariable, "Playwright MCP");
                names.Add(provider.ApiKeyEnvironmentVariable);
                names.Add(MafProviderRuntimeSettings.OpenAiApiKeyEnvironmentVariable);
            }

            return names;
        }

        private sealed class BrowserMcpModelContextBoundedAIFunction(
            AIFunction innerFunction,
            string workspaceRoot,
            WorkspaceScopeDescriptor workspaceScope) : DelegatingAIFunction(innerFunction)
        {
            protected override async ValueTask<object?> InvokeCoreAsync(
                AIFunctionArguments arguments,
                CancellationToken cancellationToken)
            {
                var fileName = TryGetStringArgument(arguments, "filename");
                BrowserMcpArtifactPathService.EnsureWritableArtifactDirectories(workspaceRoot, workspaceScope, fileName);

                var result = await base.InvokeCoreAsync(arguments, cancellationToken).ConfigureAwait(false);
                var importResult = BrowserMcpArtifactPathService.TryImportAfterInvocation(
                    workspaceRoot,
                    workspaceScope,
                    fileName,
                    WorkspaceExecutionAuditContext.Current?.ProcessRunId);

                return CompactBrowserMcpToolResultForModelContext(
                    Name,
                    arguments,
                    result,
                    importResult.ImportedRelativePaths);
            }
        }

        private sealed class LocalMcpRuntimeAIFunction(
            AIFunction innerFunction,
            JsonElement jsonSchema) : DelegatingAIFunction(innerFunction)
        {
            public override JsonElement JsonSchema { get; } = jsonSchema;
        }

        private sealed class LocalMcpRuntimeClientLease(IMcpRuntimeClient client) : IAsyncDisposable
        {
            public async ValueTask DisposeAsync()
            {
                await client.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        private static void EnsureHostedMcpSupported(CapabilityCatalogItem capability, ProviderProfile provider)
        {
            var support = ProviderFeatureService.GetNativeToolSupport(provider, ProviderNativeToolFamily.HostedMcpServer);
            if (support.IsSupported)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Capability '{capability.Name}' cannot attach provider-native hosted MCP to provider '{provider.Name}'. {support.Summary} {support.Remediation}");
        }
}

internal static class LocalMcpCredentialEnvironmentPolicy
{
    public static void Add(
        IDictionary<string, string?> environmentVariables,
        string variableName,
        string value)
    {
        ArgumentNullException.ThrowIfNull(environmentVariables);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!McpEnvironmentVariableNamePolicy.IsValid(variableName))
        {
            throw new InvalidOperationException(
                "Provider credential injection requires a valid environment variable target name.");
        }

        if (!environmentVariables.TryAdd(variableName, value))
        {
            throw new InvalidOperationException(
                "Provider credential injection cannot overwrite an existing environment variable target.");
        }
    }
}
