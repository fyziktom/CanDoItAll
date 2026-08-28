# SB10 proof manifest

- Status: Completed. Live behavioral acceptance and the SB09 regression review pass
  for this requested scope; broad repository suites are not green.
- Raw notes N013/R13 and N014/R14: bundle://inputs/07-provider-model-thinking-settings-feedback.md.
- Contract: bundle://proof/SB10/semantic-invariants.md.
- Actual UI: mcp-configuration-results.json, mcp-execution-results.json,
  mcp-final-results.json and browser-validation.md. Screenshot paths are under browser/.
- Source bytes/tests: bundle://proof/SB09/manifest.md. Proof hashes: proof-hashes.csv.
- Final semantic verifier: bundle://reviews/04-model-thinking-final-verifier.md.

## Root cause and source administration

5214 retained the old 128-model import without thinking metadata. Rebuilding alone
did not refresh that persisted catalog. The source already defined Sol correctly.
The new explicit refresh was exercised on the user's unsaved agent draft: it kept
Sol/Medium while replacing unavailable metadata with the source's actual choices.

Source Providers > Thinking shows provenance and per-model support/options/default.
Automatic means discovery then built-in definitions. Administrator override wins;
unchecking support explicitly disables configurable thinking. Custom Ollama models
can use levels or boolean controls. Save persists the normal provider draft.
Shared clients mirror the source read-only and expose a refresh action.

## Actual upstream request matrix

Six dedicated agents already present from SB08 were used, not user agents. Configuration
and agent changes used Playwright MCP. SQL reads only collected source usage evidence.
Source Proof Responses Sol was temporarily restricted to Low/High, default Low;
the source-default test agent temporarily selected Sol. Original global Medium,
automatic Sol/Ollama definitions and that agent's Mini selection were restored.

| Agent / real model | Source applied effort | Agent override | Request ID |
| --- | --- | --- | --- |
| Source Default / gpt-5.6-sol | low | false | 0HNO4KUPCJ5EA:00000001 |
| Sol High / gpt-5.6-sol | high | true | 0HNO4KUPCJ5EA:00000002 |
| Mini Low / gpt-5.4-mini | low | true | 0HNO4KUPCJ5EA:00000003 |
| Mini High / gpt-5.4-mini | high | true | 0HNO4KUPCJ5EA:00000004 |
| Ollama Low / gptoss20b64k:latest | low | true | 0HNO4KUPCJ5ED:00000001 |
| Ollama High / gptoss20b64k:latest | high | true | 0HNO4KUPCJ5ED:00000002 |
| Final-image Sol High / gpt-5.6-sol | high | true | 0HNO4LF30SO0M:00000001 |
| Final-image Sol Medium / gpt-5.6-sol | medium | true | 0HNO4LF30SO0O:00000001 |

All eight requests returned upstream HTTP 200, actual 323 responses, Succeeded and
Complete source usage. live-source-evidence.txt and final-source-evidence.txt join
dispatch to PostgreSQL rows by RequestId. No token-count or model self-report inference.
Image1 ran the six-case matrix at 02:12-02:17 UTC on 2026-08-28. Image2 changed only
desktop layout; final Sol High was repeated at 02:29 UTC. Runtime/mapper code is unchanged.
Explicit Sol Medium also passed at 02:53 UTC. sol-medium-proof.json includes original
UI output, correlated source usage and reopened settings after restoring the agent to High.

## Docker lineage

docker-build-1.txt and docker-build-2.txt: successful Release publish and image export.
Final tag candoitall-shared-providers-ui:model-thinking-20260828-2, image ID
sha256:96be062a4d15b1d239ce30d23e5c8eefe9a3c8223cf46da575734804fb7f6cdb.
deploy-1.txt and deploy-2.txt preserve named volumes/database and stopped pair rollback
containers. Compose updates only the manual client app, without reset or volume deletion.
All three /health endpoints return 200 Healthy; final-health.json retains exact identity.

- Source: http://localhost:5210/agents?tab=providers
- Existing client: http://localhost:5212/agents?tab=agents
- Manual client: http://localhost:5214/agents?tab=agents
- Internal source address remains http://candoitall-spui-shared:8080/.

5214 user assignments, configuration and data were not cleared. No upstream secret was
copied to a client. Both imported catalogs were synchronized through UI. Existing scoped
source JWTs remain stored; credentials were never printed. 5032 was untouched.

## Explicit limits

For verified OpenAI reasoning with tools, use Thinking Proof OpenAI Responses. Existing
Chat Completions compatibility restrictions remain; manual settings cannot remove an
upstream transport limitation. Unknown metadata stays Unknown until configured or
refreshed. Historical failed/active runs are not erased. This is not full-repository
green or a re-run of the earlier image-generation feature; eight reasoning requests
and actual model-dependent agent/Simple Chat option checks are this extension's proof.
