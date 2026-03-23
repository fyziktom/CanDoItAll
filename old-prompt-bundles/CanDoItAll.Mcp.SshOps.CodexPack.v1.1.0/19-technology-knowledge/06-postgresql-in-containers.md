# PostgreSQL v kontejnerech

## Doporučený pattern
- persistentní volume,
- healthcheck přes `pg_isready`,
- žádný veřejný host port defaultně,
- app se připojuje přes interní Docker DNS `postgres`.

## Rizika
- špatná volume permissions,
- špatné credentials,
- aplikace startuje dřív než DB je ready,
- náhodná veřejná expozice portu 5432.

## Co má kontrolovat MCP server
- health status,
- compose config,
- nepublikování host portu,
- volitelně test přihlášení bezpečným způsobem.
