# Administration boundaries (SB05/SB06)

## Current-state inventory

CodeAnalytics snapshot snap-20260827203310-80990695: four scoped projects, 390
documents; scoped project graph has no cycles. Exact sources inspected:
AgentProviderProfilesPanel.razor/.cs, SharedProviderManagementPanel.razor/.cs,
SettingsPage.razor/.cs, ApiAccess.cs, ApiServiceCollectionExtensions.cs,
ApiEndpointRouteBuilderExtensions.cs, ControlPlanePaths.cs,
ControlPlaneFileCoordination.cs and DurableFileWriter's existing private-write API.

SharedProviderManagementPanel mixes publication/import settings with source
connections. SettingsPage mixes unrelated settings with token issuance. ApiTokenService
has two constructor dependencies and no storage; JWT validation has no revocation
check. Existing focused tests cover source/publication UI and scoped API access.

## Boundary ownership and dependency direction

- SB05 extracts source connection UI into SharedProviderSourcesDialog; publication
  and imported-profile controls stay in SharedProviderManagementPanel. No business
  logic moves out of the existing management application service.
- SB06 extracts token creation UI from SettingsPage into ApiTokenAdministrationPanel,
  composing ApiScopePickerDialog and ApiTokensDialog.
- IApiTokenRegistry and metadata contracts belong to the existing Infrastructure
  control-plane boundary. FileApiTokenRegistry uses private durable writes and
  per-record IDs; it never stores JWT bearer values or signing keys.
- ApiTokenService must register every new JWT before returning it. Web authentication
  checks managed-token state on each request, after normal cryptographic validation.
- No new package/project references or business-runtime partial classes. Existing
  cohesive Razor/code-behind pairs remain allowed. Infrastructure cannot reference UI.
- ApiTokenAdministrationService requires IApiTokenAdministrationAccess for every
  operation. The Web adapter accepts the existing trusted local operator or an
  authenticated principal with the explicit api.tokens.issue scope; its unconfigured
  default denies access. The old Settings form had no such explicit gate.
  No new remotely accessible management API is needed for this request.

## Pattern decision

Use a narrow registry interface as a real persistence/test seam, not a generic
repository or service locator. Per-token private metadata files reuse the existing
durable filesystem primitive: authentication is one direct ID lookup, while a
dialog search scans metadata only on user demand and returns a bounded page.
This avoids adding an unrelated SQL engine to an instance-wide control plane.
No bearer values in search results, diagnostics or persisted metadata.

New tokens carry a signed managed-version claim and require an active registry
record. Deleting the record also denies them; missing/corrupt storage must not
grant access. Pre-feature tokens have no recoverable issuance history and remain
valid until expiry; the UI explicitly explains that upgrade limitation. Signature,
audience, issuer, expiry and scope checks remain in force for both generations.

## Testability and shallow traps

Independent registry tests: persistence/reopen, paging/search, revoke/delete,
concurrent writes and malformed metadata. HTTP tests prove actual denial after
revoke/delete, missing registration and unchanged scoped/legacy behavior.
Component tests prove dialog-only source management, lazy token data loading,
scope confirmation/cancel/empty selection and dangerous-action confirmation.
An empty selection never silently becomes the broad api scope.
A list row disappearing is not revocation proof.

## Checkpoints

SB05 component regression gate and desktop MCP normal/open-dialog inspection.
SB06 registry/issuer/authentication and UI tests, then real container MCP token
issuance/search/revoke/delete and protected-endpoint denial. Final source ownership,
no new project-cycle review, Docker health, existing data preservation and fresh
5214 zero-state proof. Existing provider model/catalog behavior is not reopened.
