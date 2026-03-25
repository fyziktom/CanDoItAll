# Architecture And Scope

## Chosen architecture

The tray app will sit on top of the existing bundle-2 backend architecture.

It will reuse:

- the machine-wide backend catalog under local app data
- existing backend manager HTTP endpoints
- the existing backend dashboard webpage
- the wrapper shadow-host preparation path

It will add:

- a Windows tray process with persistent polling
- notifications for duplicate, unreachable, or missing backend states
- operator quick actions
- startup and reinstall integration

## Why this approach

- it avoids introducing a second competing control plane
- it keeps the tray app light enough not to affect watch performance
- it works with the detached backend model already in production
- it gives the operator manual recovery even when the current Codex session is stale

## Operator actions in scope

- open current backend manager page
- open aggregate backend manager page when available
- refresh status
- start or recover the backend for the current workspace
- open logs folder
- exit tray app

## Health states to surface

- healthy backend for current workspace
- no backend running for current workspace
- duplicate live backends for the same workspace and settings
- unreachable live process still present in catalog
- stale catalog records

## Performance guardrail

The tray app must not interfere with bundle-2 hot-reload timing. Polling must stay lightweight and infrequent enough that it does not perturb the UI loop.
