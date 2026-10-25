# Closed-child Process accounting recovery

Status: qualified for the affected P10 scope. All 134 selected cases pass: 131
correctness/compatibility cases, including 55 new cases, and three diagnostic profiles.
Finite latency, sustained concurrent capacity, parent cancellation/refresh and producer
activation remain open. SPA remains clean on `codex/concurrent-mutations` at `70c65dd`.

## Implemented behavior

The [P10 design](../plan/P10-closed-child-accounting.md) adds an explicit accounting
action when an original Agent child has closed but the Process still awaits its outcome
and measured work. It can recover without the lost approval request. The existing
Process completion owner verifies and atomically settles the original child; the action
does not invoke a provider, mutate the Agent journal or publish another Project effect.

Workbench owns the small product bridge. Web exposes `POST /review/accounting` under the
same exact run/step/child route and permission boundary as review. The read model offers
a separate accounting expectation only for verified closed work. Strict bounded JSON
reading is shared with the existing review write, and current manager/profile/source
checks are shared without changing their order. Retained v1 approval digests are unchanged.

Terminal/cancelling parents still refuse new review completion. Their accounting and
snapshot refresh require the separate P11 transitions; this action does not reopen them.
Clients retain their exact accounting request before sending and reload current runtime
after a result. A refused action does not assert that accounting is still pending.

## Validation

| Lane | Passed cases |
| --- | ---: |
| Current manager/profile/source authority | 14 |
| Strict bounded request reader and accounting contract | 24 |
| Real SQL/file accounting recovery | 14 |
| Actual HTTP accounting and restart recovery | 3 |
| Existing product review integration | 14 |
| Existing HTTP access | 4 |
| Existing review HTTP and restart | 7 |
| Fixed v1 saved-intent compatibility | 9 |
| Real narrow Agent reader | 7 |
| Isolated narrow Agent reader | 8 |
| Existing review API contract | 27 |
| Existing diagnostic read profiles | 3 |
| Total | 134 |

Recovery covers both snapshot modes and approve/reject outcomes, original-request loss,
exact receipt replay, renewed credentials, another actor/action, current revocation,
stale Agent/Process expectations, competing action CAS, lost acknowledgement, cancellation
and authority changes after commit. Assertions retain exact work, unchanged Agent files,
one original provider invocation and the expected zero/one Project effects. HTTP tests
use the registered services and restart the host; GET construction cannot create the
execution workspace.

Direct standard Release builds pass for Workbench (55.22 s), Web (81.33 s), Unit (30.80 s)
and Integration (36.65 s). The final test-only correction rebuilds Integration in 34.10 s.
Source verification bridges both builds. The full portability scan passes final enforcement
without baseline writing: 15,665 reviewed findings. The only baseline movement is the
unchanged authority call relocated into its shared owner. Rendering register (1,212
inputs), documentation and diff checks pass.

The first candidate ran 38 cases: 37 passed and one depth assertion failed because it
required exactly JsonException, while the correctly rejected input produced its
JsonReaderException subtype. The correction accepts that subtype; production and its
depth limit are unchanged. The bridge proves one test-source change and only the
Integration DLL/PDB changed. All 134 cases were then discovered and executed again.
The initial observer also refused an outdated baseline pin before test execution; the
reviewed baseline pin was corrected. Both events remain retained.

## Read latency diagnosis

All 15 profile samples and seven other diagnostic rows are retained. These profile
tests enforce correctness, not the separate one-second acceptance limit.

| Mode / steps | Five elapsed samples, ms | Median process allocation | Median SQL execution, ms |
| --- | --- | ---: | ---: |
| RunSnapshot / 1 | 926.82, 729.73, 920.45, 657.71, 727.20 | 43,571,000 B | 136.10 |
| StepSnapshot / 1 | 917.42, 924.59, 875.99, 817.09, 811.73 | 43,754,152 B | 135.00 |
| StepSnapshot / 1024 | 1110.17, 1055.78, 675.97, 750.68, 658.50 | 45,873,400 B | 132.02 |

Every measured GET retains 27 Process SQL commands. Scoped reader construction takes
median 2.85-3.24 ms, two current manager checks together 6.23-7.70 ms, and the explicit
manager source check below 0.1 ms. These are not dominant costs. The product reader
contains median 709-840 ms; existing catalog normalization accounts for 60-114 ms and
file run loading for 42-50 ms. Inclusive spans overlap and cannot be summed. Measure
the inherited Agent authority/read stages and database connection opening next. Do not
remove current permission checks or split DI merely on the earlier hypotheses.

The two bounded CPU observation windows retain 413 visible-process samples, median
7.37% and maximum 15.06%. Their original timestamps and small overlap are explicit.
These observations do not measure inaccessible processes or qualify sustained traffic.

## C# Architecture Gate Result

Status: Pass for the accounting responsibility/dependency scope; activation remains
gated by the independent capacity, cancellation and refresh requirements.

Workbench snapshots `snap-20261021211104-88466e8e` and
`snap-20261021225413-81f98ea8` have 402/403 documents, 1,167/1,172 types,
10,124/10,139 members and 2,192/2,196 edges. One module cycle, five type cycles and all
402 findings are unchanged. Three new product types form the request/result/service;
two additional types are generated Debug validation attributes. The 73 existing workspace
diagnostics remain; direct successful Release builds establish compilation.

The actual 33 project references in each Workbench/Web project are byte-identical.
No new project, partial class, schema or reverse module dependency was added. The new
service file has 64 lines. Shared authority and request parsing remove duplication.
The review data record gains one cohesive accounting expectation; its existing member
count finding is reviewed. Isolated authority/parser tests use explicit seams; real
SQL/file/HTTP tests prove the composed behavior.

## Identity and resources

Final source identity: 9,907 inputs, manifest SHA256
`6A905D8080778F8CE76C6F8D9A13894C55DB858439A61E6481178C75FEB8AB0F`.
Final runtime identity: 7,013 files, manifest SHA256
`1D016C5D214ABF0B47EAFFBB134D2EAE5DD5E41A2EF75AB20F2FE7619AFD6134`.
Both remain unchanged through execution. The 1,349.43-second resource observer covers
all 12 test commands and reconciles zero new surviving processes, database names or
temporary paths. Both CPU observers closed normally. The existing isolated PostgreSQL
server and standard Release outputs were reused.

- [Initial failed candidate](../proof/2026-10-19-planning-authority/c4-accounting-recovery-candidate.json):
  127,835 bytes, `9F366054C6BFD0612CD1C11E9ACEBB09E7BA66A70CFC7859C0DA2BC5A25E3CD5`.
- [Final correction and full proof](../proof/2026-10-19-planning-authority/c4-accounting-recovery-parser-correction.json):
  1,239,305 bytes, `A12B5F7071B5C3A8EBF9A9350B8A5D87ED6BF23BD8EECEF4B88DFECB6DADF40C`.

Both retained proofs pass secret scanning and remain ignored generated artifacts.
Historical cleanup denials listed in [resources](../resources.md) remain unresolved.
Automatic approval review rejected those deletions as "blocked by policy"; none was
retried. Zero new resource deltas are not a claim of complete historical cleanup.
