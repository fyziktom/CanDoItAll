# Requirement Traceability

| Requirement | Raw note coverage | Owning legacy tasks | Current proof |
| --- | --- | --- | --- |
| `R01` | `G01`, `G06` | `T10`, `T11`, `T12`, `T13`, `T15`, `T17` | Canvas stage shell, canvas links, canvas nodes, canvas minimap, and Playwright retained-renderer pack are green. |
| `R02` | `G08` | `T14`, `T17` | `exportImageData` composes canvas surfaces directly and export/browser artifacts remain green. |
| `R03` | `G02` | `T03`, `T15` | `HandleNodesMovedAsync` patches committed positions and reloads only on fallback conditions. |
| `R04` | `G03` | `T02`, `T16` | ProjectStructure and PromptFactory both schedule delayed persistence for drag and state-change flows. |
| `R05` | `G07` | `T07`, `T17` | `CanvasLibHeadAssets` and `CanvasLibBodyAssets` are the shared include path for web and sandbox shells. |
| `R06` | `G04`, `G05` | `T04`, `T05`, `T16` | ProjectStructure and PromptFactory browser artifact capture and interaction tests are green. |
| `R07` | `G06` | `T09`, `T17` | Dead legacy drag/SVG helpers were removed from the runtime source and the remaining non-runtime compatibility surfaces are documented. |
| `R08` | `G01` through `G08` | `T15`, `T16`, `T17` | Asset verification, component tests, Playwright tests, browser artifacts, execution report, and bundle validator gates are all current. |
