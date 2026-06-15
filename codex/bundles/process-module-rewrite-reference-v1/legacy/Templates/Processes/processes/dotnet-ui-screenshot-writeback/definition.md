# .NET UI screenshot project-structure writeback

**Key:** `dotnet-ui-screenshot-writeback`
**Criticality:** High
**Autonomy level:** Guarded

Captures UI screenshots when a .NET delivery target has browser-visible UI and stores accepted screenshots under a Screenshots parent node below the process run node.

## Value
Gives UI delivery runs durable visual proof while making backend-only/no-UI applicability explicit.

## Permission model
Every step declares explicit operations and target scope so role permissions remain bounded and product mutation cannot leak into planning, review, validation, screenshot, or writeback work.

## Steps
### 1. Resolve UI screenshot applicability (`resolve-ui-screenshot-applicability`)
- Step kind: Start
- Operation target scope: ExternalProductTargetReadOnly
- Depends on: None
- Outputs: Screenshot applicability manifest with UI routes or explicit no-UI evidence.
- Evidence: App type, UI/no-UI decision, route list, viewport set, runtime command references, and Screenshots parent target.

### 2. Capture UI screenshots when required (`capture-ui-screenshots`)
- Step kind: Work
- Operation target scope: ExternalProductTargetReadOnly
- Depends on: resolve-ui-screenshot-applicability
- Outputs: Screenshot files and browser evidence for UI targets, or explicit no-UI receipt for non-UI targets.
- Evidence: Screenshots, durable browser_snapshot or browser_evaluate state output, route URLs, console state, runtime command references, cleanup receipt, or no-UI evidence.

### 3. Store screenshots under process run node (`store-ui-screenshots`)
- Step kind: Review
- Operation target scope: ExternalActionControlled
- Depends on: resolve-ui-screenshot-applicability, capture-ui-screenshots
- Outputs: Screenshots parent node under process run node and image asset storage receipts for accepted screenshots.
- Evidence: Screenshots parent node id, image asset ids, inspection results, rejected images, and no-UI receipt when applicable.

### 4. Hand off screenshot writeback evidence (`screenshot-handoff`)
- Step kind: End
- Operation target scope: ExternalProductTargetReadOnly
- Depends on: store-ui-screenshots
- Outputs: Parent-ready screenshot writeback handoff.
- Evidence: Applicability, node ids, asset ids, route evidence, no-UI status, and blockers.
