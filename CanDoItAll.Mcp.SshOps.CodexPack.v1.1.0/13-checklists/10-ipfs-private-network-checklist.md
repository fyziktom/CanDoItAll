# Checklist privátní IPFS sítě

- [ ] Kubo container používá persistentní volume.
- [ ] `swarm.key` je dodaný bezpečně.
- [ ] Veřejné bootstrap peers byly odstraněny.
- [ ] Používají se jen kontrolované privátní peers.
- [ ] API je interní a není veřejně publikované.
- [ ] Gateway není veřejně publikovaná, pokud to není explicitně požadované.
- [ ] Pokud je potřeba multi-host swarm, port 4001 je správně routovaný.
- [ ] `ipfs_private_validate` potvrzuje privátní režim.
- [ ] App komunikuje s IPFS přes interní Docker DNS.
- [ ] Monitoring zahrnuje peer count a repo size.
