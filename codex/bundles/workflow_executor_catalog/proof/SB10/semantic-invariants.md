# SB10 Semantic Invariants

## Invariant SB10-END-TO-END-CATALOG

- Invariant ID: `SB10-END-TO-END-CATALOG`
- Source raw note: RN01-RN05 require the executor catalog work to be safe, useful, authorable, and honest about unsupported runtime surfaces.
- Expected behavior: validation rejects unsafe or unavailable workflows; artifact content is retrievable; file/folder, JSON, Markdown, delay, approval, HTTP, and source ingestion scenarios execute through typed executor contracts; UI exposes honest catalog metadata.
- Disallowed shallow implementation: isolated unit tests without scenario coverage, UI templates for non-runnable behavior, or claims of durable runtime support not implemented in this bundle.
- Failing-first test: N/A - process/non-production exemption because SB10 is final verification and review rather than a production behavior change.
- Passing test: `WorkflowExecutorScenarioMatrixCoversRealWorldExamples` in `bundle://proof/SB10/transcripts/dotnet-test-scenario-harness-workflow-executor-catalog.txt`.
- Changed source files: see `bundle://proof/SB10/transcripts/changed-file-hashes.txt` for source, template, test, and bundle document hashes.
- Production assertions: `bundle://proof/SB10/transcripts/source-assertions-artifact-content.txt`; `bundle://proof/SB10/transcripts/source-assertions-workspace-file-ops.txt`; `bundle://proof/SB10/transcripts/source-assertions-validator-catalog-policy.txt`; `bundle://proof/SB10/transcripts/source-assertions-template-ui.txt`.
- Red-team negative case: validator/unit proof rejects unknown/planned/schema-invalid executor definitions and private-network HTTP targets.
- Downstream dependency check: all SB01-SB09 gates are marked passed in `bundle://reviews/01-execution-report.md`.
