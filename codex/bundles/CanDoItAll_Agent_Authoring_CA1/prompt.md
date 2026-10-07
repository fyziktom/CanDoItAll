# Execute: Agent capability authoring CA1 and team editor completion

You are implementing a substantial, staged C#/Blazor UI-decoupling slice on the owner's current
checkout. Finish the actual capability definition wizard/editor and technical-agent team
administration, not merely a plan or one tab. First close bounded PP3 carry-over. The owner
explicitly prefers a longer coherent run and fixes before continued extraction.

## Entry and authority

Read current AGENTS.md, .github/copilot-instructions.md, docs/architecture/ui-component-seams.md,
docs/testing.md and applicable CI, plus relevant CanDoItAll.SharedInfo guidance. Read
[shared/prompt.md](shared/prompt.md) and the shared architecture/validation sections, then this
package's [scope](SCOPE_AND_ARCHITECTURE.md), [source review](CAPABILITY_SOURCE_REVIEW.md),
[teams](TEAMS_SOURCE_REVIEW.md), [state contracts](STATE_AND_OWNER_CONTRACTS.md),
[validation](VALIDATION_MATRIX.md) and [journeys](APPLICATION_JOURNEYS.md).

The review used main components-decoupling at b3aec979eae708edd0a53b42cef41b9d52fbb49b.
Record actual current revisions, dirty/index state, source mode, SDK and siblings. Do not reset,
change branches, discard user edits, remove historical bundles or assume review SHAs are required
checkouts. Distinguish bundle-only commits from implementation changes. Refresh the affected
source/caller/test census before edits; the [source register](SOURCES.md) identifies what was read.

Arrange native OpenPGP unlock early when required. Use the existing configured key, host user,
GnuPG home, persistent native session and gpg-agent; no password/key in chat, scripts, logs or
containers. Make verified signed commits at coherent stages, not dozens of microcommits.
See [signing](COMMITS_AND_SIGNING.md). Push, merge, release and bundle cleanup are not authorized.

## S0 — preserve PP3, close bounded carry-over, then continue

PP3's entire History rendering family is already extracted. Preserve lazy Search, frozen applied
queries, origin-bound results and dialogs, explicit content authorization, cancellation and
content clearing. Preserve the native exact-role fix for readable Workflow PrimaryEvidence.
Do not re-extract History or repeat the entire old campaign merely to improve its headline.

1. Locate the existing locally tested Tooltip fix b495d4c4a28f0a6588ba10bfaa7be6e8409eae18 or
   its reviewed successor in Components. Remote development was still 4a858412d2c2a3f6123bf23d8c4584f05b47627d
   and a fetch of the fix returned no commit. Verify actual source, signature and loaded
   assemblies/assets. Reuse the completed repair, not a substitute copied into main. Record
   local verification separately from remote dependency delivery; a still-private sibling
   must not be called remotely delivered. Do not push without separate authorization. A verified
   local pair can support this development slice; unavailable source blocks dependent proof,
   not unrelated safe mapping. Ask specifically for the missing sibling only if local discovery
   cannot resolve it; do not request a password.
2. Reproduce the precise existing Workflow OpenAPI description coverage failure. Document the
   actual modelCatalog and isSourceManaged response properties through the canonical schema
   documentation path. Their meaning is model ID/display-name metadata and source ownership;
   do not remove JSON fields, hide them from coverage, or weaken the coverage assertion.
   Preserve route IDs, availability and override restrictions. Build Web and the owning test
   assembly, discover and run the exact failing family. This small task explicitly permits the
   necessary schema/XML documentation despite the default preference against unnecessary XML.
3. Carry the recorded Responses streaming timing flake honestly. If rerunning that lane, add
   bounded numeric timing/terminal-event evidence and identify fixture/image/load conditions.
   Same-image resume passed, but did not establish a cause. Do not increase production/test
   thresholds, suppress the assertion or rerun until green. A small proven harness issue can be
   fixed; a larger runtime issue is mapped separately. This is not a fresh mandatory full stress test.
4. Run targeted PP3 owner/origin/content and delivery checks relevant to changed sources. Update
   the maintained module map with PP1/PP2/PP3, not historical sealed audits. Then move on.

## C1 — complete capability definition authoring

Extract BOTH CapabilitySetupWizardDialog and CapabilityDetailsDialog, their real typed
MCP/Skill/Tool configuration renderers, setup-result views, raw metadata and CSS/assets.
Wizard scope is all three existing steps and all supported creation modes, including actual
bounded skill upload. Detail scope is Identity, Configuration and Raw, including saved/built-in
and unavailable/invalid legacy states. These are definition authoring, not the already completed
capability list or Agent Editor A2 assignment surface.

Prefer a cohesive capability-authoring rendering leaf and independent sandbox, for example
CanDoItAll.AgentFramework.CapabilityAuthoring.UI / .CapabilityAuthoring.UiSandbox. Reuse a
better existing light boundary if the evaluated graph justifies it. No mandatory helper/interface
count or class layout. A separate contracts/presentation project requires a real dependency
boundary, not consistency of naming.

The current ConfigurationEditorSupport mixes UI raw values, serializers and Core policy helpers.
Do not copy it into a new assembly while retaining Core/WorkspaceService/runtime dependencies.
Separate the smallest useful draft/presentation family from native compilation/normalization.
Reuse Models and genuinely light capability/MCP abstractions after evaluating their graphs;
keep process execution, HTTP, MCP clients, secret resolution and trusted path policy in their
owners. Do not add reverse references from Core/Foundation/neutral components to product UI.

