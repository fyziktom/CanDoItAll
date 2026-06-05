# Subprocess Method Classification

Codex must fill this with the current source before moving production code.

| Method/Region | File | Category | Side effects | Migration candidate | Required proof |
| --- | --- | --- | --- | --- | --- |
| `HandleSubprocessDispatchAsync` | Dispatch.cs | lifecycle orchestration | transitions, service call, finalizer | yes, facade/partial | route parity |
| `TryBuildSubprocessCapabilityGapBlockReasonAsync` | Dispatch.cs | child-step query + reason | DbContext read | yes | capability-gap tests |
| `ProjectCompletedSubprocessArtifactsAsync` | Dispatch.cs | projection orchestration | DbContext read/write, claim check | yes, coordinator | projection parity |
| `WriteProjectedSubprocessArtifactAsync` | Dispatch.cs | file write | directory/file write | yes, writer coordinator | path safety + content proof |
| `RecordSubprocessProjectionGapAsync` | Dispatch.cs | journal write | DbContext write | yes, journal coordinator | dedupe proof |
| `ResolveSubprocessParentStepStatus` | Dispatch.cs | pure status mapping | none | yes | unit tests |
| `BuildSubprocessParentTransitionReason` | Dispatch.cs | pure reason text | none | yes | unit tests |
