# Test surface and gap map

| Area | Observed state | Gap / required addition |
| --- | --- | --- |
| Current canvas runtime | Strong semantic Playwright helpers already exist. | Mirror the helper style for WebGL; do not regress to screenshot-only proof. |
| Current Processes component tests | Strong coverage for move/connect/delete semantics in 2D editor. | Reuse semantics to design sandbox interaction tests, but keep them sandbox-only. |
| Template services | Existing tests around template pack and projection services exist. | Add projection-to-WebGL adapter tests rather than re-testing the pack itself. |
| New library | No current WebGL-specific coverage exists. | Add focused wrapper/contract tests and Playwright coverage on dedicated sandbox route. |

## Minimum new proof expected from execution

- contract-level tests for the new WebGL library,
- adapter tests for template-to-scene projection,
- sandbox interaction tests for in-memory move/connect flows,
- Playwright proof for semantic automation bridge and screenshot export.
