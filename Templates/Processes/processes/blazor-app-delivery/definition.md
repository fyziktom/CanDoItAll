# Blazor app delivery
Simplified multi-team delivery for a new or rebuilt Blazor SSR, WASM, or WASM PWA application from project-structure requirements.
This process is generic for Blazor SSR, WASM, and WASM PWA work. It keeps process runtime generic by placing Blazor-specific delivery, validation, screenshot, browser console, cleanup, summary, and project-structure writeback requirements in process-template data.
## Required Proof
- dotnet restore/build/test evidence as applicable
- one app startup receipt
- Playwright screenshot files for visible UI surfaces
- browser_snapshot or browser_evaluate state output
- browser_console_messages output with no active JavaScript/runtime errors
- URL or entrypoint evidence
- cleanup receipt
- run evidence index
- project-structure result writeback
- unresolved repair escalation records for no-go or replan decisions

