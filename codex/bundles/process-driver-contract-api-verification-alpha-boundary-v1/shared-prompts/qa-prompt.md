# QA Agent Prompt

Validate that:
- Core remains deterministic and dependency-clean.
- Driver abstractions are contract-only.
- Verification-only and manager-readonly cannot mutate state.
- Audit facts and redaction are test-covered.
- `.NET/Rust` alpha rehearsal is test-only and cannot run commands.
- There is no UI/media drift.
- Every critical gate has command transcript proof.
