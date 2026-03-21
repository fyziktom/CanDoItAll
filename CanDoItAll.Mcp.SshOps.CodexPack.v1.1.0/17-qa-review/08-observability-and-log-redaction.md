# Observability and log redaction

## Log categories
- connectivity
- security
- transport
- operations
- docker
- traefik
- postgres
- ipfs
- validation

## What to log
- correlationId
- target
- tool name
- operationId
- duration
- normalized command kind
- result status
- retry count
- timeout information

## What to redact
- SSH private key content
- passphrases
- DB passwords
- ACME/DNS provider tokens
- swarm keys
- Authorization headers
- full connection strings unless safely masked

## Rules
- log summaries, not raw secrets,
- prefer structural logs,
- never echo uploaded file content unless explicitly non-sensitive,
- tool responses must already be redacted before returning.
