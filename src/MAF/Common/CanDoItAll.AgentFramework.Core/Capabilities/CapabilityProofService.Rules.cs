using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed partial class CapabilityProofService
{
    private async Task<CapabilityVerificationResult> VerifySkillAsync(
        CapabilityCatalogItem capability,
        List<string> notes,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        var inlineInstructions = TryReadConfigurationString(capability.ConfigurationJson, "inlineSkill", "instructions");
        if (!string.IsNullOrWhiteSpace(inlineInstructions))
        {
            notes.Add("Inline skill instructions are embedded in the capability configuration.");
            return Verified(notes, checkedAt);
        }

        var registeredSkillServiceType = TryReadConfigurationString(capability.ConfigurationJson, "registeredSkillServiceType");
        if (!string.IsNullOrWhiteSpace(registeredSkillServiceType))
        {
            var serviceType = Type.GetType(registeredSkillServiceType, throwOnError: false);
            if (serviceType is null)
            {
                return Failed($"Registered skill type '{registeredSkillServiceType}' could not be resolved.", checkedAt);
            }

            notes.Add($"Registered skill type '{registeredSkillServiceType}' resolves in the current application.");
            return PendingReview(
                $"{string.Join(" ", notes)} DI-backed skill execution still needs a live runtime proof path.",
                checkedAt);
        }

        if (TryResolveConfiguredPath(capability, "skillRoot", out var filePath) || TryResolveFilePath(capability.EndpointOrPath, out filePath))
        {
            var configuredSkillRoot = TryReadConfigurationString(capability.ConfigurationJson, "skillRoot") ?? capability.EndpointOrPath;
            var allowedExternalRoots = ReadConfigurationStringArray(capability.ConfigurationJson, "allowedExternalRoots");
            var expandedConfiguredSkillRoot = ExpandPortablePath(configuredSkillRoot);
            var configuredSkillRootFullPath = Path.GetFullPath(Path.IsPathRooted(expandedConfiguredSkillRoot) ? expandedConfiguredSkillRoot : Path.Combine(Environment.CurrentDirectory, expandedConfiguredSkillRoot));
            var currentWorkspaceRoot = Path.GetFullPath(Environment.CurrentDirectory);
            var isExternalSkillRoot = !IsPathWithinRoot(configuredSkillRootFullPath, currentWorkspaceRoot);
            var normalizedAllowedExternalRoots = allowedExternalRoots
                .Select(ExpandPortablePath)
                .Select(root => Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(Environment.CurrentDirectory, root)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (isExternalSkillRoot && normalizedAllowedExternalRoots.Count == 0)
            {
                return Failed($"Skill '{capability.Name}' points to external root '{configuredSkillRoot}', but allowedExternalRoots is missing.", checkedAt);
            }

            if (isExternalSkillRoot &&
                !normalizedAllowedExternalRoots.Any(allowedRoot => IsPathWithinRoot(configuredSkillRootFullPath, allowedRoot)))
            {
                return Failed($"Skill '{capability.Name}' points to external root '{configuredSkillRoot}', but allowedExternalRoots does not cover it.", checkedAt);
            }

            if (Directory.Exists(filePath))
            {
                var skillFile = Path.Combine(filePath, "SKILL.md");
                if (!File.Exists(skillFile))
                {
                    return Failed($"Skill directory '{filePath}' does not contain SKILL.md.", checkedAt);
                }

                filePath = skillFile;
            }

            if (!File.Exists(filePath))
            {
                return Failed($"Skill file '{filePath}' was not found.", checkedAt);
            }

            notes.Add($"Skill file exists at '{filePath}'.");
            if (isExternalSkillRoot)
            {
                notes.Add("External skill root is explicitly allowlisted.");
            }

            var preview = await ReadPreviewAsync(filePath, cancellationToken);
            if (preview.Contains("name:", StringComparison.OrdinalIgnoreCase) ||
                preview.Contains("#", StringComparison.Ordinal))
            {
                notes.Add("Skill file contains recognizable markdown or front matter.");
                return Verified(notes, checkedAt);
            }

            return PendingReview(
                $"{string.Join(" ", notes)} The file exists, but its contents do not look like a normal skill document yet.",
                checkedAt);
        }

        if (TryCreateUri(capability.EndpointOrPath, out var uri))
        {
            return uri.Scheme switch
            {
                "app" or "plugin" => PendingReview(
                    $"Skill connector '{uri}' is configured, but connector-host execution must be proven in a host environment.",
                    checkedAt),
                _ => PendingReview(
                    $"Skill entry uses URI scheme '{uri.Scheme}', which this sandbox records but does not execute directly.",
                    checkedAt)
            };
        }

        return PendingReview(
            $"Skill '{capability.Name}' is recorded, but its endpoint or path is not concrete enough for local proof.",
            checkedAt);
    }

    private static async Task<CapabilityVerificationResult> VerifyToolLikeCapabilityAsync(
        AgentDefinition agent,
        ProviderProfile? provider,
        CapabilityCatalogItem capability,
        List<string> notes,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        if (!agent.Permissions.CanUseTools)
        {
            return Failed($"Agent '{agent.Name}' is not allowed to use tools.", checkedAt);
        }

        notes.Add("Agent permissions allow tool usage.");

        if (provider is null)
        {
            return Failed("Agent does not have a provider profile selected.", checkedAt);
        }

        notes.Add($"Provider '{provider.Name}' is assigned.");

        if (!provider.SupportsTools)
        {
            return Failed($"Provider '{provider.Name}' is marked as not supporting tools.", checkedAt);
        }

        notes.Add($"Provider '{provider.Name}' is configured to support tools.");

        var configuredToolKey = TryReadConfigurationString(capability.ConfigurationJson, "tool") ?? capability.Key;
        if (ProviderNativeToolKeys.TryResolveFamily(configuredToolKey, out var family))
        {
            if (TryReadConfigurationBoolean(capability.ConfigurationJson, "approvalRequired") == true)
            {
                return Failed(
                    $"Capability '{capability.Name}' requests approvalRequired for {ProviderNativeToolKeys.GetDisplayName(family)}, but provider-native hosted tools do not yet project approval wrappers through the current MAF bridge.",
                    checkedAt);
            }

            var support = ProviderFeatureService.GetNativeToolSupport(provider, family);
            if (!support.IsSupported)
            {
                return Failed($"{support.Summary} {support.Remediation}", checkedAt);
            }

            notes.Add($"Provider feature matrix supports {ProviderNativeToolKeys.GetDisplayName(family)} for '{provider.Name}'.");
            return Verified(notes, checkedAt);
        }

        if (BuiltInToolKeys.Contains(capability.Key))
        {
            notes.Add($"Built-in sandbox handler '{capability.Key}' is registered.");
            return Verified(notes, checkedAt);
        }

        return await VerifyEndpointProofAsync(capability, notes, checkedAt, cancellationToken);
    }

    private static async Task<CapabilityVerificationResult> VerifyPluginCapabilityAsync(
        AgentDefinition agent,
        ProviderProfile? provider,
        CapabilityCatalogItem capability,
        List<string> notes,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        if (!agent.Permissions.CanUseTools)
        {
            return Failed($"Agent '{agent.Name}' is not allowed to use plugins or tool-backed capabilities.", checkedAt);
        }

        notes.Add("Agent permissions allow plugin usage.");

        if (provider is not null)
        {
            if (!provider.SupportsTools)
            {
                return Failed($"Provider '{provider.Name}' is marked as not supporting tools.", checkedAt);
            }

            notes.Add($"Provider '{provider.Name}' supports tools.");
        }

        var registeredPluginServiceType = TryReadConfigurationString(capability.ConfigurationJson, "registeredPluginServiceType");
        if (!string.IsNullOrWhiteSpace(registeredPluginServiceType))
        {
            var serviceType = Type.GetType(registeredPluginServiceType, throwOnError: false);
            if (serviceType is null)
            {
                return Failed($"Registered plugin type '{registeredPluginServiceType}' could not be resolved.", checkedAt);
            }

            notes.Add($"Registered plugin type '{registeredPluginServiceType}' resolves in the current application.");
            return PendingReview(
                $"{string.Join(" ", notes)} Plugin execution still needs a live runtime proof path.",
                checkedAt);
        }

        return await VerifyEndpointProofAsync(capability, notes, checkedAt, cancellationToken);
    }

    private async Task<CapabilityVerificationResult> VerifyMcpCapabilityAsync(
        AgentDefinition agent,
        ProviderProfile? provider,
        CapabilityCatalogItem capability,
        List<string> notes,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        if (!agent.Permissions.CanUseTools)
        {
            return Failed($"Agent '{agent.Name}' is not allowed to use MCP or tool endpoints.", checkedAt);
        }

        notes.Add("Agent permissions allow external capability usage.");

        if (provider is not null && provider.SupportsTools)
        {
            notes.Add($"Provider '{provider.Name}' is available for capability-backed runs.");
        }
        else if (provider is null)
        {
            notes.Add("No provider is bound yet, so only structural MCP proof is possible.");
        }
        else
        {
            notes.Add($"Provider '{provider.Name}' is bound but currently marked as not supporting tools.");
        }

        if (TryReadConfigurationBoolean(capability.ConfigurationJson, "hosted") == true)
        {
            if (provider is null)
            {
                return Failed($"Hosted MCP capability '{capability.Name}' requires a provider profile before support can be proven.", checkedAt);
            }

            var support = ProviderFeatureService.GetNativeToolSupport(provider, ProviderNativeToolFamily.HostedMcpServer);
            if (!support.IsSupported)
            {
                return Failed($"{support.Summary} {support.Remediation}", checkedAt);
            }

            notes.Add($"Provider feature matrix supports {ProviderNativeToolKeys.GetDisplayName(ProviderNativeToolFamily.HostedMcpServer)} for '{provider.Name}'.");
        }

        if (HasNonEmptyConfigurationObject(capability.ConfigurationJson, "environmentVariables"))
        {
            return Failed(
                $"MCP capability '{capability.Name}' persists raw environmentVariables. Use environmentVariableBindings so secrets resolve only at runtime.",
                checkedAt);
        }

        if (HasNonEmptyConfigurationObject(capability.ConfigurationJson, "headers"))
        {
            return Failed(
                $"MCP capability '{capability.Name}' persists raw headers. Use headerBindings so secrets resolve only at runtime.",
                checkedAt);
        }

        if (capability.EndpointOrPath.Contains('.', StringComparison.Ordinal) &&
            !TryCreateUri(capability.EndpointOrPath, out _))
        {
            return PendingReview(
                $"{string.Join(" ", notes)} MCP capability is mapped to a logical integration seam and still needs host-level execution proof.",
                checkedAt);
        }

        var command = TryReadConfigurationString(capability.ConfigurationJson, "command");
        if (!string.IsNullOrWhiteSpace(command))
        {
            var allowedTools = ReadConfigurationStringArray(capability.ConfigurationJson, "allowedTools");
            if (allowedTools.Count == 0)
            {
                return Failed($"Local MCP capability '{capability.Name}' is missing allowedTools.", checkedAt);
            }

            if (!LocalMcpCommandPolicy.IsAllowed(command))
            {
                return Failed(
                    $"Local MCP capability '{capability.Name}' uses command '{command}', which is outside the approved interpreter policy. Allowed commands: {LocalMcpCommandPolicy.DescribeAllowedCommands()}.",
                    checkedAt);
            }

            var approvalMode = TryReadConfigurationString(capability.ConfigurationJson, "approvalMode");
            if (string.IsNullOrWhiteSpace(approvalMode))
            {
                return Failed($"Local MCP capability '{capability.Name}' is missing approvalMode.", checkedAt);
            }

            var workingDirectory = TryReadConfigurationString(capability.ConfigurationJson, "workingDirectory");
            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                var allowedWorkingDirectories = ReadConfigurationStringArray(capability.ConfigurationJson, "allowedWorkingDirectories");
                var expandedWorkingDirectory = ExpandPortablePath(workingDirectory);
                var workingDirectoryFullPath = Path.GetFullPath(Path.IsPathRooted(expandedWorkingDirectory) ? expandedWorkingDirectory : Path.Combine(Environment.CurrentDirectory, expandedWorkingDirectory));
                var currentWorkspaceRoot = Path.GetFullPath(Environment.CurrentDirectory);
                var isExternalWorkingDirectory = !IsPathWithinRoot(workingDirectoryFullPath, currentWorkspaceRoot);
                var normalizedAllowedWorkingDirectories = allowedWorkingDirectories
                    .Select(ExpandPortablePath)
                    .Select(root => Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(Environment.CurrentDirectory, root)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (isExternalWorkingDirectory && normalizedAllowedWorkingDirectories.Count == 0)
                {
                    return Failed($"Local MCP capability '{capability.Name}' uses external workingDirectory '{workingDirectory}' without allowedWorkingDirectories.", checkedAt);
                }

                if (isExternalWorkingDirectory &&
                    !normalizedAllowedWorkingDirectories.Any(allowedRoot => IsPathWithinRoot(workingDirectoryFullPath, allowedRoot)))
                {
                    return Failed($"Local MCP capability '{capability.Name}' uses external workingDirectory '{workingDirectory}', but allowedWorkingDirectories does not cover it.", checkedAt);
                }

                if (!Directory.Exists(workingDirectoryFullPath))
                {
                    return Failed($"Local MCP capability '{capability.Name}' points at missing workingDirectory '{workingDirectoryFullPath}'.", checkedAt);
                }
            }

            var environmentVariableBindings = ReadConfigurationStringDictionary(capability.ConfigurationJson, "environmentVariableBindings");
            if (environmentVariableBindings.Count > 0)
            {
                notes.Add($"Local MCP environment bindings resolve {environmentVariableBindings.Count} value(s) from runtime environment variables or stored secret references.");
            }

            var headerBindings = ReadConfigurationStringDictionary(capability.ConfigurationJson, "headerBindings");
            if (headerBindings.Count > 0)
            {
                notes.Add($"MCP header bindings resolve {headerBindings.Count} value(s) from runtime environment variables or stored secret references.");
            }

            notes.Add($"Local MCP command '{command}' uses explicit allowedTools and approvalMode '{approvalMode}'.");
            return PendingReview(
                $"{string.Join(" ", notes)} Local MCP transport is structurally safe enough to proceed to runtime proof.",
                checkedAt);
        }

        return await VerifyEndpointProofAsync(capability, notes, checkedAt, cancellationToken);
    }

    private static CapabilityVerificationResult VerifyRagCapability(
        CapabilityCatalogItem capability,
        List<string> notes,
        DateTimeOffset checkedAt)
    {
        if (!TryResolveConfiguredPath(capability, "ragRoot", out var ragPath) && !TryResolveFilePath(capability.EndpointOrPath, out ragPath))
        {
            return PendingReview(
                $"{string.Join(" ", notes)} The RAG root is not concrete enough for local verification.",
                checkedAt);
        }

        if (!Directory.Exists(ragPath) && !File.Exists(ragPath))
        {
            return Failed($"RAG path '{ragPath}' does not exist.", checkedAt);
        }

        notes.Add($"RAG path exists at '{ragPath}'.");
        return Verified(notes, checkedAt);
    }

    private static CapabilityVerificationResult VerifyAiContextCapability(
        CapabilityCatalogItem capability,
        List<string> notes,
        DateTimeOffset checkedAt)
    {
        var message = TryReadConfigurationString(capability.ConfigurationJson, "message");
        if (string.IsNullOrWhiteSpace(message))
        {
            return Failed($"AI context capability '{capability.Name}' does not define a message payload.", checkedAt);
        }

        notes.Add("AI context capability contains a non-empty injected message.");
        return Verified(notes, checkedAt);
    }

    private static async Task<CapabilityVerificationResult> VerifyEndpointProofAsync(
        CapabilityCatalogItem capability,
        List<string> notes,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        if (TryResolveFilePath(capability.EndpointOrPath, out var filePath))
        {
            if (!File.Exists(filePath))
            {
                return Failed($"Configured path '{filePath}' does not exist.", checkedAt);
            }

            notes.Add($"Configured path exists at '{filePath}'.");
            return Verified(notes, checkedAt);
        }

        if (TryCreateUri(capability.EndpointOrPath, out var uri))
        {
            return uri.Scheme switch
            {
                "http" or "https" => await VerifyHttpEndpointAsync(uri, notes, checkedAt, cancellationToken),
                "sandbox" => capability.IsBuiltIn
                    ? Verified($"{string.Join(" ", notes)} Sandbox capability URI '{uri}' is registered as built-in.", checkedAt)
                    : PendingReview($"{string.Join(" ", notes)} Sandbox URI '{uri}' is configured but not mapped to a known built-in handler.", checkedAt),
                "app" or "plugin" => PendingReview(
                    $"{string.Join(" ", notes)} Connector URI '{uri}' is configured but requires host-level proof.",
                    checkedAt),
                _ => PendingReview(
                    $"{string.Join(" ", notes)} URI scheme '{uri.Scheme}' is recorded, but this sandbox does not execute it directly.",
                    checkedAt)
            };
        }

        return PendingReview(
            $"{string.Join(" ", notes)} The endpoint is recorded, but it is not concrete enough for local verification.",
            checkedAt);
    }

    private static async Task<CapabilityVerificationResult> VerifyHttpEndpointAsync(
        Uri uri,
        List<string> notes,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            notes.Add($"Endpoint responded with HTTP {(int)response.StatusCode}.");
            return (int)response.StatusCode < 500
                ? Verified(notes, checkedAt)
                : Failed($"{string.Join(" ", notes)} Endpoint returned a server error.", checkedAt);
        }
        catch (Exception exception)
        {
            return Failed($"HTTP endpoint '{uri}' could not be reached: {exception.Message}", checkedAt);
        }
    }
}
