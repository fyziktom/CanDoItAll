# Known risks and open questions

## Accepted risks for MVP
1. OpenSSH CLI transport není v MVP.
2. DNS provider specific automatizace pro wildcard certifikáty není v MVP.
3. Multi-host distributed Traefik cluster není v MVP.
4. Plná secret-management integrace s externím vaultem není v MVP.
5. Full blue/green rollout orchestrace není v MVP.

## Open questions for implementation
1. Existuje v solution sdílená knihovna pro result envelopes / error taxonomy?
2. Existuje interní logging / telemetry standard, který má projekt převzít?
3. Má být detached operation journal částečně i lokálně persistovaný?
4. Má mít target config podporu více identit per target?
5. Má se podporovat `sudo -n` only, nebo i no-sudo targety jako first-class mode?
6. Bude IPFS běžet vždy side-by-side s app stackem, nebo i jako shared infra stack?
7. Má rollback vracet i previous `.env` / secret-file references, nebo jen compose tree?

## Risks requiring explicit operational attention
- host key rotation bez aktualizace pinů způsobí hard fail,
- Let's Encrypt production resolver může narazit na rate limits při častých testech,
- Docker group je na Linuxu prakticky root-equivalent,
- špatná práva `acme.json` mohou blokovat issuance,
- veřejná expozice IPFS API je kritická bezpečnostní chyba,
- plný disk může rozbít Docker pull i Traefik ACME storage.
