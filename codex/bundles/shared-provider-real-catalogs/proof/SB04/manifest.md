# SB04 behavioral proof

Scope: N006/R6 avatar consistency and N007/R7 rebuilt pair plus isolated manual client.
Primary-agent review, not an independent review. Previous SB01-SB03 evidence remains
historical; this scope does not claim another full model/image/vision execution run.

## Cause and shipped behavior

The catalog seeded generated avatars with DefinitionId, while the settings editor and
nested picker used Name. Both previews now receive the stable existing ID. Explicit
bundled/uploaded URLs remain unchanged, and other AvatarPicker callers retain their
previous default seed behavior. No new UI layer, CSS, service, or shared library edit.

A fresh database was not provider-free: bootstrap seeded profiles/secrets, the canonical
loader added a runtime fallback, and the registry repeated that fallback policy. Typed
SeedDefaults options default to true for compatibility; false suppresses only automatic
initialization/fallback, preserving explicit providers and imports. Workspace totals now
use the canonical provider list instead of stale file-seed catalog counts.

## Exact focused checks

All paths below are relative to this directory. Project paths are repository-relative.
Use `dotnet test <project> --no-restore --filter <filter> --logger
'trx;LogFileName=<file>' --results-directory codex/bundles/shared-provider-real-catalogs/proof/SB04 -v quiet`.
Live browser runs use `--no-build` after a successful test-project build.

| Project under tests | Filter | Evidence | Result |
| --- | --- | --- | --- |
| Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj | FullyQualifiedName~LlmChatDefinitionUiTests | avatar-red.trx → avatar-green.trx | 3 fail/12 pass before; 15 pass after |
| Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj | FullyQualifiedName~ProviderInitializationIntegrationTests | initialization-red.trx → initialization-green.trx | 1 fail/1 pass before; 2 pass after |
| Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj | FullyQualifiedName~ProviderInitializationIntegrationTests | provider-count-red.trx | 1 fail/1 pass: canonical list empty but header count six |
| Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj | FullyQualifiedName~ProviderInitializationIntegrationTests\|FullyQualifiedName~AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests | provider-count-green.trx | 7 pass, including the two initialization cases |
| Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj | FullyQualifiedName~ProviderCatalogProjectionFailureTests | registry-green.trx | 12 pass |
| Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj | FullyQualifiedName~SimpleChatAvatarBrowserTests | avatar-browser-final-pass.trx; browser-discovery.txt | 2 pass on final Docker image |

Final nonduplicated focused total: 36 passing cases (15 + 7 + 12 + 2). No full-suite claim.
Frozen browser discovery and repeated final run: avatar-browser-confirmed.trx, two pass
again after avatar-browser-final-pass.trx. No retries inside the behavioral test.
Browser environment: CANDOITALL_REAL_SHARED_URL=http://localhost:5210,
CANDOITALL_REAL_CLIENT_URL=http://localhost:5212, CANDOITALL_AVATAR_UI_EVIDENCE is this
directory's absolute browser subdirectory. Fresh Chrome contexts, 1920x1080, no injected JWT.
The final test explicitly confirms first-visit database selection and waits for the
interactive workspace tab list before clicking controls; it waits for reset render state.

Earlier avatar-browser*.trx attempts with failures are retained, not passing evidence.
They exposed prerender/startup-confirmation timing and premature navigation/reset reads
in the newly authored test. They did not justify weakening production assertions.
The final test checks card, editor and picker; selected asset save/reload; reset save/reload.
Component cases additionally cover uploaded URL and rename stability.

## Deployment and manual consumer

- docker-build-final.txt: successful image build with both sibling library contexts.
- Image: candoitall-shared-providers-ui:avatar-blank-client-20260827-2.
- Image ID: sha256:32f3894029f8af3c3f2d3f02808b68c0e0a32227825bdb634c9809aa97f9f791.
- Pair replaced with existing Restart-TestInstances.ps1, preserving env/volumes and
  explicit loopback gateway trust. Rollbacks end before-avatar-count-20260827.
- Fresh Compose app recreated on final image; independent PostgreSQL role/database,
  app-data volume, API signing key and password; non-root/read-only root filesystem.
- Read-only SQL after recreation: Workspace_ProviderProfiles=0,
  Workspace_SharedProviderSources=0, Workspace_SharedProviderImports=0,
  Security_SecretRecords=0. Fresh role cannot connect to central/client_a/client_b DBs.
- 5210/5212/5214 health: 200 Healthy. Fresh Docker health is healthy.
- Fresh Docker DNS reaches candoitall-spui-shared:8080; anonymous catalog returns 401.
- Existing client's UI Test: Catalog connection verified, one source/three imports.
- Fresh Sharing/Add source and Simple Chat/New definition opened without auth denial;
  both drafts cancelled/discarded, no configuration saved.
- Exact operator instructions: ../../subbundles/04-avatar-and-fresh-client/HANDOFF.md.

## Visual review and limits

Inspected browser/client-selected-editor.png, client-existing-picker.png,
fresh-empty-sharing.png and fresh-simple-chat-create.png at 1920x1080. Correct image,
selection highlight and reset control are visible; editor Save/Cancel and picker Close
fit the viewport. Fresh header/list/source/import counts are all zero and Add source is
available without a local provider. Existing narrow New provider wrapping is unchanged.
The picker's existing AI generation help still displays an opaque shared-image route;
this avatar-identity repair makes no claim to change that separate help text.

Shallow-pass traps rejected: changing only the outer preview; using names as identity;
deleting seeded rows once; cloning populated data; trusting the header count alone;
disabling JWT checks or inserting provider credentials on the fresh client.
No secrets in proof. Live tests create only identifiable avatar-check draft definitions
on the existing pair. The user's fresh client remains untouched for actual import testing.

## Architecture gate

Pass. Existing typed owners retain their responsibilities. Composition binds options;
ProviderManagement owns the option; canonical loader alone owns fallback inclusion;
the existing workspace service composes canonical totals; Razor renders identity.
No project/reference/interface/schema or new partial-class changes. Registry shrinks.
ProviderManagement CodeAnalytics: before snap-20260827185022-b43fde6e (70 documents),
after snap-20260827191152-b43fde6e (71 documents), one project, 245 edges, zero scoped
cycles, no blocking errors. Eight informational factory-interpretation diagnostics
remain. Direct source review covers Composition/Core outside this scoped snapshot.
No whole-solution architecture or absence-of-existing-debt claim.
