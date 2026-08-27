# Execution Report

## Status

Completed on 2026-08-27. SB01 and SB02 behavioral/architecture gates pass.
Final canonical validation and hash audit are recorded in reviews/02-final-verifier.md.
No historical failure was relabeled a pass.

## Outcome Check

The source now uses the OpenAI v1 API at api.openai.com and Ollama at 192.168.10.132:11434,
not the old deterministic fixture. Admin setup, discovery, save, publication, client
synchronization and runtime model selection were performed through the UI.

- OpenAI Chat: all 128 returned IDs, 10 configured known price rows.
- OpenAI Image: five real image IDs; default gpt-image-1-mini.
- Ollama: all 72 installed IDs, zero invented price rows; private-provider flag retained.
- Client: exact full source model names, all nine price fields and private flag.
  Existing internal publication routes remain an isolation mechanism, not displayed aliases.
- Real nondefault selections persist: gpt-5.4-mini and gpt-oss:20b for agents,
  gpt-5.4-mini and gemma3:4b for simple chats.
- One real approved image generation finished its agent turn; the generated PNG is a
  blue geometric lighthouse. Vision correctly identified a blue circle on the left and
  an orange square on the right, without these answers being supplied in the prompt.

## Root Causes And Minimal Repairs

1. Catalog refresh merged stale membership and price-derived defaults instead of replacing
   membership with actual upstream IDs. Price normalization invented missing rows.
   Repair stays in existing catalog, pricing, administration and rendering owners.
2. Provider-kind changes retained incompatible connection, credential, catalog and price
   state. An explicit UI event clears that state before new discovery.
3. Real image execution exposed a tool-name/route mismatch and an image response whitelist
   that rejected documented OpenAI metadata. Existing input/response boundaries now accept
   the supported real contract while rejecting unknown names, fields and invalid payloads.
4. Opaque shared routes bypassed real-model compatibility rules; the relay also rejected
   the required reasoning option. Compatibility uses unique source metadata for policy only;
   wire routing stays constrained and the closed request schema admits documented values.
5. Restored approval context was inserted between an assistant tool call and its results.
   The existing OpenAI wire compatibility owner moves only framework-stamped context before
   the pending call group. It preserves messages, call IDs, approvals, normal history and
   caller input; it neither fabricates results nor disables context/compaction.

Thirteen production files; no new project, interface, DI registration, generic manager,
fallback transport or approval bypass. Exact paths/hashes are in proof/SB01/changed-files.csv.

## Deployment

Image: candoitall-shared-providers-ui:real-catalogs-20260827-6
Image ID: sha256:3a92a5a65a81dfd2b03e8cb91a901ecdabddc836feeafc33624c1d02a4ea4800

- candoitall-spui-shared: 127.0.0.1:5210 -> 8080
- candoitall-spui-client: 127.0.0.1:5212 -> 8080
- Named volumes, application IDs and database retained. Prior app containers remain
  stopped and recoverable as rollback checkpoints. The fixture upstream stays stopped.
- Port 5032 and unrelated working-tree changes were untouched.
- proof/SB01/transcripts/docker-build-6.txt and deploy-6.txt: exit 0.

The computer restart was recovered. The standard sandbox helper had an ACL initialization
failure; approved escalated execution and the standard apply-patch engine were used.
Credentials are not captured in the proof.

## Affected Test Evidence

Every filtered run records --list-tests, expected/actual discovery and exit code.
These are scope counts, not a sum of unique tests; repeated subsets are not double-counted.

| Frozen affected scope | Result | Durable transcript/TRX prefix |
| --- | --- | --- |
| Catalog/pricing/seed/publication unit contracts | 134/134 | proof/SB01/transcripts/unit-final |
| Provider editor/refresh/pricing components | 9/9 | proof/SB01/transcripts/component-final |
| Catalog API/sync/runtime projection integration | 46/46 | proof/SB01/transcripts/integration-affected |
| Real image-name input boundary | 20/20 | proof/SB01/transcripts/image-tool-final |
| Image response allowlist and adversarial payloads | 18/18 | proof/SB01/transcripts/image-envelope-adversarial |
| Relay policy plus real SDK wire schema | 54/54 (44 relay + earlier 10 wire) | proof/SB01/transcripts/shared-wire-relay-final |
| HTTP forwarding/persistence integration | 23/23 | proof/SB01/transcripts/build5-relay-integration |
| Rebuilt shared runtime projection integration | 16/16, subset of 46 | proof/SB01/transcripts/build5-runtime-integration |
| Final approval/context/compatibility/SDK scope | 100/100, including final 18 wire cases | proof/SB01/transcripts/approval-compatibility-regression |
| Build6 source/client real OpenAI UI | 1/1 | proof/SB01/transcripts/build6-openai-ui |
| Build6 source/client real Ollama UI | 1/1 | proof/SB01/transcripts/build6-ollama-ui |
| Build6 actual chats/agents/image/vision UI | 1/1 | proof/SB01/transcripts/build6-runtime-ui |

