# SB01 governed proof manifest

State: `PASS`

## Baseline and implementation

The implementation started from branch `providers-shared` at commit
`e46f81d5ee33627dccb548732725e1c37e980ab5`, preserving the readiness-repair and completed SB00
working-tree changes. No commit, staging operation, discard, or unrelated-file rewrite occurred.

SB01 added one zero-dependency contract project, the Web-owned access-context binding, exact
12/10/10 focused tests, and governed evidence. It added no EF entity, endpoint, outbound HTTP
client, provider SDK integration, persistence behavior, or UI.

## Honest failing-first replay

The parallel implementation run did not retain its initial red transcript. To satisfy the
Governed proof contract without manufacturing a failure, the exact final adversarial test source
files were replayed in a disposable detached worktree at the unchanged baseline commit. Both
commands exited 1 because `CanDoItAll.SharedProviders` production contracts did not exist:

- Unit protocol/routing control: `transcripts/sb01-failing-first-unit-baseline.txt`.
- Integration access-context control: `transcripts/sb01-failing-first-access-baseline.txt`.

The same test sources then discovered and passed exactly 12, 10, and 10 tests against the SB01
implementation. The disposable baseline worktree was verified, unregistered, and removed after
capture.

## Production behavior artifact matrix

| Artifact/behavior | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Canonical catalog document/revision | `SharedProviderProtocolJson` and `SharedProviderCanonicalRevision` in `production-source-assertions.md` | Catalog fetch success validates/copies the document and its ETag; 12-test protocol lane | Pure deterministic construction/validation; persistence is intentionally SB02/SB03 | unknown/duplicate/version/capability/cross-publication/default/secret cases in protocol lane |
| Routing model ID | `SharedProviderRoutingModelIdCodec.Create` | `TryParse`/`Matches` plus catalog validation; 10-test routing lane | stable deterministic value; persistent lookup/index is downstream | malformed/version/case/truncation/wrong-publication/private-data vectors |
| Request-scoped access context | Web middleware calls internal state `Set` | registered `IAccessContextReferenceAccessor`; real-host endpoint probe in the 10-test access lane; relay/audit consumers remain SB04/SB07 gates | scoped DI, single assignment, concurrent isolation, status re-execution safety | malformed/repeated/comma/oversized/default/forged-auth cases |

The access accessor is deliberately a boundary seam in SB01, not an invented production business
consumer. Central relay/audit forwarding and proof that the value is absent at the external
upstream remain named downstream requirements rather than false SB01 claims.

## Commands and durable evidence

| Gate | Result | Artifact |
| --- | --- | --- |
| Entry validator | Pass | `transcripts/sb01-entry-validator.txt` |
| Unit Release build | 0 warnings/errors | `transcripts/sb01-build-unit-release.txt` |
| Integration Release build | 0 warnings/errors | `transcripts/sb01-build-integration-release.txt` |
| Protocol list/run | 12 discovered, 12 passed | `transcripts/sb01-list-protocol-release.txt`; `sb01-run-protocol-release.txt` |
| Routing list/run | 10 discovered, 10 passed | `transcripts/sb01-list-routing-release.txt`; `sb01-run-routing-release.txt` |
| Access list/run | 10 discovered, 10 passed | `transcripts/sb01-list-access-release.txt`; `sb01-run-access-release.txt` |
| Anti-stub | Pass, 13 files | `transcripts/sb01-anti-stub-audit.txt` |
| Dependency boundary | Pass | `transcripts/sb01-forbidden-dependency-scan.txt` |
| Access trust boundary | Pass | `transcripts/sb01-access-boundary-scan.txt` |
| Credential/private-key scan | Pass | `transcripts/sb01-secret-scan.txt` |
| Diff whitespace | Pass | `transcripts/sb01-diff-check.txt` |

## Architecture evidence

The force-refreshed comparison is
`snap-20260824204913-6a7763ae -> snap-20260824213007-c65710b4`: 11 to 12 projects,
23 to 24 direct production references, and zero project-level cycles before and after. The sole
new production edge is `Web -> SharedProviders.Abstractions`; Abstractions has zero outgoing
package/project edges. The two known module cycles and one nested-type cycle are unchanged.

The public inventory, source assertions, checked architecture review, behavior proof, security
proof, and independent frozen-code PASS are under `proof/architecture`, `proof/behavior`, and
`proof/security`.

## Progression

SB01 passes. SB02 may add only the authorized Workspace-to-Abstractions edge and persistence/
reconciliation behavior. The single broad test gate remains reserved for SB12.
