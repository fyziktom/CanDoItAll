# Structured Input

## Core Objective

- Make Process Manager chat voice mode and run usage answers match the behavior expected from a process manager agent.

## Success Criteria

- Voice mode enabled in Manager chat automatically sends synthesized assistant audio after a successful voice-originated or voice-mode send.
- The manual read button remains functional.
- Manager chat loads selected-run usage telemetry and prompt context includes cost, input tokens, cached input tokens, output tokens, and total tokens when available.
- A cost/token prompt no longer encourages the agent to claim the data is absent when the projection has it.

## Hard Constraints

- Keep fixes localized to the Process Manager chat/runtime projection path and tests.
- Preserve voice access checks based on the selected manager agent configuration.
- Do not add broad provider-driver fallback behavior.

## Allowed Side Effects

- none beyond documented subbundles

## Source Artifacts

- User-provided transcript and manager prompt in `inputs/00-original-request.md`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceShell.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessManagerChatPromptClassifier.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Processes.Projections\ProcessWorkspaceShellProjectionContracts.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\ProcessManagerChatPromptClassifierTests.cs`

## Input Coverage Signals

- N001: voice transcription succeeds in Manager chat.
- N002: when voice mode is active, assistant response should auto-read after send.
- N003: manual read works, so synthesis/playback path exists.
- N004: Manager prompt lacks selected-run cost/token usage data.
- N005: process manager agent should have access to process runtime usage data.

## Dependency And Sequencing Signals

- Usage telemetry loading and prompt enrichment must land before browser/user validation of cost-token questions.
- Auto-speak parity can be implemented independently but must share the same Manager chat voice access gate as manual read.

## Validation Expectations

- Component tests must fail against the old behavior and pass after the fix.
- Unit tests must cover prompt/tool classification where it affects cost/token questions.
- Build must pass before restarting 5032.
- Browser proof must show the Manager chat tab loads and exposes voice controls after restart.

## Evidence Contract

- `dotnet test` for targeted component/unit tests.
- `dotnet build src/CanDoItAll.Web/CanDoItAll.Web.csproj`.
- Restarted listener on `http://localhost:5032` with PID and health check.
- Browser screenshot or DOM proof for `/processes` Manager chat tab on desktop viewport.

## UI Validation Strategy

- Use a large desktop browser pass for `/processes`, open Manager chat, confirm tab content and voice controls are present. Narrower-width follow-up is low risk because the intended fix changes behavior/context, not layout.

## Browser Validation Analytics

- Route: `http://localhost:5032/processes`.
- Viewport: desktop large screen; optional narrower pass if controls shift unexpectedly.
- Actions: open Manager chat tab, inspect voice control availability and manager prompt/composer state.
- Evidence path: `.artifacts/process-manager-audio-reply-and-run-metrics/`.

## Working Assumptions

- Process runtime usage telemetry is already available through `ProcessRuntimeWorkspaceProjection.Stats` when `IncludeUsageTelemetry` is true.
- Manager chat should prefer preloaded selected-run metrics for simple cost/token questions rather than forcing tool calls for data already in the process projection.

## Primary Risks

- Loading usage telemetry for Manager chat can increase projection cost; mitigate by keeping history and metric history disabled.
- Auto-speak may fire for typed messages while voice mode is enabled, matching existing agent chat behavior; document this parity rather than creating a separate hidden state.
