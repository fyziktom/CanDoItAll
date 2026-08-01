# Security Policy

## Supported Versions

The `main` branch is the supported development line. This repository does not publish a
supported public package release channel.

## Reporting A Vulnerability

Use the repository's
[private GitHub security advisory form](https://github.com/fyziktom/CanDoItAll/security/advisories/new).
Do not publish exploit details, credentials, private data, or sensitive proof in a public
issue.

If the advisory form is unavailable, contact the `fyziktom` account on LinkedIn only to
arrange a private reporting channel. Do not include vulnerability details in a public
message.

Include the affected application area, commit, reproduction steps, expected impact, and
any safe mitigation already tested.

## Scope

Security reports may cover:

- authentication, authorization, API-token, and OpenAPI exposure
- secrets, provider credentials, OAuth integrations, and configuration handling
- agent tools, approvals, capability policy, prompt injection, and workspace isolation
- process, workflow, plugin, Memory-provider, MCP, and external-system boundaries
- PostgreSQL data isolation, migrations, leases, and persisted execution state
- file upload, generated artifacts, and local desktop integration
- sensitive CRM/HR, workforce, or project data disclosure

Report vulnerabilities in sibling-owned servers, packages, or reusable skills to their
owning repository unless this repository's integration contributes to the issue.
