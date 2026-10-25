# Accepted Process source scaling baseline

The [measurement plan](../plan/P07-accepted-source-scaling.md) ran five pure codec shapes
in three fresh Release test processes: all 15 executions and 45 measured rounds pass.
All 5327 production source files are unchanged from the registered-source candidate.
The opt-in Unit test checks the exact original assignment outside timed intervals.

| Steps | Prompt characters per step | Prepared payload bytes | Median ms/read | Median allocated bytes/read |
| --- | --- | --- | --- | --- |
| 1 | 256 | 9,964 | 0.076 | 64,208 |
| 64 | 256 | 283,168 | 2.107 | 1,962,521 |
| 256 | 256 | 1,116,340 | 8.758 | 7,749,653 |
| 1024 | 256 | 4,449,532 | 71.075 | 31,073,808 |
| 1 | 1,048,576 | 1,058,284 | 1.911 | 5,305,808 |

Each median uses nine rounds across three processes. The 1024-step timing range is
42.032–114.808 ms/read. Host noise is visible; these measurements exclude SQL, other
authorization gates and sustained load. Allocation growth and dependence on unrelated
steps/prompts establish a real cost independently of timing noise. Four indexed queries
do not make this a bounded source read. The [storage design](../plan/P07-accepted-source-projection.md)
reopens the owner implementation before producer activation. Hash-copy reduction alone
would leave whole-graph decoding.

The Unit Release build takes 44.242 seconds; executions take 7.203, 7.279 and 7.139 seconds.
Full portability passes with 15,647 unchanged reviewed findings. Validation-tool,
rendering, documentation, diff and exact retained-proof secret checks pass. The 45.021-second
observer covers all commands. Frozen identities pass; the root is Released with zero
remaining observed processes and zero database-name delta. Nine unowned TEMP entries
are preserved. No new project, standalone probe, host or database was created. Historical
probe-output cleanup rejection remains unchanged.

Source: 9645 files, `DB55CE4C4BAC63DCB42AE98C6E870D2C603B8A792A7EDD307EAEAAD23C93527B`.
Runtime: 1484 files, `0B299B8B6F1E7538A42D3147617268761091933172335DCDA3E75C98C277CD78`.
Retained proof `proof/2026-10-19-planning-authority/c4-accepted-source-scaling.json`:
`1BDEA6877DE5BADE33453CB0DDFC7785CCA674EDE763E70B303F2E9681883C33`.

This is a baseline and a design invalidation, not a capacity pass or producer activation.
