using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

internal sealed record SharedProviderValidatedRuntimeShape(
    ProviderProfile Profile,
    SharedProviderImport Import,
    SharedProviderSource Source,
    SharedProviderCatalogPublication Publication,
    Uri BaseUri,
    SharedProviderSourceInstanceId SourceInstanceId,
    ProviderTransportKind Transport,
    ProviderProfilePurpose Purpose);
