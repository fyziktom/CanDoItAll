using System.ComponentModel;

namespace CanDoItAll.Modules.Projects;

[Description("The project currently bound to a normalized external namespace/key pair. Use LifetimeId as expectedLifetimeId when resolving again and when editing this project.")]
public sealed record ProjectExternalIdentityResolution(
    Guid ProjectId,
    Guid LifetimeId,
    string ExternalNamespace,
    string ExternalKey);
