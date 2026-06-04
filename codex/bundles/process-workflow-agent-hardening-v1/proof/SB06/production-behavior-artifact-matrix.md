# SB06 Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| Agent canonical contract instructions | Agent template files under `Templates/Agents/teams/.../instructions.md` | Runtime-seeded agents and process-assigned role executors | Updated in repository templates; consumed when templates seed or refresh agent instructions | `failing-first-removed-mcp-assumption-mutation.txt`; `removed-mcp-assumption-restored-test.txt` |
| Software-delivery process governance notes | `Templates/Processes/processes/software-delivery/definition.json` and `.md` | Process template import, process governance tests, and process authors | Stored with the template and asserted by integration governance tests | `process-template-governance-tests.txt` |
| API skill canonical contract guidance | `codex/skills/candoitall-api-*/SKILL.md` | Codex agents using HTTP API skills for agents, processes, workflows, and project structure | Updated in repo and synchronized to active skill root; hash proof recorded | `failing-first-skill-canonical-contract-mutation.txt`; `skill-canonical-contract-restored-test.txt` |
| Active skill synchronization record | `proof/SB06/transcripts/active-skill-sync.txt` | Downstream SB07/SB08 agents and bundle reviewers | Produced after copying repo skills to `C:\Users\lucys\.codex\skills`; repo and active hashes must match | `active-skill-sync.txt` |
| Removed-MCP source scan | Unit parity test and standalone scan transcript | Skill/template maintainers and bundle validator | Run before closure; rejects stale MCP-only assumptions without removed-server qualifiers | `failing-first-removed-mcp-assumption-mutation.txt`; `removed-mcp-assumption-restored-test.txt` |

## Dependency Smoke Proof

- SB07 can rely on active API skills naming canonical operation, browser proof, provider usage, project-structure, and workflow side-effect contracts.
- SB08 can run E2E process scenarios without agents following stale removed-MCP instructions.
- SB09 can red-team skill/template drift by mutating canonical contract names or stale MCP-only instructions and expecting parity failures.
