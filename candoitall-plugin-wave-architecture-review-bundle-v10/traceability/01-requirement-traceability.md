# Requirement traceability

| Requirement | Finding(s) | Subbundle(s) | Closure proof |
|---|---|---|---|
| R1 read seam zero-write | F1 | P10-001 | static gate + zero-write integration tests |
| R2 explicit repair boundary | F1 | P10-002 | repair tests + final report |
| R3 zero-write behavior proof | F1, F3 | P10-003 | required test names + runtime results |
| R4 gate must fail current false-green | F2 | P10-003 | `gate_check_phase10.py` output |
| R5 unknown-manifest editor proof | F4 | P10-004 | component/integration tests |
| R6 legacy fallback stays visible | Advisory | N/A (warning only) | gate warnings + report |
