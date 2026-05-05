---
name: candoitall-api-agents
description: Use when managing CanDoItAll agents, providers, capabilities, chat sessions, execution runs, approvals, artifacts, logs, metrics, and runtime snapshots through the HTTP API.
---

# CanDoItAll Agents API

Use this skill when a task needs agent catalog, provider, chat, execution, approval, or diagnostics control through the CanDoItAll web API.

## Access

- Start the CanDoItAll web app and inspect Swagger/OpenAPI at `/swagger`.
- Check `/api/access/status` before assuming bearer tokens are required.
- If JWT is active, send `Authorization: Bearer <token>`.

## Catalog And Configuration

- Agents: `GET /api/agents`, `GET /api/agents/bootstrap`, `GET /api/agents/{agentId}`, `POST /api/agents`, `DELETE /api/agents/{agentId}`, clone, convert-to-template, export, and import routes.
- Providers: `/api/agents/providers`, `/providers/{providerId}/editor`, create/delete/test/test-chat, and Ollama modelfile routes.
- Capabilities: `/api/agents/capabilities`, `/capabilities/{capabilityId}/editor`, create/delete, and per-agent capability verification.
- Memory: `/api/agents/{agentId}/memory`, `POST /api/agents/memory`, and delete memory routes.

## Chat And Execution

- Chat sessions: `/api/agents/{agentId}/chat-sessions`, rename, chat workspace, and `/chat`.
- Execution runs: `POST /api/agents/execution-runs`, `POST /api/agents/{agentId}/execution-runs`, list routes, and run detail routes.
- Approvals: `/api/agents/execution-runs/{executionRunId}/pending-approvals` and run approval listing.
- Evidence: execution artifacts, checkpoints, tool receipts, execution log, runtime snapshot, and metrics routes.

## Operating Rules

- Prefer agent-scoped routes when you already know `agentId`; use global execution-run routes for cross-agent review.
- For debugging, query run detail first, then fetch artifacts/checkpoints/receipts/log only for the run under review.
- Use provider test routes before assigning a provider to production-like agents.
- Use capability verification before assuming a tool or skill is usable by an agent.

## Validation

- For created/updated agents, read back the agent editor/detail.
- For execution, verify run state, artifacts, receipts, and metrics instead of relying on a single status field.
- For provider changes, run provider health/test-chat when credentials and model availability are relevant.
