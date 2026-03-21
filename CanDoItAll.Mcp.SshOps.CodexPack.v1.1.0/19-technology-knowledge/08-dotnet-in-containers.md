# .NET aplikace v kontejnerech

## Pattern
- app naslouchá na interním portu, typicky 8080,
- Traefik routuje na tento port přes label,
- config jde přes env nebo mounted production config file,
- connection string ukazuje na `postgres`,
- IPFS API URL ukazuje na `http://ipfs:5001`.

## Co má řešit MCP server
- po deployi ověřit health endpoint,
- umět číst compose logs app služby,
- bezpečně signalizovat, jestli je problém v app, DB nebo ingress vrstvě.
