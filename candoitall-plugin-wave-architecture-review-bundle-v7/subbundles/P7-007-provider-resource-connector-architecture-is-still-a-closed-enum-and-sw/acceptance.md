# Acceptance contract

This item may be closed only when all of the following are true:

1. The forbidden patterns for this item are gone from the codebase.
2. Required tests exist and pass in a real .NET environment.
3. The hard-gate script no longer fails because of this item.
4. The final QA review accepts the implementation.

## Required code proof

- changed files must be listed
- the responsible test names must be listed
- code search output proving removal of the forbidden pattern must be attached

## Required architecture proof

- the target architecture document updated by the implementation must be named
- if ADRs changed, they must match the implementation and not contradict it
