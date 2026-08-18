# Original architect notes — preserved input

```text
- nyní to máme jen tak předpřipravené a použité ve workflows.
- pro běžné použití v aplikaci bude potřeba přiblížit tyto jednoduché chaty podobě Agentů.
- Jednoduché llm chaty nebudou mít nástroje a skilly jako agenti, ale musí být možné mu dát jméno, avatar, systém prompt či nějaké základní definice, teplotu, apod.
- Jednoduché llm chaty se pak musí objevit v podobných situacích jako agenti. Tedy třeba v seznamu plovoucích chatů uvidím jak agenty tak jednoduché chaty (mělo by jít filtrovat na kliknutí že chci vidět v seznamu jen agenty nebo jen chaty).
- plovoucí jednoduchý chat nebude mít možnost si třeba načíst project structure data. Proto bychom měli mít v takovém chatu navíc tlačítko na přidání kontextu. Třeba celé project structure, nebo aktuálně vybraného node s jeho subtree, apod.
```

These notes remain product requirements, but UI and concrete Project Structure context capture are
deliberately deferred to later bundles. This bundle must preserve the backend extension seams required
to implement them without transcript or API redesign.
