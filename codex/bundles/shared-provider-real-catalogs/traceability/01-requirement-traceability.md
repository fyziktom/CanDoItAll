# Requirement Traceability

| Input or requirement | Owning subbundle | Actual closure proof | Status |
| --- | --- | --- | --- |
| N005 / R5 local Simple Chats denied | SB03; SB02 normal-browser handoff restored | proof/SB03/manifest.md; 38 component/9 HTTP/3 real browser cases; complete source usage | Solved |
| N001 / R1 polluted Ollama | SB01 + SB02 | component-final.trx; build6-ollama-ui.trx; proof/SB02/browser/metadata-real-ollama-parity.json; 72 real IDs and zero invented rates | Solved |
| N002 / R2 fake OpenAI and rates | SB01 + SB02 | failing-first.trx -> unit-final.trx; build6-openai-ui.trx; proof/SB02/browser/metadata-real-openai-parity.json | Solved |
| N003 / R3 faithful client | SB01 + SB02 | integration-affected.trx; full final UI parity; nondefault agent/chat selections; real source invocations | Solved |
| N004 / R4 new bundle/live proof | SB01 + SB02 | docker-build-6.txt; deploy-6.txt; build6-runtime-ui.trx; proof/SB02/transcripts/real-runtime-evidence.txt; reviews/02-final-verifier.md | Solved |

TRX/build filenames above resolve under proof/SB01/transcripts. Exact commands, scope,
raw-note closure and limitations are in reviews/01-execution-report.md.
