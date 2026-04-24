# Review gate checkpoints

| Gate | Trigger | Required evidence | Pass criteria | Corrective on failure |
| --- | --- | --- | --- | --- |
| Gate A | After subbundle 03 | Generic scene smoke proof, build, contract review, DOM mirror check | Library stayed universal, runtime is JS-owned, and the guided perspective default is intact | _corrective-renderer-boundary-reset |
| Gate B | After subbundle 07 | Sandbox screenshots, move/connect proof, in-memory-state review | Scene is readable enough, interactions are meaningful, no persistence leakage | _corrective-scene-contract-and-layout-reset |
| Proof gate | After subbundle 09 | Playwright proof, semantic snapshots, screenshot export | Automation is deterministic and can prove actual state changes | _corrective-automation-and-proof-reset |
| Final closure | After subbundle 10 | Fresh build/tests/screenshots/reports/workbook | Concept can be honestly judged as pilot-ready or not pilot-ready | N/A |

## Memo rule

Every gate must create or update:

- `reviews/02-architecture-gate-memo-log.md`
- the execution report,
- any linked corrective subbundle evidence if triggered.
