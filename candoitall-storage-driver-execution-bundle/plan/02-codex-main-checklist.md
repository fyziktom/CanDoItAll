
# Codex Main Checklist

This checklist is deliberately strict. Codex should work through it in order and update `reviews/01-execution-report.md` continuously.

## 1. Read-before-coding checklist

- Read `README.md`, `inputs/00-original-request.md`, `inputs/02-structured-input.md`, and `inventories/04-storage-driver-touchpoints.xlsx`.
- Read `traceability/01-requirement-traceability.md` and `traceability/03-touchpoint-coverage-from-xlsx.md`.
- Read the current phase README and every nested workstream note under that phase.
- Confirm prerequisites from the current phase README are truly satisfied before starting.

## 2. Implementation discipline checklist

- Keep code comments in English.
- Do not bypass the compatibility seam by inventing one-off provider-specific branches in modules.
- Do not store provider secrets in plain config or logs.
- Do not update only SQLite or only PostgreSQL migrations.
- Do not claim FTP proof unless a real protocol-backed validation path ran.
- Do not claim UI completion from reasoning alone.

## 3. Testing checklist

- Run the required build command.
- Run targeted unit tests for storage/routing/capability changes.
- Run targeted integration tests for access routes/provider behavior.
- Run targeted Playwright tests for changed UI surfaces.
- Use `mtp-hot-reload` only as an optional debugging accelerator; finish with normal clean test runs.

## 4. Manual Playwright MCP checklist

- Run a headed browser pass at `1900x1200`.
- Run a narrower-width pass around `1366x900` for any layout-affected surface.
- Capture screenshots for every changed route listed in `inventories/03-ui-proof-surfaces.md`.
- For dialogs/dropdowns/overlays, capture open-state screenshots.
- Review screenshots for:
  - clipped or truncated text
  - overflow outside panels/dialogs
  - overlapping buttons/inputs
  - hidden or unreachable wizard navigation
  - broken preview panes/media areas
  - unsupported actions that should be disabled but remain clickable
  - z-index/layering problems

## 5. Inventory closure checklist

- Re-open the workbook and verify every in-scope touchpoint row has:
  - owning phase/workstream
  - code change or explicit defer/block note
  - proof path
  - matching execution-report evidence
- Update `reviews/02-qa-coverage-audit.md` with any missing or blocked item.
- Do not close the bundle if any in-scope row lacks an owner or proof route.

## 6. Completion honesty checklist

- If a required proof path is blocked, mark the workstream/phase `Blocked` and explain why.
- Do not convert missing screenshot review into a residual-risk paragraph.
- Do not say a provider is supported if only compile-time scaffolding exists.
- Do not skip `reviews/01-execution-report.md`; it is a required artifact, not optional notes.
