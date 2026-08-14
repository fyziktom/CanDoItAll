# SB07 — HTTP definition and conversation API

Proof tier: **Governed**

## Objective

Expose definition and conversation catalog behavior through thin Web adapters.

## Scope

- Add LlmChatsApi route group using current Web mapping conventions.
- Add bounded transport DTOs and mapping helpers.
- Implement definition list/create/get/update/activate/suspend/archive routes.
- Implement a sanitized provider-options route with per-model thinking-effort capability/default
  projections and typed nullable effort input/output on definition DTOs.
- Implement conversation list/create/get/rename/archive routes.
- Add expected concurrency/ETag handling and stable ProblemDetails mapping.
- Apply current API authorization conventions.
- Add OpenAPI metadata.

## Expected change surface

- src/App/CanDoItAll.Web/Api/LlmChatsApi.cs and focused mapping files
- Web composition mapping
- API response/error tests

## Targeted validation

- LlmChatsDefinitionApiIntegrationTests
- LlmChatsConversationApiIntegrationTests
- DTO boundary/source guard tests

All test commands must comply with `test-budget.json`. Record exact commands and results in the proof
manifest.

## Acceptance criteria

- [x] API does not expose EF entities or generic internal documents.
- [x] Provider input is stable IDs and validated options only.
- [x] API exposes per-model thinking-effort availability without credentials/configuration and rejects unsupported or duplicate JSON effort input.
- [x] Lists are bounded and pageable.
- [x] Definition revision is visible in conversation responses.
- [x] Authorization and errors follow current conventions.

## Forbidden work

- business logic in endpoint lambdas
- full ProviderProfile request DTO
- UI pages/components

## Handoff

Complete `SESSION-HANDOFF.md` and `proof/proof-manifest.json`. Do not unlock the next subbundle until
all acceptance criteria and any checkpoint are satisfied.