Preserve existing canonical configuration semantics, supported extension data, binding references,
MCP argument order, authority-bearing paths, approval/side-effect policy, output limits, inline
skill resources and built-in identity restrictions. No silent defaulting of malformed stored
configuration followed by an unintended rewrite. Raw invalid text remains editable with an error.
Intentional rejection/removal of forbidden plaintext headers/environment is not an invitation to
preserve secrets as unknown extension data.

Use one draft/context per editor lifetime, immutable operation submissions and explicit accepted
identities. Setup invocation is not Save, not an automatic operation on typing, and not published
capability proof. It can start a real process/server or make an HTTP call. A successful diagnostic
for an older configuration must not appear as verification of the current configuration. Preserve
it as clearly historical or invalidate that presentation; never auto-repeat the effect.

Characterize and fix the concrete risks in the source review: effect origin, raw validation,
post-dispatch edits, update conflicts, known rejection versus unknown commit, nested completion,
and parent refresh after a confirmed create. Current SaveCapabilityAsync treats a supplied missing
Id as an invalid UPDATE. Do not copy the provider candidate-ID technique by populating Id on a
create. Reuse a suitable existing owner result or make a bounded additive owner-specific seam
that preserves established non-UI callers. No new generalized receipt/replay framework.

Keep the current parent behavior: an existing agent's capability toggle/assignment can save its
WHOLE unsaved agent draft, whereas a new agent stages the assignment. Existing native composition
tests prove that distinction. Do not change it to uniform local staging or a partial-agent save.
A definition successfully created but not yet assigned remains exactly one definition; read-back
and assignment retry cannot recreate it. Preserve A2 Verify's separate proof reconciliation.

## C2 — complete technical-agent team administration

Move actual team metadata, icon selection and member selection renderers; integrate the actual
catalog host and its existing operation owner. Do not redesign CRM/HR teams or make technical
membership confer project/storage/tool authority. Reuse real AgentSelectionCard and Material icon
catalogs; do not pull the entire MAF component runtime solely to display a card.

Prefer the existing lightweight AgentFramework.UI and its scenario host for small team views,
or one justified sibling leaf. Do not broaden the capability leaf simply to share a dialog title.
The result is independent scenarios for the complete team family, not a second technical-agent
catalog. Read [team-specific requirements](TEAMS_SOURCE_REVIEW.md).

Capture metadata, exact team identity, original profile and membership context before awaits.
Metadata-only editing must not silently revert membership changed elsewhere. Preserve missing-agent
refusal and unrelated team/agent state. If the legacy full-team upsert cannot provide the editor's
required guarantee, add a bounded editor operation at the actual coordinated owner; a pre-read
check or UI-only mutex is not concurrency control. Do not silently redefine the existing API/store
upsert, add a database migration or invent a global ownership system.

Member Save returns the original selection to the parent, which performs the native update.
Bind that result to the opening team/profile/lifetime; old callbacks, A-B-A and stale cancellation
cannot update or close a successor. Distinguish confirmed membership mutation from a failed later
catalog refresh. Preserve filtering, multiple memberships, private-provider badges, icons and
other agent settings. Whole-team deletion must not delete its agents.

## C3 — integrated validation, performance and closure

Use [the matrix](VALIDATION_MATRIX.md) and [native journeys](APPLICATION_JOURNEYS.md).
Build affected production projects first, refresh owning tests, verify discovery, then execute
focused cases. Use available CodeAnalytics and Components MCP; if unavailable, use explicit
source/caller/evaluated-graph discovery and native CLI, and record that limitation.

Prove actual UI-created definitions, explicit harmless setup execution, native saved identities,
fingerprint conflicts, A2 assignment/Verify, agent tool allow/deny, project file content and History.
Use a scoped shared provider through real owned application instances for a relevant consumer;
the external model response may be deterministic, but runtime tools, approvals, canonical stores,
queries and bytes must be real. No capability/permission/approval broadening to make a test pass.
A capability definition, assignment, runtime exposure, approval and a verification proof are
separate facts. Team grouping changes none of these authorizations by itself.

Use unique owned fixtures, loopback ingress, separate stores and marked roots. Preserve ordinary
5032, historical evidence, retained provider instances and all foreign containers. No paid calls
or reset of the old exhausted budget. Harmless local HTTP/MCP/process fixtures may be provisioned
and invoked only within the ownership and limits in the runbook.

Run source AND standalone published sandboxes with production-equivalent real controls/assets.
New visual validation is large-desktop only, 1920x1080 at scale 1. No small/medium screen tuning.
Measure repeat Razor/C#/CSS visible changes with the same PID and source restoration. Record
restart fallback separately from hot reload. The goal is a smaller useful development loop, not
an arbitrary project count or a universal Web speedup claim.

After coherent source changes settle, run full affected authoring/team families plus the named
application journeys. Reassess broad Stable only at the final frozen checkpoint according to
current invalidation rules. Real shared catalog/Core owner changes normally trigger that decision;
render-only work does not automatically require the whole suite. Do not launch it after every
step. Provide adequate owned PostgreSQL capacity and observe storage use. Mandatory portability
scan/review/enforcement, docs, secret export and source-pair delivery checks remain required.

Fix small reproduced defects with regression tests. If a new issue requires redesign of shared
transactions, authority, schema or runtime admission, preserve precise evidence and map the owning
call graph, effects, affected consumers, reproduction and proposed repair slices; do not improvise
a broad redesign. Continue independent safe work, but never label an unresolved critical path ready.

Commit coherent compiling stages with verified signatures. Finish with precise scope completion,
new production/sandbox graphs, test attempts and source fingerprints, exact original/follow-up
failures, remaining qualifications and dependency publication. Do not stop after S0, one wizard
kind or the team dialog. Do not start Workflow canvas, Workbench, Processes or another family.
