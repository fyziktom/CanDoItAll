# Checklists

## Design checklist
- [ ] Machine-wide backend discovery does not break workspace-local reuse.
- [ ] Manager actions can target remote backends.
- [ ] Auth tokens are reused only for local loopback manager/API calls.
- [ ] Stale catalog entries are removed.
- [ ] Raw log persistence stays unchanged.
- [ ] Agent-facing log responses are reduced by default.

## Implementation checklist
- [ ] Add catalog models and storage.
- [ ] Wire catalog registration into backend startup and shutdown.
- [ ] Extend manager API response to aggregate all backends.
- [ ] Extend manager UI to show per-backend cards and actions.
- [ ] Add manager action endpoints and proxy logic.
- [ ] Add watch rebuild trigger support.
- [ ] Add log reducer and response metadata.
- [ ] Add unit tests for catalog and log filtering behavior.

## Validation checklist
- [ ] Manager UI shows `CanDoItAll` backend.
- [ ] Manager UI shows `pveinvoicing` backend.
- [ ] Manager UI exposes stop / force stop / rebuild controls.
- [ ] `CanDoItAll` survives MCP stdio re-instancing.
- [ ] `pveinvoicing` survives MCP stdio re-instancing.
- [ ] Small live change is applied on `CanDoItAll` without losing the running backend.
- [ ] Small live change and revert are applied on `pveinvoicing` without losing the running backend.
- [ ] App status confirms the same backend-owned session remains live after re-instancing.
- [ ] Reduced logs materially shrink output versus raw logs.
