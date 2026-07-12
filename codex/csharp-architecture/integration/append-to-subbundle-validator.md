# Appendix for `candoitall-subbundle-validator`

Add these checks for architecture-relevant C# subbundles.

## Entry gate

The subbundle may start only if:

- prerequisite architecture checkpoints passed
- current source references are still valid
- dependency direction plan is present
- partial-class policy is present
- testability contract is present
- pattern decision is present or explicitly not needed
- target owner type/project is named

## Closure gate

The subbundle may close only if:

- build and targeted tests passed or a blocker is honestly recorded
- extracted behavior has direct unit tests
- source assertion proves behavior moved or new behavior lives in the new owner
- no new partial class was added without policy-compliant justification
- project reference changes match the target dependency direction
- old class did not gain a new unrelated responsibility
- architecture gate result is `Pass` or `Pass with follow-up`
- follow-up subbundles exist for temporary bridges
