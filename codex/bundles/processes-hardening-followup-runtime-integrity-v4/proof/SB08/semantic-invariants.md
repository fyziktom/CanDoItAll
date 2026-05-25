# SB08 Semantic Invariants

## SB08-INV-001

Expected behavior: step operation contracts are durable typed data, not prompt text. Allowed operations and target scope persist on process step definitions, flow through editor save/load/import/export/template/clone paths, and drive runtime metadata before the legacy text parser is consulted. Text-only operation contracts remain supported as backfill but are linted as inferred and lower-confidence.

Disallowed shallow implementation:

- prompt-only change
- source-assertion-only proof
- tests that manually seed final state instead of exercising producer/consumer lifecycle
- branch-specific hardcoding
- software-only behavior for generic process runtime

Required proof:

- failing-first/red-team proof
- passing proof
- source assertions
- anti-stub audit
- changed-file hashes
- production behavior artifact matrix when new runtime state is introduced

Closure proof:

- `BuildProcessInvocationMetadataJson_SB08_INV_001_uses_persisted_operation_contract_without_text_markers`
- `Analyze_SB08_INV_001_warns_when_operation_contract_is_text_inferred`
- `Analyze_SB08_INV_001_accepts_typed_operation_contract_without_text_markers`
- `Analyze_SB08_INV_001_rejects_partial_typed_operation_contract`
- `Save_export_import_and_publish_SB08_INV_001_preserve_step_operation_contract`
- `Render_SB08_INV_001_operation_contract_controls_update_model`
- `Process_step_operation_contract_editor_controls_work_in_browser`
