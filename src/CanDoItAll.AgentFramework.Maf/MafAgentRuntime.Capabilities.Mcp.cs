using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private sealed class McpCapabilityBuilder(MafAgentRuntime owner)
    {
        private const string PlaywrightMcpPackagePrefix = "@playwright/mcp";
        private const string PlaywrightCapsArgument = "--caps";
        private const string PlaywrightVisionCapability = "vision";

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
                state.Tools.Add(wrappedToolApprovalRequired ? new ApprovalRequiredAIFunction(tool) : tool);
            }

            state.HasApprovalTools |= wrappedToolApprovalRequired;
            await progressCallback(ExecutionState.Preparing, "MCP", $"Attached {tools.Count} MCP tool(s) from '{capability.Name}'.");
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
