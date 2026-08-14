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

`Auto` selects current-user DPAPI with typed `Strong` protection on Windows. On non-Windows hosts it selects `LocalUserFile`, the guaranteed first-launch profile: payloads use AES-256-GCM, vault directories are enforced to `0700`, and vault files are enforced to `0600`. Its key is stored in that same directory, so code running as the same operating-system user can access it. Startup therefore reports typed `BasicLocal` protection and logs a non-secret warning instead of presenting this profile as equivalent to an operating-system vault.

Use an explicit stronger provider when the deployment threat model requires it: `Dpapi` on Windows, `MacOsKeychain` for an interactive macOS user, `LinuxSecretService` for an available and unlocked D-Bus session keyring, or `ExternalWrappingKeyFile` when a protected deployment input supplies the wrapping key. Explicit strong providers fail closed when their platform, session, dependency, or key input is unavailable; they do not fall back to `LocalUserFile`.

`LocalUserFile` is Unix-only; explicit selection on Windows is rejected with guidance to use `Auto` or `Dpapi`. `DataProtectionFile` is the legacy name for the same file format. It remains readable for migration and can be selected only in Development with the existing explicit insecure-provider compatibility opt-in, where it reports `DevelopmentOnly`. New Unix configuration should use `Auto` or `LocalUserFile` and should not enable `AllowInsecureDevelopmentProviders`.

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

The project-structure secret-reference dialog searches existing stored secrets, offers a dropdown stored-secret selector when adding or editing a secret node, and can create a new stored secret before adding or updating the reference node. The node must never persist the secret value itself.

Workflow HTTP executor creation and the selected HTTP executor inspector use the same stored-secret selector for request-header credentials. The workflow stores only the secret id and resolves the secret value server-side at execution time.
