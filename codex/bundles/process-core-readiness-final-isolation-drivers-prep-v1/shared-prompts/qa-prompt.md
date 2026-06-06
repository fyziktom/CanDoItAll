# QA Prompt

Review whether the implementation preserves process dispatch behavior while improving architecture.

Reject the implementation if:
- Process Core appears.
- Production process-driver APIs appear.
- UI/mobile proof appears without UI changes.
- Route order changes.
- Existing subprocess/finalizer/materialization behavior is simplified.
- Execution report collapses all work into one row.
- Any subbundle lacks proof.
