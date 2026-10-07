# Capability authoring and technical teams (CA1)

Status: complete for local development on the verified source pair below. PP1, PP2, A2 and
PP3 remain completed slices. Remote Components delivery is still a separate qualification.
The sealed execution package remains under `codex/bundles/CanDoItAll_Agent_Authoring_CA1`.

## Source and delivery

The clean entry main was `d232c8a645acc576ca00b171b02898d9cb1f604b`, containing the CA1 package
after completed PP3. Production source froze at main
`73789c39598eb50eb06975ce6b6746e28015eb76`, with Components
`b495d4c4a28f0a6588ba10bfaa7be6e8409eae18` and FileTools
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`, SDK 10.0.303 and local sibling source mode.
Validation checkpoint `baaebd9ed312b1544c8c283b549dbf25117d32fc` changes only CI selection and
two browser helpers; its exact diff proves production source equivalence. Authoring and initial
consumer tests use `AgentAuthoringCa1`, the two browser follow-ups use `Ca1Ui`, and final Stable
is freshly built from that validation checkpoint in `Ca1Stable`. The original assembly receipts
retain their actual version suffixes. The final delivery commit contains maintained
documentation, its reviewed portability baseline entry and the bounded catalog-test fixture correction; its exact SHA
and production equivalence are in the delivery receipt.

The Windows proof's loaded Web, MAF, capability UI and Agent UI assemblies report the executable main SHA.
Loaded BaseLib reports `0.3.0+b495d4c4a28f0a6588ba10bfaa7be6e8409eae18`, with SHA-256
`f206c49273bb85d1c18416a43947dcff6a1ea4ec283eed6a27930b1708f65a37`; its evaluated static-assets
manifest resolves 40 BaseLib assets. The completed Tooltip repair remains in Components.
The three-instance Docker builds have Gitless version strings; their source identity is recorded
by the exact build-input fingerprint and matching DLL/static-asset hashes across all three instances.
Components remote development was verified at `4a858412d2c2a3f6123bf23d8c4584f05b47627d`.
The local fix is verified but has not been published by this task. No push, merge or release occurred.

| Signed checkpoint | Main revision | Responsibility |
|---|---|---|
| S0 | `b5e84364699b92f5700d26f4d8c4551d64b05f98` | Bounded PP3 documentation and delivery checks |
| C1 | `d52dbd22c31cf1f52152e8586116d786035ccd30` | Complete capability definition authoring |
| C2 | `3a9e591f4bdab9c7724e581b658330bd5c800567` | Team metadata, icons, members and native coordination |
| C3 | `73789c39598eb50eb06975ce6b6746e28015eb76` | Native journeys and bounded MCP checkpoint repair |
| C4 | `baaebd9ed312b1544c8c283b549dbf25117d32fc` | Native test readiness and complete CI component selection |

Every checkpoint and the final closure commit is verified against the configured
OpenPGP fingerprint `96E836FAA8854EE98ABC10903C206549E1D7EAD6`.

## Rendering and operation owners

| Surface | Rendering owner | Native host / operation owner |
|---|---|---|
| Wizard Identity, Configure, Review | CapabilityAuthoring.UI form and shared fields | Module CapabilitySetupWizardDialog → NativeCapabilityAuthoringHost |
| Details Identity, Configuration, Raw | Same form and fields | Module CapabilityDetailsDialog → same native host |
| MCP stdio/HTTP/SSE/logical | McpConfigurationFields and configuration codec | Native setup flow, compiler and registered MCP adapter |
| Skill file/inline/upload/registered, resources | SkillConfigurationFields, draft and bounded upload | Native catalog and skill compilation/trust policy |
| Tool process/HTTP, input/limits/side effects | ToolConfigurationFields and setup result panel | Native setup flow, tool compiler and process/HTTP implementations |
| Global capability creation/details | Shared authoring form | AgentCapabilitiesPanel and its original profile lifetime |
| Creation from existing/new agent | Shared authoring wizard | AgentDetailsDialog and A2 whole-agent assignment owner |
| Team metadata/icon/member selection | AgentFramework.UI Teams family | Thin module dialogs, AgentCatalogHost and coordinated catalog owner |

The capability rendering leaf and independent sandbox contain the actual controls, typed fields,
raw editor, setup-result presentation and CSS. The old duplicated dialog renderers/configuration
support are removed. Models, light capability/MCP abstractions and neutral Components remain
allowed dependencies; Core, Persistence, module implementations, process/HTTP/MCP effects,
credentials and trusted path policy stay outside this rendering graph. No extra contracts
project or general receipt/replay framework was introduced.

Each editor acquisition owns one draft/EditContext, raw invalid text and immutable submissions.
Supported extension data, case-distinct keys, argument order, authority paths, bindings, inline
resources, policy and output limits round-trip. Malformed stored JSON requires explicit repair;
forbidden plaintext secrets are masked/rejected. Skill upload is a real bounded 1 MiB InputFile
read, including Unicode/BOM and stale-completion fencing.

Setup remains an explicit effect. Typing, Save, tab switches and read-back do not invoke it.
Results belong to the submitted revision and acquisition; a previous result cannot certify a
new draft, including A→B→A. Unknown acknowledgement prohibits automatic replay. The additive
native capability save returns the accepted identity and fingerprint from the coordinated
write, preserving null-ID create, missing-ID update refusal and established callers.

A successful create followed by failed parent read retains that one definition ID for recovery.
Existing-agent assignment still saves the whole dirty agent draft; new-agent assignment stages
until Save. A2 Verify remains a separate proof publication with its own reconciliation.

Teams reuse the real AgentSelectionCard, private-provider badges and Material icon catalog in
AgentFramework.UI. The additive metadata operation captures name/description/icon before awaits
and preserves membership from the record at the coordinated write. Legacy full-team upsert
semantics remain unchanged. Metadata updates reject missing targets and blank/duplicate names;
membership freezes incoming IDs and retains missing-agent refusal. Parent results are bound to
the opening team/profile/acquisition. A confirmed mutation followed by failed refresh offers
read recovery without repeating the mutation. Stale icons, cancellation and old callbacks cannot
change a successor. Deleting a group preserves its agents and neighboring groups. Grouping
does not change runtime, provider, project or storage authority.

## PP3 carry-over and bounded native repair

The original Workflow OpenAPI coverage failure was reproduced as 22 passes and one failure for
`WorkflowProviderOption.modelCatalog` and `.isSourceManaged`. Canonical XML documentation now
describes model ID/display-name metadata and source ownership. The same family passes 23/23;
wire fields, route IDs, availability and override restrictions are unchanged. S0 also passed
Tooltip lifecycle 21/21, History UI 84/84, History host/provider selectors 37/37 and native
Workflow provider projection 1/1. Global and scoped History retain lazy Search, applied queries,
origin-bound dialogs, separate content authorization, cancellation and clearing.

The native approved MCP journey exposed a checkpoint gap after exactly one external effect:
the installed SDK returns AI content with native MCP raw representations, which the MAF codec
previously rejected. The bounded repair serializes only MCP ContentBlock/ResourceContents using
the installed SDK serializer and checks its package version. Unknown raw objects, foreign types
and incompatible versions still require explicit recovery; OpenAI/Ollama checkpoint fingerprints
remain compatible. Admission and approval policy are unchanged. The fixture uses the existing
`mcp_` classification; its earlier unclassified name was correctly denied before dispatch.
The original post-effect reconciliation failure is retained. Native codec follow-up passed
14/14, and actual journal/restart admission coverage passed 73/73 without repeating the effect.

## Final proof

All new visual checks use 1920×1080, scale 1. No smaller viewport tuning was performed.

| Final frozen campaign | Result | Actual proof |
|---|---|---|
| Independent source and published sandboxes plus native authoring | 8/8 | All 24 capability scenarios; wizard/detail modes, bounded upload, invalid raw input, two editors, held operations, icons and 90-card bounded scrolling; native setup, A2 parents and teams |
| Native shared consumers | 10/10 | Defaults 1, consumers 5, custom metadata 1, final UI/History 3 |
| Source plus two clients, deterministic upstreams | 19/19 | Synchronization, exact routes/names, streaming, unpublish/reappearance, identity mismatch, outage/recovery and no fallback |
| Final Stable on owned PostgreSQL 18.6 | 16,315 passed; 9 failed; 16,324 executed | Shared catalog/Core changes triggered this single final broad campaign; full affected families included |
| Portability static | PASS, 15,205 reviewed findings | Full 8,336-file scan; 11 MCP codec additions and one final README description reviewed; enforcement without baseline writes |
| CI policy and selection | 22/22 and three matching project lists | All 20 Components solution projects selected in Linux and both host scopes |
| Documentation and evidence tooling | 351 maintained files; 9/9 evidence and 22/22 package self-tests | Maintained links/docs, 22 package-validator self-tests, exact package integrity and completed evidence structure |

Native definition proof records one HTTP setup call, five bounded MCP start/list operations,
one approved MCP invocation and four deterministic model requests across allow/deny. Invalid
input dispatches nothing; Save does not repeat setup. Process and stdio helpers exit under
owned lifecycle checks. Logical MCP retains its supported refusal. The actual A2 existing/new
parents preserve unrelated dirty instructions and Memory settings and create one definition each.

Project Structure exercises the actual contextual chat, a prompt-hidden content canary,
non-overwriting write/attachment, native metadata/content and downloaded byte/hash equality,
with the unrelated project unchanged. Scoped/global History use explicit Search and separately
authorized content; metadata-only credentials cannot read operator-owned content. Existing
Workflow/TestLab consumers verify immutable input/version, stored output and incomplete-output
refusal. Both clients retain native model labels and opaque routes; saved default/nondefault
choices, cross-circuit synchronization and denied/no-fallback invocation are exercised.

The exact final application/upstream images, source fingerprint, timings, terminal markers,
fixture IDs and request counts are in `final-protocol-proof.json` and native consumer receipts.
The first complete CA1 topology passed 19/19, but the subsequent native UI guard required the
existing `shared-providers-e2e-pp2-` ownership prefix. Its pre-effect refusal is retained. The
first topology was stopped; a fresh correctly named successor ran the final campaign without
changing source or resetting any historical fixture. The original missing-Reset guard refusal
also remains recorded. Neither issue was hidden by weakening ownership checks.

The old PP3 Responses timing failure remains unexplained despite its same-image successful
resume. The CA1 timing evidence keeps the original 50 ms assertion and numeric first-data,
terminal and completion observations. The old PP3 Stable result remains 16,197 passed and 14
failed out of 16,211; CA1 results do not retroactively turn that attempt green. Implementation
stage failing-first and harness correction attempts remain separate from final frozen proof.

The final Stable campaign freshly built all 26 assemblies at C4 and ran for 2 h 55 min 24 s,
including build and discovery. Discovery selected 16,269 cases; 55 additional runtime cases
across seven nonserializable theories account for all 16,324 executions. Every discovered method
ran, no method contracted its case count, and no selected case was skipped.

Eight failures were the existing `AgentCatalogPanelTests` fixture missing the native catalog
host's `IDatabaseSwitchNotificationService` registration. The bounded correction adds the real
service registration and namespace import, without changing assertions or runtime behavior.
A fresh isolated build, discovery and follow-up pass all ten catalog cases.

The ninth failure is the working-tree secret-pattern scan reading generated negative controls
in two retained historical A2 reports and this campaign's discovery JSON. All three reported
paths and nine pattern matches were classified against the scanner's constructed controls,
including xUnit's explicitly truncated 50-character display. The historical files are unchanged.
The separate source-only follow-up uses the identical frozen scanner on a hash-verified copy
of all tracked/nonignored inputs; its receipt records actual discovery, result and scope.
That qualification does not replace the original failed working-tree attempt or claim coverage
of ignored evidence. The bounded safe-export scan covers its own explicit manifest.

`stable-summary.json` and `stable-disposition.json` retain the original counters, each affected
family, every original failure and the exact focused follow-ups. The broad campaign is mixed;
it is not reported as all green.

The first custom-metadata browser attempt timed out on the Thinking dialog after source edits;
its original timing cause was not proved. The helper now reacquires the source profile and waits
for native startup and saved-value readiness, with source-side failure capture. Its follow-up
passed through actual restart. The first final UI run passed two cases but received 200 instead
of 401 after attempted credential deletion. Its helper could read an unaccepted token search
and return without deleting. The helper now waits for the accepted query and committed deletion,
and requires exactly one deletion before the unchanged 401 assertion. The three-case follow-up
passed, including the replacement credential's native turn. Neither correction changes production
authority, application images or test thresholds.

Server logs are retained and reviewed, rather than labeled clean: zero unhandled circuit/render
signals, six error headers and 43 warning headers across the three instances. They include
explicit denied/canceled token checks, one unavailable-provider refusal, one undecryptable
antiforgery cookie, existing EF/development-vault warnings and retryable History maintenance
cancellations. Final canonical History/content assertions passed; the warning observations
are not evidence that every background maintenance pass completed.

## Dependency and development loop

The evaluated production Web closure contains 169 projects; the capability leaf has 10 and
its standalone sandbox 11. The existing team/History sandbox remains at 16. Protected
PP1/PP2/A2/Workspace/Projects closures are 10/5/18/8/6. All 176 distinct evaluated project
files are unchanged at the final checkpoint, with no unresolved references or cycles.
Components, CodeAnalytics and dotnetwatch MCPs were unavailable; explicit source/caller census,
evaluated MSBuild graphs and native CLI/browser proof supplied the recorded fallback.

Nine visible watch probes used PID 73488 throughout: Razor 1841/422/384 ms, C# 144/30/21 ms,
CSS 600/413/299 ms. C# probes reacquired the scenario; two browser observation upper bounds
include orchestration delay. All three sources were restored byte-for-byte and the owned watch
process stopped. These are local small-host observations, not a universal Web speed claim.

## Evidence and remaining scope

Local receipts are under `artifacts/agent-authoring-ca1/20261003`: `evidence.json`,
`test-attempts.json`, `stable-summary.json`, `frozen-source-pair.json`, `source-pair-final.json`,
graphs, watch restoration, signature checks and exact cleanup. A bounded safe export is scanned
with private credential sentinels; its coverage/review is separate from retained private roots,
raw native inputs and original failed attempts. Only exact task-owned processes/containers are
stopped; retained volumes, historical PP3 fixtures and ordinary port 5032 are preserved.
No paid inference was used.

Capability wizard/details and technical-team metadata/icons/members are complete. Native effect,
storage, runtime admission and owner authority remain with their existing services. Residual
chat/usage and provider/runtime surfaces need their own caller census. Workflow canvas,
Workbench and Processes feature work was not started; their existing consumer journeys were
used only for regression proof. Remote dependency publication remains separately authorized work.
