# Proposed examples

These JSON fragments describe the proposed option names. They are not active application configuration and are not proof the current host supports the new settings. Merge them deliberately after implementation.

Secure profiles intentionally omit the signing key and administrator password hash. Supply actual private values through the deployment configuration, for example the env keys `Api__Authorization__SigningKey` and `Api__BootstrapAdmin__PasswordHash`. Missing required secrets must cause validation failure, not a fallback. Preserve an existing installation's signing key to keep machine credentials valid.

Use the real application hash-generation helper implemented by Codex. Do not generate an unrelated hash format, pass passwords on the command line or check secrets into these examples. No example password/hash is provided.

`users-local-admin.proposed.json` enables user login while keeping HTTP administration closed. The trusted local Settings surface is independent of this HTTP gate. The examples omit deployment-specific proxy, TLS, database and control-plane mounts; they are not complete Docker deployment recipes.

`validation-evidence.template.json` is an unexecuted reporting template. Replace values only with actual observations.
