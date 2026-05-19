# Requirement Traceability

| Requirement | Primary Subbundle | Secondary Subbundles | Proof |
| --- | --- | --- | --- |
| FR-FU-001 | 00 | 04, 06, 10 | Original invariant checklist and tests. |
| FR-FU-002 | 10 | 03, 07 | API route contract tests and docs diff. |
| FR-FU-003 | 00 | 08, 09 | Database/profile smoke proof. |
| FR-FU-010 | 02 | 08 | LB4U manifest and workbook stage sheet. |
| FR-FU-011 | 02 | 04, 08 | Read-only file ingestion proof. |
| FR-FU-012 | 02 | 08, 09 | Secret exclusion absence checks. |
| FR-FU-013 | 02 | 04 | Chunk extraction tests for docx/pdf/pptx/xlsx. |
| FR-FU-014 | 08 | 04, 06 | Stage-by-stage operation ids and snapshots. |
| FR-FU-020 | 04 | 05, 08 | Candidate quality tests and review records. |
| FR-FU-021 | 04 | 10 | Source span/provenance assertions. |
| FR-FU-022 | 04 | 06 | Review decision tests. |
| FR-FU-023 | 05 | 06 | Rejection/revision records. |
| FR-FU-024 | 05 | 08 | Coverage/gap scan reports. |
| FR-FU-030 | 06 | 08, 09 | Probe session transcripts and context sources. |
| FR-FU-031 | 06 | 08 | Probe rubric assertions. |
| FR-FU-032 | 06 | 08 | Before/after study-loop evidence. |
| FR-FU-033 | 06 | 10 | Accepted/rejected recommendation audit. |
| FR-FU-040 | 08 | 03 | OpenAI model profile evidence. |
| FR-FU-041 | 09 | 03 | Ollama model profile evidence. |
| FR-FU-042 | 03 | 08, 09 | Token/truncation metadata tests. |
| FR-FU-043 | 03 | 04, 09 | Provider failure tests. |
| FR-FU-044 | 09 | 03 | Ollama output token proof. |
| FR-FU-050 | 07 | 01 | File split diff and tests. |
| FR-FU-051 | 07 | 02, 03, 04 | Helper extraction tests. |
| FR-FU-052 | 07 | 10 | Component/UI tests and browser proof. |
| FR-FU-053 | 01 | 07 | Refactor review checklist. |
| FR-FU-060 | 10 | 03, 04, 06 | API skill/doc updates. |
| FR-FU-061 | 10 | all | Workbook final status. |
| FR-FU-062 | 10 | all | Execution report and bundle validation. |

## Validation Commands

- `dotnet build`
- `dotnet test`
- Targeted cognitive-memory unit, integration, component, and Playwright tests as identified by subbundle 00.
- `python codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py codex\bundles\cognitive-memory-followup-lb4u-validation-refactor --profile initiative --stage prepared`
- `python codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py codex\bundles\cognitive-memory-followup-lb4u-validation-refactor --profile initiative --stage completed`
