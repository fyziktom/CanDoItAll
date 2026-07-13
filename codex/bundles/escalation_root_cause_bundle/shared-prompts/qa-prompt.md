# QA Prompt

```text
Validate the completed subbundle against codex/bundles/escalation_root_cause_bundle. Confirm the implementation proves the actual behavior, not only structure.

Required checks:
- Run the tests named in the subbundle README.
- Confirm failing-first or negative tests exist for the shallow-pass traps.
- Confirm source proof cites changed production files.
- Confirm changed-file hashes and command transcripts are recorded in proof/SBxx/manifest.md.
- Confirm no hard gate remains prose-only when typed metadata is required.
- Confirm the 5032 incident behavior or an equivalent local reproduction is covered when relevant.
```
