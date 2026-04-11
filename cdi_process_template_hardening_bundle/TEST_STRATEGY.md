# Test strategy

## Structural and audit layer
- `tools/validate_process_template_pack.py`
- `tools/audit_bundle_application.py`
- `tools/scan_process_module_long_files.py`

## Unit layer
- Existing pack loader, catalog, projection, exporter, and MCP tool tests
- New materialization and sidecar-parity tests from `repo-overlay/tests/CanDoItAll.Mcp.Processes.Tests/`

## Integration layer
- Existing process runtime and SQLite coordination tests
- New import-metadata integration test from `repo-overlay/tests/CanDoItAll.Tests.Integration/`

## Component and browser layer
- Preserve existing component and Playwright validation for process-canvas authoring and runtime flows

## Acceptance layer
1. The repository contains `output/process-template-pack/`.
2. The validator reports zero errors.
3. The audit script reports the old missing-pack problem is gone.
4. Review-gate memos record any remaining architectural debt explicitly.
