# Codex — multi-instance provider verification, then Sharing UI PP2

You are the implementing senior C#/.NET/Blazor architect. Finish the ordered scope in this
complete package on the existing working branch. Do not stop at a plan or after one dialog.
This prompt authorizes implementation and testing, and explicitly requires signed local commits.

## Read and establish origin

Read current `AGENTS.md`, `.github/copilot-instructions.md`, `docs/testing.md`, current CI,
`docs/architecture/ui-component-seams.md`, the maintained PP1 record, this package's required
inputs in `bundle.json`, and `shared/README.md` with its architecture files. Current repository
contracts beat stale historical notes. Preserve the history packages under `codex/bundles`;
never modify a supplied NOT_RUN template into a validation result. Removing packages before
merge is not in scope. The reviewed commit is not a branch/reset instruction.

Record actual application, Components, FileTools and relevant tool sibling SHAs, uncommitted
inputs, SDK, build configuration and output hashes. Distinguish product commits from bundle-only
history commits. Inspect a diff rather than relying on a completion label. Use the available
CodeAnalytics/Components MCPs for impact and component contracts; record their genuine absence
and use source/MSBuild/CLI alternatives rather than stalling or inventing tool results.

### Signing is part of the task, not a final optional step

Before long test work, tell the operator that PGP signing needs their unlock. Inspect the existing
Git/GPG configuration without printing secrets, arrange native pinentry and preserve the same
host user, GnuPG home, agent and persistent PowerShell session for subsequent commits.
Follow COMMITS_AND_SIGNING.md. Do not collect, cache in your own files, pass on a command line,
or export a private key/passphrase. Cache expiry still applies. No unsigned fallback, no
`--no-verify`, no unsigned GitHub API commit and no later promise to sign. Use coherent commits
for (1) S0 test/fix closure, (2) sharing/publication rendering, (3) source/refresh rendering,
and (4) final cross-instance validation/documentation when such chunks exist. Combine tightly
coupled chunks if needed; do not create empty commits or one commit per cosmetic edit.
Verify every new commit locally. No push, merge, rebase of user history, or package release.

## Operator priorities

PP1's native/component/sandbox results are valuable but did not execute the external two-instance
acceptance. Do not relabel that gap as covered. Reuse the existing three-application container
fixture and complete native-driver model tests through actual production UI and HTTP.
Only an external upstream's responses may be scripted. Provider drivers, publication,
import/materialization, API access, credentials, receipts and UI remain real production paths.

The shared instance's accepted model names, default and public metadata must survive publication
and client import. Include native OpenAI defaults, not only hand-written synthetic model catalogs.
Display names are not route IDs: keep `sp1...` IDs in internal requests and persistence; show
source model names in operator labels. A shared Ollama profile being OpenAI-compatible on the
client does not authorize replacing its models with a local OpenAI fallback list.

Work only at large desktop sizes, primarily 1920x1080/100% zoom. No mobile/tablet tuning.
The purpose is clean UI boundaries and a useful isolated dotnet-watch loop, not an API-only
migration, runtime rewrite, general responsive redesign or extra policy framework.

## Stage S0 — rigorous provider baseline, regressions and repairs FIRST

1. Read MULTI_INSTANCE_RUNBOOK.md and inspect the real runner/Compose files in full before executing
   them. The existing runner has fixed default roots/project names and destructive Reset behavior;
   do not assume an environment variable isolates them. Parameterize or safely scope the harness
   when necessary. Never reset a retained/manual fixture or use the ordinary 5032 application.
2. Create task-owned central, client-a and client-b application containers with isolated data,
   control planes, signing keys and secrets; use the existing separate PostgreSQL databases/roles
   and deterministic upstream topology. Include an unselected personal provider on a client.
   Keep the test network isolated. Record actual container/image/process identities and source
   hashes. No TestServer-only substitute, shared in-memory repository, or host-to-itself call.
3. Execute the existing protocol scenarios plus the specific UI/model matrix. Create/import/sync
   through real production controls. The command-line fixture alone does not prove those controls.
   The old ExternalSharedProviderUi helper overwrites provider model lists and hard-codes a fixture
   hostname; preserve that useful synthetic lane but add a distinct native-default lane with its
   source catalog captured BEFORE any synthetic rewrite.
4. Reproduce S0-R1 (raw routing ID in a human-facing tree tooltip) and S0-R2 (updated catalog with
   stale acquired imported editor after a metadata/toolbar refresh). Confirm via real component
   events, fix at the narrow rendering/state boundary and test exact names, added/removed/default
   changes, separate browser circuits and preservation of a dirty LOCAL provider draft. Do not
   solve S0-R2 by globally replacing every acquired editor or by removing the same-target no-op.
5. Run source-to-client parity for every published native-default model, and invoke default plus
   non-default representatives through each relevant transport. Verify raw routing evidence on
   the client leg and exact upstream model on the source leg. Include duplicate names in separate
   publications, caller scopes, price null-versus-zero, Thinking controls and saved unavailable IDs.
6. Test resync/304, source changes, two independent clients, local aliases, credentials, outage,
   unpublish/republish, restart of central/client and source identity mismatch. Assert no fallback
   invocation of a similarly named local provider. Track request counts and native outcomes.
