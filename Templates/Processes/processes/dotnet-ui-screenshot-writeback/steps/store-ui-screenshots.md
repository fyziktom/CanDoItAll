# Store screenshots under process run node

Create or reuse a Screenshots parent node under the current process run node. Prefer `ProcessRunNodeId`, `ParentProcessRunNodeId`, or `TargetProcessRunNodeId` from launch variables as the parent node id; do not fall back to `ProjectNodeId` or project root when one of those process-run nodes is present in project structure. For UI targets, inspect each screenshot, reject blank/error/wrong-route images, and create image asset nodes under Screenshots for accepted screenshots. For no-UI targets, record a no-UI screenshot receipt under Screenshots or as managed evidence so the parent release step can see that screenshot capture was intentionally not applicable. Do not mutate product files.

## Contract
- Inputs: Screenshot files, browser evidence, and screenshot target manifest.
- Outputs: Screenshots parent node under process run node and image asset storage receipts for accepted screenshots.
- Evidence: Screenshots parent node id, image asset ids, inspection results, rejected images, and no-UI receipt when applicable.
- Operation target scope: `ExternalActionControlled`
