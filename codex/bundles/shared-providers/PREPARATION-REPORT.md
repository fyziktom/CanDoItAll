# Preparation report

Prepared on 2026-08-24 for Codex 5.6 ultra.

## Repository evidence baseline

- `fyziktom/CanDoItAll`, branch `development`, commit
  `1625b336e4f60ddb64987240c3a3dc485591d20f`.
- `fyziktom/CanDoItAll.SharedInfo`, branch `main`, commit
  `053f8b356fbc8a28bf822e0a051c25804bd81b65`.

The baseline is preparation evidence. Every subbundle requires re-entry against the current
operator-supplied branch before editing.

## Material current-state findings

1. Workspace EF `ProviderProfile` data is the current master provider configuration. The
   AgentFramework catalog is a runtime projection and must not become a second writable source
   of truth.
2. Internal AgentFramework provider request records contain complete internal provider
   profiles and binary payloads. They are not safe HTTP DTOs.
3. Ordinary MAF agent creation selects SDK/client behavior from the effective provider kind and
   transport. A cross-cutting `ProviderKind.Shared` switch would spread the remote concept into
   inner runtime code.
4. Provider setup is already manifest/schema driven, but shared-source identity/credential and
   per-publication imports require separate relational ownership rather than repeated profile
   configuration JSON.
5. The current API has a common `/api` group, optional JWT bearer authorization, granular
   scopes, standardized errors, and generated OpenAPI. Shared providers must reuse those
   conventions while returning OpenAI-style errors only on the compatibility surface.
6. Provider usage models currently distinguish Agent and Simple Chat work. A central relay
   invocation must not be falsely classified and must not create a duplicate cost ledger.
7. Repository testing explicitly requires affected builds and focused filters with discovery
   counts. Stable, browser, live-process, and Docker lanes are separate frozen gates.

## Locked target decisions

- Use explicit central publication, client source, and import concepts.
- Store a source credential reference once; never copy upstream credentials or one token value
  into every imported provider.
- Use a native versioned catalog for discovery/synchronization and a bounded
  OpenAI-compatible surface for inference.
- Keep public wire records independent of Workspace EF and MAF internal records.
- Isolate HTTP/protocol details in `SharedProviders.Http`; keep stable ports/records in
  `SharedProviders.Abstractions`; wire implementations in Web/Composition.
- Project an imported shared provider into the existing OpenAI-compatible AgentFramework path
  instead of introducing a shared-provider branch throughout inner MAF code.
- Preserve hybrid use. A shared-provider outage is explicit and never silently falls back to a
  personal provider.
- Carry `CanDoItAll-Access-Context-Ref` as opaque request metadata independent from
  authentication and W3C tracing. Strip it before calling the actual upstream provider.
- Support only capabilities proven by exact positive and negative contract tests. The v1 scope
  includes chat completions, Responses, streaming, function tools, structured output/vision
  where supported, and image generation. Audio routes remain absent unless SB00 finds and the
  bundle implements real production drivers and tests.
- Require a three-CanDoItAll-instance backend checkpoint before UI.
- Leave the final central/client-a/client-b stack healthy and running for manual operator tests.

## Prepared proof model

- Thirteen dependency-ordered subbundles.
- Only SB00 is initially executable.
- Each work unit declares ownership, dependency direction, selected pattern, testability,
  partial-class policy, positive/negative evidence, and progression/reopen rules.
- UI is hard-locked behind SB07.
- The stable aggregate is budgeted exactly once in SB12.
- The Docker multi-instance lane is budgeted once for the backend checkpoint and once for final
  clean closure.
- OpenAPI capture is budgeted once after the external contract freezes.

## Preparation validation

The included validator completed with:

```text
PASS: bundle is structurally ready (0 warning(s))
```

Additional preparation checks parsed every JSON file, found no TODO/TBD/FIXME placeholders,
and found no common private-key/API-key/password literal patterns. These are preparation-time
checks only; execution proof must repeat applicable secret, content, dependency, test, and
artifact validation against implemented code.
