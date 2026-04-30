# Assumptions And Risks

## Assumptions

- The launcher belongs on the project structure workbench route rather than the projects card board because the requested workflow is mindmap/project-structure analysis.
- The process launcher belongs on the process definition canvas in the Steps tab because the requested validation adds a review role to the mindmap-style process surface.
- A contextual chat opens one active floating chat window at a time and always creates a new persisted thread when an agent is double-clicked.
- Existing agent access metadata is the source of truth for whether an agent appears in a contextual launcher.

## Critical Path Risks

- The shared contextual component is a critical foundation; incorrect access filtering would expose wrong agents on both host pages.
- Chat orchestration is a critical foundation because the request requires parity with the Agents chat tab and durable thread visibility.
- Host integration is risky because project and process canvas windows already have several overlapping overlays and z-order/positioning defects would break the workflow.

## Validation Risks

- Real agent runtime responses may depend on configured providers and API credentials; proof must still verify thread creation and chat send behavior, and record any provider blocker honestly.
- Seed data may not include agents with project/process access for the chosen calculator project or process; validation may need to use existing seeded agents or create/update an agent through the app before proving the launcher.
- Playwright proof must inspect screenshots for clipping, lateral overflow, z-order, and whether both launcher and chat windows remain readable.

## Reopen Triggers

- Reopen the shared component subbundle if either host shows agents outside the allowed context or hides agents with valid access.
- Reopen the project integration if the launcher cannot open on the project structure canvas or the created thread is not visible from the Agents chat tab.
- Reopen the process integration if the launcher cannot open on the process canvas or process-specific access is ignored.
- Reopen validation if screenshots do not show the open launcher and open chat states.
