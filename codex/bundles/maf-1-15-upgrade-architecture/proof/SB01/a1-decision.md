# A1 Baseline and Compatibility-Fixture Decision

## Decision

`GO` to SB02 package alignment.

Recorded at `2026-07-28T05:17:27.4884718+00:00` against immutable baseline commit
`797d7ce11205d630756ec9335b1b84295257a315`.

This is a baseline-readiness decision, not an approval to release, enable optional 1.15
behavior, migrate a live approval, or declare target compatibility complete. Every target
fixture and security assertion still has to pass after restore.

## Baseline Identity

- Branch: `agents-loading-refactor`.
- Bundle pin: `59f558bc866d39d438b53f5f743dd5e87c2a6253`.
- Baseline HEAD: `797d7ce11205d630756ec9335b1b84295257a315`.
- Pin is an ancestor of baseline HEAD.
- The 3,083 changed paths between pin and baseline are all below `.codex/bundles` or
  `codex/bundles`; there is no product/package-source drift in that range.
- SDK: `10.0.204`.
- Stable direct MAF references: `1.13.0`.
- Direct A2A preview references: `1.13.0-preview.260703.1`.

See `repository-head.txt`, `discovery/direct-maf-package-references.txt`, and
`package-graph/*.json`.

## Discovery and Package Graph

- Discovery completed and classified 13,899 matches:
  - production: 11,084;
  - test: 2,054;
  - repository configuration: 405;
  - documentation: 328;
  - tooling: 28.
- Exactly three direct package-owner projects were found:
  - `CanDoItAll.AgentFramework.Maf`;
  - `CanDoItAll.AgentFramework.Hosting`;
  - `CanDoItAll.AgentFramework.Workflows.MafAdapter`.
- Explicit restore succeeded once the process could read the user NuGet configuration.
- Direct and transitive `net10.0` package graphs are retained for all three owners.
- MAF SDK types remain contained by MAF adapters/hosting/tests; canonical application and
  workflow contracts do not expose them.

## Build and Test Baseline

| Evidence | Result | Classification |
|---|---:|---|
| Three direct-owner builds with command-line MAF suppression override | 3 succeeded, 0 errors | Pass |
| `maf-session-approval-1.13.trx` | 106/106 passed | Pass |
| `maf-a2a-package-1.13.trx` | 10/10 passed | Pass |
| `maf-handoff-1.13.trx` | 3/3 passed | Pass |
| `maf-a2a-filetools-1.13.trx` | 282/282 tests passed; runner did not exit and was stopped | Inherited runner/resource-cleanup hang |
| `maf-workflow-a2a-filetools-1.13.trx` | 302 passed, 6 failed; runner did not exit and was stopped | Inherited test-data drift plus runner hang |

The six workflow failures all belong to `WorkflowFoundationTests` and reflect the current
branch's immutable-instruction-snapshot validation: fixtures expecting a basic LLM
workflow omit the now-required immutable snapshot. They are not hidden and are not caused
by a MAF package edit.

The 282-test slice finished all test bodies successfully but the test host retained two
active file-service tests:

- `WorkspaceExternalTargetAliasTests.WriteTextFile_writes_to_real_external_target_for_alias_path`;
- `WorkspaceFileServiceTests.WriteTextFile_registers_showcase_deliverable_as_execution_artifact`.

The primary agent stopped only the owned hung test-host processes. Target-version
validation must compare against these exact inherited conditions and must not classify a
new failure as baseline noise without evidence.

## Warning Decision

The unsuppressed targeted builds surface only inherited `NU1903` warnings for
`System.Security.Cryptography.Xml` `10.0.7` across five high-severity advisories. No MAF
experimental, compiler, downgrade, or release-train warning appears after overriding
project suppressions.

Decision: A1 may proceed because the direct package owners compile, but the target package
graph must not worsen the advisory set. No blanket suppression is authorized. See
`warning-baseline.txt`.

## Fixture Decision

Ten deterministic, sanitized fixture payloads and ten schema-shaped manifests are stored
under `fixtures/maf-1.13`. `fixture-hashes.sha256` covers every payload:

