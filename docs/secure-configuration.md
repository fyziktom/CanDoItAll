# Secure Configuration

Do not commit provider API keys or other plaintext secrets.

Configure `OPENAI_API_KEY` through an environment variable, user-secret, enterprise secret provider, or deployment secret store. `appsettings.json` must only contain non-secret configuration.

If a provider key is ever committed, treat it as compromised and rotate or revoke it outside the repository before reuse.

## Secret Vault

Stored workspace secrets go through `ISecretVault`. The app stores secret metadata in the database, but the secret value is stored behind a vault reference such as `vault:v1:...`.

The default provider is configured with:

```json
{
  "SecretVault": {
    "Provider": "Auto",
    "ApplicationName": "CanDoItAll",
    "VaultPath": null
  }
}
```

`Auto` currently selects `DpapiSecretVault` on Windows. DPAPI uses the current Windows user scope, so another Windows user cannot decrypt the payload. macOS, Linux, MAUI, Azure Key Vault, and HashiCorp Vault providers are represented by explicit provider types, but non-Windows desktop providers are not implemented yet. Selecting one of those providers fails explicitly instead of silently downgrading security.

`DataProtectionFileVault` exists as an explicit cross-platform fallback for development scenarios. Do not use it as the production default until the host has decided where and how to protect the fallback key file.

## Runtime Secret Use

Use stored-secret references instead of plaintext values in agent, workflow, process, MCP, and project-structure settings.

Current supported runtime paths:

- Agent provider credentials can point at a stored secret record. The credential resolver reads the value only while constructing the provider client and does not promote stored-secret values into process environment variables.
- Agent MCP environment/header bindings can use `secret:{secret-id}` or a bare secret id. The secret id must be present in the agent's allowed-secret list.
- Workflow HTTP steps can select a stored secret for a request header. The executor applies the header to the outgoing request and does not include the secret value in workflow output.
- Project-structure secret nodes store only reference metadata: secret id, name snapshot, purpose, and optional external reference.

Do not add a generic model-visible "read secret" tool. If an action needs a secret, resolve it inside the server-side action and return only non-secret output.

## Secret UI

The shared `SecretField` BaseLib component is the standard secret-value editor. It masks by default, supports copy buttons for the value and secret name, and only reveals the value through a timed "Show for 30s" action.

The project-structure secret-reference dialog searches existing stored secrets and can create a new stored secret before adding the reference node. The node must never persist the secret value itself.
