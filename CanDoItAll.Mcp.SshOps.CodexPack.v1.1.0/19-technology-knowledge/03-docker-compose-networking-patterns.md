# Docker Compose networking patterns

## Doporučený pattern
- Traefik na externí `proxy` network,
- app service na `proxy` + `backend`,
- PostgreSQL jen na `backend`,
- IPFS jen na `backend` (a případně 4001 speciálně, pokud je potřeba multi-host swarm).

## Health and readiness
`depends_on` samo o sobě nestačí, pokud nepracuješ s healthcheckem.  
Pro PostgreSQL používej healthcheck a na něj navazuj readiness wait.

## Pro MCP server to znamená
- `compose_validate` musí kontrolovat sítě, service names a healthcheck patterny,
- `compose_apply` má po deployi spouštět navazující wait/probe kroky.
