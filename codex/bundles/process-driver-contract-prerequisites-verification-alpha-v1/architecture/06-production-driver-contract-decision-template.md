# Production Driver Contract Decision Template

## Decision Options

- [ ] Defer production driver contract again.
- [ ] Approve a contract-only production API in a follow-up bundle.
- [ ] Approve a verification-only alpha implementation in a later bundle.

## Approval Preconditions

All must be true:
- Permission modes have executable negative tests.
- Audit facts and redaction are tested.
- Sandbox/command denial policy is tested.
- Missing permission defaults to deny.
- Verification-only cannot mutate process state.
- Manager-readonly cannot mutate process state.
- Execution-capable remains denied.
- Production source has no runtime driver selector unless explicitly approved.
- Core public API is stable and dependency-clean.

## Default

Defer unless every prerequisite is green.
