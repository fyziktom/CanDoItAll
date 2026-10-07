# Agent Editor Completion A2

**One continuation: repair capability-verification reconciliation, complete the six remaining
technical-agent editor sections, then validate the entire ten-section editor and its consumers.**
Do not start provider administration, capability-definition authoring, Workflow authoring,
Workbench or Processes in this run.

Entry point: [prompt.md](prompt.md). The [execution stages](EXECUTION_STAGES.md) organize a larger
work session without turning each section into a separately negotiated task. The original
[shared v3](shared/README.md) is included byte-for-byte; its old audit is historical, not the
current module census. [A1 review](A1_REVIEW.md) and [roadmap](ROADMAP.md) describe the current state.

The checked implementation is `ed64d4edf868cb26c749c31a94ff918683e0f4a0`. Its immediate child,
`0aad5360b4ac037ed4471ed44fc6083b7f09fb85`, adds only the preceding A1 handoff as history.
Neither is an execution pin. Preserve historical bundles throughout this task, including
tracked and intentionally untracked inputs; the operator removes them before a future merge.
Do not delete them to obtain a cosmetically clean checkout or modify their sealed evidence.

This package contains instructions, evidence templates and optional integrity utilities, not
an application patch. The reviewer read repository source and implementation reports; no C#,
PostgreSQL, browser or watch execution was performed here. The [source register](SOURCES.md)
identifies reviewed paths and coverage, and the [evidence review](TEST_EVIDENCE_REVIEW.md)
distinguishes recorded implementer results from independent reproduction.

Supported UI validation is **large desktop only**, normally **1920 × 1080**.
There is no new paid/live-model authorization. Use existing deterministic external-provider
fixtures with actual application owners, tools, approvals and file content.

```text
Read and execute CanDoItAll_Agent_Editor_Completion_A2/prompt.md.
Use the whole extracted package and current repository instructions.
```

Run `python -B tools/validate_package.py` to check package integrity. This is not a product gate.
Copy [templates/evidence.json](templates/evidence.json) to an owned run directory before recording
results; the supplied template intentionally contains only NOT_RUN outcomes.
