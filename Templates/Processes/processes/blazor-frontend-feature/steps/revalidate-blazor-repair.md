# Revalidate Blazor repair

Run validation against the delivered Blazor SSR, WASM, or WASM PWA app. Before running restore/build, stop any prior `workspace_dotnet_run` process for the same product root by calling `workspace_dotnet_stop` with the recorded startup.json receipt when available; do not run build while the product executable is still locked by a previous validation runtime. Run dotnet restore, dotnet build, and dotnet test when a test project exists or was created. Start the app once from the approved product root and record the startup receipt. Use Playwright browser tools for every visible UI surface named by project structure or implementation evidence. Capture screenshot image paths, browser_snapshot or browser_evaluate state output, browser_console_messages output, actual URL or entrypoint, visible behavior assertions, and cleanup receipt. After browser proof, call workspace_dotnet_stop with the startup receipt path and record cleanup.json evidence before returning any branch outcome, including repair-escalation or Blocked. For frontend/browser validation, include the matching API/backend proof as well. Missing, blank, detached, stale, or chat-only screenshots are not acceptable.

## Evidence

Record commands, files, URLs, screenshots, console messages, errors, assumptions, and project-structure writeback references as applicable.

