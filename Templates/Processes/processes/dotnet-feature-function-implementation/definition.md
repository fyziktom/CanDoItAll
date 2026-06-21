# .NET feature/function implementation subprocess

**Key:** `dotnet-feature-function-implementation`
**Criticality:** High
**Autonomy level:** Guarded

Atomic child process for implementing one bounded .NET function, feature, or UI behavior with explicit design, test contract, code change, targeted validation, repair recheck, and handoff evidence. When launched from a broader app-delivery scope, intake derives the first reviewable MVP behavior from upstream architecture, product-root, setup, and validation facts instead of blocking solely because the parent request is large.

## Steps
- Capture feature or function boundary.
- Decide implementation approach.
- Define focused validation contract.
- Implement the feature or function.
- Run focused validation and choose `feature-accepted` or `feature-repair-required`.
- Repair focused validation findings when required.
- Re-run focused validation after repair.
- Hand off accepted implementation evidence or escalate unresolved repaired proof.
