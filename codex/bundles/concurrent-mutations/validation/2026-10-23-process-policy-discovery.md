# Process policy discovery qualification

Status: qualified, 2026-10-23. Documentation and artifact review pass. SPA checkpoint
`707bcc57bf99e4fce6e58e2fe69eb9261729b0dc` is signed (`G`); the backend checkpoint identity
is recorded beside the proof after committing this report. This closes [P25](../plan/P25-process-policy-discovery.md)
only; Process launch UI, definition defaults, broader policies/lifecycle, capacity,
remaining owners and final engine E2E remain open. SPA stays on
**codex/concurrent-mutations** for colleague review.

## Contract and boundary

`GET /api/processes/launch/decision-contracts` exposes exact registered execution
identities, operator guidance and complete supported policy combinations with owner
ceilings. Workbench advertises its two qualified snapshot modes, current publication,
pause for review, zero automatic replans and ten-minute maximum work/basis age.
Preparation still intersects actual Workspace/request bounds and checks the selected
definition, Project, Agent and original authority. Catalog availability is not eligibility.

The existing Application admission-provider boundary owns descriptive metadata. The
coordinator validates and copies that metadata into a deterministic read-only snapshot;
discovery never invokes preparation. Workbench uses the same descriptor to validate its
resolved policy. Empty/invalid/duplicate registrations fail explicitly. No project
reference, migration, runtime service lifetime, business partial or service locator was
introduced. Existing persisted intent and preparation shapes/fingerprints are unchanged.

The endpoint resolves the existing scoped providers and reads their static descriptors.
Constructor inspection confirms bounded in-memory/path setup; the catalog performs no
settings, Agent, Project or history query, lease acquisition or provider invocation.
Discovery retains normal Process read permission and the existing broad legacy API grant;
a restricted read-only caller cannot use launch/check.

## Failures retained and repaired

The first HTTP run had two failures. Its permission fixture incorrectly treated the broad
legacy `api` grant as a restricted caller. The corrected test proves denial for an
unrelated scoped grant, acceptance for both legacy and Process read grants, and refusal
of execution under Process read alone. Production authorization was not weakened.

The actual OpenAPI document also emitted `supportedPolicies` as an array without item
type information. A small Web schema transformer now requests the existing complete
`DecisionDataPolicy` item schema. It does not reproduce the serializer or policy rules.
The second run proved that schema and authorization, then found a test typo: the route
contract property is `endpoints`, not `routes`. The original test source and failed
results remain preserved. Only that assertion and the reviewed portability baseline
changed before the final integration build.

## Executed proof

There are **56 distinct passing backend cases across an explicit test/baseline repair**:

| Selection | Cases | Proof |
| --- | ---: | --- |
| ProcessLaunchDecisionDiscoveryTests | 13 | Snapshot isolation, ordering, empty/invalid/duplicate registration, no preparation calls, advertised modes and ceilings. |
| ProcessLaunchDecisionTests | 38 | Legacy fingerprints, exact admission bindings, supported/refused policies and original intent. |
| ProcessLaunchDecisionDiscoveryApiTests | 3 | Real host catalog, scoped/legacy authorization and exact OpenAPI shapes. |
| P23 public producer positive method | 2 | RunSnapshot and StepSnapshot through HTTP preparation, changed settings, original acceptance, queued MAF proposal, review/publication and terminal accounting. |

Application, Workbench, Web, Unit and Integration Release builds passed serially using
existing standard outputs. Integration was rebuilt after the assertion correction.
Build-backed discovery matched every selected count. No broad Stable or live combined
SPA/backend deployment is claimed. Existing package advisories remain in build logs.

`Transfer.py` verifies the retained 51 Unit cases against exact source and output
identities. Only the one route-property assertion and the reviewed baseline differ;
only Integration's DLL/PDB changed. The final five integration cases and isolated schema
export passed under a resource observer. **1,235 source pins and 20 outputs** remained
unchanged during that run. Failed/unfinished cases are excluded from the retained total.

The new export contains **318 routes**, adding only discovery. It adds one descriptor
schema and removes or changes no existing schema. Its SHA-256 is
`d41fd2a7fb5d04e183f5a6f5ec2edeeeec2267cae547aba27488b043b830540b`
(4,532,724 bytes). The installed SPA generator consumed those reviewed bytes. Generated
model movement is ordering only; existing execution serializers retain their fields.

SPA passes **103 tests**: 25 generator, 15 actual SDK/transport and 63 app Node cases.
The three new SDK cases preserve the registered identities, both complete policy
ceilings and exact duration strings, empty discovery and permission refusal. The app
typecheck/build and owned formatting/lint pass. Six changed source files and 124 compiled
assets are recorded after the checks. The existing chunk-size warning remains. No UI
markup changed, so no new browser scenario is claimed. Generated default endpoints
reflect the isolated export; the app supplies its explicit same-origin endpoint.

## Architecture, static gate and resources

Scoped CodeAnalytics snapshot `snap-20261023120826-c3ed56ba` covers Application, Workbench
and Web before the schema-only correction. Its three internal project edges preserve
direction and its six cycles are the previously recorded Workbench cycles. Existing
ProcessesApi size/member findings remain. Direct final-source review includes the new
schema helper and confirms unchanged project references. This is not a global health
pass: the collector reports 189 WS0002 errors, two DEP0002 warnings, 14 DI0001 messages
and six DI0002 warnings. The actual affected builds pass.

The final full portability scan covers **10,083 files / 47,399 findings**, is nontruncated
and includes new files. Exactly one allowance was reviewed: the literal HTTP route
matches the scanner's `/proc` pattern; it performs no OS process discovery. Fingerprint
`7fc3b4989546106a1009076ce8160244265e2cc4fd60ea876723f954a5811e3c`
is the sole added allowance. Final enforcement passes with **15,690 allowances** without
the write flag. Tool tests (6/4/7) and the rendering register pass. The maintained-doc
gate passes for 438 maintained Markdown files; its normal bundle exclusions still apply.

All three observed candidate runs reconcile to zero new live processes, database-name
deltas and temporary-family residue. The diagnostic export separately verifies its own
process and temporary directory were removed; it was outside continuous observation.
The final observer covers both integration commands and the export (133.6 seconds).
The isolated PostgreSQL fixture is reused and retained for subsequent bundle work.
Ordinary 5032 and installed tooling are preserved. Six new bulk scan files were replaced
by reviewed summaries, source excerpts and content hashes, then verified removed.
Earlier automatic-review cleanup denials remain exactly as recorded in prior reports;
this is not a whole-bundle cleanup claim.

Artifact scans cover 95, 88 and 105 text files respectively, with no oversized or
unreadable text skipped and no exact secret-sentinel match. Each has the same three
reviewed lexical matches on literal PowerShell file-read/variable expressions. The
excluded C#/Python helper sources were inspected separately; no credential values were
introduced. No new cleanup action was rejected in this slice.

Proof roots under `.artifacts/concurrent-mutations/validation/`:
`c4-process-policy-discovery`, `c4-process-policy-discovery-final` and
`c4-process-policy-discovery-verified`. The last contains discovery/results, unchanged
candidate identities, retained-Unit proof, architecture review, OpenAPI/SDK records,
static enforcement and resource reconciliation. Native UI still requires the currently
unavailable Components MCP; no native renderer work was attempted in this slice.
