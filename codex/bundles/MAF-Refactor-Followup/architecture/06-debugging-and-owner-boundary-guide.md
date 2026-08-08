# Post-change debugging and owner-boundary guide

Classify every failure before editing:

| Stage | Typical evidence | Owning layer |
|---|---|---|
| admission | operation rejected, profile generation | application activity coordinator |
| UI context | route/navigation mismatch, Loading/Failed | UI context registry/module publisher |
| authority | source/scope/access mismatch | source authority provider |
| workspace scope | identity mismatch/path denial | workspace runtime factory/services |
| capability composition | missing/duplicate/denied tool | capability planner/contributors |
| provider dispatch | queue, credentials, transport | provider runtime/driver |
| session restore | compatibility/migration outcome | MAF state adapter |
| tool governance | policy decision/proposal | generic governance pipeline/module contributor |
| tool execution | typed failure/receipt | owning tool domain |
| approval | proposal/decision coverage | application governance |
| output/finalizer | schema/finalizer trace | runtime output policy/MAF mapping |
| persistence | revision/conflict/terminal state | execution store/service |
| process completion | branch/gates/artifacts | Processes |
| workflow LLM | stateless request/result | LLM port/workflow invoker |
| UI refresh | completion notification/superseded load | UI coordinator |

## Bugfix protocol

1. Capture operation ID, execution run ID, session ID, authority ID/fingerprint, workspace identity, provider/model, state schema, and failure stage.
2. Reproduce with a deterministic fake where possible.
3. Add a failing test at the owner boundary.
4. Fix the smallest cohesive owner, not the symptom caller.
5. Run focused tests, checkpoint tests, architecture guards, and changed-project build.
6. Update the bugfix record and durable handoff.

A path denial is not fixed by broadening scope. A continuation failure is not fixed by transcript replay. A process gate failure is not fixed inside MAF.
