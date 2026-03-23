# Prompt: repo discovery + shared inventory

Nejdřív analyzuj solution `CanDoItAll`.

## Cíl
Nesmíš jen najít místo pro nový SSH projekt.  
Musíš nejdřív pochopit, které části už existujícího `CanDoItAll.Mcp.DotNetWatch` mají být přesunuté do shared knihoven.

## Postup

1. Najdi solution file a potvrď projektovou strukturu.
2. Projdi `src/CanDoItAll.Mcp.DotNetWatch`.
3. Najdi:
   - naming conventions,
   - options/configuration patterns,
   - logging conventions,
   - error handling patterns,
   - process/runtime helpery,
   - security guardy,
   - long-running operation model.
4. Porovnej to s návrhem `CanDoItAll.Mcp.SshOps`.
5. Rozděl nalezené typy do tří skupin:
   - **extract now to shared foundation**
   - **possible future shared candidate**
   - **must stay server-specific**
6. Navrhni přesné shared projekty, namespaces a project references.
7. Označ místa s nejvyšším regresním rizikem při refaktoru dotnetwatch.

## Povinný výstup

- krátká discovery zpráva,
- seznam shared kandidátů,
- seznam typů, které nesmí do shared vrstvy,
- doporučené namespaces,
- doporučené project references,
- seznam breaking-risk míst,
- návrh pořadí extrakce a refaktoru.

Teprve potom začni scaffold shared projektů.
