# Risk-to-proof map

| Risk | Preventive design | Required negative proof | Final artifact |
| --- | --- | --- | --- |
| R-001 secret leak | sanitized catalog mapper | serialization/log/database scan | SB03/SB07 proof |
| R-002 open proxy | routing ID + adapter registry | caller URI/header/model tampering | SB04 proof |
| R-003 tool semantics | function-tool relay | built-in tool rejected; function call round-trip | SB04/SB07 proof |
| R-004 runtime branch spread | OpenAI effective projection | dependency/switch guardrail | SB06 architecture review |
| R-005 cycle | lower abstractions + outer composition | CodeAnalytics no cycle | SB01/SB06 proof |
| R-006 token duplication | source-owned secret reference | persistence scan and source edit invariant | SB02/SB05 proof |
| R-007 destructive sync | availability state machine | outage/unpublish preserve local ID | SB05/SB07 proof |
| R-008 route collision | publication-namespaced ID | duplicate upstream model scenario | SB01/SB07 proof |
| R-009 silent fallback | explicit availability gate | central down while personal exists | SB06/SB07 proof |
| R-010 advanced feature egress | allowlisted fields/tool types | store/background/web/file/MCP denied | SB04 proof |
| R-011 stream failure | streaming session/cancellation | first-chunk timing and disconnect | SB04/SB07 proof |
| R-012 context trusted | auth/access separation | forged context cannot authorize | SB01/SB03 proof |
| R-013 context upstream leak | header allowlist | upstream capture lacks header | SB07 proof |
| R-014 usage false/double | one invocation record/completeness | missing usage remains unavailable | SB04 proof |
| R-015 SSRF | URI/network policy | userinfo/redirect/private policy/DNS cases | SB05 proof |
| R-016 migration drift | module configs + migration gate | clean DB migrate/model validation | SB02/SB12 proof |
| R-017 in-process false confidence | 3-app real HTTP | full Docker scenario | SB07 |
| R-018 remote field mutation | service-side ownership | forged UI/service update denied | SB08/SB09 |
| R-019 OpenAPI overclaim | exact operation subset | no audio/management routes | SB11 |
| R-020 test overspend | machine budget | command audit/broad count <= 1 | every proof/SB12 |
| R-021 paid dependency | deterministic upstream | external network denied | SB07/SB12 |
| R-022 cleanup too early | final no-down contract | healthy running status after closure | SB12 handoff |