The final 100-case scope supersedes earlier wire-client results after the last production
change. Catalog/editor and relay-policy owners did not change after their named checkpoints;
their focused evidence remains valid. The final Docker build and three UI tests exercise
the current combined code. No unfiltered project or solution test gate was invoked.

Meaningful failing-first proof remains in failing-first.trx (4), image-name-red.trx (6),
image-envelope-red.trx (5), shared-reasoning-red.trx (3 shared failures/3 local controls),
relay-reasoning-red.trx (11 failures/3 controls), and approval-context-red.trx
(4 context failures/4 controls). Approval tests use the installed real SDK, streaming and
buffered responses, restored sessions, context and compaction. Pure tests verify parallel
tool groups, unchanged caller history and no fabricated missing results.

## SB01 Semantic Adequacy Evidence

- Raw note owned: N001-N003 require real models/prices and the client to be a full mirror; N004 requires approved real image/agent execution, not just names in a dropdown.
- Shipped behavior: authoritative inventory replaces stale membership, empty prices stay unknown, real names remain visible, and approved shared image tools complete their agent turn.
- Source proof: bundle://proof/SB01/changed-files.csv; existing catalog/pricing/editor/image/relay/compatibility owners; bundle://proof/SB01/semantic-invariants.md.
- Test proof: bundle://proof/SB01/transcripts/unit-final.txt; bundle://proof/SB01/transcripts/component-final.txt; bundle://proof/SB01/transcripts/approval-compatibility-regression.txt; exact filters/discovery are recorded.
- Shallow-pass trap: rendering one real label while stale defaults remain, inventing free prices, or accepting a generated image without successful approval continuation.
- Adversarial negative proof: bundle://proof/SB01/transcripts/failing-first.txt; bundle://proof/SB01/transcripts/approval-context-red.txt; unknown/ambiguous names, injected context, empty rates and malformed schemas are exercised.
- Semantic positive proof: bundle://proof/SB01/transcripts/build6-openai-ui.txt; bundle://proof/SB01/transcripts/build6-ollama-ui.txt; bundle://proof/SB01/transcripts/build6-runtime-ui.txt prove full parity and real requested behavior.
- Anti-stub audit: no fixture-specific branch, TODO or NotImplemented remains in the owned production path; bundle://proof/SB01/transcripts/source-audit.txt.

## SB02 Semantic Adequacy Evidence

- Raw note owned: N004 requires UI configuration on both instances, real chats/agents including image generation/analysis, and source-side provider usage records.
- Shipped behavior: both real upstreams are configured through UI, client synchronization mirrors full metadata, all requested runtime modalities complete and source accounting records their actual usage.
- Source proof: unchanged production relay invocation writer plus the repaired owners in bundle://proof/SB01/changed-files.csv; bundle://proof/SB02/semantic-invariants.md.
- Test proof: bundle://proof/SB01/transcripts/build6-openai-ui.txt; bundle://proof/SB01/transcripts/build6-ollama-ui.txt; bundle://proof/SB01/transcripts/build6-runtime-ui.txt; bundle://proof/SB02/transcripts/real-runtime-evidence.txt.
- Shallow-pass trap: fixture replies, count-only parity, prompt echo, stale generated bytes or historical/manual ledger rows presented as fresh execution.
- Adversarial negative proof: bundle://proof/SB01/transcripts/real-runtime-third.txt and bundle://proof/SB01/transcripts/build5-runtime-ui.txt retain real failures; the verifier rejects failed/incomplete records or stale/non-PNG artifacts.
- Semantic positive proof: all eight final real invocations succeed with complete usage; actual fresh lighthouse and correct blue-circle/orange-square recognition are inspected in bundle://proof/SB02/browser and backed by the current passing runtime transcript.
- Anti-stub audit: no fixture endpoints are used by the shared profiles; no synthetic source-ledger writes supply acceptance; bundle://proof/SB01/transcripts/source-audit.txt and bundle://proof/SB02/transcripts/real-runtime-evidence.txt.

## Real Source Usage And Health

Final run: 2026-08-27T16:28:40.6122214Z through 16:30:29.0597135Z.
proof/SB02/browser/execution-result.json records the actual UI result.
proof/SB02/transcripts/real-runtime-evidence.txt records read-only source SQL and host checks:

- Eight invocations, all Succeeded and UsageCompleteness=Complete.
- Real upstream models gpt-5.4-mini, gemma3:4b, gpt-oss:20b and gpt-image-1-mini.
- Exactly one ImageGenerations invocation with ImageCount=1.
- Fresh 1,075,935-byte PNG in the client managed workspace:
  shared-provider-real-validation/lighthouse-1787848120.png.
- PNG SHA256: 846177136dde3913836e903e6e159de56e3dc9d820b96f1724f07a9f20cafc83.
- Both health endpoints HTTP 200 Healthy; zero new fail/crit/unhandled log headings.

Ledger verification uses the final start time, excludes earlier failures and does not insert
records. Prior failed image runs remain visible in history. The default Overview usage
filter is local-agent/chat activity, not the relay ledger. PricingCompleteness in the relay
ledger is Unavailable; this proves token/image accounting, not monetary settlement.

