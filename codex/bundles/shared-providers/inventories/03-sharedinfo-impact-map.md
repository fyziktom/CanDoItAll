# SharedInfo impact map

## New skill

Preferred path:

`codex/skills/candoitall-api-shared-providers/SKILL.md`

It must cover:

- native catalog;
- OpenAI-compatible base;
- access/status and scopes;
- source setup and synchronization;
- compatibility limits;
- function tools remain client-executed;
- denied built-in tools/storage/background;
- access-context header;
- errors, streaming, images;
- route appendix parity markers;
- maturity/status wording;
- live OpenAPI precedence and snapshot provenance.

Suggested references:

- `references/catalog-and-import.md`
- `references/openai-compatibility.md`
- `references/security-and-access-context.md`
- `references/examples.md`

## Shared OpenAPI

Update:

- `_candoitall-api-shared/references/candoitall-web.openapi.json`
- `_candoitall-api-shared/manifest.json`
- `_candoitall-api-shared/README.md`

Manifest changes:

- final CanDoItAll commit/worktree provenance;
- capture time and host;
- SHA-256;
- operation/schema counts;
- new documented operation set `sharedProviders`;
- exact route/method list;
- validator/version fields already used by current schema.

## Skill/index/install surfaces

Search current SharedInfo conventions and update all required:

- skill index/catalog;
- install/copy scripts;
- validation allowlists;
- route-parity validator inputs;
- README links;
- active-skill synchronization if the repository owns a generated active copy.

Do not guess paths; SB11 discovers current generation flow and records it.

## Validation

At minimum:

- `tools/validation/Test-CanDoItAllWebOpenApi.ps1`
- `tools/validation/Test-SharedInfo.ps1`
- current skill route-parity validator
- hash/provenance verification
- clean source scan for stale branch/commit metadata.
