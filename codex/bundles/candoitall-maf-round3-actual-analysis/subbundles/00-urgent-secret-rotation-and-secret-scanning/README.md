# 00 - Urgent Secret Rotation and Secret Scanning

## Problem

A real-looking OpenAI API key pattern exists in `src/CanDoItAll.Web/appsettings.json`. This bundle intentionally does not copy the secret.

## Required implementation

1. Remove the plaintext secret from all tracked files.
2. Replace it with a safe placeholder or omit the key entirely.
3. Read secrets only from environment variables, user-secrets, or a secret provider.
4. Rotate/revoke the exposed key outside the repository.
5. Add a secret scanning test or CI script.
6. Ensure logs and generated bundles never print secret values.

## Suggested scanner

Add a test/script that scans source, docs, tests, fixtures, appsettings, and generated bundles for patterns such as:

```text
sk-[A-Za-z0-9_-]{20,}
```

Allow explicit placeholders like `__OPENAI_API_KEY__`, but reject realistic keys.

## Acceptance criteria

- Repository grep finds no real-looking OpenAI API key.
- Build/test does not require a committed key.
- Local developer setup documentation explains secure configuration.

## Execution status

Completed. Key material was removed from source/config, `SecretScanningTests` was added, tracked and source scans returned no matches, and external credential rotation remains an operator action.
