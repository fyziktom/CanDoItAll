# Assumptions And Risks

## Assumptions Confirmed

- Existing OpenAI credential was used for bounded authorized real validation.
- Saved Ollama endpoint http://192.168.10.132:11434 recovered and returned 72 installed IDs.
- Source-managed profiles retain internal routing isolation; UI shows real source names.
- Unknown prices are absent, not zero. Ollama /api/tags does not publish token rates.
- Explicit UI refresh/save/sync repairs contaminated profiles without deleting custom
  or fine-tuned names by spelling. No destructive heuristic migration was added.

## Critical Path Risks

- Catalog normalization affects save, publication and runtime projection. Their focused tests
and real source/client equality passed; later changes invalidate those dependent checks.
- Approval/context repair is verified through a restored real-SDK session and actual image run.

## Validation Risks

- Real final UI acceptance passed on build6; failures from older attempts remain historical.
- OpenAI inventory includes non-chat models. Mirroring all IDs does not prove every ID
  supports the selected operation. Actual selected models are recorded in execution-result.json.
- Only configured verified price rows are mirrored; unknown rates are never invented.
- Relay token/image usage is Complete, but its monetary PricingCompleteness is Unavailable.
  Do not describe the ledger evidence as cost settlement.
- Source UI token lifetime is four hours. Renew through source Settings/API authentication
  and update the client source secret after expiry. Tokens are absent from proof files.
- Simple Chats tests used a scoped client JWT, not anonymous API access.
- 5032 was not changed. Existing stopped rollback containers and historical runs are retained.
- Fifteen exact pre-edit hashes exist. Other before hashes are explicitly historical/HEAD
  provenance, not fabricated pre-edit captures. No independent reviewer or full-suite claim.

## Reopen Triggers

- Stale/default/fake rows after refresh or sync reopen SB01 and dependent SB02.
- Fixture endpoints, failed actual execution, lost source usage or mismatched names reopen SB02.
- New deployment, catalog/routing changes or altered approval/context ordering invalidate
  the corresponding focused tests and real runtime acceptance.
