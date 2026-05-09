using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private sealed class McpCapabilityBuilder(MafAgentRuntime owner)
    {
        private const string PlaywrightMcpPackagePrefix = "@playwright/mcp";
        private const string PlaywrightCapsArgument = "--caps";
        private const string PlaywrightVisionCapability = "vision";
        private const int MaxBrowserMcpToolResultCharacters = 12000;
        private const int MaxScreenshotResultCharacters = 2000;

        public async Task AddMcpToolsAsync(
            RuntimeCapabilityState state,
            CapabilityCatalogItem capability,
            AgentDefinition agent,
            ProviderProfile provider,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken,
            bool suppressApprovalRequirements = false)
        {
            var configuration = DeserializeConfiguration<McpCapabilityConfiguration>(capability.ConfigurationJson) ?? new McpCapabilityConfiguration();
            var approvalRequired = agent.Permissions.RequiresApprovalForExternalCalls;
            var hostedApprovalRequired = !suppressApprovalRequirements
                && (approvalRequired || string.Equals(configuration.ApprovalMode, "AlwaysRequire", StringComparison.OrdinalIgnoreCase));
            var wrappedToolApprovalRequired = !suppressApprovalRequirements
                && (approvalRequired || string.Equals(configuration.ApprovalMode, "AlwaysRequire", StringComparison.OrdinalIgnoreCase));
            var allowedTools = configuration.AllowedTools?.Where(item => !string.IsNullOrWhiteSpace(item)).ToHashSet(StringComparer.OrdinalIgnoreCase);

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
                var hostedTool = CreateHostedMcpTool(capability, configuration, approvalRequired, suppressApprovalRequirements);
                state.Tools.Add(hostedTool);
                state.HasApprovalTools |= hostedApprovalRequired;
                await progressCallback(ExecutionState.Preparing, "MCP", $"Attached hosted MCP server '{capability.Name}' through Microsoft Agent Framework.");
                return;
            }

            var mcpClient = await CreateMcpClientAsync(capability, configuration, provider, cancellationToken);
            state.AsyncDisposables.Add(mcpClient);

            var tools = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
            foreach (var tool in tools.Where(tool => allowedTools is null || allowedTools.Contains(tool.Name)))
            {
                var boundedTool = CreateModelContextBoundedMcpTool(tool);
                state.Tools.Add(wrappedToolApprovalRequired ? new ApprovalRequiredAIFunction(boundedTool) : boundedTool);
            }

            state.HasApprovalTools |= wrappedToolApprovalRequired;
            await progressCallback(ExecutionState.Preparing, "MCP", $"Attached {tools.Count} MCP tool(s) from '{capability.Name}'.");
        }

        private AIFunction CreateModelContextBoundedMcpTool(AIFunction tool)
        {
            return IsBrowserMcpToolName(tool.Name)
                ? new BrowserMcpModelContextBoundedAIFunction(tool, owner.workspaceRoot, owner.workspaceScope)
                : tool;
        }

        private static bool IsBrowserMcpToolName(string? toolName)
            => !string.IsNullOrWhiteSpace(toolName) &&
               toolName.StartsWith("browser_", StringComparison.OrdinalIgnoreCase);

        private static object? CompactBrowserMcpToolResultForModelContext(
            string toolName,
            AIFunctionArguments arguments,
            object? result)
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

        private static HostedMcpServerTool CreateHostedMcpTool(
            CapabilityCatalogItem capability,
            McpCapabilityConfiguration configuration,
            bool approvalRequired,
            bool suppressApprovalRequirements)
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
            foreach (var header in ResolveSecretBindings(configuration.HeaderBindings, capability.Name, "header"))
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

        private async Task<McpClient> CreateMcpClientAsync(
            CapabilityCatalogItem capability,
            McpCapabilityConfiguration configuration,
            ProviderProfile provider,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(configuration.Command))
            {
                ValidateLocalMcpConfiguration(capability, configuration);
                ThrowIfPersistedSecretsConfigured(capability, configuration);
                var workingDirectory = string.IsNullOrWhiteSpace(configuration.WorkingDirectory)
                    ? null
                    : owner.ResolvePathFromWorkspace(
                        configuration.WorkingDirectory,
                        allowExternal: (configuration.AllowedWorkingDirectories?.Count ?? 0) > 0,
                        allowedExternalRoots: configuration.AllowedWorkingDirectories);
                var commandExecutionService = owner.services.GetService(typeof(IWorkspaceCommandExecutionService)) as IWorkspaceCommandExecutionService
                    ?? new WorkspaceCommandExecutionService(owner.workspaceRoot, new LocalWorkspaceProcessHost(), owner.workspaceScope);
                var environmentVariables = ResolveSecretBindings(configuration.EnvironmentVariableBindings, capability.Name, "environment variable")
                    .ToDictionary(
                        pair => pair.Key,
                        pair => (string?)pair.Value,
                        StringComparer.OrdinalIgnoreCase);
                AttachProviderCredentialForLocalMcp(capability, configuration, provider, environmentVariables);
                var launchDescriptor = commandExecutionService.PrepareLocalMcpServerLaunch(
                    capability.Name,
                    configuration.Command,
                    configuration.Arguments?.ToArray(),
                    workingDirectory,
                    environmentVariables,
                    approvalRequired: string.Equals(configuration.ApprovalMode, "AlwaysRequire", StringComparison.OrdinalIgnoreCase));
                var transport = new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = configuration.ServerName ?? capability.Name,
                    Command = launchDescriptor.Command,
                    Arguments = launchDescriptor.Arguments.ToList(),
                    WorkingDirectory = launchDescriptor.WorkingDirectory,
                    EnvironmentVariables = launchDescriptor.EnvironmentVariables.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value,
                        StringComparer.OrdinalIgnoreCase)
                });

                return await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
            }

            var endpoint = ResolveConfiguredEndpoint(capability, configuration);
            var httpTransportOptions = new HttpClientTransportOptions
            {
                Endpoint = new Uri(endpoint, UriKind.Absolute),
                Name = configuration.ServerName ?? capability.Name
            };

            var additionalHeaders = httpTransportOptions.AdditionalHeaders;
            ThrowIfPersistedSecretsConfigured(capability, configuration);
            foreach (var header in ResolveSecretBindings(configuration.HeaderBindings, capability.Name, "header"))
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

            if (!LocalMcpCommandPolicy.IsAllowed(configuration.Command))
            {
                throw new InvalidOperationException(
                    $"Local MCP capability '{capability.Name}' uses command '{configuration.Command}', which is outside the approved interpreter policy. Allowed commands: {LocalMcpCommandPolicy.DescribeAllowedCommands()}.");
            }

            if (string.IsNullOrWhiteSpace(configuration.ApprovalMode))
            {
                throw new InvalidOperationException($"Local MCP capability '{capability.Name}' must declare ApprovalMode before it can launch a stdio server.");
            }
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
        }

        private static IReadOnlyDictionary<string, string> ResolveSecretBindings(
            IDictionary<string, string>? bindings,
            string capabilityName,
            string bindingKind)
        {
            var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

                var resolvedValue = AgentProviderEnvironmentCredential.ResolveAndPromote(binding.Value);
                if (string.IsNullOrWhiteSpace(resolvedValue))
                {
                    throw new InvalidOperationException(
                        $"MCP capability '{capabilityName}' requires environment variable '{binding.Value}' to resolve {bindingKind} '{binding.Key}'.");
                }

                resolved[binding.Key] = resolvedValue;
            }

            return resolved;
        }

        private void AttachProviderCredentialForLocalMcp(
            CapabilityCatalogItem capability,
            McpCapabilityConfiguration configuration,
            ProviderProfile provider,
            IDictionary<string, string?> environmentVariables)
        {
            if (provider.Kind != ProviderKind.OpenAi ||
                !UsesPlaywrightVisionMcp(configuration))
            {
                return;
            }

            var credential = owner.ResolveProviderCredential(provider);
            if (!credential.IsResolved)
            {
                throw new InvalidOperationException(
                    $"Local MCP capability '{capability.Name}' requires provider credential for '{provider.Name}' to enable Playwright vision. {credential.FailureMessage}");
            }

            AddCredentialEnvironmentVariable(environmentVariables, provider.ApiKeyEnvironmentVariable, credential.ApiKey);
            AddCredentialEnvironmentVariable(environmentVariables, OpenAiApiKeyEnvironmentVariable, credential.ApiKey);
        }

        private static void AddCredentialEnvironmentVariable(
            IDictionary<string, string?> environmentVariables,
            string variableName,
            string value)
        {
            if (string.IsNullOrWhiteSpace(variableName) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            environmentVariables[variableName.Trim()] = value.Trim();
        }

        private static bool UsesPlaywrightVisionMcp(McpCapabilityConfiguration configuration)
        {
            var arguments = configuration.Arguments ?? [];
            return arguments.Any(argument => argument.StartsWith(PlaywrightMcpPackagePrefix, StringComparison.OrdinalIgnoreCase))
                   && HasVisionCapability(arguments);
        }

        private static bool HasVisionCapability(IReadOnlyList<string> arguments)
        {
            for (var index = 0; index < arguments.Count; index++)
            {
                var argument = arguments[index];
                if (string.Equals(argument, PlaywrightCapsArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return index + 1 < arguments.Count &&
                           ArgumentContainsCapability(arguments[index + 1], PlaywrightVisionCapability);
                }

                if (argument.StartsWith($"{PlaywrightCapsArgument}=", StringComparison.OrdinalIgnoreCase) &&
                    ArgumentContainsCapability(argument[(PlaywrightCapsArgument.Length + 1)..], PlaywrightVisionCapability))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ArgumentContainsCapability(
            string argument,
            string capability)
        {
            return argument
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(item => string.Equals(item, capability, StringComparison.OrdinalIgnoreCase));
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
                var result = await base.InvokeCoreAsync(arguments, cancellationToken).ConfigureAwait(false);
                if (string.Equals(Name, "browser_take_screenshot", StringComparison.OrdinalIgnoreCase))
                {
                    MirrorScreenshotToScopedArtifactPath(workspaceRoot, workspaceScope, TryGetStringArgument(arguments, "filename"));
                }

                return CompactBrowserMcpToolResultForModelContext(Name, arguments, result);
            }

            private static void MirrorScreenshotToScopedArtifactPath(
                string workspaceRoot,
                WorkspaceScopeDescriptor workspaceScope,
                string? fileName)
            {
                if (workspaceScope.IsDefaultSandbox ||
                    string.IsNullOrWhiteSpace(fileName))
                {
                    return;
                }

                var normalizedFileName = WorkspaceScopeDescriptor.NormalizeRelativePath(fileName);
                if (string.IsNullOrWhiteSpace(normalizedFileName) ||
                    Path.IsPathRooted(normalizedFileName) ||
                    !MatchesRoot(normalizedFileName, "artifacts") ||
                    MatchesRoot(normalizedFileName, workspaceScope.ArtifactRootRelativePath) ||
                    normalizedFileName.StartsWith("artifacts/scopes/", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var unscopedFullPath = Path.GetFullPath(Path.Combine(
                    workspaceRoot,
                    normalizedFileName.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(unscopedFullPath))
                {
                    return;
                }

                var suffix = RemoveRoot(normalizedFileName, "artifacts");
                var scopedRelativePath = string.IsNullOrWhiteSpace(suffix)
                    ? workspaceScope.ArtifactRootRelativePath
                    : WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(workspaceScope.ArtifactRootRelativePath, suffix));
                var scopedFullPath = Path.GetFullPath(Path.Combine(
                    workspaceRoot,
                    scopedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                var scopedDirectory = Path.GetDirectoryName(scopedFullPath);
                if (string.IsNullOrWhiteSpace(scopedDirectory))
                {
                    return;
                }

                Directory.CreateDirectory(scopedDirectory);
                File.Copy(unscopedFullPath, scopedFullPath, overwrite: true);
            }

            private static bool MatchesRoot(string relativePath, string rootRelativePath)
            {
                return string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase) ||
                       relativePath.StartsWith(rootRelativePath + "/", StringComparison.OrdinalIgnoreCase);
            }

            private static string RemoveRoot(string relativePath, string rootRelativePath)
            {
                return string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : relativePath[(rootRelativePath.Length + 1)..];
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
}
