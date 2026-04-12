# Activate emergency bridge and classify severity

**Process:** `hotfix-rollout` / Emergency hotfix rollout with shard-risk governance  
**Step key:** `activate-emergency-bridge`  
**Step kind:** Start  
**Target lead hours:** 1

## Summary
Command posture

## Notes
Establish the incident bridge, initial severity, and named command boundaries before packaging a hotfix.

## Contracts
- Input contract: Active production signal, customer impact, and first-response telemetry.
- Output contract: Explicit command bridge with severity posture, responder roster, and immediate constraints.
- Evidence contract: Bridge activation log, severity declaration, and named responders.

## Governance
- Decision rights: Incident commander owns classification and decision pacing during the emergency window.
- Exception policy: Do not begin emergency packaging while command ownership or severity framing is still implicit.
- Requires approval: False
- Requires decision record: False

## Dependencies
- No explicit predecessor.

## Role assignments
- `incident-commander` / Incident commander => Responsible; required=True; fallback-order=0; rebind=Command ownership may rebind across rota changes, but the command contract remains explicit.
- `customer-liaison` / Customer liaison => Reviewer; required=True; fallback-order=0; rebind=Customer communication owner tracks external impact from the start.

## Artifact expectations
- `emergency-bridge-log` -> `emergency-bridge-log` / Emergency bridge activation log | kind=Transcript | trust=ReviewRequired | sensitivity=Internal | validation=Must record activation time, severity, named responders, and first declared constraints.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.
