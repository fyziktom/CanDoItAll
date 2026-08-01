# Documentation

This directory contains durable engineering documentation for the application. Product
positioning and general information belong on [aicandoitall.com](https://aicandoitall.com).
Source code, project files, runtime composition, endpoint mapping, and migrations remain
authoritative.

## Architecture

- [Architecture overview](architecture/overview.md)
- [Internal communication](architecture/internal-communication.md)
- [Module map](architecture/modules.md)
- [Agent output contracts](agent-output-contracts.md)
- [Agent runtime tool surface](agent-runtime-tool-surface.md)
- [PostgreSQL runtime canonicality](postgresql-runtime-canonicality.md)

## Development And Operations

- [Development runtime](development-runtime.md)
- [Installed Windows web app](operations/installed-web-app.md)
- [Testing](testing.md)
- [Secure configuration](secure-configuration.md)
- [Container operations](operations/containers.md)
- [Development PostgreSQL backup and restore](operations/backup-and-restore.md)
- [Process agent operator runbook](process-agent-operator-runbook.md)

## Integration Contracts

- [API control plane](api-control-plane.md)
- [CRM/HR API](crm-hr-api.md)
- [OAuth email plugins](oauth-email-plugins.md)
- [Provider capability and pricing](provider-capability-and-pricing.md)
- [Memory providers](memory-providers/README.md)
- [Shared UI component boundary](ui-shared-components/README.md)
- [UI support scope](ui-support-scope.md)

Project-level navigation begins at [`src/README.md`](../src/README.md). Repository policy
is defined in [`CONTRIBUTING.md`](../CONTRIBUTING.md),
[`SECURITY.md`](../SECURITY.md), and [`LICENSE`](../LICENSE).
