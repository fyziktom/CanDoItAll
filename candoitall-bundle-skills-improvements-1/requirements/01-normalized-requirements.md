# Normalized Requirements

| Requirement | Description |
| --- | --- |
| `R001` | The preparation validator must verify that every markdown bullet under a subbundle `## Exact Source References` section is an absolute path and that the referenced file or directory exists at validation time. |
| `R002` | The preparation validator must verify that feedback bundles include execution-report scaffolding with at least `## Status` and `## Raw Note Closure` so note-by-note closure cannot be omitted accidentally. |
| `R003` | The workflow and execution skills must explicitly require a final bundle-documentation sync after proof lands, including root README validation summary and subbundle/report status updates. |
| `R004` | The workflow and execution skills must require bundle re-validation after bundle repair or status/documentation updates that change the bundle contract materially. |
| `R005` | The workflow, preparation, and execution skills must document `mtp-hot-reload` as an optional iteration aid only when the targeted test project already uses Microsoft Testing Platform, and must require a clean standard confirmation run before completion. |
