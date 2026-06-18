# Live Processes And Staffing Follow-up 2026-06-17

## Raw User Report

The candidate selection mechanism still selected Delivery Manager for `.NET architecture` and `.NET implementation` roles even though `.NET Architect` and `.NET Developer` agents exist in the team.

When clicking Start, the HR assignment window should hide and show a notification that the process started.

The process is probably still running on port `5032`, but `C:\programovani\dotnet\output` is empty. If agents are still working, keep them finishing; if they do not finish correctly, analyze the data and add repairs into the bundle.

Repair and improve Live Processes by comparing the `maf-processes-refactor` branch:

- Active process cards should be visible.
- The selected time window must be honored; with `1h`, older runs should not be shown.
- Blocks and escalations should appear on the first tab and allow resolution or rework there.
- Original dialogs on Live Processes and Processes pages must be analyzed for missing UI/UX elements.
- Actual dialogs are simplified and miss necessary information.
- Active working agents should be shown as cards.
- Live Processes tabs should use the available width.

## Initial Findings

- The software-delivery parent subprocess-launch steps are authored with `delivery-manager` as `Responsible` for `architecture-review` and `implementation`, so launch resolution chooses Delivery Manager even though the visible step titles describe technical architecture and implementation work.
- `GET /api/processes/live?windowMinutes=60&take=50` returns active runs whose `LastEventAtUtc` is many hours older than the selected one-hour window because `ProcessRuntimeProjectionQueryService.GetLiveProcessesAsync` intentionally includes active snapshots outside the window.
- The current Live Processes projection page only displays generic run/event rows and one manager-context card. The `maf-processes-refactor` branch had activity notification cards, escalation cards, active-agent cards, process detail, stage detail, artifact detail, escalation detail, suppression, and manager-chat dialogs.
- `C:\programovani\dotnet\output` was empty during follow-up inspection. The live API showed active/attention runs, but the latest SB31 smoke run had only launch-created events and had not dispatched work.
