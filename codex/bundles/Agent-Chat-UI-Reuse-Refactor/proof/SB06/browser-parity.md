# Settings browser parity — SB06

Host: isolated fresh Development build on `http://127.0.0.1:5046` using per-project artifacts output. The proof host was stopped after inspection.

## Identity

- Opened the Agents catalog, selected `Blazor Application Developer`, and opened the editor by its existing double-click gesture.
- Verified the preserved editor title, technical-agent copy, avatar choose/clear state, Name, Role title, Tags, Summary, and Instructions labels and values.
- Verified the original Agent test IDs and the complete ten-tab order.

## Runtime

- Verified provider `OpenAI default`, model `gpt-5.6-luna`, custom-model override control, thinking effort `Medium`, status, category, FrameworkManaged history, and approval controls.
- Verified Agent-owned reasoning help, temperature-omission policy, and automatic-approval notice remained visible.
- No value was changed or saved.

## Agent-only tab

- Opened Capabilities and verified MCP, skill, and tool content plus Assign and Verify controls remained rendered.
- Current-navigation console result: zero errors and zero warnings.

Evidence:

- `proof/SB06/browser/SB06-identity.png`
- `proof/SB06/browser/SB06-identity-state.json`
- `proof/SB06/browser/SB06-runtime.png`
- `proof/SB06/browser/SB06-runtime-state.json`
- `proof/SB06/browser/SB06-agent-capabilities.png`
- `proof/SB06/browser/SB06-agent-capabilities-state.json`
- `proof/SB06/browser/SB06-console-errors.txt`

Decision: settings parity passes. Reusable presentation owns field composition; Agent product behavior, policy tabs, and persistence remain intact.
