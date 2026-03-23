# Checklist bezpečnosti a soukromí

- [ ] Private key je pouze v env proměnné nebo secure secret store.
- [ ] Host key pinning je zapnuté pro produkční targety.
- [ ] `StrictHostKeyChecking` equivalent politika je definovaná.
- [ ] Žádné tajné hodnoty nejsou v repu.
- [ ] Žádný secret není vracen v tool response.
- [ ] Docker socket není zbytečně mountovaný jinam než Traefik/read-only potřeba.
- [ ] Traefik dashboard není veřejně bez ochrany.
- [ ] PostgreSQL není publikovaná na host port.
- [ ] IPFS RPC API není publikovaná na veřejný port.
- [ ] IPFS private swarm má vlastní `swarm.key`.
- [ ] Default bootstrap peers veřejné sítě byly odstraněny.
- [ ] Dangerous tools mají explicitní opt-in.
- [ ] Logy jsou redigované a auditovatelné.
