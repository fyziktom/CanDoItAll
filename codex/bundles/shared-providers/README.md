# CanDoItAll Shared Providers

Implementation bundle for Codex 5.6 ultra.

## Mission

Add enterprise-grade provider sharing between a central CanDoItAll instance and multiple
user-owned CanDoItAll installations while preserving hybrid use of shared and personal
providers.

The central instance owns the real upstream provider profiles and credentials. Only profiles
explicitly published by an administrator are discoverable. A client installation stores one
shared-provider source, discovers the sanitized catalog, imports selected publications as
ordinary local provider profiles, and invokes them through the central instance without
receiving upstream credentials.

The bundle covers:

- central publication and sanitized catalog;
- an OpenAI-compatible inference subset for chat, Responses, streaming, function tools,
  structured output, vision where actually supported, and image generation;
- local source registration, catalog synchronization, selection, reconciliation, and runtime
  projection;
- hybrid shared plus personal providers;
- an opaque access-context reference prepared for a future Enterprise Gateway and Control
  Plane;
- authorization, SSRF defenses, redaction, usage/cost attribution, auditing, and cancellation;
- backend-first validation;
- three real CanDoItAll application containers plus PostgreSQL and a deterministic upstream
  test provider;
- desktop UI, documentation, OpenAPI export, and SharedInfo API skill updates;
- a final stack left running for operator testing.

## Prepared baselines

| Repository | Branch | Commit |
| --- | --- | --- |
| `fyziktom/CanDoItAll` | `development` | `1625b336e4f60ddb64987240c3a3dc485591d20f` |
| `fyziktom/CanDoItAll.SharedInfo` | `main` | `053f8b356fbc8a28bf822e0a051c25804bd81b65` |

These commits are preparation evidence, not checkout instructions. Before every subbundle,
Codex must re-read the current branch, preserve unrelated changes, and reopen affected
decisions when a named trigger has changed.

## Start here

1. Read `CODEX-EXECUTION-CONTRACT.md`.
2. Read `inputs/`, `requirements/`, `current-state/`, and `architecture/`.
3. Load the mandatory current skills from `CanDoItAll.SharedInfo`.
4. Run `python scripts/validate_bundle.py .`.
5. Execute only the subbundle marked `READY` in `STATUS.md`.
6. Complete its proof manifest and `SESSION-HANDOFF.md`.
7. Unlock the next subbundle only when its progression gate passes.

A ready-to-paste kickoff is in `START-CODEX-PROMPT.md`.

## Architectural headline

This is not implemented by exposing internal `ProviderProfile` records and not by copying
upstream secrets to clients.

The design separates:

- **publication**: a central, explicit public projection over a local provider profile;
- **catalog protocol**: a versioned CanDoItAll contract for discovery and capabilities;
- **inference protocol**: a deliberately bounded OpenAI-compatible surface;
- **source**: one client-side central endpoint and credential reference;
- **import**: a stable relationship between a remote publication and a local provider profile;
- **runtime projection**: a shared connector that materializes an OpenAI-compatible local
  provider without creating a second agent runtime;
- **access context**: an opaque request-scoped reference, independent of authentication;
- **usage observation**: metadata and provider usage only, never prompt or response content.

## Non-negotiable delivery rule

UI work is locked until SB07 proves the backend through real HTTP, persistence, authorization,
streaming, synchronization, failure behavior, and three separate CanDoItAll app instances.
