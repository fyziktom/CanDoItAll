# Implement API access users and selected API-only UI support

You are the implementing senior C#/.NET architect and engineer. Work in the main CanDoItAll repository on the current `development` line in preparation for a later development-to-main merge. Complete the implementation, focused repairs, real API validation and Settings browser validation in one sustained task.

This is **not** a CanDoItAll workflow bundle. Do not create bundle scaffolding, subbundle gates, artificial approval checkpoints or a plan-only deliverable. Maintain a short working plan and evidence log, make coherent implementation steps, and continue until the acceptance criteria are satisfied or a concrete environmental blocker is documented. You may improve internal decomposition and names where the current code warrants it, but preserve the security and scope boundaries below.

## Repository orientation

Read the repository's current `AGENTS.md`, `.github/copilot-instructions.md`, `docs/testing.md`, `.github/workflows/ci.yml` and `docs/architecture/ui-component-seams.md`. Use the applicable SharedInfo standards and `candoitall-codeanalytics-mcp` skill. These standards apply; their bundle ceremony does not.

The source analysis accompanying this prompt is pinned to:

- development: `b82ffc57283f5e4819d82322e1c5bf836dcd9536`;
- UI reference: `e101d5db1478ea329a572db79c0104b927d97f15`;
- merge base: `a2903c400cc35e6d1d2f233c51e73feb256ce2aa`.

Verify the actual local HEAD, branch, working tree and sibling dependencies first. Preserve unrelated work. If development advanced, reconcile this handoff with the newer implementation; do not reset it to the reviewed SHA. Treat the UI branch as a read-only reference. Compare its changes against the common ancestor, then check whether development already implements each behavior. A direct development-versus-UI diff is not a ready-to-apply patch.

Read all supporting documents in this directory before choosing the implementation details. Source paths and symbols are starting points, not permission to reintroduce an obsolete owner or dependency.

## Product objective

An API-only UI will access a CanDoItAll instance on a server. The existing Blazor SSR UI remains a trusted operator surface on a desktop or protected administrative path. Container deployments expose only explicitly allowed API traffic through a proxy and do not expose SSR, its interactive transport or developer endpoints. This is an internal PC/LAN/VPN application, not a public SaaS identity service.

Implement:

1. Minimal API accounts with stable IDs, username, display name, enabled state, password hash and explicit API permissions. Administrators create, edit, disable, delete and reset passwords. No registration, email delivery, password recovery links or CRM identity integration.
2. A configured administrative identity whose password **hash** comes from deployment configuration/environment. No built-in password and no silent fallback. The supported simple development path is to leave the new surfaces disabled, or supply a developer-owned hash and use the real flow.
3. Login that issues a fresh registered user-session JWT. A user can log in again after expiry without asking an administrator to issue another token. Do not add refresh tokens unless a concrete requirement cannot be met without them; the agreed baseline intentionally does not require them.
4. A separately enabled, strictly administrator-only HTTP surface for managing API users and machine-token metadata/lifecycle, including the existing token issuance route. Neither an open API mode, broad `api`, a claimed role nor an old `api.tokens.issue` token may silently bypass this administrative boundary.
5. Server-side API-section enforcement for users **and** machine tokens. Keep existing explicit privileged scopes and resource authorization checks. A narrowly scoped shared-provider token must not gain access to unrelated workflow/project/settings endpoints merely because its signature is valid.
6. Selective integration of the colleague's useful API additions: process-definition reads, workflow-template listing/draft creation, workspace-settings reads/writes, and missing accurate OpenAPI contracts. Do not port the UI/theme refactor or overwrite development hardening.
7. Extension of Settings → API access to manage users, show effective configuration and continue managing machine tokens. No new application-wide SSR login requirement.

## Security and compatibility invariants

- Preserve existing service/client JWT operation, signing-key/issuer/audience configuration, machine token defaults, registry revocation and shared-provider use. Tightening unintended cross-section access is intentional; silently invalidating all machine credentials is not.
- Distinguish legacy tokens, registered machine tokens and registered user/admin sessions using an unambiguous validation path and authoritative server-side metadata. Never infer a user or administrator from an arbitrary `sub`, `name`, `role` or scope in a machine token.
- Existing accepted legacy tokens without a managed-token marker are not automatically administrators. Do not accidentally remove their legitimate data access or give them new rights. Preserve existing claim mapping unless a compatibility-tested change is necessary.
- Administration via HTTP requires a validated administrative session and the independent host exposure switch. This includes direct calls to administrative application services; a hidden button is not authorization.
- Trusted local UI administration remains a separate server-side capability. It must never authenticate a raw HTTP API request, a forwarded request or an arbitrary background scope as a local operator.
- Password reset, disable, deletion and permission changes invalidate existing affected user sessions on subsequent authorization. Re-enabling an account must not resurrect its old sessions. Do not revoke unrelated machine credentials on a user change.
- User-selected permissions and administrator authority must come from the server, not login JSON. Reject attempts to overpost privileged fields. Empty business permissions must never become broad `api` access.
- No signing key, password hash, token plaintext, secret configuration or credential fingerprint in list/status DTOs, OpenAPI examples, normal logs, screenshots or test reports. Token values are one-time responses with no-store headers.
- Keep bootstrap secrets and exposure switches deployment-owned. Do not provide a generic HTTP configuration writer that can enable its own administrative API or read/replace signing secrets.
- Preserve existing domain ownership/approval checks, sandboxing, provider publication grants and resource-level checks. API accounts are not a new application-wide identity/ownership model.
- Preserve the intentional open business API mode. Clearly show that user scope isolation is not enforced on anonymously open business endpoints; never pretend an open mode is a protected multi-user deployment.
- The new login/admin/settings routes must not accidentally sit outside configuration or policy groups. Verify actual endpoint metadata and HTTP behavior, including error-pipeline re-execution, aliases, streams and companion download routes.

