# Security Policy

## Supported Versions

The current `main` branch is the supported development line. Older commits, feature branches, local installer artifacts, and historical bundle snapshots are not supported unless a release announcement says otherwise. This repository does not currently publish a supported public package release channel.

## Reporting A Vulnerability

Use the repository's [private GitHub security advisory form](https://github.com/fyziktom/CanDoItAll/security/advisories/new). Do not publish exploit details, credentials, private data, or sensitive proof in a public issue.

If the private advisory form is unavailable, contact [fyziktom on LinkedIn](https://www.linkedin.com/in/fyziktom/) only to arrange a private reporting channel. Do not send vulnerability details through a public LinkedIn message.

Include the affected application area, version or commit, reproduction steps, expected impact, and any safe mitigation already tested.

## Scope

Security reports may cover:

- authentication, authorization, API-token, and OpenAPI exposure
- secrets, provider credentials, OAuth integrations, and configuration handling
- agent tools, approvals, capability policy, prompt or tool injection, and workspace sandbox escapes
- process, workflow, plugin, Memory-provider, MCP, and external-system boundaries
- PostgreSQL data isolation, migrations, leases, and persisted execution state
- file upload, generated artifacts, desktop launch, and local installer behavior
- sensitive CRM/HR or project data disclosure

Vulnerabilities in a sibling-owned MCP server, shared component package, native Memory provider, or canonical skill should be reported privately to that repository's maintainer. Include this repository when its integration or configuration contributes to the issue.