## Subbundle Gate Results

| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |
| --- | --- | --- | --- | --- | --- |
| SB01 | Pass | Pass | Source save/discovery/publication, exact client sync, real runtime | Completed | bundle://proof/SB01/manifest.md |
| SB02 | Pass after each SB01 repair | Pass | Both upstreams, all requested modalities, source usage and health | Completed | bundle://proof/SB02/manifest.md |

## Browser Validation Analytics

All screenshots below are under proof/SB02/browser and were inspected by the primary agent.
Desktop viewport is 1920x1080. Playwright uses actual Chromium UI actions; API inventory reads
are independent oracles, and SQL is read-only usage evidence.

| Subbundle | Route | Viewport | Playwright MCP evidence | Screenshots | Result |
| --- | --- | --- | --- | --- | --- |
| SB01/SB02 | 5212 agents/providers OpenAI Prices | 1920x1080 | C# Playwright Chromium, not MCP; bundle://proof/SB01/transcripts/build6-openai-ui.trx | bundle://proof/SB02/browser/metadata-real-openai-client.png | Pass: real names, 128 models/10 priced, read-only metadata |
| SB01/SB02 | 5212 agents/providers Ollama Prices | 1920x1080 | C# Playwright Chromium, not MCP; bundle://proof/SB01/transcripts/build6-ollama-ui.trx | bundle://proof/SB02/browser/metadata-real-ollama-client.png | Pass: 72 real names, private flag, explicit unpriced rows |
| SB01/SB02 | 5212 agents Runtime dropdowns | 1920x1080 | Exact DOM identity and persisted selection in both final catalog tests | bundle://proof/SB02/browser/metadata-agent-models-real-openai-open.png; bundle://proof/SB02/browser/metadata-agent-models-real-ollama-open.png | Pass: real nondefault IDs visible; native dropdown owns long-list scrolling |
| SB02 | 5212 agents/chat image approval/completion | 1920x1080 | bundle://proof/SB01/transcripts/build6-runtime-ui.trx | bundle://proof/SB02/browser/real-image-approval.png; bundle://proof/SB02/browser/real-image-response.png | Pass: Approve once, Completed and successful final message/thread summary |
| SB02 | Client managed workspace generated image | 1024x1024 artifact | Real provider invocation plus PNG signature, time and hash in source verifier | bundle://proof/SB02/browser/real-generated-lighthouse.png | Pass: inspected blue geometric lighthouse, genuine fresh PNG |
| SB02 | 5212 agents/chat vision | 1920x1080 | bundle://proof/SB01/transcripts/build6-runtime-ui.trx | bundle://proof/SB02/browser/real-vision-response.png; bundle://proof/SB02/browser/vision-input.png | Pass: actual assistant recognizes both shapes/colors and Completed |

UI composition: provider list/editor remains the primary surface; compact badges and existing
tabs are retained. Price table owns horizontal overflow. Editor/dialog/chat bodies own
vertical scroll; the long image conversation requires inner scrolling to see the full last
bubble, while completion and the thread summary remain visible. Native model lists extend
upwards and scroll; selected names and Save agent remain readable. Existing narrow New
provider/Switch agent button wrapping is unchanged, not a new layout improvement claim.

## Raw Note Closure

| Raw note | Status | Proof |
| --- | --- | --- |
| N001 polluted Ollama | Solved | bundle://proof/SB01/transcripts/build6-ollama-ui.trx; bundle://proof/SB02/browser/metadata-real-ollama-parity.json; 72 real IDs/0 invented rates |
| N002 invented OpenAI names/prices | Solved | bundle://proof/SB01/transcripts/build6-openai-ui.trx; bundle://proof/SB02/browser/metadata-real-openai-parity.json; authoritative discovery/pricing regressions |
| N003 faithful client mirror | Solved | bundle://proof/SB02/browser/metadata-real-openai-parity.json; bundle://proof/SB02/browser/metadata-real-ollama-parity.json; exact dropdowns and real nondefault requests |
| N004 new bundle and real validation | Solved | bundle://proof/SB01/transcripts/build6-runtime-ui.trx; bundle://proof/SB02/browser/execution-result.json; bundle://proof/SB02/transcripts/real-runtime-evidence.txt |

## Remaining Limits And Handoff

- Source and client are ready on 5210/5212. This does not redeploy 5032.
- Source JWT issued by the UI has a four-hour lifetime. Renew through source Settings/API
  authentication and update the client source secret if testing continues after expiry.
- Simple Chats UI acceptance used a scoped client JWT. Anonymous Simple Chats access is
  not claimed. Source catalog/invocation authentication was not weakened.
- Upstream inventory can include models for other operations. Full catalog mirroring does
  not imply that every OpenAI ID supports chat, or that every Ollama model supports vision.
- Unknown rates remain unpriced. Do not claim a free service or infer unverified prices.
- Source hashes distinguish 15 exact pre-edit captures from historical/HEAD recovery.
- No full-suite, whole-solution architecture, independent reviewer or all-model execution
  claim is made. Historical shared-providers SB07 is not closed by this bundle.
