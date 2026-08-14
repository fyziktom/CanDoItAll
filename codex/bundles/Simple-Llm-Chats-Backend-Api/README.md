# Simple LLM Chats — Backend and HTTP API

Implementation bundle for the first production activation of ordinary multi-turn LLM chats in
CanDoItAll. The scope is deliberately limited to backend/domain, persistence, runtime composition,
and HTTP API. No product UI, floating-chat integration, shared-component refactor, Project Structure
context provider, attachment upload surface, voice surface, or browser automation belongs to this
bundle.

Prepared against:

- repository: `fyziktom/CanDoItAll`
- branch: `development`
- baseline commit: `c0117109c6ef6166d1d8b1b42d75e7f4af83c5ee`
- baseline message: `Merge branch 'unix-adoption' into development`
- prepared: `2026-08-14`
- runtime target: `.NET 10`
- supported hosts: Windows, Linux, and macOS
- canonical database: PostgreSQL through the existing `AppDbContext` and migrations project

## Outcome

After this bundle is implemented and its final gate passes, CanDoItAll must provide:

1. reusable simple-chat definitions with immutable revisions, including model-specific thinking-effort setup;
2. durable ordinary conversation threads and messages in PostgreSQL;
3. profile-generation-fenced provider execution;
4. persistent operation idempotency, cancellation, recovery, and usage audit;
5. a non-UI HTTP API for definition, conversation, turn, operation, and recovery flows;
6. focused API and PostgreSQL integration proof;
7. stable present-day identities plus documented compatibility boundaries for later Project Structure
   context, attachments, streaming, product UI, and external enterprise-chatbot deployments.

The follow-up in `source/05-thinking-effort-follow-up.md` is part of the locked backend/API scope.
Thinking effort is a typed per-definition-revision setting validated against the selected provider and
model. It reuses the existing provider capability truth used by agents without using agent execution.

## Non-negotiable boundary

A simple LLM chat is **not** an agent with tools disabled.

The ordinary conversation path must remain free of:

- agent execution runs;
- agent sessions and agent catalog identity;
- tools, skills, MCP, memory, approvals, finalizers, handoffs, and process semantics;
- workspace mutation authority;
- MAF agent construction.

Inference continues through `ILlmInvocationPort`. Canonical transcript behavior continues through the
ordinary-conversation layer. Product semantics belong to the new LLM Chats module.

## Start here

1. Read `CODEX-EXECUTION-CONTRACT.md`.
2. Read `architecture/00-current-state.md` through `architecture/11-deferred-work.md`.
3. Run `python scripts/validate_bundle.py --bundle-root .`.
4. Execute exactly one unlocked subbundle at a time, starting with
   `subbundles/SB00-baseline-and-decision-lock`.
5. Do not run a solution-wide test suite before `SB11-final-regression-and-release-gate`.

The authoritative execution order is `plan/01-execution-order.md`.
