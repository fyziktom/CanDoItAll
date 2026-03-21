# Failure injection plan

## Cíl
Ověřit, že server selhává bezpečně, čitelně a reverzibilně.

## Povinné failure injections
1. SSH authentication failure
2. Host key mismatch
3. SSH timeout during upload
4. Compose file syntax error
5. Missing Docker network
6. Port 80 occupied
7. Port 443 occupied
8. Full disk during image pull
9. ACME staging misconfiguration
10. Wrong file mode on `acme.json`
11. PostgreSQL healthcheck never becomes healthy
12. IPFS swarm key mismatch
13. Public bootstrap peers left in config
14. Operation reconnect mid-run
15. Rollback without available revision

## Pro každý scénář zaznamenat
- použitý target,
- krok, který selhal,
- vrácený status a error code,
- log excerpt po redakci,
- zda proběhl cleanup,
- zda byl nabídnut další akční krok.
