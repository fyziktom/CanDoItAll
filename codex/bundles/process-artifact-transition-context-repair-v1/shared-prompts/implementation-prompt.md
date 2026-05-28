# Implementation Prompt

Implement the smallest runtime repair that carries process-owned artifact validation lineage from `FinalizeStepCompletionAsync` into `ProcessesService.TransitionStepAsync` without exposing that lineage to API/manual callers. Preserve `ProcessCompletionArtifactValidator` as the single validator. Add focused integration tests proving matching automation lineage passes and manual stale lineage still fails.

