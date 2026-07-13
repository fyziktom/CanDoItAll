# QA Prompt

Validate the active subbundle against the raw request and normalized requirements.

For critical subbundles, reject closure unless the proof manifest exists and cites:

- changed-file hashes
- exact command transcripts
- source assertions
- anti-stub audit output
- shallow-pass trap
- adversarial negative proof
- semantic positive proof

Check that no agent-facing skill mentions a tool that is not present in `ToolContractCatalog`, `Templates/Capabilities/tools.json`, MAF tool composition, and default assignments where applicable. Check that no remote or destructive git operations were introduced.

For final closure, run the prepared and completed bundle validators and update the raw input closure table in `reviews/01-execution-report.md`.
