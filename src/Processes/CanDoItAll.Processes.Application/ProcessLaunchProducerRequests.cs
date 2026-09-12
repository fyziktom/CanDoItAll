using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public interface IProcessLaunchOperatorAuthoritySource {
    Task<ProcessLaunchAuthority> CaptureLocalAsync(Guid? projectId, ProcessLaunchOperatorSurface surface,
        CancellationToken cancellationToken = default);
    Task<ProcessLaunchAuthority> CaptureAuthenticatedAsync(Guid? projectId, string subjectId, DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default);
}

public static class ProcessLaunchProducerRequests {
    public static Task<ProcessLaunchAuthority> CaptureUserInterfaceAsync(this IProcessLaunchOperatorAuthoritySource source,
        Guid? projectId, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(source);
        return source.CaptureLocalAsync(projectId, ProcessLaunchOperatorSurface.UserInterface, cancellationToken);
    }

    public static string InputFingerprint(ProcessLaunchRequest input, ProcessLaunchAuthority caller) {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(caller);
        return ProcessLaunchIntentFingerprint.Compute(input with {
            Authority = caller with { ProjectAdmission = null },
            ProjectAdmission = null,
            LinkTarget = null,
            ProducerInputFingerprint = null
        });
    }

    public static async Task<ProcessLaunchRequest?> FindReplayAsync(ProcessLaunchRequest input, ProcessLaunchAuthority caller,
        IProcessPreparedLaunchStore store, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(store);
        var saved = input.PreparedAdmissionId is { } preparedId
            ? await store.GetAsync(preparedId, cancellationToken)
            : input.CallerIntentId is { } intentId
                ? await store.FindByIntentAsync(intentId, cancellationToken)
                : null;
        if (saved is null) {
            if (input.PreparedAdmissionId is not null) {
                throw new InvalidOperationException("The reviewed process preparation was not found.");
            }
            return null;
        }

        ProcessLaunchIntentFingerprint.RequireSameCaller(saved.Preparation, caller);
        if (saved.Preparation.Request.ProducerInputFingerprint != InputFingerprint(input, caller) ||
                input.CallerIntentId is { } intent && saved.Preparation.CallerIntentId != intent) {
            throw new ProcessLaunchIntentConflictException(input.CallerIntentId,
                "The process retry changed its original input or has no saved producer input for comparison.");
        }
        return Restore(saved, caller, input.Execute);
    }

    public static ProcessLaunchRequest Restore(ProcessPreparedLaunchSnapshot saved, ProcessLaunchAuthority caller, bool execute) {
        ArgumentNullException.ThrowIfNull(saved);
        caller.Validate();
        ProcessLaunchIntentFingerprint.RequireSameCaller(saved.Preparation, caller);
        var original = saved.Preparation;
        var authority = original.Authority
            ?? throw new InvalidOperationException("The process preparation has no saved authority.");
        if (authority.ProjectAdmission != original.InitialCommit.Mutation.State.ProjectAdmission) {
            throw new InvalidOperationException("The saved process authority and admitted project do not agree.");
        }
        return original.Request with {
            CallerIntentId = original.CallerIntentId,
            PreparedAdmissionId = original.AdmissionId,
            Authority = caller with { ProjectAdmission = authority.ProjectAdmission },
            ProjectAdmission = authority.ProjectAdmission,
            LinkTarget = original.LinkTarget,
            ToolSource = original.ToolSource,
            Execute = execute
        };
    }

    public static async Task<ProcessLaunchRequest?> FindConcurrentReplayAsync(ProcessLaunchRequest mapped,
        IProcessPreparedLaunchStore store, CancellationToken cancellationToken = default) {
        if (mapped.CallerIntentId is not { } intent || mapped.Authority is not { } caller ||
                string.IsNullOrEmpty(mapped.ProducerInputFingerprint)) {
            return null;
        }
        var winner = await store.FindByIntentAsync(intent, cancellationToken);
        if (winner is null || winner.Preparation.Request.ProducerInputFingerprint != mapped.ProducerInputFingerprint ||
                ProcessLaunchIntentFingerprint.CallerFingerprint(winner.Preparation.Authority) != ProcessLaunchIntentFingerprint.CallerFingerprint(caller)) {
            return null;
        }
        return Restore(winner, caller, mapped.Execute);
    }
}
