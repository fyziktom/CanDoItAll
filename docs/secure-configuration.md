# Secure Configuration

Do not commit provider API keys or other plaintext secrets.

Configure `OPENAI_API_KEY` through an environment variable, user-secret, enterprise secret provider, or deployment secret store. `appsettings.json` must only contain non-secret configuration.

If a provider key is ever committed, treat it as compromised and rotate or revoke it outside the repository before reuse.
