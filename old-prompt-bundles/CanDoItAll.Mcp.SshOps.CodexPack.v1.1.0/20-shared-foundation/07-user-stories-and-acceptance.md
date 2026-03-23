# Shared foundation user stories and acceptance

## Story SF-01
Jako architektka chci mít jednu shared response envelope a error model, aby všechny MCP servery vracely konzistentní payloady.

### Acceptance
- `CanDoItAll.Mcp.Core` obsahuje shared envelope,
- `CanDoItAll.Mcp.DotNetWatch` ji používá,
- `CanDoItAll.Mcp.SshOps` ji používá od začátku.

## Story SF-02
Jako vývojářka chci mít sdílený log buffer a redaction vrstvu, aby logy a operation logs měly jednotné chování.

### Acceptance
- cursorované čtení je jednotné,
- file-backed persistence je jednotná,
- redaction pravidla nejsou duplikovaná.

## Story SF-03
Jako provozní inženýrka chci mít shared mutation gate, aby různé MCP servery nezaváděly vlastní nekonzistentní locking.

### Acceptance
- existuje shared gate abstraction,
- dotnetwatch ji používá,
- SSH server ji používá pro target/stack locks.

## Story SF-04
Jako maintainerka chci mít local process runtime v oddělené optional knihovně, aby další lokální MCP servery nemusely kopírovat process supervisor.

### Acceptance
- `CanDoItAll.Mcp.LocalRuntime` existuje,
- dotnetwatch přes něj dál bezpečně startuje/ukončuje child procesy.

## Story SF-05
Jako QA manažerka chci regression gate pro dotnetwatch po extrakci shared foundation, aby se chyba odhalila dřív než v SSH serveru.

### Acceptance
- existuje regression checklist,
- dotnetwatch smoke flows jsou green,
- známá rizika jsou explicitně zdokumentovaná.

## Story SF-06
Jako implementační agent chci jasná boundary rules, abych do shared layer nevytlačil doménovou logiku příliš brzy.

### Acceptance
- je zdokumentováno co patří do shared layer,
- je zdokumentováno co tam nepatří,
- review checklist to vynucuje.
