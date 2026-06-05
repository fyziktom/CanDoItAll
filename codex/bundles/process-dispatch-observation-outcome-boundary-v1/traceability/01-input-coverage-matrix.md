# Input Coverage Matrix

| Raw note | Requirement IDs | Owning subbundles | Proof |
| --- | --- | --- | --- |
| Do not rush Process Core | RQ-002 | SB01, SB04, SB42, SB44, SB48 | no-core source scans |
| Preserve original functionality | RQ-001, RQ-004-RQ-008 | SB05-SB40 | focused tests + smoke matrix |
| Break down huge services gradually | RQ-004-RQ-010 | SB05-SB40 | helper files + line count |
| More phases / force refactor gates | RQ-013 | SB01-SB48 | gate table + manifests |
| Prepare future drivers without production APIs | RQ-003, RQ-012 | SB41, SB42, SB44, SB48 | documentation-only map + no-driver scan |
| Do not use small/medium/mobile proof | RQ-011 | all | proof-path scan |
