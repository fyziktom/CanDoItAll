using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

public sealed record ProjectLifecycleWriteContext(Guid DatabaseProfileId, DatasetNamespaceId Namespace,
    ProjectCallerBinding CallerBinding, ProjectOperationEpoch Epoch, DateTimeOffset AdmissionClosesAtUtc,
    DateTimeOffset ReplayUntilUtc);
