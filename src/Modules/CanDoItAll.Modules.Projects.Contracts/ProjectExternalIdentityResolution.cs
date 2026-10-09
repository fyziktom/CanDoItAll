using System.ComponentModel;

namespace CanDoItAll.Modules.Projects;

[Description("The project currently bound to a normalized external namespace/key pair. Use LifetimeId as expectedLifetimeId when resolving again and when editing this project.")]
public sealed record ProjectExternalIdentityResolution(
    [property: Description("Identifier of the project currently bound to this external identity in the active database profile.")] Guid ProjectId,
    [property: Description("Current project lifetime. Send it as expectedLifetimeId when resolving again or editing the project to detect deletion and recreation.")] Guid LifetimeId,
    [property: Description("Trimmed, lowercase external namespace of the resolved identity.")] string ExternalNamespace,
    [property: Description("Trimmed, lowercase external key within the resolved namespace.")] string ExternalKey);