- empty local session;
- framework-managed text history;
- provider-managed conversation;
- app-owned legacy function approval;
- app-owned legacy MCP approval;
- mixed ordinary call plus multiple app-owned approvals;
- request-scoped attachment scrub;
- governed-step isolation;
- deterministic handoff;
- app-owned approval checkpoint shadow.

### Native 1.13 approval binding

`N/A`. The 1.15 approval-response binding StateBag state does not exist in the 1.13
baseline. The captured approval fixtures are app-owned compatibility records. They are
display/audit evidence only after upgrade and have expected outcome `approval-reissue`.
They must not be translated into private 1.15 framework JSON or execute directly.

### Hosted A2A message/session

`Inactive`. The product registers the card factory but never invokes
`AddAgentFrameworkA2AServer`/`AddA2AServer`; no hosted message route or session authority
exists to capture. Card mapping has a 10/10 package/host unit baseline. No A2A message
fixture is fabricated.

### Native workflow checkpoint

`Inactive`. Current ordinary workflow checkpoints are metadata-only with resume marked
unsupported. The captured workflow fixture is the app-owned approval checkpoint shadow
used for consistency validation. It is not native MAF workflow state and does not assert
native resume.

### Provider and non-durable behavior

The environment reports `OPENAI_API_KEY` present without exposing its value. SB01 does not
commit a real provider payload or remote identifier. Background-response and reasoning
ordering have no deterministic durable 1.13 payload in the current suite; they remain
target behavior tests rather than invented JSON.

See `fixtures/maf-1.13/README.md`.

## Tool and Runtime Baseline

- `file-tool-inventory.json` records all 45 catalog workspace tool names, the 18
  composition-time approval wrappers, and representative context filters.
- The canonical managed host is `CanDoItAll.Web` on port `5032`.
- At the baseline observation, PID `52052` listened on IPv4/IPv6 loopback and the root
  returned HTTP `200`.
- The process-local approval cache is explicitly non-authoritative across restart.
- PostgreSQL, workspace files, and control-plane/Data Protection state form one rollback
  boundary.

See `runtime-lifecycle.md` and `rollback-consistency-boundary.md`.

## Sequencing Caveat

While SB01 evidence was being materialized, the primary implementation agent began target
package edits in the shared working tree. Those concurrent changes are not part of this
subtask and are excluded from baseline evidence. The package graph, direct-reference
inventory, builds, tests, and fixture source commit all remain pinned to 1.13.

This is a process-order exception to the bundle's preferred “write A1 before edit” rule,
not a claim that the target edits supplied missing baseline proof. Re-running SB01 must use
the immutable baseline commit or an equivalent isolated checkout.

## A1 Checklist

- [x] Branch/head and pin drift recorded.
- [x] Discovery completed and every result classified.
- [x] Direct/transitive package graphs captured.
- [x] Targeted builds and inherited test conditions recorded.
- [x] Warning and experimental-suppression inventory captured.
- [x] Applicable 1.13 compatibility fixtures sanitized, hashed, and manifested.
- [x] Native approval binding, hosted A2A session, and native workflow checkpoint
  non-applicability/inactivity recorded explicitly.
- [x] Deterministic handoff baseline captured.
- [x] File-tool inventory captured.
- [x] Managed runtime lifecycle captured.
- [x] Three-component rollback snapshot/rehearsal procedure documented.

## Conditions Carried Into SB02 and Later Gates

Stop or reopen A1 if:

- a new direct MAF owner or provider factory is found;
- target restore resolves a mixed 1.13/1.15 train or an unexpected package;
- target warnings add a downgrade, compiler error, or new advisory;
- any fixture hash changes without an intentional regenerated source record;
- a target implementation proposes direct execution of a legacy app-owned approval;
- hosted A2A or native workflow resume is claimed without an actual registered/runtime
  path;
- package behavior cannot be compared to the recorded inherited failures/hangs;
- the rollback boundary cannot be rehearsed coherently.

Subject to those conditions, A1 is `GO`.
