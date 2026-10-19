# Planning saved actions and live publication capability

The repaired candidate passed **146/146**: 70 Unit and 76 Integration cases. Its 15 new
planning-action cases use the real file owner, dispatch leases, atomic publications and
selected Agent/run/chat snapshot. They cover approval wait exclusion, accumulated work,
claim and publication expiry, disposed/missing authority, competing independent owners,
stale or forged expectations, denied current reads, lost acknowledgement, Refresh,
bounded automatic replan and cancellation after basis expiry. Existing provider, live
owner, file persistence and schema-five saved-effect lanes also pass.

Approval opens its work interval in the same file publication as the original decisions.
The acknowledged saved lease alone receives the live capability. Historical provider
origins remain unchanged. The local publication guard requires the original bound claim
and selected snapshot and performs no IO. Known outcomes remain retainable after budget
expiry. Refresh retires old approvals, retains policy and spent work, and requires a new
generation/basis/approval. Committed or uncertain effects cannot become a basis-change
checkpoint. Cancellation reuses the existing saved-effect cancellation transformation.

The first candidate passed 145/146. It exposed an actual Refresh bug: declined prepared
proposals retained the default Unknown effect status, which correctly prevented retiring
that generation. The repair records NotCommitted only while declining an undispatched
schema-six prepared proposal with no claim or result. It leaves schema-five semantics
and entered/uncertain effects unchanged. The regression now also checks that evidence.

The first observer rejected a stale companion checksum after the test filter changed.
Tests had started, so its later observation has an explicit startup gap and is not
complete resource proof. That failure and the failed test remain in retained evidence.
The successor checks companion inputs before execution; its baseline precedes the
observer and the observer temporally covers both complete test commands.

Both candidates have passing final standard Release Core/Unit/Integration builds,
confirmed 70+76 discovery, complete portability enforcement (15,610 unchanged reviewed
findings), rendering, documentation and diff checks. No skipped cases are counted as
passing. Final source/runtime comparisons are unchanged.

| Candidate | Source SHA-256 | Runtime SHA-256 |
| --- | --- | --- |
| Initial | `228D4997E1C6F511AEB1916DC1B217A7FC9EA749892FE88ADDF145BBFFCFE4DF` | `CE650AE57867DC93A8722913BFE900F679FF7D2B41CDE9B58FE8448378B06292` |
| Repair | `D89C0CD369732F3A01812478D72994E4B6AF0A5C0DAE023B8C6596E71E226DB9` | `99857C7934DC75A64C6C508FF2710E5AA1B1BE128F294EE4DDFD4F10893EE0C6` |

Both comparisons cover 9,203 source inputs and 6,436 runtime files. The repaired
observer exited with zero remaining observed processes and no database-name delta.
Nine unowned TEMP deltas were preserved (three during the initial partial observation).
The isolated PostgreSQL fixture remains available for module integration. The previously
reported profiler cleanup blocker is unchanged.

CodeAnalytics snapshots `snap-20261019060527-54d1f7d3` and
`snap-20261019061335-54d1f7d3` cover five projects, 530 documents and 1,953 types. Their
195 diagnostics are 189 existing package advisories and six truncated-diagram notices;
the analyzer reports blocking errors for those advisories. No project/reference changed.
The live owner/work type pair joins the existing journal/lease/claim cycle because the
actual saved lease carries its issued capability. This is an explicit Core lifetime
relationship; no module dependency, store, registry, partial or parallel pipeline was
introduced. The other three local cycles are unchanged. This scoped review does not
claim repository-wide package health.

Proof under `../proof/2026-10-19-planning-authority/`:

- `c4-planning-saved-actions-validation.json`: `021196A14E7B7AF7630844A774909EBE065EFA6AB7EE7AB9C8066B41262571CC`.
- `c4-planning-saved-actions-repair.json`: `B7AE25351009944FF01871C2E3089803E022E71B7F3A36A02E9538C19536F2BA`.

Source freezes are released after final identity/resource checks. Product admission,
the real Workbench participant/capability adapter, user-facing actions, process/workflow
policy capture and sustained/max-size capacity remain open.
