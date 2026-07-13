# Target Solution

## Runtime Shape

- `software-delivery` remains the parent process for broad app delivery.
- `dotnet-development-slice` remains a child process that owns scoped implementation orchestration and evidence handoff.
- Bounded feature/function work remains in smaller subprocesses where each child can be tested independently.

## Operation Contract Invariants

- Architecture/planning steps: read process/project/upstream artifacts and write managed process artifacts only; no product mutation.
- Subprocess launcher steps: explicit external-action/start-process authority; no direct product mutation unless the step also owns repair implementation.
- Code implementation/repair steps: mutable product target plus validation permissions needed by their prompt and tool policy.
- QA steps: read-only product target plus validation/runtime/browser/image-analysis authority required by the proof contract.

## HR/Readiness Invariants

- HR matching must evaluate both role family and tool/operation capability.
- A step that semantically requires product mutation, subprocess launch, validation, runtime proof, or image analysis cannot be marked ready when the declared contract or selected agent lacks those capabilities.
- Failure must be explicit and actionable: name the missing operation/tool and the step that requires it.
