# Secrets, konfigurace a env management

## Zásady
- SSH key patří do lokální env proměnné pro MCP server,
- aplikační secrets preferuj jako remote env file nebo secret file mimo repozitář,
- repozitář smí obsahovat jen example values,
- logy a response musí redigovat tajné hodnoty.

## Doporučení
- odděl settings MCP serveru od aplikačních secrets,
- cílový host má mít vlastní secure umístění pro `.env` / secret files,
- revision backupy musí řešit, zda snapshotují i secret references, ne nutně obsah secretů.
