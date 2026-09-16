# CanDoItAll Architecture Foundation — v1.1 English

**Date:** 2026-09-08  
**Audience:** GPT-6 Astra xhigh / Codex, architects, implementers, and reviewers  
**Purpose:** shared normative architecture for bounded refactoring, not an implementation bundle

This is the complete English replacement for v1.0. It retains every original catalog ID and translates/revises the complete foundation, while extending safe cross-module reads and writes beyond Project Structure. No historical implementation subbundles or untranslated instructions are included.

## Start here

Read [START_HERE_FOR_ASTRA.md](START_HERE_FOR_ASTRA.md), [LANGUAGE_POLICY.md](LANGUAGE_POLICY.md), and [evidence/baseline.md](evidence/baseline.md). Then follow [INDEX.md](INDEX.md) and the affected module cards. All subsequent engineering instructions, bundles, documentation, comments, and reports are in English unless a later explicit requirement overrides this. Existing localized product strings and technical identifiers retain their compatibility.

## What changed in this revision

The foundation now explicitly supports an agent, workflow executor, or process step querying or requesting mutations from **the actual owner of CRM, Simple Chats, Resources, Scheduler, Prompts, Projects, Structure, TestLab, and other supported domains**. It does not route unrelated administration through Structure, invent an all-module CRUD gateway, or grant every actor every capability.

Four new chapters specify runtime adapters, managed agents/HR and Simple Chats administration, safe reverse queries/data use, and the command/receipt/recovery protocol. New catalogs distinguish currently observed tools/APIs from requested extensions and map reads/writes for all 24 module/boundary cards. There are 76 semantic contracts, 122 capability records, 110 glossary terms, 52 invariants, 165 planned QA scenarios, 41 gap records, and 22 requirements. These counts describe this package, not the number of interfaces to implement or tests already executed.

### Important current-versus-target distinctions

HR already has owner-backed CRM party creation and affiliation operations in the inspected provider. Preserve them rather than create a second CRM writer. Simple Chats already has a definition application API, but an HR definition-administration tool was absent from the fully inspected HR provider and is a requested extension. Managing its definitions **does not turn Simple Chats into an agent**, add tools/implicit context, or grant transcript access.

The inspected managed HR and Scheduler packs are InteractiveChat-only. Scheduler's inspected tools cover workflow discovery/schedules/create, not general process scheduling. A process assistant/template or HTTP endpoint does not prove a direct Process tool exists. Supporting other agents or background steps requires an explicit compatible adapter, purpose, delegation, and owner policy. Source details and limits are in [runtime-surfaces](catalogs/runtime-surfaces.md) and [sources](evidence/sources.md).

## Binding architecture

CanDoItAll remains a modular monolith. Agents owns technical agent definitions; Providers owns AI prices and pricing evidence. CRM owns parties, local staffing/governance, and technical-agent projections. Projects owns project lifecycle/hierarchy. Structure owns native notes/nodes/placements/links and composes foreign projections; Work Management owns work meaning and assignments. Processes and Workflows own their definitions/runs/checkpoints. Storage owns bytes; Resources owns catalog metadata. Simple Chats remains an ordinary product.

One fact has one logical owner/writer. UI, HTTP, agent tools, workflow executors, and process steps call that owner with typed, authorized operations. Copies are labeled projections, independent local enrichment, or historical snapshots. Never replace current coupling with foreign EF DTOs, a service locator, dual masters, or a giant gateway.

Preserve admission, actual commit, execution completion, required/optional result delivery, and business acceptance as different outcomes. New retry-safe local mutations must atomically record effect and owner receipt. A post-hoc receipt wrapper leaves a crash gap; an outbox cannot guarantee exactly-once for non-idempotent remote effects.

## Sequencing

Finish the **current bounded Agents decoupling checkpoint**, not every future Agents feature. Pause new broad UI extraction over unresolved domain/data boundaries. Refactor the selected owner/contract first; resume its UI decoupling when that seam is stable. Do not wait for the whole application's contexts to be split. Pure presentation fixes may continue where they do not freeze incorrect ownership.

## Product preservation

Keep floating agents over Structure/Gantt, scoped file reads/writes, native notes and tasks, workflow/process launch and result return, current curator/scheduler/HR paths, versioned prompts/providers, CRM participation/staffing, exports, cleanup, and supported host behavior. Closing or switching a view cannot silently retarget an admitted operation. A cleaner dependency graph is not permission to disable useful integration.

The current inventory is a map, not a complete executable audit of every method. Before each slice discover its actual callers, registrations, persistence, serializers, public inputs, and side effects. An absent feature in this catalog is not authorized for removal.

## Evidence and validation

Original observations retain their recorded baseline e9fb82be7a43f843303cbd81233450acb6d24dd0. The targeted revision audit inspected selected files at 504c47d8f3fa085085b3fdbf955b69b9fdfb8856. This does not reverify every historical source at the newer commit. Rebaseline the final stabilized branch before implementation.

**No application build, migration, browser suite, provider call, or live workflow/process was executed in this preparation.** Application QA remains NOT_RUN. The package validator checks structural integrity, references, retained records, language, and hashes only. Reviews are documented architecture/QA perspectives applied during preparation, not an independent human audit.

Run the package-only validator with Python 3.10+:

```bash
python tools/validate_package.py --verify-hashes
```

The ZIP contains Markdown, JSON, a standard-library validator, manifest, and checksums. No production code, credentials, font assets, or implementation subbundles are included.
