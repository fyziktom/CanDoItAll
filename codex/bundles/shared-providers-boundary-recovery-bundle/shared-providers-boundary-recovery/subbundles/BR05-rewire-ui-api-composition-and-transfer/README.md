# BR05 — Rewire UI, API, composition, and transfer

## Objective

Finish all outer-layer wiring so the correct boundary is visible in DI, UI, endpoint code, and database transfer.

## Agent provider UI

1. Keep `/agents?tab=providers` authoritative.
2. Replace `WorkspaceService` injection and Workspace provider aliases with ProviderManagement ports/models.
3. Remove wording such as “Workspace-owned provider” and “workspace-backed”.
4. Preserve provider list/editor, secret handling, health/pricing display, personal/shared/hybrid representation, and bookmarkable tab behavior.

## Workspace UI

1. Remove all duplicate provider editor state, validation, save/delete methods, and provider DTOs.
2. Retain only a compatibility redirect to `/agents?tab=providers` where old URLs exist.
3. Retain opaque default-provider preference UI only when it is genuinely workspace-specific and can use a narrow provider catalog/query port without owning provider behavior.

Do not add a Workspace-to-ProviderManagement project reference merely for a dead settings panel. Route users to the authoritative AgentFramework UI.

## Web APIs

1. Keep existing route templates and wire DTOs.
2. Make shared-provider catalog, relay, source/admin, and related endpoints depend on ProviderManagement application ports.
3. Remove provider-specific Workspace imports from endpoint files.
4. Keep HTTP/auth mapping in Web and domain behavior in ProviderManagement.
5. Preserve secret redaction, authorization, status codes, and error contracts.

## Dependency injection and composition

1. `AddWorkspaceModule()` registers zero provider/shared-provider services.
2. Add and invoke `AddAgentFrameworkProviderManagement(...)` exactly once.
3. Remove registration-order replacement between legacy and AgentFramework runtime gateways.
4. Hosted recovery/synchronization services are registered by ProviderManagement/Composition, not Workspace.
5. Confirm service lifetimes remain safe for DbContext, secret store, HTTP clients, caches, and hosted workers.

## Database transfer

1. ProviderManagement exports/imports provider profiles, provider secrets, metadata, publications, sources/imports, and relevant shared-provider state according to current supported semantics.
2. Workspace exports/imports only workspace data and its opaque default-provider ID.
3. Composition coordinates ordering and cross-reference restoration.
4. Preserve backward compatibility for existing transfer payloads where practical; add version-aware mapping rather than silently changing meaning.
5. Never emit secret plaintext.

## Acceptance

- Agent provider UI has no Workspace provider dependency.
- Workspace has no second provider editor.
- Web provider endpoints have no Workspace provider dependency.
- ProviderManagement registration occurs exactly once.
- Workspace DI is provider-free.
- Transfer ownership follows the target matrix.
- Existing route/UI behavior remains compatible.

## Focused tests

- Agent provider panel service wiring
- old Workspace provider settings redirect
- endpoint route/authorization/DTO compatibility
- DI registration uniqueness and lifetime validation
- provider transfer round-trip and workspace default-ID restoration
- secret redaction/no plaintext

## Commit

`BR05: rewire provider UI API and composition`
