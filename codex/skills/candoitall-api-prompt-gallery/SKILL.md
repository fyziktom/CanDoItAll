---
name: candoitall-api-prompt-gallery
description: Use when searching, reading, creating, versioning, archiving, checking compatibility, managing warning preferences, or rebuilding the search projection for canonical CanDoItAll prompt items through the HTTP API.
---

# CanDoItAll Prompt Gallery API

Operate the canonical prompt and prompt-part library through the CanDoItAll web API. Do not write reusable prompt content through workflow-component endpoints; workflows consume Gallery items and immutable versions.

## Access

- Start the web app and inspect `/swagger` or `/openapi/v1.json` before generating a client.
- Check `/api/access/status` before assuming bearer authentication is disabled.
- If JWT is active, send `Authorization: Bearer <token>`.
- Before exposing the host outside a trusted local environment, set `Api:Authorization:Enabled` to `true` and provide a secret signing key of at least 32 bytes. The repository's local-first defaults do not protect mutation or projection endpoints.

## Browse And Read

- Search with `GET /api/prompt-gallery/items`.
- Use `text`, repeated `tag`, `kind`, `status`, `includeArchived`, `favoritesOnly`, `provider`, `model`, `pageIndex`, and `pageSize` query values.
- Read an item with `GET /api/prompt-gallery/items/{promptId}`.
- Read an immutable version with `GET /api/prompt-gallery/items/{promptId}/versions/{versionId}`.
- Page instead of fetching the entire catalog. Treat returned totals as point-in-time search metadata.

## Change Items

- Create or update a draft with `POST /api/prompt-gallery/items`.
- Supply the last-read `expectedUpdatedAtUtc` when updating; on a conflict, reread and reconcile instead of overwriting.
- Create an immutable version with `POST /api/prompt-gallery/items/{promptId}/versions`.
- Archive with `POST /api/prompt-gallery/items/{promptId}/archive`; do not hard-delete reusable content.
- Set favourite state with `POST /api/prompt-gallery/items/{promptId}/favorite`.
- Preserve a supplied item id only when importing or synchronizing an established identity.
- Treat source keys and provenance as import identity. Do not overwrite user-edited content merely because a packaged seed has the same source key.

## Compatibility And Search Projection

- Evaluate provider/model compatibility with `POST /api/prompt-gallery/compatibility/evaluate` before inserting into an agent chat or workflow.
- Store a suppressible selection warning preference with `POST /api/prompt-gallery/warning-suppressions` only after explicit user choice.
- Never suppress execution-blocking compatibility errors.
- Read projection state with `GET /api/prompt-gallery/projection`.
- Run `POST /api/prompt-gallery/projection/rebuild` only when the configured projection driver is enabled. A disabled outcome is expected when no RAG/vector driver is configured.

## Operating Rules

- Gallery owns title, kind, tags, favourite state, prompt text, versions, provenance, portable provider/model compatibility, an optional preferred pair, and parameter recommendations.
- Workflow definitions own execution topology, provider profile selection, permissions, ports, and an immutable prompt snapshot/reference.
- Read an exact version for reproducible workflow execution. Use current draft content only for interactive authoring.
- Keep prompt content out of logs, URLs, and error messages. Log ids, version numbers, source keys, and content lengths instead.
- Validate mutations by reading the item or immutable version back.
