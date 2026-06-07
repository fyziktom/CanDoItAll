# Input Coverage Matrix

Raw note | Requirement | Owning subbundles | Proof
---|---|---|---
Do not rush Process Core | No Core project/API in this bundle | SB001-SB036 | Source scans, final decision template
Preserve functionality | All behavior-preserving refactors require parity proof | All gates | Build, unit, focused integration
Use fewer broader subbundles | 36 meaningful subbundles across 12 phases | SB001-SB036 | Execution report rows
Move faster toward Core and drivers | Cover route, finalizer, hydration, pre-execution, subprocess, execution, artifact, wrapper, Core rehearsal, driver readiness | All phases | Gate A-L proof
No production drivers | Driver work documentation/test-only | SB031-SB033 | No driver API scans
No UI/mobile proof | Runtime/service-only changes | All | No UI/media diff scan
