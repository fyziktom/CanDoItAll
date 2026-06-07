# Input Coverage Matrix

| Raw note | Requirement | Owning subbundles | Proof |
| --- | --- | --- | --- |
| Do not rush Process Core unless clearly justified | REQ-001, REQ-002 | SB001-SB006, SB030 | Core guard scans, final red-team |
| Preserve existing functionality | REQ-005 | All implementation SBs | Build, unit, integration |
| Fewer broader meaningful subbundles | REQ-009 | SB001-SB030 | Execution report rows |
| Move closer to Process Core | REQ-002, REQ-003 | SB004-SB021 | Core seed + pure-rule proof |
| Prepare future drivers safely | REQ-007 | SB022-SB024, SB030 | No production driver API scans |
| No UI/mobile proof for runtime changes | REQ-008 | All | No UI/media diff scan |
