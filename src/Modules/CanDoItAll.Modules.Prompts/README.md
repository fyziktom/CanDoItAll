# CanDoItAll.Modules.Prompts

## Purpose

Product module for prompt library management and prompt assets used by users and agents.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Prompts/CanDoItAll.Modules.Prompts.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Modules.Prompts.csproj](CanDoItAll.Modules.Prompts.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

This module owns product semantics for its bounded area. Keep business behavior here and expose it through typed services, Razor components, and module contracts. UI and transport adapters should call into these services instead of duplicating module logic.

## Runtime persistence and integration

PromptsDbContext maps the ten existing Prompt Gallery record types explicitly. The
same artifact/version/tag/usage mappings and UpdatedAtUtc concurrency token are used
by the complete canonical migration model. Its pooled factory binds to the host's
immutable database profile; normal reads and writes never redirect through another
owner's ambient transaction. No new schema or data copy is introduced.

PromptsService remains the single draft/version/import writer. Search, seed import
and projection reads use the same bounded owner model. SearchIndexPromptGalleryProjectionDriver
translates projection data for SearchProjectionStore, which owns the actual index
writes and retains the existing advisory-lock identity, bounded batches and atomic
rebuild. A canonical Prompt state check explicitly enlists and locks the source row
before an incremental index mutation, so a delayed projection cannot replace a
newer canonical state. Derived index and activity errors remain logged after commit.

IPromptArtifactProjectionQueryService supplies project-scoped projection facts and
binding membership without exposing entities or a context. Independent reads and
explicitly enlisted mutation reads are separate methods. IPromptGalleryMutationService
stages creation through the same draft writer inside the caller's coordinated
transaction, returning a preparation with the original receipt and activity fields.
The caller must invoke CompleteDraftCreationAsync only after its commit and scope
release. Completion checks canonical existence and never reapplies the draft over
intervening edits. This preparation is an in-process transaction handoff, not a
durable retry intent or a separate writer.

Normal API payloads, version IDs, pinned Workflow content, tags, recommendations,
compatibility, archival/favorite behavior, seed provenance and curator operations
remain unchanged. Workbench's remaining direct Prompt reads/staged caller cutover,
project lifetime admission, the explicit global transfer-maintenance adapter and
product-specific tool policy in Core remain dependent boundaries.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
