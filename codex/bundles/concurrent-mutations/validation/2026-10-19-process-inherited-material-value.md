# Original inherited material value validation

The first pure contract from the [inherited-material design](../plan/P07-inherited-material-and-child-runtime.md)
passes all 44 selected cases. Canonical storage, shared parent budgets, child journals
and actual governed execution remain open; this is not R5 or E2E closure.

`ProcessDecisionMaterial` binds profile, namespace, root/owning run, optional logical
step, material generation, execution contract, original UTC capture and bounded UTF-8
JSON. Its versioned SHA256 frame covers every field and exact content. Child startup
cannot renew capture time. Original comparison rejects substitution; freshness rejects
expiry, clock reversal, non-UTC observations and invalid limits. Business schema,
canonical scope membership and current authority remain the owning adapter's duties.

Direct standard Release builds passed for Runtime and Unit. The exact filter was
`FullyQualifiedName~ProcessDecisionMaterialTests|FullyQualifiedName~ProcessDecisionExecutionContractTests`:
44 expected, 44 discovered and 44 passed without skips. This comprises 22 new material
cases and 22 protocol regressions. Independent Python fingerprints cover run/step scopes;
other cases cover delayed children, exact expiry, identity substitution, Unicode/byte
bounds and invalid data. No database/provider/browser behaviour is claimed.

The first candidate executed 44 cases: 41 passed, three new tests failed. One constructed
an empty typed ID before its assertion; two expected the exact JSON exception instead of
the parser subtype. After verification/release, only those fixtures/assertions changed.
Production was unchanged and the complete 44-case set was rebuilt and rediscovered.

The first repaired run passed but was unobserved: shared TEMP contained 16,387 directories
and 7,231 files, exceeding the observer's 16,384-entry baseline bound. Its reviewed local
helper now permits a finite 32,768-entry baseline per family; the separate 16,384 new-delta
limit remains unchanged. No TEMP entries were deleted or omitted. After confirmed
observer startup the unchanged tests passed again: 44/44 in 3.07081s, fully covered by
20.7597302s of observation. The unobserved passing attempt is explicitly excluded from
accepted observed proof.

Full portability was regenerated after test edits and passed at 15,623 unchanged findings,
without baseline rewriting. Baseline/secret-scanner/rendering tool tests, rendering
register, documentation and diff checks passed. No broad Stable or capacity result is claimed.

CodeAnalytics `snap-20261019201441-d1ce2c09` covers four Process projects: 1,014 types,
9,076 members, 27 entities, 330 findings and no detected cycles. Two added informational
member-count findings were reviewed: the immutable material/scope properties and checks
have one responsibility, without service behaviour or dependency growth. Existing 190
load diagnostics and the AddSingleton ambiguity remain. Gate: **Pass with follow-up**;
no new interface, project reference or runtime partial. Runtime activation remains gated.

| Candidate | Source SHA256 | Runtime SHA256 |
| --- | --- | --- |
| `c4-process-inherited-material-value` | `9AA48020622F03321A57B580D1338A44F0764421EDE395DCD81186F6B7994E4D` | `19289107F2CC28368D4348304445E9A612F3D3654D6A4EB54A78227914CAB681` |
| `c4-process-inherited-material-test-repair` | `E152FCBB6A10E80F7F2831B474CCAAE0C2765CFFDFBFA1B5A33C6342A1ACC00D` | `32BA7BCEC3796B64287766D260468F7DFA9ACE99BCFF659CF2F89177EE8EBE56` |

Both complete 9,467-source/1,472-runtime identities were verified before release. Final
reconciliation found no remaining observed process, database-name delta or new TEMP entry.
Three unattributed TEMP entries from the first candidate were preserved. Earlier denied
cleanup remains unchanged; the fixture remains needed for continuing work.

Retained proofs under `proof/2026-10-19-planning-authority`:

- `c4-process-inherited-material-value.json`: SHA256 `147584030A78A20F9FF4C55F6AD1CD7D07E359F367A60DC478F06641764B70DE`.
- `c4-process-inherited-material-test-repair.json`: SHA256 `DD02B534143D8AD9CF3DD4E545BFE991A645E7A31914A4BBC11E6DE5C027C318`.

The latter retains two passing attempts but marks only
`process-inherited-material-observed.trx` as accepted observed proof: 44 distinct cases,
not 88. Exact checkpoint blobs follow the execution-contract checkpoint while signing
of the earlier Agent checkpoint still waits on GPG.
