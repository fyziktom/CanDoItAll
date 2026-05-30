# QA Prompt

Review SB01 closure.

Confirm the old tracked project path is gone, the new project path exists, assembly/root namespace are `CanDoItAll.AppComponents`, direct web/test consumers point at the renamed project, and exact old facade imports are repaired. Confirm `CanDoItAll.Components.*` package references and sibling-repo settings were not renamed.

Accept closure only when the proof manifest, semantic invariant contract, targeted build transcript, component test transcript, stale-reference search transcript, anti-stub audit, raw-note closure, and completed-stage bundle validator all pass. Browser proof is not required unless the implementation changes rendered behavior.
