# Process policy authority boundary

The unsupported-policy boundary now covers persistence authority as well as executor
entry. **58 distinct selected behaviors pass** through 31 unchanged earlier cases and
27 repaired/related cases. This is not a single 58-case run or policy activation.
The [decision runtime design](../plan/P07-process-decision-runtime.md) remains prospective.

## Behavior and architecture

Agent and mapped Workflow authority readers retain original historical source evidence,
but report policy-bearing assignments as unavailable for current dispatch. Agent
reservation and enlisted mutation guards consequently refuse them under the existing
root transaction fence. Workflow continuation checks both its observed assignment and
the locked current assignment explicitly; legitimate released legacy claims still work.
The real HTTP Workflow executor rejects an unsupported policy before vault access or
external request, including substitution between observation and its transaction.

No new project reference, interface, schema, fingerprint, SQL round trip or production
partial was introduced. Existing history remains readable. The ordinary Agent source
read still passes its two-query/no-write assertion. This closes an alternate owner-call
gap while keeping Process/Workflow settings inactive pending their governed runtime.

CodeAnalytics before `snap-20261019161427-d1ce2c09`, after
`snap-20261019163221-d1ce2c09`: four projects, 946 types, 8,689 members, one recognized
service registration, 22 entities, 319 findings, 190 diagnostics and zero cycles,
unchanged. Module references Application/Persistence/Runtime; Persistence references
Application/Runtime; Application references Runtime. Existing workspace-load blocking
diagnostics remain; actual affected Release builds pass. Architecture gate: pass for
this local correction only, with no claim that the prospective decision runtime exists.

## Validation

Standard Release builds passed for Process Persistence and Integration using
`UseLocalCanDoItAllLibraries=true`, serialized `/m:1`. Existing compiler/NuGet warnings
remain. Every filter was discovered before execution. No cases were skipped.

| Selected filter in Integration | First run | Final repair / bridge |
| --- | --- | --- |
| `FullyQualifiedName~ProcessExecutionAuthorityPersistenceTests` | 18/18 | Unchanged source and production |
| `FullyQualifiedName~ProcessCatalogAuthorityPersistenceTests.Mapped_workflow` | 13/15 | Exact new two-case method passes 2/2; other 13 unchanged |
| Seven exact methods in `WorkflowHttpSecretAdmissionIntegrationTests.MappedContinuation.cs` | 16/19 | All 19 rerun and pass |
| Exact `Deferred_mapped_read_rechecks_current_parent_and_exact_child_receipt` | Not selected | 6/6 affected fixture callers pass |

Exact method names, full namespace filters, discovery commands, transcripts and results
are in each candidate's resource manifest and retained proof. The first candidate's
five failures are preserved: two compared separately deserialized collection references,
two expected the inner authority exception instead of the executor's sanitized wrapper,
and one used the retired Guid-only Project deletion fixture. Repairs compare exact
serialized authority, assert the real wrapper and sanitized reason, and use the existing
original-intent lifecycle test driver. Production guards were unchanged and not weakened.

The successor changed exactly two test files. The source bridge verifies the same HEAD
and every production/other source input. It repeats all 19 continuation cases and adds
the six related read cases because they share the repaired project-replacement fixture.

Both full portability scans enforce **15,615 unchanged reviewed findings**, without
baseline writes. Rendering register, documentation and `git diff --check` pass.

## Retained proof and resources

| Candidate | Source inventory SHA-256 | Runtime inventory SHA-256 | Compact proof SHA-256 |
| --- | --- | --- | --- |
| `c4-process-policy-authority` | `2A011027FFD61FE7D704A8EEB28390ED5A73A3A471E22CA11B9C969724026743` | `20B67CFF679CB1E3B4E66D5DC15AD2EC89E85F1E4F7E9066002BC5E23CDA08B2` | `B58AC5CA33833A6E59E4A1FE4FE537BFC2F7DF674AB9405AFED2F53F0C104626` |
| `c4-process-policy-authority-ready` | `203FCE13CFB55F5C0560AF0BA1EF057C05081FCD5F8A8973B2056D99A40307AC` | `B403912B737BB8C68DCB18AE3BD48425E1EDE4D80395BF4784537200ECA19B66` | `CF0D15C7FC0819C07CE01776B3672486A3E5469967611E794FCECD2C5367C67D` |

Both inventories contain 9,412 source and 1,443 runtime files and were verified unchanged
before freeze release. Proof is retained under ignored
`proof/2026-10-19-planning-authority/<candidate>.json`; command/resource inventories are
under `.artifacts/concurrent-mutations/validation/<candidate>`.

Ownership was recorded before creating either directory. Both reused the existing
isolated PostgreSQL 18 fixture. Observers covered all six entire execution commands;
reconciliation found zero remaining observed processes and zero new database delta.
Twelve and ten unattributed TEMP entries were preserved, respectively. Earlier cleanup
blockers were not retried or bypassed; the shared test fixture remains for continuing work.

Generation/child publication fencing, shared nested allocations, actual policy producers,
governed execution, manager pause/resume and Workflow occurrence integration remain open.
SPA remains isolated on `codex/concurrent-mutations`; no merge or push occurred.
