# Structured Input

| Raw note | Exact wording | Normalized requirements | Owner | Planned proof |
| --- | --- | --- | --- | --- |
| RN-001 | Check whether processes now work like before. | REQ-001, REQ-005, REQ-006, REQ-007, REQ-009 | SB01, SB03, SB04, SB06 | Release audit, deterministic integration matrix, Playwright UI proof, final decision. |
| RN-002 | If not, identify what refactoring broke and prepare a follow-up bundle. | REQ-004, REQ-009 | SB02, SB06 | Failure classification and final decision with blocker or follow-up path. |
| RN-003 | Run a test with OpenAI; OPENAI_API_KEY is in env and other live env vars should be set by Codex or safe defaults should be used. | REQ-003, REQ-004 | SB02 | Live OpenAI transcript with redacted env presence and explicit model/timeout/token cap. |
| RN-004 | Stabilize process functionality before attempting further separation of process runtime core/dispatcher into separate libraries. | REQ-005, REQ-008, REQ-009 | SB03, SB05, SB06 | Deterministic runtime proof and boundary scans proving no new runtime extraction or driver drift. |

## Execution Corrections
- SB02 must use `5.4-mini` for the smoke test.
- SB02 may use a max token value up to `100000` if the live smoke needs the larger bounded ceiling.
