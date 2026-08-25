# SB03 honest execution chronology

State: `COMPLETE`.

All timestamps are UTC and come from durable transcripts unless explicitly identified as a
review event.

| Time | Event | Honest interpretation |
| --- | --- | --- |
| 2026-08-24 23:46:08 | entry validator passed | SB02 dependency and entry shape were valid before SB03 work |
| 2026-08-24 23:48:22 | worktree captured | cumulative SB00–SB02 changes were already present; SB03 did not claim a clean tree |
| 2026-08-24 23:50:22 | before CodeAnalytics snapshot `snap-20260824235022-a4b340a8` captured | 13 projects, 31 direct references, no project-level cycle |
| 2026-08-25 00:01:09–00:01:52 | exact 18-test unit source ran before production behavior | exit 1 on missing publication/policy/projection/cache/application types; valid failing-first compile evidence |
| 2026-08-25 00:10:02–00:10:30 | combined Web test discovery attempt 1 | exit 1 on real Workspace `CS9135`; not represented as endpoint behavior failure |
| 2026-08-25 00:13:20–00:14:19 | combined Web test discovery attempt 2 | exit 1 on a missing explicit Http test reference; dependency-wiring evidence only |
| 2026-08-25 00:16:40–00:17:14 | combined Web discovery succeeded | planned 14 catalog/API and 10 authorization tests were discoverable before route behavior |
| 2026-08-25 00:17:31–00:17:41 | catalog/API failing-first run | 14 discovered, 0 passed, 14 failed |
| 2026-08-25 00:17:50–00:18:00 | authorization failing-first run | 10 discovered, 0 passed, 10 failed |

## Implementation and review repair sequence

Production behavior was then added across Abstractions, Http, Workspace, Security, Composition,
and thin Web adapters. Independent review found and repaired the following before the final
freeze:

1. Numeric enum strings passed `Enum.TryParse`; exact named tokens are now required.
2. OpenAI image publication was unreachable; OpenAI/Responses and ComfyUI/ChatCompletions are
   now the exact image combinations, while Azure remains excluded.
3. A non-null secret GUID was treated as sufficient; save, publish, and query now require an
   existing non-empty reference without resolving its value.
4. Real Workspace, AgentFramework, and bootstrap save paths omitted canonical metadata; all now
   call one typed writer. A follow-up removed pricing-table rows from implicit model claims.
5. Deletion lacked a ProviderProfile reference boundary; Security/Workspace gained typed policy
   composition and save/delete gained stable multi-key serialization.
6. Catalog cache correctness was strengthened from observer notification to a persisted public
   stamp plus current eligibility/secret-existence recheck.
7. Raw health text was replaced by bounded public health and included in public revision changes.
8. RFC review rejected a wildcard mixed with another entity tag.
9. OpenAPI review added conditional/access-context parameters and ETag/cache/request-ID response
   headers.
10. A nullable suggested-model warning and secret GUID exception-message disclosure were removed.

One overlapping sandboxed diagnostic build encountered `UnauthorizedAccess` in a sibling
Components static-web-assets cache while another build owned it. It produced no C# finding and is
not used as governed validation. The team stopped duplicate execution and adopted the later
serialized authoritative transcripts below.

## Final validation sequence

| Time | Event | Result |
| --- | --- | --- |
| 2026-08-25 01:20:29–01:20:53 | Web Release build | exit 0; 0 warnings, 0 errors |
| 2026-08-25 01:21:06–01:21:46 | Integration Release build | exit 0; 0 warnings, 0 errors |
| 2026-08-25 01:22:04–01:22:06 | exact catalog/API and authorization discovery | 14 and 10 respectively; both exit 0 |
| 2026-08-25 01:22:13 | after CodeAnalytics snapshot `snap-20260825012213-a17e36ed` | 14 projects, 736 documents, 35 modules, 4,954 dependency edges, 33 direct references, no project cycle, unchanged two module plus one type cycles, no error finding |
| 2026-08-25 01:22:18–01:22:28 | exact catalog/API run | 14 passed, 0 failed, 0 skipped |
| 2026-08-25 01:22:39–01:22:47 | exact authorization run | 10 passed, 0 failed, 0 skipped |
| 2026-08-25 01:28:17–01:29:04 | authoritative Unit Release build | exit 0; 0 warnings, 0 errors |
| 2026-08-25 01:29:14–01:29:17 | exact unit discovery | exactly 18 tests |
| 2026-08-25 01:29:25–01:29:30 | exact unit run | 18 passed, 0 failed, 0 skipped |
| 2026-08-25 01:29:46–01:29:47 | anti-stub audit | exit 0; 56 selected files passed |
| 2026-08-25 01:30:48 | credential/private-field scan | exit 0; clean |

No broad solution test, browser lane, paid-provider call, live-network call, or three-instance
deployment lane ran under SB03. That is intentional scope control, not omitted evidence.
