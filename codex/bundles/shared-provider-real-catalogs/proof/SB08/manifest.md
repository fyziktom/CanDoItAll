# SB08 proof manifest

- Status: Completed for N011/R11 and N012/R12 live acceptance, with transport limits below.
- Raw input: bundle://inputs/06-thinking-effort-feedback.md.
- Invariants: bundle://proof/SB08/semantic-invariants.md.
- Source/test/bundle hashes: bundle://proof/SB07/changed-files.csv; proof artifact hashes:
  bundle://proof/SB08/proof-hashes.csv. All SB08 artifacts are new; no prior bytes exist.
- Source review, negative regressions and anti-stub audit: bundle://proof/SB07/manifest.md.
- Browser evidence: bundle://proof/SB08/browser-validation.md and mcp-results.json.
- Final verifier: bundle://reviews/03-thinking-final-verifier.md.

## Real request acceptance

Seven dedicated agents were created, configured, saved and reopened through Playwright
MCP on 5212. No user agent was overwritten. Actual source URLs were api.openai.com/v1
and the existing Ollama host 192.168.10.132:11434; no fixture upstream was substituted.
All nine final requests returned HTTP 200, an actual answer, and Succeeded/Complete
source usage records. The join key is RequestId in live-source-dispatch.txt and
live-source-usage.txt. Effort proof is outgoing request metadata, not token-count inference.

| Agent / model | Applied effort | Override | Request ID |
| --- | --- | --- | --- |
| Mini Low / gpt-5.4-mini | low | true | 0HNO4J8ONG1NV:00000002 |
| Mini High / gpt-5.4-mini | high | true | 0HNO4J8ONG1NV:00000003 |
| Luna Low / gpt-5.6-luna | low | true | 0HNO4J8ONG1NV:00000004 |
| Sol High / gpt-5.6-sol | high | true | 0HNO4J8ONG1NV:00000005 |
| Source Default / gpt-5.4-mini | medium | false | 0HNO4J8ONG1NV:00000006 |
| Same default agent after source-only change | high | false | 0HNO4J8ONG1O5:00000001 |
| Mini Low while source default is High | low | true | 0HNO4J8ONG1O5:00000002 |
| Ollama Low / gptoss20b64k:latest | low | true | 0HNO4J8ONG1O5:00000003 |
| Ollama High / gptoss20b64k:latest | high | true | 0HNO4J8ONG1O5:00000004 |

Window: 2026-08-28 00:21-00:28 UTC. The source-default change used a second browser
tab; no reload or synchronization of the client occurred between default tests.
Explicit overrides remained independent. Source default was restored to Medium.

## Host lineage and commands

Docker build used src/App/CanDoItAll.Web/Dockerfile, repository context, additional
components=../CanDoItAll.Components and filetools=../CanDoItAll.FileTools contexts,
and tag candoitall-shared-providers-ui:thinking-20260827-6. Release publish and final
image export succeeded: docker-build-final6.txt (exit 0).

The existing scoped Restart-TestInstances.ps1 in shared-providers/SPMETA proof recreated
the source/client with retained mounts and rollback suffix before-thinking-terminal-20260827.
Transcript: restart-final6.txt, exit 0. Docker compose up -d --no-deps --force-recreate app
using subbundles/04-avatar-and-fresh-client/compose.yaml updated 5214 without resetting it;
restart-5214-final6.txt, exit 0. No database/volume was deleted; 5032 was untouched.

Final read-only Docker inspect plus GET /health on 5210, 5212 and 5214:
final-health.json, all running, all HTTP 200 Healthy, same image ID
sha256:92887c940e5dfb375c02c73f8d2821f646c602af9c052ffe5b2e33c81e4248ac.
The source/client named volumes and 5214's existing reset-20260827 volume remain mounted.

## Limits and handoff

For OpenAI agents with reasoning and tools, select Thinking Proof OpenAI Responses.
The original Chat Completions provider was preserved; the actual upstream rejected
Sol High and existing Mini/Luna tool compatibility can change effort to None on that
transport. Such preliminary requests are explicitly excluded from positive proof.
The separate Responses profile was configured/published/imported through UI and proves
Low/High unchanged for Mini, Luna and Sol. Ollama Low/High uses Chat Completions.

Existing client credentials remain stored, never printed in proof. The renewed source
JWT expires approximately 2026-08-28 05:22 UTC; renew via source Settings/API and update
the client's existing source secret after expiry. All prior histories/setup are retained.
The nine final records are green; historical failed/active runs were not erased.
