# SB16 Architecture Review

## Boundary Decision

- Generic runtime owns typed diagnostic recurrence and subprocess lifecycle only.
- Process templates own role sequencing, artifacts, branch decisions, and retry bounds.
- Modules.Processes .NET drivers own subprocess mapping and browser evidence policy.
- Modules.Workbench .NET launch policy owns .NET validation receipts and UI scaffold checks.

## Separation Review

- Diagnosis, mutation, and acceptance are different child steps and roles.
- The parent cannot mutate product files, launch runtime, or capture browser proof.
- The pure `DotNetDeliveryQualityLaunchPolicyBuilder` removes delivery-quality serialization from the oversized contributor.
- The contributor is no longer partial and is 166 lines smaller than HEAD.
- No service locator, new project reference, cycle, or fake forwarding abstraction was introduced.

## Risks

- A product can still reach the bounded no-go branch after two evidence-guided attempts; that is intentional escalation, not a retry loop.
- UI content checks are .NET/Blazor policy and must remain outside generic process runtime.
- E2E production evidence remains the SB17 progression gate.

## Result

Passed. SB17 may exercise the seeded process on production-like project structures.
