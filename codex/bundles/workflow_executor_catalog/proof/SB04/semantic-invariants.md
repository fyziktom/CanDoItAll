# SB04 Semantic Invariants

## Invariant SB04-DETERMINISTIC-JSON

- Source raw note: RN02 and R5 require JSON data shaping without arbitrary code execution.
- Expected behavior: JSON transformations are driven by typed operations and JSON paths; invalid paths fail clearly.
- Disallowed shallow implementation: routing users to LLM prompts or dynamic script execution for deterministic JSON tasks.
- Positive proof: `JsonTransformExecutorShapesArraysAndRejectsInvalidPaths` in `bundle://proof/SB10/transcripts/dotnet-test-unit-workflow-executor-catalog.txt`
- Source proof: `bundle://proof/SB10/transcripts/source-assertions-executor-implementations.txt`
