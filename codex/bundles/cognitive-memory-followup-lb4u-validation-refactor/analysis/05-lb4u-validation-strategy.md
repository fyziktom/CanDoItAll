# LB4U Validation Strategy

## Validation Shape

The LB4U test must run like a person building a project over time:

- Start with product discovery and presentation notes.
- Add architecture and installation details.
- Add procurement spreadsheets.
- Add custom button engineering files.
- Add release and business plan material.
- Run consolidation cycles between stages.
- Probe memory after each stage.
- Approve useful recommendations and reject weak ones through review endpoints.
- Run final recall and cross-project knowledge checks.

## Expected Good Memory

- The system remembers LB4U as a care-call/patient-button project, not just a collection of file names.
- Source chunks map to original files and sections.
- Installation steps become procedure-like memory.
- Button engineering constraints become requirements and risks.
- Procurement spreadsheets become structured cost/BOM evidence.
- Business-plan sections become a mix of LB4U-specific facts and reviewed reusable planning knowledge.
- Probes return traceable context and distinguish source-backed facts from inferred recommendations.

## Expected Bad Memory

- Large undifferentiated document summaries.
- Repeated generic summaries such as "classified source item as reflection".
- Recall that depends on current chat context instead of durable memory.
- Business-plan advice with no LB4U provenance.
- Missing spreadsheet facts because only text documents were extracted.
- Silent Ollama truncation.
- Secret-file names or contents appearing in any prompt, memory, or recall result.

## Minimum Test Cycles

| Cycle | Provider | Purpose |
| --- | --- | --- |
| 1 | OpenAI `gpt-5-mini` | Stage import and first consolidation. |
| 2 | OpenAI `gpt-5-mini` | Human-style probing, review decisions, deeper study, second consolidation. |
| 3 | OpenAI `gpt-5-mini` | Regression probes and final cross-project knowledge check. |
| 4 | Ollama `gptoss20b64k` | Local model parity, output token limit, and truncation behavior. |

## Evidence To Capture

- Ingestion manifests and operation ids.
- Snapshot counts before and after each stage.
- Review item ids and decisions.
- Consolidation run ids and candidate summaries.
- Probe session ids, prompts, answer summaries, and context sources.
- Token/output-length metadata where available.
- Test command outputs.
- Any UI/browser proof for review pages or memory pages touched by the work.
