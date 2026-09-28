using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;

namespace CanDoItAll.Web.Api;

internal sealed record ValidatedApiCredential(Guid Id, string Issuer, string DisplayName,
    ApiCredentialKind Kind = ApiCredentialKind.Machine, ApiSessionIdentity? Session = null);
