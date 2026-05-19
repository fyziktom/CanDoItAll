# Cognitive Memory API Notes

This document captures the operational API shape used by the LB4U follow-up validation.

## Runtime Checks

- `GET /api/access/status` tells whether bearer tokens are required.
- `GET /api/cognitive-memory/status` must report an active PostgreSQL profile before multi-cycle consolidation, recall, or projection validation.
- Qdrant remains a rebuildable projection store. Durable memory facts, evidence, review decisions, traces, and proposals are in the app database.

## Model Execution Profiles

`GET /api/cognitive-memory/settings` and `PUT /api/cognitive-memory/settings` include `modelExecutionProfiles`.

Each profile is role-scoped:

```json
{
  "role": 3,
  "providerProfileId": null,
  "modelId": { "value": "gpt-5-mini" },
  "maxOutputTokens": 4096,
  "timeoutSeconds": 120,
  "localOnly": false,
  "notes": "Default OpenAI cognitive-memory execution profile."
}
```

Default OpenAI validation uses `gpt-5-mini` with `maxOutputTokens = 4096`.

Local Ollama validation uses `gptoss20b64k` with `maxOutputTokens = 8192`, `timeoutSeconds = 180`, `localOnly = true`, and `modelAccessMode = LocalProvidersOnly`. Also validate Ollama directly with `options.num_predict = 8192` so the local server is not silently limiting output.

## External Source Ingestion

Use `POST /api/cognitive-memory/external-sources/files` for `multipart/form-data` uploads.

Supported extraction paths:

- `.docx`: structural document text.
- `.pptx`: slide text.
- `.xlsx`: worksheet cell text.
- `.pdf`: page text.
- text-like files: bounded UTF-8 text.

Unsupported binary files should fail clearly instead of producing fake UTF-8 content.

For staged project ingestion, maintain a manifest rooted at the read-only source directory. Excluded paths must be validated by resolved path only. Do not read, summarize, upload, log, or probe excluded password/router files.

External-source run idempotency is operation-scoped. Reusing a caller key for alternate representations of the same logical source must not collide across distinct upload operations.

## Consolidation Quality

Consolidation candidates should be source-backed facts, not raw file dumps. The current extractor:

- detects planning dimensions such as business plan, product, marketing, finance and expenses, staffing, procurement, milestones, and validation risk;
- matches Czech/Slovak terms diacritic-insensitively;
- skips contact-heavy chunks so emails, phone numbers, and raw procurement boilerplate do not become canonical memory;
- increments the consolidation algorithm version when extraction semantics change so existing source items can be reprocessed intentionally.

Review contact-only, PII-heavy, or raw boilerplate candidates with `Reject`. Approve only chunks that are useful as project memory.

## Epistemic Drive And Probes

`POST /api/cognitive-memory/epistemic-drive/scans` creates approval-gated learning proposals for source-backed planning gaps. Approving a proposal plans learning work; it must not directly mutate canonical truth. Repeat scans should not recreate proposals for a region that already has a reviewed or pending proposal.

Probe summaries persist:

- the user question;
- selected context section summaries;
- included source references;
- recall warnings.

Persisted probe summaries redact email and phone values before storage.

## Validation Loop

For realistic staged validation:

1. Check access, PostgreSQL profile, settings, and Qdrant readiness.
2. Ingest sources by stage, not all at once.
3. Run consolidation with review items enabled.
4. Approve useful candidates and reject noisy candidates.
5. Run recall and probe questions against product, business-plan, marketing, finance, staffing, procurement, milestone, and risk knowledge.
6. Run epistemic-drive scans, approve useful source-backed proposals, and confirm repeat scans do not duplicate them.
7. Repeat probes after extraction or proposal changes.
8. Validate OpenAI `gpt-5-mini`, then local Ollama `gptoss20b64k` with explicit output-token proof.
