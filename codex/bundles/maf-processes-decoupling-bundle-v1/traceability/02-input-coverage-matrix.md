# Input Coverage Matrix

| Raw input | Literal wording / signal | Normalized requirement | Owner | Exception |
| --- | --- | --- | --- | --- |
| User request | "rozplést ty závislosti" | RQ-001, RQ-002, RQ-003 | SB02-SB05 | None |
| User request | "MAF má závislost na process modulu se mi dlouhodobě nelíbí" | RQ-001, RQ-002 | SB05 | None |
| User request | "po menších krocích" | RQ-014 | All subbundles | None |
| User request | "prvně bundle s detailními subbundles" | RQ-013, RQ-014 | Bundle prep | None |
| User request | "dotkne se to i hodně testů" | RQ-006, RQ-007, RQ-010 | SB06 | None |
| User request | "xlsx s detailními checklisty" | RQ-013 | Bundle prep | None |
| User request | "nesmí se ztratit" | RQ-013, RQ-014 | All subbundles | None |
| User request | "nesmí věci zjednodušit nebo něco vynechat" | RQ-005, RQ-006, RQ-007 | SB04, SB06 | None |
| Source finding | `MafAgentRuntime.ProcessTools.cs` directly uses Processes | RQ-001-RQ-004 | SB02-SB05 | None |
| Source finding | 23 process tools in current MAF process builder | RQ-005 | SB04, SB06 | None |
| Source finding | Dispatcher has 33 partial files / 25k lines | RQ-011 | All | Scope exception: no dispatcher split in this bundle |
