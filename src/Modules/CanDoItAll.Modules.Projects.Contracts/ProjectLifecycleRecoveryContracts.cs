namespace CanDoItAll.Modules.Projects;

public sealed record ProjectLifecycleFrozenSubmission(ProjectLifecycleSubmission Submission, string Payload);
public sealed record ProjectLifecycleReceiptPayload(ProjectLifecycleAccepted Accepted, string Payload);
public sealed record ProjectLifecycleAbsencePayload(ProjectLifecycleLookup.ClosedAbsent Absence, string Payload);

public interface IProjectLifecycleRecoveryCodec {
    ProjectLifecycleFrozenSubmission Freeze(ProjectLifecycleSubmission original);
    ProjectLifecycleFrozenSubmission ReadSubmission(string payload);
    ProjectLifecycleReceiptPayload EncodeAccepted(ProjectLifecycleSubmission original, ProjectLifecycleAccepted accepted);
    ProjectLifecycleReceiptPayload ReadAccepted(ProjectLifecycleSubmission original, string payload);
    ProjectLifecycleAbsencePayload EncodeClosedAbsent(ProjectLifecycleSubmission original, ProjectLifecycleLookup.ClosedAbsent absence);
    ProjectLifecycleAbsencePayload ReadClosedAbsent(ProjectLifecycleSubmission original, string payload);
}
