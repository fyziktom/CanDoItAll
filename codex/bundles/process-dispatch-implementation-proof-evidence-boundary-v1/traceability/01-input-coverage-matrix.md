# Input Coverage Matrix

| Raw input | Normalized requirements | Covered by | Proof |
| --- | --- | --- | --- |
| Do not rush Process Core | RQ-002 | All gates | No-core scans |
| Preserve original functionality | RQ-004 through RQ-012 | SB02-SB25 | Focused parity tests |
| Continue smaller isolation steps | RQ-004 through RQ-012 | SB01-SB28 | Helper extraction + wrappers |
| Prepare for future drivers | RQ-013 | SB03, SB26, SB28 | Documentation-only map + no-driver scan |
| Codex should work longer with refactor gates | RQ-014 | SB01-SB28 | 28 subbundles and 7 critical gates |
| No small/medium/mobile proof | RQ-015 | All | Proof path scans |
