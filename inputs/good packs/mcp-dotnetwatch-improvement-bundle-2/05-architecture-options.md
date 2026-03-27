# Architecture Options

## Option A: Fix The Existing Architecture

This is the recommended first move.

### SourceWatch lane

- stop passing `--artifacts-path`
- enable MSBuild server by default
- keep one long-lived watch process per logical app
- treat browser truth as mandatory for UI loops

### Runtime confirmation

- add a real hot-reload generation token to `/_dev/runtime`
- increment it on in-process hot reload using a `MetadataUpdateHandler`
- only mark the change confirmed when that generation changes or the process truly restarts

### Wait semantics

- rename or redefine `RevisionConfirmed`
- introduce a distinction between:
  - `WatchReportedApplied`
  - `RuntimeGenerationAdvanced`
  - `BrowserValidated`

This preserves fast backend waits while making it obvious which layer was actually proven.

## Option B: Split Fast Watch From Isolated Build/Test

This should happen even if Option A is chosen.

- `SourceWatch` should use normal project outputs and optimize for developer speed
- build/test/atomic lanes can keep isolated outputs where isolation matters more
- log cleaning should happen after the build, not by reshaping the build into a slower workflow

This is the cleanest separation of concerns in the current design.

## Option C: Long-Lived Tray Or Service Manager

This aligns with the user's idea.

### Pros

- clear ownership model
- manual restart and visibility for the operator
- MCP becomes an RPC client instead of the conceptual owner
- less confusion when multiple agents are attached

### Limits

- it does not fix the primary hot-reload bug by itself
- if the manager still launches watch with `--artifacts-path`, the same failure remains

Recommendation: treat the tray/service manager as a control-plane improvement after the `SourceWatch` launch shape is fixed.

## Option D: Mutation Queue For UI Loops

This addresses the user's suspicion about overlapping edits and repeated waits.

- one active nearby edit per logical app
- no second edit until the first change reaches a terminal state
- terminal states should be:
  - runtime generation advanced
  - restart completed
  - browser validation failed
  - timeout

This would stop agents from piling edits on top of unverified state.

## Preferred Roadmap

1. remove `--artifacts-path` from `SourceWatch`
2. add a real hot-reload generation signal
3. split wait semantics into reported vs confirmed
4. keep isolated artifacts only for build/test/atomic
5. consider tray/service manager for operator control
6. add a mutation queue for nearby UI edits
