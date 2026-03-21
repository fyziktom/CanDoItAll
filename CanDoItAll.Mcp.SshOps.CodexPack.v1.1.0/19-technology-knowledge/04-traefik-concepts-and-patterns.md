# Traefik koncepty a patterny

## Preferovaný pattern
- shared Traefik stack,
- `exposedByDefault=false`,
- routování přes labels nebo file provider,
- shared `proxy` network,
- dashboard jen interně nebo chráněně.

## Co musí validovat MCP server
- existenci proxy network,
- správný resolver,
- správný `loadbalancer.server.port`,
- nepřítomnost nechtěné veřejné expozice dashboardu,
- persistentní ACME storage.

## Praktické doporučení
Pro testovací rollouts začínej staging resolverem.  
Production resolver zapínej až po průchodu staging validace.
