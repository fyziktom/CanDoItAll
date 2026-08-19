# SB01 — Canonical model and generic caller identities

Proof tier: **Governed**

## Objective

Create the product-neutral canonical LLM Chat model and the minimal backward-compatible generic conversation identity extension.

## Scope

- Add CanDoItAll.Modules.LlmChats as a non-Razor SDK project.
- Add focused IDs, enums, definition/current revision/conversation/operation/invocation models, validation constants, normalizers, and deterministic fingerprints.
- Add optional caller-supplied conversation ID and turn ID to generic conversation start/turn requests.
- Add the smallest backward-compatible typed thinking-effort override to `LlmModelSettings`; preserve
  the existing provider-neutral JSON envelope for callers that do not use the typed property.
- Make LlmConversationService consume supplied IDs while preserving existing default GUID behavior.
- Add a typed origin model for current Application/API use and document how a later deployment bundle adds external-channel ownership without changing transcript identity.

## Expected change surface

- new domain project and README
- domain Models, ValueObjects, Validation, Fingerprints folders
- LlmConversationContracts.cs and LlmConversationService.cs minimal additive edits
- solution project entries
- focused unit tests

## Targeted validation

- LlmChatCanonicalModelTests
- LlmChatFingerprintTests
- LlmConversationService caller-ID focused tests
- existing LlmConversationServiceTests narrow regression filter

All test commands must comply with `test-budget.json`. Record exact commands and results in the proof
manifest.

## Acceptance criteria

- [x] Definition and thread title are distinct models.
- [x] Definition revisions are immutable and validated.
- [x] Definition revisions and settings fingerprints distinguish provider default from every explicit thinking effort, including `None`.
- [x] Operation ID can be used as turn ID.
- [x] Existing callers without supplied IDs behave byte-for-byte semantically the same.
- [x] No EF, ASP.NET, UI, MAF, tools, skills, Memory, or Processes reference in the domain project.

## Forbidden work

- persistence implementation
- DI production activation
- API
- generic product metadata

## Handoff

Complete `SESSION-HANDOFF.md` and `proof/proof-manifest.json`. Do not unlock the next subbundle until
all acceptance criteria and any checkpoint are satisfied.
