# QA / Red-team Prompt

Review the implemented route-handler pipeline.

Look for:
- route order drift
- missing route stages
- duplicated route stages
- finalizer handoff before competing/run-closed guards
- missing claim-held checks
- hidden transition/finalizer/execution side effects in pure helper classes
- Process Core or driver API leakage
- UI/mobile/browser proof drift
- collapsed execution report rows
- wrapper-only refactor that leaves route bodies in RouteExecution.cs

Reject the bundle if any issue is found.
