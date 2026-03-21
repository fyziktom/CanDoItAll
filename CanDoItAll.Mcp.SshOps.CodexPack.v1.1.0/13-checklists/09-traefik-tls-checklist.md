# Checklist Traefik + TLS

- [ ] Traefik běží na shared proxy network.
- [ ] `exposedByDefault=false` je nastavené.
- [ ] Routers jsou deklarované přes labels nebo file provider.
- [ ] HTTP -> HTTPS redirect je definovaný.
- [ ] Cert resolver má persistentní storage.
- [ ] `acme.json` má správná práva.
- [ ] Staging resolver je připraven pro testy.
- [ ] Production resolver se používá až po ověření.
- [ ] Dashboard je interní nebo chráněný.
- [ ] App service má správný `loadbalancer.server.port`.
- [ ] `cert_check` vrací očekávaný SAN/hostname.
