# Initial registered Process business binding

The [accepted design](../plan/P07-initial-business-binding.md) introduces an explicit
initial-binding capability on the existing inherited participant seam. Core selects
exactly one business owner and parent contract; Process evidence stays in Application,
and Workbench owns catalog, Project, original material and finite write-context checks.
The workspace request can supply source evidence, not arbitrary business grants.

The first frozen runtime candidate is **172/175 passing**, retained as failed:
`proof/2026-10-19-planning-authority/c4-initial-business-binding.json`,
251875 bytes, SHA-256
`28DFA79502C66CE5B322DB4C4A7A514BC5AF9B712AB16D8AE4AAC9F40C2816EA`.
Secret scan: zero findings.

One corrupt-material fixture violated the database byte-count constraint before reaching
the binder. Its correction changes the stored byte count consistently while leaving the
original fingerprint unchanged. Both producer/workspace fixtures admitted read-only
assignments and correctly hit the publication-permission guard. Positive cases now admit
that operation before launch acceptance; explicit read-only cases remain negative tests.
These are test-only corrections. No production authority check is relaxed.

The initial build also retained one Integration compile failure for a missing test-support
import, then passed the corrected build. Direct Core, Processes.Application and Workbench
Release builds pass. Unit and Integration builds pass. Full portability enforcement reports
15652 reviewed findings unchanged; documentation, rendering register and whitespace pass.

Source: 9789 files,
`3A856E377EAE71247EB17F814BF541FCD5BF238EEF1742A955B83D582969EE5E`.
Runtime: 2595 files,
`D825FDA3F6551E1AF2A6F32626458A0102B62F18D1AF9787147267B128B3CD89`.
Both remained unchanged through all ten observed test commands. No observed process,
database-name or new temporary-path delta remains.

Architecture snapshots `snap-20261021031959-77361fd7` (five business/Process projects)
and `snap-20261021032147-1c388f7c` (Core/Persistence) add no project edges or cycles.
One informational many-members finding covers the ten-field immutable source wire record;
it contains no orchestration responsibility. Existing workspace diagnostics are retained.

The second frozen candidate is **4/5 passing**, retained in
`proof/2026-10-19-planning-authority/c4-initial-business-binding-final.json`,
41965 bytes, SHA-256
`2DE35E8BE4A847E63CA379A8722D6D64D5E379CA6DEEC6FDFD5C2FBB11EF1B10`.
Its only failure expected the parent StepSnapshot mode on the child policy. The
parent selects the material at the Process-step boundary; the inherited Agent keeps
that material fixed for its entire child run. The corrected assertion requires the
original parent mode and the child's complete resolved policy, including RunSnapshot.
No production behavior changes. The four-case producer/workspace suite is repeated.
The second candidate also retains an Integration compile correction for a test constant's
namespace. Full portability remains 15652 unchanged; source/runtime and resource
reconciliation pass. No process, database-name or temporary-path delta remains.

The final four-case suite passes. Its retained proof is
`proof/2026-10-19-planning-authority/c4-initial-business-binding-verified.json`,
31510 bytes, SHA-256
`CBCE36C42E42698979626B56C9190926FB42AC5AE9C86D7FF1E9474C9DA363E2`.
Production sources and production assemblies are identical across all three candidates;
the exact test/document and two test-assembly deltas are retained. Together with 172
unchanged earlier passing cases and the corrected corruption case, this qualifies
**177 distinct behaviors**. Secret scan: zero findings.

Final source: 9790 files,
`122FDC210999F2DF2B1390541F7A10904EF5CFC3D594BACD184BA17BEECA867D`;
runtime: 2595 files,
`7BF2521F3BCD5AF2FA97777BBB68AD8F767DB5ECF956C96F4D23346631211DF6`.
No observed resource delta remains. Full portability remains 15652 unchanged.
The first final whitespace check found a trailing blank line in the resource record;
that documentation-only correction is outside the frozen runtime candidate.

Original immutable input, initial failure accounting, capacity, manager review, cancellation,
Workflow and settings/UI closure still gate full product activation.
