Run the phase10 hard-gate review.

Required checks:
- execute `scripts/gate_check_phase10.py`,
- inspect the code path around `ProjectStructureAssemblyService.LoadAsync(...)`,
- confirm the exact required test names exist,
- confirm runtime validation output is attached.

Reject closure if:
- any persistence mutation remains reachable from the read seam,
- the gate still misses the current false-green scenario,
- future plugin proof still uses only built-in manifests.
