using System.Net;
using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Access;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Tools;
using CanDoItAll.AgentFramework.Tools.Abstractions;
using CapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;

namespace CanDoItAll.Tests.Unit;

public sealed class ToolImplementationContractsTests
{
    [Fact]
    public async Task SB02_INV_INTERNAL_001_internal_tool_registry_resolves_mockable_tool_and_exposes_policy_descriptor()
    {
        var descriptor = ToolDescriptorFactory.Internal(
            CapabilityKey.Create("workspace-read-file"),
            RuntimeToolName.Create(ToolContractCatalog.WorkspaceReadFile),
            ImplementationKey.Create("workspace.read-file"),
            [CapabilityTag.Create("workspace"), CapabilityTag.Create("read")],
            [CapabilityOperationClassification.Read],
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.WorkspaceRead, false, false));
        var registry = new InternalToolRegistry();
        registry.Register(new DelegateInternalTool(
            descriptor,
            request => ToolInvocationResult.Success(request.CorrelationId, JsonDocument.Parse("""{"ok":true,"path":"README.md"}""").RootElement)));

        var tool = registry.Resolve(ImplementationKey.Create("workspace.read-file"));
        var result = await tool.InvokeAsync(ToolInvocationRequest.Create(descriptor.Identity, descriptor.ImplementationKey, """{"path":"README.md"}""", "SB02_INV_INTERNAL_001"), CancellationToken.None);
        var exposure = ToolExposureDescriptorFactory.Create(descriptor);

