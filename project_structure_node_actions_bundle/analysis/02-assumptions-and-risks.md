# Assumptions And Risks

## Assumptions

- Runtime-capable detection should use the resolved launch plan, not a hard-coded object type list, so future runtime-capable nodes inherit the actions.
- The existing "Edit" action remains the first quick-action modal option. Runtime nodes add the run options as the requested second/extra choices without removing edit.
- A file node that is both locally backed and routable may show both File Explorer and route/new-tab actions when both are true.
- IPFS nodes need only open the existing route/new-tab URL; this bundle does not add IPFS pinning, gateway setup, or content retrieval.
- Agents need descriptive capability metadata and not direct host-launch tools.

## Critical Path Risks

- If runtime capability detection is duplicated in UI, MCP, and internal tools, later behavior can drift. The implementation should centralize capability resolution in Workbench.
- If the double-click attachment preview path keeps bypassing the modal, the file/IPFS requirements can be partially invisible even when right-click works.
- If MCP summary payloads grow without default compact behavior, internal agent context may become noisy.
- Host launch validation may be limited by UAC and desktop integration in automated tests.

## Validation Risks

- Browser proof can prove that actions are visible and layered correctly, but it cannot prove UAC elevation completed.
- File Explorer launch is host-visible, not DOM-visible; automated proof may need to stop at guarded unit tests plus an explicit host-proof note.
- IPFS route proof depends on having an IPFS-backed or simulated node with a deterministic route.
- The whole solution may emit unrelated package vulnerability warnings from existing dependencies; validation should distinguish compile/test failures from pre-existing warnings.

## Reopen Triggers

- Reopen subbundle 01 if any runtime-capable node shows only one run option or if right-click lacks either normal or administrator run.
- Reopen subbundle 02 if a local file node lacks File Explorer or an IPFS node lacks new-tab open.
- Reopen subbundle 03 if `project_structure_read` or internal agent compact nodes still require agents to parse raw metadata to know whether runtime/file/IPFS actions exist.
- Reopen the relevant UI subbundle if Playwright screenshots show clipped quick-action dialogs, clipped context menus, unreadable labels, or broken z-order.
- Reopen the relevant host-action subbundle if unit tests bypass the existing guarded launcher/opener.

## Scope Exceptions

- No direct MCP or internal-agent tool will launch local PowerShell, UAC, File Explorer, or a browser tab. Those are interactive host actions exposed as capabilities for the UI.
