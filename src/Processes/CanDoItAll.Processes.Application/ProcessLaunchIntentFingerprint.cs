using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public static class ProcessLaunchIntentFingerprint {
    private enum CallerKind {
        LegacyUnverified,
        LocalOperator,
        AuthenticatedOperator,
        AgentExecution
    }

    private sealed record CallerIdentity(CallerKind Kind, Guid ProfileId, string Subject, ProcessLaunchOperatorSurface? Surface);

    public static string Compute(ProcessLaunchRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        var requestHash = Hash(JsonSerializer.Serialize(new {
            request.DefinitionKey,
            request.ProcessDefinitionId,
            request.LiveRunProfileKey,
            request.ProjectId,
            request.ProjectNodeId,
            request.RequestedBy,
            Variables = request.Variables.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(),
            ExecutorOverrides = request.ExecutorOverrides.OrderBy(item => item.StepKey, StringComparer.Ordinal).ToArray(),
            request.RootRunIdOverride,
            request.RunReadiness,
            request.ProjectAdmission,
            request.LinkTarget,
            AgentOperation = (request.Authority?.Principal as ProcessLaunchPrincipal.AgentExecution)?.Operation,
            Caller = Caller(request.Authority)
        }));
        return request.ProducerInputFingerprint is { } input
            ? Hash(JsonSerializer.Serialize(new { Request = requestHash, ProducerInput = input }))
            : requestHash;
    }

    public static string CallerFingerprint(ProcessLaunchAuthority? authority) => Hash(JsonSerializer.Serialize(Caller(authority)));

    public static void RequireSameCaller(ProcessPreparedLaunch preparation, ProcessLaunchAuthority? caller) {
        if (preparation.Authority is null || caller is null ||
                CallerFingerprint(preparation.Authority) != CallerFingerprint(caller)) {
            throw new InvalidOperationException("The saved process launch cannot be observed or continued through another or missing authority source.");
        }
    }

    private static CallerIdentity Caller(ProcessLaunchAuthority? authority) => authority?.Principal switch {
        ProcessLaunchPrincipal.LocalOperator local => new(CallerKind.LocalOperator, authority.DatabaseProfileId, string.Empty, local.Surface),
        ProcessLaunchPrincipal.AuthenticatedOperator authenticated => new(CallerKind.AuthenticatedOperator, authority.DatabaseProfileId, authenticated.SubjectId, ProcessLaunchOperatorSurface.Api),
        ProcessLaunchPrincipal.AgentExecution agent => new(CallerKind.AgentExecution, authority.DatabaseProfileId, agent.Ceiling.AgentId.ToString("D"), null),
        null => new(CallerKind.LegacyUnverified, Guid.Empty, string.Empty, null),
        _ => throw new InvalidOperationException("The process launch has an unsupported authority source.")
    };

    internal static string Hash(string value) => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
