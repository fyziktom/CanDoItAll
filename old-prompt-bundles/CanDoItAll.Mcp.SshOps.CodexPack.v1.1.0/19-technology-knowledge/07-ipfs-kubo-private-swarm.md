# IPFS Kubo privátní swarm

## Cíl
Používat IPFS izolovaně jako privátní síť, ne veřejný swarm.

## Klíčové prvky
- `swarm.key`,
- odstranění výchozích veřejných bootstrap peers,
- vlastní seznam privátních peerů,
- API pouze interně,
- gateway pouze pokud ji opravdu potřebuješ.

## Rizika
- ponechání veřejných bootstrap peers,
- veřejná expozice RPC API,
- chybné peer listy,
- chybějící port 4001 při multi-host topologii.

## Co má validovat MCP server
- přítomnost `swarm.key`,
- že bootstrap list neobsahuje veřejné peers,
- že API není publikované veřejně,
- že app používá interní adresu `http://ipfs:5001`.
