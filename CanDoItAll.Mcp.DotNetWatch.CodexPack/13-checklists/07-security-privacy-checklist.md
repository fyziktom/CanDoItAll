# Security & privacy checklist

## Input boundary
- [ ] Všechny vstupní cesty se normalizují a validují.
- [ ] Není možné spustit projekt mimo povolený workspace/root.
- [ ] Tooly nepřijímají raw shell command string.
- [ ] Environment overlay používá whitelist.
- [ ] Nejsou podporované riskantní inline command concatenations.

## Process boundary
- [ ] Child procesy běží jen s potřebným env minimem.
- [ ] Server nepropouští klientem zadané tajné hodnoty do logů.
- [ ] Kill tree necílí na cizí procesy mimo vlastněný workspace context.

## Output boundary
- [ ] stdout není kontaminované.
- [ ] Logy procházejí redaction vrstvou.
- [ ] Response nevracejí connection stringy, tokeny ani hesla.
- [ ] Diagnostika vrací evidence bezpečně.

## Network boundary
- [ ] Health probe defaultně cílí jen na loopback.
- [ ] Externí hosty nejdou bez explicitní konfigurace.
- [ ] Lokální self-signed HTTPS je povolené jen pro loopback scénář.

## Recovery
- [ ] Stale cleanup neukončuje procesy, které server nevlastní.
- [ ] Registry record obsahuje dost identifikátorů pro bezpečnou verifikaci.
- [ ] Audit logy zachytí cleanup akce.