7. Repair small reproduced defects with regression tests. Never weaken constraints, model filters,
   network checks, approvals, publication eligibility or assertions to obtain a pass. Larger owner,
   schema or authority problems require a causal map and block the affected unsafe continuation.

Passing S0 core naming/routing/security scenarios is a prerequisite to structural PP2 work.
A missing Docker prerequisite is an actionable environment block, not PASS. Attempt safe owned
provisioning supported by the existing environment; do not reuse real instances or silently skip
this operator-mandated lane. If blocked, finish available diagnostics/tests and preserve explicit
blocked evidence, but do not call the shared boundary validated or start speculative reshaping.
Commit the coherent S0 implementation/tests with PGP before the next major stage.

## Stage PP2 — Sharing and source-connection renderers

Preserve PP1 catalog and Connection/Prices/Runtime/Thinking. Extract the complete current Sharing
family: local publication/eligibility, imported-local alias/enabled/retirement, sources list,
source editor, Test, discovery/selection/import, synchronization, enable/disable/delete,
confirmations, pending-result/verification/delivery UI and reusable source-refresh rendering.
Use the actual child components and scoped styles, not wrappers around original large renderers.

Preferred leaf: `src/UI/CanDoItAll.AgentFramework.SharedProviders.UI` and an independent
`src/Sandboxes/CanDoItAll.AgentFramework.SharedProviders.UiSandbox`. An equivalent cohesive
boundary is acceptable after a graph comparison. Extra contracts/presentation projects require
an actual need. Do not make existing Providers.UI, Agent Editor.UI, Workspace.UI or their
sandboxes acquire this feature/backend graph. Compose the new leaf only in the production host.

Keep `ISharedProviderManagementService`, original mutation/recovery and delivery semantics,
public routing/catalog protocols, credential resolution, persistence, source identity,
network policy and runtime authority at their current owners. Use safe UI projections for
implementation-bound DTOs; do not move EF entities or runtime services into UI, and do not add
an upward reference from Core/Foundation/ProviderManagement to a feature renderer. Existing
neutral SharedProviders.Abstractions may be reused after graph/public-signature inspection.

One operation retains its exact source, import/publication, expected tokens, generation and
submitted values. Separate read snapshots from draft settings. Later metadata must not discard
new alias/URL/secret-reference edits; stale callbacks must not target another source. A known
commit survives view retirement and failed delivery. Retry delivery/verification is not repeated
publication/import/diagnostic. Keep durable facts with their current owner and UI transients
with their own session. Do not build a second generic operation framework.

Credentials: PP2 selects secret metadata; actual token issuance and vault editing remain
Workspace/API Access integrations. Local fixture-only issuance/rotation/revocation is explicitly
authorized for verification. There is no permission to mint or revoke real external credentials.
Do not place tokens, protected roots or secret values into URLs, histories, screenshots or traces.

Request History and unrelated test-chat/model-maintenance/team/capability authoring remain
functional production hosts and are NOT extracted in this bundle. Test History as a sensitive
consumer of cross-instance runs without redesigning it. Update the remaining-surface census
honestly: PP2 is not all-provider-UI completion.

## Validation and closure

Use current source-backed discovery before each new filter; build changed production projects
and owning tests before no-build execution. Use deterministic barriers, not sleeps, for races.
Test actual renderers with BaseLib, native owners and isolated PostgreSQL. Source sandbox and
independently published sandbox both need desktop browser coverage and served-asset checks.

After PP2 is stable, rebuild the actual app image from FINAL sources, rerun the full required
multi-instance suite and UI parity/security/consumer matrix. S0's old image cannot prove PP2.
Use app-created provider definitions in Agent Editor, Simple Chat, Workflow and Project Structure
file/approval journeys; verify native model identity, actual output and file bytes. Exercise
formerly blank/deferred Sharing and Sources through the new renderers. Keep plain source-owned
checks and test fixtures separate from paid live evidence.

Run the named-trigger broad-validation decision once at a stable checkpoint; this combined
sharing/source/exact identity boundary warrants one final frozen Stable checkpoint unless current
repository policy documents a strictly narrower justified alternative. Do not repeat full Stable
at every phase, and do not run concurrent watch/builds against frozen outputs. Run full mandatory
portability-static review/enforcement, inspect all baseline changes, and preserve failed attempts.
A mixed broad run followed by targeted repair is not a retroactive all-green whole run.

Repeat the PP1 hot-reload reproduction separately from rebuild/restart. Investigate a bounded
number of consecutive edits in a clean isolated sandbox. Do not globally disable hot reload,
alter watch exclusions, or upgrade packages to manufacture a speedup. If repeated hot reload
still fails, retain the restart measurements as a different method and write a causal tooling
report with SDK, files, detection/compilation/served-asset/DOM evidence.

Finish documented larger signed commits, verify signatures and final source/image/assembly
provenance. Deliver safe evidence and closure flags, exact remaining issues, reproducible commands,
updated module map and owned resource cleanup. Do not claim complete until required source/client
checks really ran. Preserve paid-model 40/40 history; make zero new real-provider paid calls.
Small actual defects are fixed here; a new complex defect is mapped without speculative redesign.
Do not start History extraction, Workflow authoring, Workbench or Processes.
