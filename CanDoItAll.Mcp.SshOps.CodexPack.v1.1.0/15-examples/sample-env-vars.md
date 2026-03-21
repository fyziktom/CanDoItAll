# Příklad environment proměnných

```bash
export CANDOITALL_MCP_SETTINGS_PATH=/secure/config/candoitall.mcpserver.settings.json
export CANDOITALL_SSH_PRIVATE_KEY_STAGING_01="$(cat /secure/keys/staging-01.key)"
export CANDOITALL_SSH_PRIVATE_KEY_PASSPHRASE_STAGING_01="replace-me"
export CANDOITALL_SSH_PRIVATE_KEY_PROD_01="$(cat /secure/keys/prod-01.key)"
export CANDOITALL_SSH_PRIVATE_KEY_PASSPHRASE_PROD_01="replace-me"
```

Doporučení:
- do env dávej pouze to, co musí být lokálně dostupné pro navázání SSH,
- aplikační secrets pro kontejnery preferuj jako remote soubory mimo repozitář,
- nikdy neloguj celý obsah key nebo passphrase.
