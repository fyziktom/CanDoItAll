using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Tools.Abstractions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Tools;

public sealed class ExternalProcessToolInvoker(
    IExternalProcessRunner processRunner) : IExternalProcessToolInvoker
{
    private readonly IExternalProcessRunner processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));

    public async Task<ToolInvocationResult> InvokeAsync(
        ExternalProcessToolDescriptor descriptor,
        ToolInvocationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(request);

        if (SensitiveTextRedactor.ContainsSecretBearingArguments(descriptor.Arguments))
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.SecretBinding,
                "$.arguments",
                $"Capability '{descriptor.Identity.Key}' contains a persisted secret-bearing process argument.",
                "Use a runtime secret binding instead of a command-line secret.");
        }

        ExternalProcessRunResult runResult;
        try
        {
            runResult = await processRunner.RunAsync(
                new ExternalProcessRunRequest(
                    descriptor.ExecutablePath,
                    descriptor.Arguments,
                    descriptor.WorkingDirectory,
                    descriptor.Timeout,
                    request.Input.GetRawText(),
                    descriptor.MaxOutputBytes,
                    request.CorrelationId,
                    descriptor.AllowedExecutableNames),
                cancellationToken);
        }
        catch (ExternalProcessCommandPolicyException)
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.CommandPolicy,
                "$.executablePath",
                $"Capability '{descriptor.Identity.Key}' resolved to an executable outside its command policy.",
                "Use an executable identity declared by the capability template command policy.");
        }
        catch (ExternalProcessAccessPolicyException)
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.AccessPolicy,
                "$.workingDirectory",
                $"Capability '{descriptor.Identity.Key}' does not have an authorized working directory.",
                "Select an existing workspace-authorized working directory.");
        }
        catch (TimeoutException exception)
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.Timeout,
                "$.timeout",
                $"Process invocation for capability '{descriptor.Identity.Key}' exceeded timeout {descriptor.Timeout}. {exception.GetType().Name}.",
                "Increase the timeout only after confirming the external tool is healthy and bounded.",
                timeout: descriptor.Timeout);
        }
        catch (OperationCanceledException)
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.Cancellation,
                "$",
                $"Process invocation for capability '{descriptor.Identity.Key}' was cancelled.",
                "Retry only if the caller still owns the setup or tool-call lifecycle.",
                timeout: descriptor.Timeout);
        }
        catch (ExternalProcessResidualProcessException)
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.ResourceCleanup,
                "$.cleanup",
                $"Capability '{descriptor.Identity.Key}' process-tree cleanup could not be confirmed.",
                "Inspect and reclaim the owned process tree before retrying this capability.");
        }
        catch (Exception exception)
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.ProcessStart,
                "$.executablePath",
                $"Capability '{descriptor.Identity.Key}' failed to start its approved executable ({exception.GetType().Name}).",
                "Check the executable path, working directory, and command policy.");
        }

        if (!runResult.Started)
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.ProcessStart,
                "$.executablePath",
                $"Capability '{descriptor.Identity.Key}' did not start its approved executable.",
                "Check the executable path and command policy.");
        }

        if (runResult.ExitCode != 0)
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.ProcessExit,
                "$.exitCode",
                $"Capability '{descriptor.Identity.Key}' exited with {runResult.ExitCode}.",
                "Inspect the non-zero exit output and repair the external tool command or setup payload.",
                exitCode: runResult.ExitCode);
        }

        return ParseAndValidateOutput(
            descriptor,
            request.CorrelationId,
            runResult.Stdout,
            CapabilityTransportKind.ExternalProcess);
    }

    private static ToolInvocationResult ParseAndValidateOutput(
        ExternalProcessToolDescriptor descriptor,
        string correlationId,
        string output,
        CapabilityTransportKind transport)
    {
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(output);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return Failure(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.JsonParse,
                "$",
                $"Output from capability '{descriptor.Identity.Key}' was not valid JSON.",
                "Return a JSON object matching the external tool output schema.");
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return Failure(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.SchemaValidation,
                "$",
                $"Output from capability '{descriptor.Identity.Key}' was not a JSON object.",
                "Return a JSON object matching the external tool output schema.",
                transport: transport);
        }

        foreach (var property in descriptor.RequiredOutputProperties)
        {
            if (root.TryGetProperty(property, out _))
            {
                continue;
            }

            return Failure(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.SchemaValidation,
                "$." + property,
                $"Output from capability '{descriptor.Identity.Key}' did not include required property '{property}'.",
                "Return all required output schema properties from the external tool.",
                transport: transport);
        }

        return ToolInvocationResult.Success(correlationId, root);
    }

    private static ToolInvocationResult Failure(
        ExternalProcessToolDescriptor descriptor,
        string correlationId,
        CapabilityDiagnosticCategory category,
        string fieldPath,
        string detail,
        string repairHint,
        int? exitCode = null,
        TimeSpan? timeout = null,
        CapabilityTransportKind transport = CapabilityTransportKind.ExternalProcess)
    {
        return ToolInvocationResult.Failure(correlationId,
        [
            ToolDiagnostics.Create(
                category,
                descriptor,
                fieldPath,
                detail,
                repairHint,
                correlationId,
                transport,
                exitCode: exitCode,
                timeout: timeout)
        ]);
    }
}
