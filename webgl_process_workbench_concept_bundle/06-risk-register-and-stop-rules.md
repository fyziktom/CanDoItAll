# Risk register and stop rules

## Major risks

| Risk | Why it matters | Mitigation | Stop rule |
| --- | --- | --- | --- |
| Free-form 3D reduces readability | Camera freedom and occlusion can make diagrams worse than 2D. | Default to a deterministic center-lane 3D layout with perspective fit, role spread, and DOM labels. | Stop and trigger `_corrective-scene-contract-and-layout-reset` if screenshots show persistent readability regression. |
| Library becomes process-specific | A concept library that depends on Processes cannot be reused and is architecturally wrong. | Keep projection outside the library and add gate review. | Stop and trigger `_corrective-renderer-boundary-reset`. |
| Blazor becomes the frame loop | Interactive-server round trips would make authoring feel broken. | Keep per-frame work in JS and commit semantic changes on release. | Stop and refactor before any later phase. |
| WebGL cannot be validated reliably | Screenshot-only proof is weak for real mutations. | Add semantic automation bridge + DOM mirror + export hook. | Stop and trigger `_corrective-automation-and-proof-reset`. |
| Sandbox leaks into production workflow | The concept would become risky before proving value. | Use a dedicated sandbox project and in-memory state only. | Stop if production routes/persistence start depending on the concept. |

## Stop rules

- Stop if the new library takes a direct reference on `CanDoItAll.Modules.Processes`.
- Stop if drag or connection preview depends on per-frame .NET calls.
- Stop if labels are only textured WebGL text with no readable DOM mirror fallback.
- Stop if the sandbox writes to real process persistence.
- Stop if either architecture review gate is failed or skipped.
- Stop if semantic automation is still missing when proof phases begin.
