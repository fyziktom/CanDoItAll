# Revalidate Blazor repair

Run validation against the delivered Blazor SSR, WASM, or WASM PWA app. Run dotnet restore, dotnet build, and dotnet test when a test project exists or was created. Start the app once from the approved product root and record the startup receipt. Use Playwright browser tools for every visible UI surface named by project structure or implementation evidence. Capture screenshot image paths, browser_snapshot or browser_evaluate state output, browser_console_messages output, actual URL or entrypoint, visible behavior assertions, and cleanup receipt. For new app delivery, include the matching API/backend proof as well. Missing, blank, detached, stale, or chat-only screenshots are not acceptable. Do not call `project_structure_node_create` or `project_structure_asset_create` from this validation step; result writeback belongs to the result-recording steps. Do not use `workspace_pwsh_run_script` for cleanup or diagnostic helper scripts; record startup/cleanup receipts from `workspace_dotnet_run`, browser tools, and managed artifacts.

## Evidence

Record commands, files, URLs, screenshots, console messages, errors, and assumptions. Project-structure writeback belongs to the result-recording steps, not this validation step.

