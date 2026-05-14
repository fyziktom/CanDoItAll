# Scenario Matrix

| Id | Scenario | Input source | Required workflow behavior | Provider/backend target | Validation expectation |
| --- | --- | --- | --- | --- | --- |
| `S01` | Mouser order XLS/PDF reconciliation | `C:\programovani\testdata\testworkflows\mouser-order` | Compare items, quantities, and totals across XLS and PDF. | `gpt-5-mini` | Summary identifies matches/mismatches and source paths. |
| `S02` | Mouser order purchasing summary | Same Mouser files | Summarize order purpose, key line items, totals, and open questions. | In-process or local | Summary node under workflow node includes both file paths. |
| `S03` | SEAMARK folder x-ray device summary | `C:\programovani\testdata\testworkflows\SEAMARK` | Read folder as input, summarize devices and use cases. | `gptoss20b64k` | Output references multiple PDFs and folder path. |
| `S04` | SEAMARK price list extraction | SEAMARK price list PDF | Extract price list highlights and uncertainty. | In-process or local | Summary includes price source path. |
| `S05` | SEAMARK model comparison | X-5600/X-6600/X-6600A specs | Compare models and recommendation criteria. | `gpt-5-mini` | Output contains meaningful model differences. |
| `S06` | IoTFactory financial plan review | `IoTFactory rozpočet-v1.xlsx` | Summarize budget, risks, and assumptions. | `gpt-5-mini` | Output names workbook path and concrete risks. |
| `S07` | Business plan markdown review | Synthetic markdown | Produce investor-style strengths/risks/actions. | In-process or local | Summary under workflow node. |
| `S08` | Customer email task extraction | Synthetic email | Create task/action result nodes. | `gpt-5-mini` | Result nodes parented under workflow node. |
| `S09` | Vendor renewal risk | Synthetic contract note | Decide needs-review and summarize risk. | In-process or local | Status and summary visible. |
| `S10` | Support SLA escalation | Synthetic ticket | Route escalation and create follow-up. | In-process or local | Run state and created node recorded. |
| `S11` | Meeting notes action extraction | Synthetic notes | Extract blocked, owner-needed, info actions. | In-process or local | Child result nodes under workflow node. |
| `S12` | Release readiness gate | Synthetic release brief | Decide ready/hold with reasons. | In-process or local | Summary includes decision. |
| `S13` | Vendor risk routing | Synthetic vendor memo | Route security/legal/finance. | In-process or local | Result notes show selected lane. |
| `S14` | Sales lead qualification | Synthetic lead list | Classify enterprise/nurture/disqualify. | In-process or local | Output has classification evidence. |
| `S15` | Incident response fan-out | Synthetic incident | Create comms/engineering/security tasks. | In-process or local | Multiple child nodes created. |
| `S16` | Folder intake summary | Synthetic folder | Summarize folder contents and file paths. | In-process or local | Summary lists paths. |
| `S17` | File-save workflow | Synthetic text input | Write file and record saved path. | In-process or local | Summary lists non-asset file path. |
| `S18` | Project subtree summary | Seeded project nodes | Include parent and subtree context. | In-process or local | Input preview and output reflect subtree. |
| `S19` | Prompt/session cleanup plan | Seeded prompt nodes | Generate cleanup actions. | In-process or local | Result nodes under workflow node. |
| `S20` | Compliance checklist extraction | Synthetic compliance memo | Create checklist summary and risks. | `gptoss20b64k` | Output grounded and status completed. |

## Harness Proof

- `ProjectStructureWorkflowScenarioHarnessTests` ran all 20 scenarios through project-structure workflow nodes using the same add-options, create-node, start, status, and readback API path used by the canvas.
- SQLite artifact: `.codex/bundles/project-structure-workflow-runs/proof/scenarios/scenario-harness-results.json`.
- PostgreSQL artifact: `.codex/bundles/project-structure-workflow-runs/proof/scenarios/scenario-harness-postgresql-results.json`.
- Provider-specific artifact: `.codex/bundles/project-structure-workflow-runs/proof/providers/provider-validation-results.json`.
- Provider-specific `gpt-5-mini` validation used the Mouser order case and completed both provider chat and workflow-run proof with marker `OPENAI-MOUSER-CHECK`.
- Provider-specific local Ollama `gptoss20b64k:latest` validation used the SEAMARK folder case and completed both provider chat and workflow-run proof with marker `OLLAMA-SEAMARK-CHECK`.
