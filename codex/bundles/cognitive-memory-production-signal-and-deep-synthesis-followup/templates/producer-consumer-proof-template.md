# Producer / Consumer / Lifecycle Proof Template

Use this for every new enum value, signal kind, anchor state, lifecycle transition, or database record used as behavioral proof.

| Domain artifact | Producer path | Consumer path | Lifecycle path | Negative test | Positive test | Notes |
|---|---|---|---|---|---|---|
| ExampleSignal | `repo://src/.../Emitter.cs` | `repo://src/.../Evaluator.cs` | `repo://src/.../ScheduledRunner.cs` | `ExampleSignal_NotEmittedByMereRetrieval` | `ExampleSignal_EmittedAfterAcceptedOutcome` | Tests must not seed ExampleSignal directly. |
