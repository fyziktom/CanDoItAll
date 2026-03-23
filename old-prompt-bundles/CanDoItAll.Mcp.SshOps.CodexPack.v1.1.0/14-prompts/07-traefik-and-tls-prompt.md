# Prompt: Traefik and TLS

Připrav tooly a validační logiku pro Traefik a TLS.

Zaměření:
- shared proxy network,
- labels-based routing,
- HTTP -> HTTPS redirect,
- ACME staging/production resolvery,
- persistentní storage certifikátů,
- dashboard pouze interně nebo chráněně,
- `cert_check`,
- `http_probe` a `http_wait`.

Zajisti:
- detekci nebezpečných konfigurací,
- jasné summary a next steps při selhání ACME,
- ochranu před náhodnou veřejnou expozicí dashboardu.