        Assert.True(result.IsSuccess);
        Assert.True(result.Output.TryGetProperty("ok", out var ok) && ok.GetBoolean());
        Assert.Equal(CapabilityKind.Tool, exposure.Identity.Kind);
        Assert.Equal(RuntimeToolName.Create(ToolContractCatalog.WorkspaceReadFile), exposure.RuntimeToolName);
        Assert.Contains(CapabilityOperationClassification.Read, exposure.OperationClassifications);
    }

    [Fact]
    public async Task SB02_INV_EXTERNAL_001_process_invoker_returns_typed_nonzero_exit_diagnostic_with_bounded_masked_output()
    {
        var descriptor = ToolDescriptorFactory.ExternalProcess(
            CapabilityKey.Create("external-audit-tool"),
            RuntimeToolName.Create("external_audit"),
            ImplementationKey.Create("external.audit"),
            executablePath: "fake-audit.exe",
            arguments: ["--json"],
            workingDirectory: ".",
            timeout: TimeSpan.FromSeconds(5),
            maxOutputBytes: 32,
            allowedExecutableNames: ["fake-audit.exe"],
            requiredOutputProperties: ["ok"]);
        var invoker = new ExternalProcessToolInvoker(new FakeProcessRunner(new ExternalProcessRunResult(
            Started: true,
            ExitCode: 7,
            Stdout: """{"ok":false,"token":"super-secret-value-that-must-not-leak"}""",
            Stderr: "failure with api_key=super-secret-value-that-must-not-leak",
            Elapsed: TimeSpan.FromMilliseconds(42))));

        var result = await invoker.InvokeAsync(
            descriptor,
            ToolInvocationRequest.Create(descriptor.Identity, descriptor.ImplementationKey, """{"input":true}""", "SB02_INV_EXTERNAL_001"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CapabilityDiagnosticCategory.ProcessExit, diagnostic.Category);
        Assert.Equal(7, diagnostic.ExitCode);
        Assert.Equal("SB02_INV_EXTERNAL_001", diagnostic.CorrelationId);
        Assert.Contains("fake-audit.exe", diagnostic.MaskedDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-value", diagnostic.MaskedDetail, StringComparison.Ordinal);
        Assert.True(diagnostic.MaskedDetail.Length < 220);
        Assert.Contains("non-zero", diagnostic.RepairHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SB02_INV_EXTERNAL_002_http_invoker_returns_typed_http_status_and_masks_auth_headers()
    {
        var descriptor = ToolDescriptorFactory.ExternalHttp(
            CapabilityKey.Create("external-http-audit"),
            RuntimeToolName.Create("external_http_audit"),
            ImplementationKey.Create("external.http-audit"),
            HttpMethod.Post,
            new Uri("https://example.test/audit"),
            new Dictionary<string, string> { ["Authorization"] = "Bearer super-secret-value" },
            timeout: TimeSpan.FromSeconds(5),
            maxResponseBytes: 48,
            requiredOutputProperties: ["ok"]);
        var invoker = new ExternalHttpToolInvoker(new FakeHttpTransport(new ExternalHttpResponse(
            HttpStatusCode.BadGateway,
            """{"error":"upstream failed","secret":"super-secret-value"}""")));

        var result = await invoker.InvokeAsync(
            descriptor,
            ToolInvocationRequest.Create(descriptor.Identity, descriptor.ImplementationKey, """{"input":true}""", "SB02_INV_EXTERNAL_002"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CapabilityDiagnosticCategory.HttpStatus, diagnostic.Category);
        Assert.Equal((int)HttpStatusCode.BadGateway, diagnostic.HttpStatusCode);
        Assert.Contains("example.test", diagnostic.MaskedDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-value", diagnostic.MaskedDetail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SB02_INV_EXTERNAL_003_process_invoker_rejects_disallowed_command_before_start()
    {
        var descriptor = ToolDescriptorFactory.ExternalProcess(
            CapabilityKey.Create("external-audit-tool"),
            RuntimeToolName.Create("external_audit"),
            ImplementationKey.Create("external.audit"),
            executablePath: "powershell.exe",
            arguments: ["-NoProfile"],
            workingDirectory: ".",
            timeout: TimeSpan.FromSeconds(5),
            maxOutputBytes: 128,
            allowedExecutableNames: ["fake-audit.exe"],
            requiredOutputProperties: ["ok"]);
        var runner = new FakeProcessRunner(new ExternalProcessRunResult(true, 0, """{"ok":true}""", string.Empty, TimeSpan.Zero));
        var invoker = new ExternalProcessToolInvoker(runner);

        var result = await invoker.InvokeAsync(
            descriptor,
            ToolInvocationRequest.Create(descriptor.Identity, descriptor.ImplementationKey, """{"input":true}""", "SB02_INV_EXTERNAL_003"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(runner.WasCalled);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Category == CapabilityDiagnosticCategory.CommandPolicy &&
            diagnostic.RepairHint.Contains("allowed executable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SB02_INV_EXTERNAL_004_process_invoker_maps_timeout_to_typed_diagnostic()
    {
        var descriptor = ToolDescriptorFactory.ExternalProcess(
            CapabilityKey.Create("external-audit-tool"),
            RuntimeToolName.Create("external_audit"),
            ImplementationKey.Create("external.audit"),
            executablePath: "fake-audit.exe",
            arguments: ["--json"],
            workingDirectory: ".",
            timeout: TimeSpan.FromMilliseconds(50),
            maxOutputBytes: 128,
            allowedExecutableNames: ["fake-audit.exe"],
            requiredOutputProperties: ["ok"]);
        var invoker = new ExternalProcessToolInvoker(new FakeProcessRunner(
            new TimeoutException("bounded process execution expired")));

        var result = await invoker.InvokeAsync(
            descriptor,
            ToolInvocationRequest.Create(descriptor.Identity, descriptor.ImplementationKey, """{"input":true}""", "SB02_INV_EXTERNAL_004"),
            CancellationToken.None);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.False(result.IsSuccess);
        Assert.Equal(CapabilityDiagnosticCategory.Timeout, diagnostic.Category);
        Assert.Equal(TimeSpan.FromMilliseconds(50), diagnostic.Timeout);
        Assert.Contains("bounded", diagnostic.MaskedDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SB02_INV_EXTERNAL_005_setup_test_service_preserves_schema_validation_failure()
    {
        var descriptor = ToolDescriptorFactory.ExternalProcess(
            CapabilityKey.Create("external-audit-tool"),
            RuntimeToolName.Create("external_audit"),
            ImplementationKey.Create("external.audit"),
            executablePath: "fake-audit.exe",
            arguments: ["--json"],
            workingDirectory: ".",
            timeout: TimeSpan.FromSeconds(5),
            maxOutputBytes: 128,
            allowedExecutableNames: ["fake-audit.exe"],
            requiredOutputProperties: ["ok"]);
        var invoker = new ExternalProcessToolInvoker(new FakeProcessRunner(new ExternalProcessRunResult(
            true,
            0,
            """{"status":"missing ok"}""",
            string.Empty,
            TimeSpan.FromMilliseconds(12))));
        var setup = new ToolSetupTestService(invoker, new ExternalHttpToolInvoker(new FakeHttpTransport(new ExternalHttpResponse(HttpStatusCode.OK, "{}"))));

        var result = await setup.TestProcessToolAsync(descriptor, """{"input":true}""", "SB02_INV_EXTERNAL_005", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Category == CapabilityDiagnosticCategory.SchemaValidation &&
            diagnostic.FieldPath == "$.ok");
    }

    [Fact]
    public async Task SB02_INV_EXTERNAL_006_http_invoker_maps_timeout_to_typed_diagnostic()
    {
        var descriptor = ToolDescriptorFactory.ExternalHttp(
            CapabilityKey.Create("external-http-audit"),
            RuntimeToolName.Create("external_http_audit"),
            ImplementationKey.Create("external.http-audit"),
            HttpMethod.Post,
            new Uri("https://example.test/audit"),
            new Dictionary<string, string>(),
            timeout: TimeSpan.FromMilliseconds(75),
            maxResponseBytes: 128,
            requiredOutputProperties: ["ok"]);
        var invoker = new ExternalHttpToolInvoker(new FakeHttpTransport(
            new TimeoutException("bounded HTTP execution expired")));

        var result = await invoker.InvokeAsync(
            descriptor,
            ToolInvocationRequest.Create(descriptor.Identity, descriptor.ImplementationKey, """{"input":true}""", "SB02_INV_EXTERNAL_006"),
            CancellationToken.None);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.False(result.IsSuccess);
        Assert.Equal(CapabilityDiagnosticCategory.Timeout, diagnostic.Category);
        Assert.Equal(TimeSpan.FromMilliseconds(75), diagnostic.Timeout);
        Assert.Contains("example.test", diagnostic.MaskedDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void SB02_INV_POLICY_001_internal_external_and_provider_native_descriptors_participate_in_access_policy()
    {
        var internalDescriptor = ToolExposureDescriptorFactory.Create(ToolDescriptorFactory.Internal(
            CapabilityKey.Create("workspace-write-file"),
            RuntimeToolName.Create(ToolContractCatalog.WorkspaceWriteFile),
            ImplementationKey.Create("workspace.write-file"),
            [CapabilityTag.Create("workspace"), CapabilityTag.Create("mutation")],
            [CapabilityOperationClassification.Mutation],
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.WorkspaceWrite, true, true)));
        var externalDescriptor = ToolExposureDescriptorFactory.Create(ToolDescriptorFactory.ExternalProcess(
            CapabilityKey.Create("external-audit-tool"),
            RuntimeToolName.Create("external_audit"),
            ImplementationKey.Create("external.audit"),
            "fake-audit.exe",
            [],
            ".",
            TimeSpan.FromSeconds(5),
            128,
            ["fake-audit.exe"],
            ["ok"]));
        var providerNativeDescriptor = ToolExposureDescriptorFactory.Create(ToolDescriptorFactory.ProviderNative(
            CapabilityKey.Create("provider-native-web-search"),
            RuntimeToolName.Create("provider_native_web_search"),
            ImplementationKey.Create("provider-native.web-search"),
            [CapabilityTag.Create("provider-native")],
            [CapabilityOperationClassification.ProviderNative],
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.ProviderNative, false, false)));
        var evaluator = new CapabilityAccessPolicyEvaluator();
        var policy = new CapabilityAccessPolicy(
        [
            new CapabilityAccessRule(
                CapabilityRuleId.Create("deny-mutation"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByOperationClassification(CapabilityOperationClassification.Mutation),
                "No mutation tools."),
            new CapabilityAccessRule(
                CapabilityRuleId.Create("deny-external"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByTag(CapabilityTag.Create("external")),
                "No external tools."),
            new CapabilityAccessRule(
                CapabilityRuleId.Create("deny-provider-native"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByRuntimeToolName(RuntimeToolName.Create("provider_native_web_search")),
                "No provider-native web search.")
        ]);

        var result = evaluator.Evaluate(new CapabilityAccessEvaluationContext(
            [internalDescriptor, externalDescriptor, providerNativeDescriptor],
            [],
            [policy],
            "SB02_INV_POLICY_001"));

        Assert.Empty(result.AllowedCapabilities);
        Assert.Contains(result.Diagnostics, item => item.Identity.Key == internalDescriptor.Identity.Key);
        Assert.Contains(result.Diagnostics, item => item.Identity.Key == externalDescriptor.Identity.Key);
        Assert.Contains(result.Diagnostics, item => item.Identity.Key == providerNativeDescriptor.Identity.Key);
    }

    [Fact]
    public void SB02_INV_PARITY_001_existing_tool_policy_metadata_maps_to_exposure_descriptors()
    {
        var failures = new List<string>();
        foreach (var metadata in ToolCapabilityRegistry.Capabilities)
        {
            if (!RuntimeToolName.TryCreate(metadata.Name, out var runtimeName))
            {
                failures.Add($"{metadata.Name}: invalid runtime name");
                continue;
            }

            var descriptor = ToolDescriptorFactory.Internal(
                CapabilityKey.Create(metadata.Name.Replace('_', '-')),
                runtimeName,
                ImplementationKey.Create(metadata.Name.Replace('_', '.')),
                ResolveTags(metadata),
                ResolveClassifications(metadata),
                new CapabilitySideEffectProfile(
                    MapSideEffect(metadata.SideEffectKind),
                    metadata.RequiresApprovalByDefault,
                    metadata.IsStateChanging));
            var exposure = ToolExposureDescriptorFactory.Create(descriptor);

            if (exposure.RuntimeToolName != runtimeName)
            {
                failures.Add($"{metadata.Name}: runtime name drift");
            }

            if (metadata.RequiresApprovalByDefault != exposure.SideEffectProfile.RequiresApprovalByDefault ||
                metadata.IsStateChanging != exposure.SideEffectProfile.IsStateChanging)
            {
                failures.Add($"{metadata.Name}: approval/side-effect drift");
            }

            if (exposure.OperationClassifications.Count == 0)
            {
                failures.Add($"{metadata.Name}: missing operation classification");
            }
        }

        Assert.Empty(failures);
    }

    private sealed class FakeProcessRunner : IExternalProcessRunner
    {
        private readonly ExternalProcessRunResult? result;
        private readonly Exception? exception;

        public FakeProcessRunner(ExternalProcessRunResult result)
        {
            this.result = result;
        }

        public FakeProcessRunner(Exception exception)
        {
            this.exception = exception;
        }

        public bool WasCalled { get; private set; }

        public Task<ExternalProcessRunResult> RunAsync(ExternalProcessRunRequest request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            if (exception is not null)
            {
                return Task.FromException<ExternalProcessRunResult>(exception);
            }

            return Task.FromResult(result ?? throw new InvalidOperationException("Fake process result was not configured."));
        }
    }

    private sealed class FakeHttpTransport : IExternalHttpTransport
    {
        private readonly ExternalHttpResponse? response;
        private readonly Exception? exception;

        public FakeHttpTransport(ExternalHttpResponse response)
        {
            this.response = response;
        }

        public FakeHttpTransport(Exception exception)
        {
            this.exception = exception;
        }

        public Task<ExternalHttpResponse> SendAsync(ExternalHttpRequest request, CancellationToken cancellationToken)
        {
            if (exception is not null)
            {
                return Task.FromException<ExternalHttpResponse>(exception);
            }

            return Task.FromResult(response ?? throw new InvalidOperationException("Fake HTTP response was not configured."));
        }
    }

    private static IReadOnlySet<CapabilityTag> ResolveTags(ToolCapabilityMetadata metadata)
    {
        var tags = new HashSet<CapabilityTag>
        {
            CapabilityTag.Create("tool")
        };

        tags.Add(metadata.Classification switch
        {
            ToolInvocationClassification.Read => CapabilityTag.Create("read"),
            ToolInvocationClassification.Validation => CapabilityTag.Create("validation"),
            ToolInvocationClassification.Mutation => CapabilityTag.Create("mutation"),
            ToolInvocationClassification.HostedProviderNative => CapabilityTag.Create("provider-native"),
            ToolInvocationClassification.LocalMcp or ToolInvocationClassification.HostedMcp => CapabilityTag.Create("mcp"),
            _ => CapabilityTag.Create("unknown")
        });
        return tags;
    }

    private static IReadOnlySet<CapabilityOperationClassification> ResolveClassifications(ToolCapabilityMetadata metadata)
    {
        var classifications = new HashSet<CapabilityOperationClassification>
        {
            metadata.Classification switch
            {
                ToolInvocationClassification.Read => CapabilityOperationClassification.Read,
                ToolInvocationClassification.Validation => CapabilityOperationClassification.Validation,
                ToolInvocationClassification.Mutation => CapabilityOperationClassification.Mutation,
                ToolInvocationClassification.HostedProviderNative => CapabilityOperationClassification.ProviderNative,
                ToolInvocationClassification.LocalMcp or ToolInvocationClassification.HostedMcp => CapabilityOperationClassification.McpTool,
                _ => CapabilityOperationClassification.ExternalAction
            }
        };

        if (metadata.BrowserProofRole != ToolCapabilityBrowserProofRole.None)
        {
            classifications.Add(CapabilityOperationClassification.BrowserAccess);
        }

        if (metadata.CanExecuteExternalAction)
        {
            classifications.Add(CapabilityOperationClassification.ExternalAction);
        }

        return classifications;
    }

    private static CapabilitySideEffectKind MapSideEffect(ToolCapabilitySideEffectKind sideEffectKind)
    {
        return sideEffectKind switch
        {
            ToolCapabilitySideEffectKind.None => CapabilitySideEffectKind.None,
            ToolCapabilitySideEffectKind.WorkspaceRead => CapabilitySideEffectKind.WorkspaceRead,
            ToolCapabilitySideEffectKind.WorkspaceWrite => CapabilitySideEffectKind.WorkspaceWrite,
            ToolCapabilitySideEffectKind.LocalProcessExecution => CapabilitySideEffectKind.LocalProcessExecution,
            ToolCapabilitySideEffectKind.RuntimeLaunch => CapabilitySideEffectKind.RuntimeLaunch,
            ToolCapabilitySideEffectKind.RuntimeProofCapture => CapabilitySideEffectKind.RuntimeProofCapture,
            ToolCapabilitySideEffectKind.ProcessMutation => CapabilitySideEffectKind.ProcessMutation,
            ToolCapabilitySideEffectKind.ProjectStructureMutation => CapabilitySideEffectKind.ProjectStructureMutation,
            ToolCapabilitySideEffectKind.ExternalAction => CapabilitySideEffectKind.ExternalAction,
            ToolCapabilitySideEffectKind.MediaGeneration => CapabilitySideEffectKind.MediaGeneration,
            ToolCapabilitySideEffectKind.DocumentConversion => CapabilitySideEffectKind.DocumentConversion,
            ToolCapabilitySideEffectKind.ProviderNative => CapabilitySideEffectKind.ProviderNative,
            ToolCapabilitySideEffectKind.McpTool => CapabilitySideEffectKind.McpTool,
            _ => CapabilitySideEffectKind.None
        };
    }
}
