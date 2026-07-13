# Input Coverage Matrix

| Input | Covered by |
|---|---|
| User requested bundle only, no implementation | README, R1, Gate A |
| Refactor `AgentFrameworkProcessExecutionAdapter` properly | SB01, SB02, SB03, SB04, SB05, architecture inventory, boundary map |
| Avoid partial-class architecture | Hard constraints, architecture checkpoints, every subbundle partial policy section |
| Keep runtime/dispatcher generic | Domain boundary constraints, SB06, Checkpoint 3 |
| `IsDotNetRuntimeLifecycleTool` leak | SB06, requirements R5, current-state inventory |
| Use domain drivers properly | Boundary map, PSR-3, SB06 |
| Use GPTPro root cause from Tetris bundle | Pro synthesis, SB03, SB05, SB07 |
| Use escalation root cause bundle | Pro synthesis, phase plan, SB03 through SB07 |
| Analyze all templates/artifacts for similar issues | Template/artifact scope, inventory plan, SB07 |
| Use C# architecture skills/quality | Architecture files, checkpoints, review gate, proof manifests |

