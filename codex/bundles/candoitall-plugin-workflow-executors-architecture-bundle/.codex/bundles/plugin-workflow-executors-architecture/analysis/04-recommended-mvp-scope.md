# Recommended MVP Scope

## Include

- New plugin abstractions project.
- New plugin module project.
- Bundled/static plugin catalog source.
- Plugin installation/enabled state.
- Plugin connection/settings persistence.
- Plugin catalog/settings page.
- Plugin health checks.
- Plugin workflow executor bridge.
- One sample bundled plugin that uses:
  - schema settings;
  - secret reference;
  - safe HTTP/workspace capability;
  - workflow executor invocation;
  - sanitized output and errors.

## Exclude From MVP

- Arbitrary remote assembly loading.
- Marketplace payments/licensing.
- OAuth2 provider implementations for Gmail/Office/Figma.
- Plugin-supplied untrusted Razor UI.
- Cross-tenant plugin sharing.
- Runtime code sandboxing.
- General-purpose scripting plugins.

## Safe Sample Plugin Candidate

Use a bundled `External Webhook` or `Mock Mailbox` plugin rather than Gmail/Office/Figma. The sample should prove the plugin architecture without depending on OAuth2.

Example executors:

- `plugin.external-webhook.send`: sends a workflow payload to a configured URL with optional secret token.
- `plugin.mock-mailbox.fetch`: reads deterministic mock messages from workspace/storage and returns normalized message objects.

This allows settings, secrets, health checks, workflow catalog, and execution proof without introducing OAuth2 prematurely.
