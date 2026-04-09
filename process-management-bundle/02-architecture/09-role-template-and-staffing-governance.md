# Role-template and staffing governance

CRM-HR remains the canonical home for reusable human and AI role templates.

## Lifecycle

Templates should support at least:

- `Draft`
- `Active`
- `Retired`

Historical process definitions and runs must keep the exact template snapshot they used even after the template changes or retires.

## Staffing chain

The intended chain remains:

1. manager defines or updates a reusable role / agent template
2. process designer references that template
3. HR / workforce / recruiting / AI sourcing fulfills the gap
4. runtime resolves an assignee, eligible pool, fallback, or supervisory coverage
5. publish and run snapshots preserve history

## New guardrail from this pass

Future AgentFramework runtime template fields must remain derivative only.  
They may help instantiate a runtime executor, but they do **not** become the business staffing template system.

## Supervisory expectations

Templates can also carry:

- supervisory requirements
- fallback expectations
- capacity sensitivity
- risk or approval posture hints
- runtime bridge hints for future AI execution

Those hints should still be governed through CRM-HR-owned templates and snapshots.
