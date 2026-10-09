# Required browser journeys and native oracles

Use the current Playwright fixtures and supported large-desktop viewport, isolated output configuration, task-owned PostgreSQL 18 and loopback hosts. Do not use the user's running application, persistent browser profile, providers, credentials or ordinary ports. Wait for the real interactivity/editor-ready state before acting. Preserve genuine failures; do not replace them with screenshots of static markup.

## J1 — Pending authoring command and same-opening read

Open the real `/processes` workspace and select a definition. Use a controlled fixture at the existing native owner/client seam to hold a valid command; do not substitute the whole renderer. Type a draft, Save, type another field if currently permitted, then switch tab or refresh without changing the edited definition. Release read and command in both orders.

Observe command count and exact submitted values, accepted/rejected/unknown result, visible raw draft and version/conflict state, and action availability. A refresh failure after an accepted command must not offer a blind duplicate write. Where the native request-local owner is still not durable, report that independently; this interleaving journey proves native-host command handling, not restart persistence.

## J2 — Role identity after Add and Delete

Open an existing role dialog, choose a legal template/Add action and apply it. Confirm owner-returned selected key and displayed fields describe the added role when the original selection remains current. Delete that role, verify the returned surviving role or explicit empty state, and confirm deleted fields/errors do not remain live. Test deleting the last role in a safe owned fixture.

Repeat with a held command followed by a different explicit role selection. Its completion must not hijack the successor selection. Use actual native owner output (or the narrow established seam returning that native result), not a mock projection tailored only to the expected screen. Keep this distinction separate from durable authoring acceptance.

## J3 — Non-first-step semantic round trip

Use a legal definition fixture with two steps carrying different decision-role bindings, then a nullable binding variant. Select the non-default second step, change only a harmless visible field and Save. Inspect the exact native command/owner result to prove its `DecisionRoleKey` and other hidden metadata still belong to that step; the first step must not change.

Repeat after changing tabs and after step reorder/selection refresh. No screenshot or 'Saved' label substitutes for the command/owner oracle. Fresh-scope/restart persistence is evaluated by P3, not assumed here.

## J4 — Raw drafts, two openings and real canvas

Through the actual renderer, enter invalid role workflow IDs/allocation and step numeric text. Change Server tabs and return; verify exact text, validation and disabled mutation state remain. Use `/two` and the production-supported multiple-opening path: edit/open/close one workspace's dialogs and canvas floating windows without corrupting the other.

Test canvas selection, gesture completion and toolbar actions under pending/read refresh. Confirm final geometry/selection survives, floating windows are independently identified, and focus/stacking/keyboard controls still work. Preserve existing view-lifetime and permission behavior; do not remove gesture or keyboard paths to simplify tests.

## J5 — Keep the existing real native journey

Run the relevant `ProcessNativeBrowserTests` cases: accepted prepared launch in global/project scope, induced first-read error, eventual workflow completion, reload without duplicate launch/workflow, authorized Markdown files and revision refresh, manager selection/context/streaming, persisted chat response, real attachment staging/submission and explicit cancellation of a different waiting run.

The external inference fixture is deterministic loopback I/O through native orchestration; label it accurately. It proves native application behavior under a controlled upstream, not a live commercial provider's availability. Verify actual prepared/run/workflow/chat/file owners and originating IDs. Closing a dialog must not cancel a run. Keep permission denial/private diagnostics out of the rendered page.

When affected, also run the existing Workbench workflow/source-link journey and voice allowed/denied/delayed-owner tests. Preserve the default non-Processes `ChatWorkspacePanel` path as well as its typed child binding. No approval, capability or context bypass is allowed to make a fixture pass.

## J6 — Asset modes, publication and complete children

Build and run all existing Fast/Parity source and published sandbox cases with their actual mode-specific manifests. Exercise all eight workspace tabs, live sections, chart, Mermaid graph, actual files and conversation children, not empty stubs. Check browser exceptions, meaningful network/static asset errors, viewport geometry, keyboard/focus/scroll and stacking. Verify the production publish does not expose development-only endpoints.

Record actual mode/configuration/source/sibling candidate, screenshots/traces retained privately, and a sanitized summary or content hashes. A missing font/JS/chart that leaves HTTP 200 is a failed asset behavior. A missing native database must not be hidden by silently routing the production journey to the sandbox.

## Result reporting

Record exact filters and expanded cases, command arguments/owner IDs with safe redaction, outcome, source candidate, and any unavailable prerequisite. Raw evidence remains outside Git unless separately reviewed for safety. The pushed summary must make clear which journeys were executed and which remain NOT_RUN/BLOCKED/FAIL.
