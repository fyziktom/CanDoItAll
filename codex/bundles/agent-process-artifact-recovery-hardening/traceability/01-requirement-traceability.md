# Requirement Traceability

| Raw note | Normalized requirements | Impacted surface | Planned proof | Owning subbundle | Status |
| --- | --- | --- | --- | --- | --- |
| Real-agent process failed at implementation step with missing migration/rollout artifact. | REQ-001, REQ-003, REQ-006 | Process runtime, software-delivery template, artifact projection | DB forensics, artifact contract tests | 01, 02 | Covered |
| Console shows repeated identical tool invocations and missing build/test tools. | REQ-001, REQ-004, REQ-007 | Agent runtime, dispatch prompt, mock runtime | Single-agent proof and mock failure tests | 01, 04 | Covered |
| Maybe artifact is missing because no DB is part of the solution. | REQ-003 | Template/prompt semantics | DB-free checklist prompt/projection test | 02 | Covered |
| If missing artifact belongs to previous agent, retrying current agent cannot fix it. | REQ-005 | Recovery routing | Current-vs-upstream missing artifact tests | 03 | Covered |
| Improve mock agents to cover possible failures. | REQ-007 | ProcessMockAgentRuntime and test harness | Failure matrix integration tests | 04 | Covered |
| Test first just one agent implementing application. | REQ-002, REQ-009 | AgentFramework/process dispatch | Focused single-agent implementation proof | 01 | Covered |
| Use simpler three-agent process to test artifact outputs. | REQ-008 | Process template/test setup/UI if visible | Deterministic three-agent process proof | 05 | Covered |
| Generated app builds/tests but fails at runtime with missing `/_Host` fallback endpoint. | REQ-010, REQ-011 | Generated app runtime proof and seeded implementation/QA guidance | Runtime smoke and browser proof in the generated-app delivery lane | 06 | Diagnostic history |
| Core process must not contain calculator, Blazor, or .NET-specific hardcoded guidelines. | REQ-012, REQ-013, REQ-014 | Process dispatch, retry/proof code, reusable seed assets, agent/skill/tool boundaries | Source scans, static regression tests, focused integration tests, bundle validator | 07 | Covered |
| Seeded skills must be generic because agents may be asked to build any kind of app, not only the calculator sample. | REQ-015 | Seeded skills/resources, seed manifest, seed builder, seed normalizer | Source scans, static regression test, bundle validator | 08 | Covered |
