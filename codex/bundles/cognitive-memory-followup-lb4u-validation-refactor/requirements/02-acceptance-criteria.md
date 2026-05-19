# Acceptance Criteria

## Prepared Bundle Gate

- The bundle validates with `validate_bundle.py --profile initiative --stage prepared`.
- Every subbundle has absolute source references, entry/closure criteria, proof requirements, and dependency impact.
- The workbook exists and includes requirement, stage, validation, risk, and evidence worksheets.

## Implementation Gate

- Cognitive memory builds and all existing cognitive-memory tests pass.
- New tests cover LB4U staged ingestion, secret exclusion, chunk quality, consolidation candidates, probing feedback, and model profile/token behavior.
- Refactors reduce the largest files or isolate helpers without changing behavior unintentionally.
- API endpoint changes are covered by integration or smoke tests.

## OpenAI Validation Gate

- OpenAI `gpt-5-mini` is used for the main staged LB4U validation.
- At least three staged cycles run with snapshots, consolidation, probing, review decisions, and evidence captured.
- Probe answers cite source-backed LB4U facts and distinguish project-specific from reusable knowledge.
- The system shows measurable improvement after study/deeper consolidation prompts.

## Ollama Validation Gate

- Ollama `gptoss20b64k` is selected explicitly.
- Output token budget is configured or observed explicitly.
- Truncation is detected and reported if it happens.
- Local validation can answer core LB4U probes with traceable context or fails with actionable provider/model evidence.

## Closure Gate

- Completed-stage bundle validation passes.
- `reviews/01-execution-report.md` contains subbundle gate rows, browser/API validation analytics where applicable, raw note closure, and evidence links.
- `checklists/cognitive-memory-followup-control.xlsx` is updated with final statuses and proof references.
- API docs, skill docs, and developer usage notes match the final implementation.
