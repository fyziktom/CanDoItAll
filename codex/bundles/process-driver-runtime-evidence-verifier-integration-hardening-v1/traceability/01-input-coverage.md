# Input Coverage Matrix

| Raw Input | Normalized Requirement | Owning Subbundles | Proof |
| --- | --- | --- | --- |
| Verify after Codex crash from real code | REQ-001 | SB001-SB003 | source scans, actual source refs, build/tests |
| Preserve stable Core | REQ-002 | all gates | Core dependency/API guards |
| Move toward domain drivers safely | REQ-003, REQ-008, REQ-010 | SB016-SB018, SB025-SB027, SB031-SB033 | driver boundary tests |
| More complex bundle with fewer micro-steps | REQ-004 to REQ-013 | all phases | 15 coherent phases, 45 broader subbundles |
| Prepare ZIP | REQ-013 | SB045 | validators, proof index, final red-team |
