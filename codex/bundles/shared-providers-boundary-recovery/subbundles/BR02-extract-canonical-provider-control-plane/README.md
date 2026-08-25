# BR02 — Extract the canonical provider control plane

## Objective

Move the pre-existing general provider ownership out of Workspace and make ProviderManagement the canonical source for persisted profiles and provider administration.

This subbundle addresses the root cause that made the shared-provider implementation drift into Workspace.

## Required moves

1. Extract the persisted provider-profile entity and EF configuration from Workspace into ProviderManagement.
   - Prefer a collision-free CLR name such as `ProviderProfileRecord`.
   - Preserve exact table, columns, keys, indexes, lengths, concurrency semantics, and IDs.
2. Extract provider CRUD, validation, provider secret mutation, capability/manifest handling, health/pricing administration, and profile query/editor models into ProviderManagement.
3. Extract database-backed provider registry/projection behavior currently named around Workspace:
   - `WorkspaceBackedAgentProviderProfileRegistry`
   - `WorkspaceAgentProviderProfileMapper`
   - runtime profile snapshot/commit observation services
   Rename by responsibility, not by historical storage location.
4. Extract provider-related database transfer implementation from Workspace. Keep workspace default-provider preference transfer separate.
5. Remove provider CRUD and editor DTO ownership from `WorkspaceService`, `WorkspaceModels.cs`, and Workspace settings code.
6. Move the existing legacy provider execution/adapter code out of Workspace only as a temporary compatibility implementation inside ProviderManagement when needed for a buildable checkpoint. Mark it internal and obsolete with an English comment naming BR04 removal. Do not create a new public contract around it.
7. Update immediate consumers so Workspace no longer needs provider entity or service types.
8. Preserve existing UI/API behavior pending later cleanup.

## Required end-of-subbundle Workspace state

Workspace may contain:

- opaque `DefaultProviderProfileId` preference
- redirect to `/agents?tab=providers`

Workspace must no longer contain:

- provider-profile EF entity/configuration
- provider editor/save/delete implementation
- provider secret mutation
- provider database transfer implementation
- canonical provider registry/projection

A transitional direct execution implementation may not remain in Workspace; if still required before BR04, it is isolated in ProviderManagement.

## Dependency controls

- ProviderManagement has no Workspace reference.
- Workspace must not gain a ProviderManagement reference solely to forward old provider methods.
- Update consumers directly or move orchestration outward. Do not solve the inversion with a facade in Workspace.

## Persistence controls

- Keep `Workspace_ProviderProfiles` physical table name.
- Do not generate a destructive migration.
- Do not rename existing columns or foreign keys.

## Focused tests

Proof tier: Behavioral

Selected owning tests:

- `ProviderCatalogProjectionFailureTests`
- `ProviderProfileSaveValidationTests`
- `ProviderRuntimeProfileSnapshotServiceTests`
- `ProviderManagementBoundaryTests`
- provider database-transfer tests added by this subbundle

The exact test filter and expected discovery count are frozen before the
focused run after the owning test sources have been updated. The impacted-test
analyzer is rerun after implementation; any healthy, high-confidence additions
are included in the frozen checkpoint. The broad non-container unit suite is
deferred to BR07 as required by the bundle execution contract.

Add/update tests for:

- provider profile CRUD
- secret create/replace/delete semantics
- referenced-provider deletion policy
- persisted-to-runtime profile mapping
- runtime snapshot/revision behavior for personal providers
- provider export/import excluding Workspace-only preference

## Acceptance

- Workspace has no canonical general provider ownership.
- Agent provider management still functions through ProviderManagement.
- ProviderManagement architecture test remains green.
- Provider profile data model is schema-compatible.
- Affected Workspace, ProviderManagement, AgentFramework, and transfer projects build.

## Commit

`BR02: extract canonical provider control plane`
