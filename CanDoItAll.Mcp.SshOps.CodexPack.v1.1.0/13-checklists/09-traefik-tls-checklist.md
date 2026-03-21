# Traefik and TLS checklist

- [ ] The deployment mode is explicit: Docker network or native service.
- [ ] `exposedByDefault=false` is set.
- [ ] Routers are declared through labels or the file provider.
- [ ] HTTP to HTTPS redirect is defined.
- [ ] Certificate storage is persistent for the chosen lane.
- [ ] `acme.json` permissions are correct when ACME is used.
- [ ] A self-signed certificate path is defined for local-only validation when ACME is intentionally skipped.
- [ ] Dashboard exposure is internal or protected.
- [ ] The app backend port matches the real application listener.
- [ ] `cert_check` returns the expected hostname or direct IP identity.
- [ ] Browser validation proves that the HTTPS endpoint loads in Playwright with the expected certificate mode.
