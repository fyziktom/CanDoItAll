# SB16 Semantic Invariants

- Invariant ID: SB16-INV-001
- Source raw note: F07 template-pack metadata closure, RQ09 PostgreSQL-only generic core, and RQ10 final red-team closure.
- Expected behavior: Final closure proves the runtime, templates, UI preflight surface, API/manual validation guardrails, workflow/subprocess mapping, and persistence assumptions are mutually consistent after SB01-SB15.
- Disallowed shallow implementation: docs-only, prompt-only, fixture-only, checklist-only, or source-text-only changes that do not exercise production code paths.
- Failing-first test: `bundle://proof/SB16/transcripts/failing-first.txt` records the editor operation/scope regression, stale process lock, manifest metadata drift, and PostgreSQL-only audit pressure.
- Passing test: `bundle://proof/SB16/transcripts/passing.txt` records the final solution build, focused unit/component/integration tests, and strict template audit.
- Changed source files: `repo://Templates/Processes/manifest.json`, `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepEditorForm.razor`, `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunStepsDialog.razor`, and component tests listed in `bundle://proof/SB16/transcripts/changed-file-hashes.txt`.
- Production assertions: final closure preserves typed template contracts, UI diagnostics, operation/scope reconciliation, finalizer-grade manual/API artifact validation, explicit workflow/subprocess mapping, and PostgreSQL-only runtime assumptions.
- Red-team negative case: weak artifact completion, ambiguous mappings, product mutation outside authorized steps, software-only metadata, and SQLite runtime paths are rejected or absent.
- Downstream dependency check: downstream Tetris/browser execution can rely on SB15 selectors/checklist and SB16 final build/test/audit closure without reinterpreting prose.
- Required proof: adversarial proof, passing build/tests/audits, source assertions, anti-stub audit, changed-file hashes, and completed bundle validator.

| Red-team pressure | Required invariant | Shallow pass rejected by |
| --- | --- | --- |
| Architect or QA step tries to mutate product source outside implementation/repair. | Template contracts and UI diagnostics expose read-only vs mutation scopes clearly. | `Blazor_process_templates_SB04_INV_001`, SB15 component proof, and strict template audit. |
| Manual/API transition tries to complete with weak or stale required artifact evidence. | Manual completion shares finalizer-grade artifact validation and rejects stale lineage. | `TransitionStepAsync_SB10_INV_001_rejects_stale_execution_lineage_required_artifact_on_manual_completion`. |
| Workflow/subprocess output mapping is missing or ambiguous. | Required artifacts carry explicit workflow output or child expectation mappings. | `Analyze_SB09_INV_001`, `WorkflowArtifactProjectionMapping_SB09_INV_001`, and `SubprocessArtifactProjectionMapping_SB09_INV_001`. |
| UI editor unchecks an operation implied by the old target scope. | The editor reconciles target scope before normalization can re-add the removed operation. | `ProcessStepEditorFormTests.Render_SB08_INV_001_operation_contract_controls_update_model`. |
| Template pack metadata still describes a software-only pack. | Mixed software and non-software template metadata is explicit. | Source assertion for `Templates/Processes/manifest.json` and raw-note F07 closure. |
| SQLite or non-PostgreSQL runtime paths are introduced. | No `UseSqlite` or SQLite migrations exist in active source/test/template paths; only retired quarantine strings remain. | PostgreSQL-only audit in `proof/SB16/transcripts/source-assertions.txt`. |