## Architectural latitude

Use the small architecture in `docs/03_ARCHITECTURE.md`. Prefer extending existing ApiAccess application services and the private control-plane persistence pattern rather than creating a new platform. Do not introduce a Foundation → Modules dependency or make shared-provider runtime depend on a Workspace UI component. Transport adapters belong in Web; state and business rules do not belong in Razor handlers.

A single configured administrator plus ordinary persisted API accounts is sufficient for this task. Ordinary account CRUD must not expose administrator promotion. Keep the configured administrator visible as configuration-owned and recoverable by the deployment operator. Multiple administrative roles, SSO and EGCP are future work.

The proposed names/routes/options may be adjusted to fit repository conventions. Preserve the user-visible behavior and record deliberate deviations, especially any client contract difference from `ui-refactoring-v2`. Do not broaden the task into unrelated module extraction or a full authentication rewrite.

## Efficient verification

Use `candoitall_codeanalytics` and its canonical `code_analytics_impacted_tests_get` tool to select tests from **actual changed paths and line ranges**. The applicable skill in SharedInfo is `codex/skills/candoitall-codeanalytics-mcp/SKILL.md`.

The product solution intentionally contains no tests. Supply the appropriate separate test workspaces. Use `changes` only for actual edits and `contextOnlyPaths` for inspected files. This task changes authorization behavior and contracts: use `BehaviorChange`, `ContractOrShapeChange` or the initial conservative `Unknown`, not a false behavior-preserving assertion. Inspect workspace health, unresolved ranges, required selectors, conditional selectors and promotion triggers. Correct missing/zero discovery; do not treat it as no impact.

Build changed production projects and refreshed owning test assemblies, discover the selected tests, record expected/actual counts, then execute the narrow selectors. Re-query impact after a coherent change expands the diff. Do not run every suite after each edit. Equally, do not discard a wider required selector merely because it is inconvenient. At final closure consider the named auth/DI/contract changes as explicit reasons for a broader gate if required by repository guidance, rather than doing repeated unfiltered runs.

Run the mandatory `portability-static` procedure and final enforcement without `--write-baseline`; reviewed baseline updates are not a substitute for repairing real findings. Use the repository's current commands, not guessed scripts.

Unit tests, fake principals and component mocks are not final security proof. Execute the real JWT handler and HTTP pipeline, then a running application and Playwright Settings journeys. Use isolated test data, the real private account/token persistence and synthetic or local provider fixtures. No unapproved paid LLM calls. Cover the positive/negative cases in `docs/06_VALIDATION.md`, including administrator bootstrap, account changes, token expiry/revocation, section isolation, disabled surfaces, legacy clients, proxy/local-operator separation and browser mutations.

The existing integration harness can support fast iterations with its documented test-only database options. Final deployment proof must also exercise the production composition and its real supported persistence, not only a replacement host with a fake authentication scheme. Report blocked PostgreSQL/container/browser lanes honestly; do not substitute mocks and claim closure.

## Working tree, commits and language

Keep code, code comments, UI text, documentation, tests, examples, filenames and commit messages in English. A final conversation summary may be in Czech. Do not add Radzen; use the current shared components and, where useful, the Components MCP.

Commit coherent validated work according to repository policy, preserving GPG signing. Reuse the existing unlocked GPG agent and compatible shell/session where possible. Do not disable signing, export keys, save passphrases, alter global GPG cache/security settings, or rewrite signed history. If signing cannot complete, leave the work intact and report that blocker instead of creating unsigned commits. Do not merge development into main, push, open a PR or modify the colleague's branch without separate authorization.

## Required final deliverable

Deliver working code and maintained operator/API documentation, not only a proposed design. Include a concise implementation report with:

- actual base/final HEAD, changed owners and signed commits or the precise signing blocker;
- what was integrated from each UI-branch API delta and what was already present;
- the configuration truth table and explicit compatibility/security changes;
- the source of administrator credentials and the documented hash-generation/setup flow;
- the actual affected-test discovery and execution results, mandatory static result, real HTTP and browser evidence, and persistence/restart proof;
- all failed, skipped, unavailable or deferred checks, clearly separated from passing ones;
- residual limitations, with no claim that the development-to-main merge has been performed or that the whole application is internet-hardened.

Keep working through ordinary failures and repair them. Only concrete environmental blockers justify incomplete lanes. Never label a task complete solely because the compiler passes or the browser renders the settings form.
