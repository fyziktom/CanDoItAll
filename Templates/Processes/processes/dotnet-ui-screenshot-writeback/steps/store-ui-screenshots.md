# Store screenshots under process run node

Create or reuse a Screenshots parent node under the current process run node. Prefer `ProcessRunNodeId`, `ParentProcessRunNodeId`, or `TargetProcessRunNodeId` from launch variables as the parent node id; do not fall back to `ProjectNodeId` or project root when one of those process-run nodes is present in project structure. For UI targets, inspect each screenshot, reject blank/error/wrong-route images, and create image asset nodes under Screenshots for accepted screenshots. For no-UI targets, record a no-UI screenshot receipt under Screenshots or as managed evidence so the parent release step can see that screenshot capture was intentionally not applicable. Do not mutate product files.

If valid current-run screenshots exist but the process-run node or Screenshots parent cannot be read or written with the allowed tools, do not discard the screenshot evidence or block only on the missing writeback target. Write a managed storage receipt that lists each accepted screenshot path, route, viewport, inspection result, attempted parent target, and the exact read/write limitation. Block only when screenshots are missing, blank, wrong-route, unreadable, or when project-structure writeback is available but fails with an unexplained or partial mutation.

## Contract
- Inputs: Screenshot files, browser evidence, and screenshot target manifest.
- Outputs: Screenshots parent node under process run node and image asset storage receipts for accepted screenshots.
- Evidence: Screenshots parent node id and image asset ids when available, or managed storage receipt with explicit writeback limitation, plus inspection results, rejected images, and no-UI receipt when applicable.
- Operation target scope: `ExternalActionControlled`
