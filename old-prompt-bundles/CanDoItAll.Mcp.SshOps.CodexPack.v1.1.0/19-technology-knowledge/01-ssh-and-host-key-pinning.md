# SSH a host key pinning

## Proč je to důležité
Codex nemá interaktivně potvrzovat cizí host key.  
Produkční workflow musí používat explicitní důvěru k hostu:

- pinned fingerprint,
- nebo known_hosts-like zápis.

## Praktický model
- private key přijde z env proměnné,
- passphrase volitelně z env,
- target config obsahuje host, user, port a host key verification,
- mismatch = hard fail.

## Co musí umět implementace
- číst OpenSSH private key,
- validovat fingerprint,
- odmítnout spojení při mismatch,
- umět vrátit bezpečnou diagnostiku.

## Provozní doporučení
- onboarding hostu dělat mimo automatický deploy flow,
- fingerprint získat z důvěryhodného kanálu,
- host key rotation mít jako explicitní change procedure.
