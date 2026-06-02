# Gap Map Against V1

| V1 subbundle | V1 claim | Current review result | Follow-up |
| --- | --- | --- | --- |
| SB01 Canonical contracts | Completed | Constants exist, but strict fail-closed enforcement is incomplete when contracts are missing. | SB01 |
| SB02 Process runtime/refactor | Completed | Some state/lineage work exists, but operation contract and manual proof gaps remain. | SB01, SB04, SB05 |
| SB03 Token/cost ledger | Completed internally; external reconciliation pending | Ledger-first process cost exists, but raw provider usage normalization and external OpenAI reconciliation remain incomplete. | SB03 |
| SB04 Tool/browser/runtime proof | Completed | Browser bounds improved, but tool registry drift and default-read fallback remain. | SB02 |
| SB05 Workflow side effects | Completed | Not deeply re-opened in this pass, but must be revalidated after registry/usage changes. | SB07, SB09 |
| SB06 Agents/skills/templates | Completed | Needs resync after fail-closed contracts and proof-quality changes. | SB07 |
| SB07 UI observability | Completed | Empty/unknown usage was handled partly; UI must show strict contract blockers and proof validity states. | SB08 |
| SB08 Multi-domain E2E | Completed | Reclassified as process API + browser harness, not real agent-driven process automation proof. | SB04, SB05 |
| SB09 Final red-team | Passed | Accepted a proof that explicitly had no provider execution runs; must be strengthened. | SB05, SB09 |
