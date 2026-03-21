# Shared foundation validation plan

## Povinné validační osy

### 1. Contract compatibility
Ověř:
- že dotnetwatch po extrakci vrací stále kompatibilní payloady,
- že SSH skeleton používá stejnou response envelope family.

### 2. Observability compatibility
Ověř:
- cursorové log čtení,
- truncation behavior,
- file persistence,
- secret redaction.

### 3. Process-runtime compatibility
Ověř:
- start/stop child processu,
- process tree kill,
- stale process cleanup.

### 4. Regression of dotnetwatch
Ověř:
- workspace info,
- app lifecycle,
- build/test operations,
- operation wait/log flow.

### 5. Shared boundary correctness
Ověř:
- že shared projekty nemají zakázané references,
- že SSH projekt neduplikuje common helpery.

## Release gate
Bez green shared foundation validation se nesmí přejít do plné SSH implementace.
