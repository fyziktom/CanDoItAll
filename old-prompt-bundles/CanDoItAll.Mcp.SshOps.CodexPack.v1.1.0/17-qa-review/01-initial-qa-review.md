# Initial QA review

## Role
Přísný senior QA / engineering manager review.

## Hodnocení prvního návrhu
Architektura je silná, ale v původním návrhu by bez doplnění hrozila tato rizika:

1. **Nedostatečně explicitní host key lifecycle**
   - chyběla samostatná guidance pro onboarding a rotaci host key.

2. **Nedostatečně explicitní detached operation model**
   - bez přesné struktury operation journalu by Codex mohl tápat při čekání na dlouhé operace.

3. **Nedostatečně explicitní ACME staging-first strategie**
   - bez ní hrozí rychlé narážení na rate limits při opakovaných deploy testech.

4. **Nedostatečně explicitní guardy veřejné expozice**
   - PostgreSQL, IPFS API a Traefik dashboard musí mít samostatné checklisty a validační scénáře.

5. **Nedostatečně explicitní private IPFS guidance**
   - samotný container nestačí; je nutné řešit `swarm.key`, bootstrap peers, peer list a interní API.

6. **Nedostatečně explicitní rollback evidence**
   - rollback musí mít revision metadata, ne jen ad hoc backup.

7. **Nedostatečně explicitní ops runbook**
   - chyběla praktická diagnostika port conflicts, ACME storage perms a full disk scénářů.

8. **Nedostatečně explicitní compatibility matrix**
   - bez ní hrozí, že implementace bude implicitně předpokládat konkrétní Ubuntu / Docker / Traefik / Kubo verze.

## Verdict po prvním průchodu
**Podmíněně schváleno pouze po doplnění remediation balíku.**
