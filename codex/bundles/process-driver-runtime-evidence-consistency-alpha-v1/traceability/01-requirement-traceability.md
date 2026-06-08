# Input Coverage Matrix

| Raw input | Normalized requirements | Owning subbundles | Proof method |
| --- | --- | --- | --- |
| Review latest Codex work after crash and inspect real code | REQ-001 | SB001-SB003 | Branch source/proof reconciliation, source scans, build/test baseline |
| Plan more complex areas toward stable Process Core and domain drivers | REQ-002..REQ-014 | SB004-SB054 | Multi-phase implementation with critical gates every three subbundles |
| Preserve quality; speed must not reduce correctness | All | All gates | Semantic Adequacy Gate, failing-first/negative tests, red-team, validators |
| Follow development bundle skill | All | SB001/SB003/SB049-SB054 | Required bundle structure, manifests, traceability, validators |
