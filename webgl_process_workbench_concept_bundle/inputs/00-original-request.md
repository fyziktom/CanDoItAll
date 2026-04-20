# Original request

```text
Ahoj, jsi senior C# Blazor a WebGL architekt. 
Musíš připravit detailní bundle jako zip. Příklady bundlů i se subbundles vidíš v přiloženém kódu. 
Co řešíme:
jakým stylem je nejlepší pracovat s webGL v blazoru? Mám procesní diagramy které jsou ale celkem nepřehledné když jsou ve 2D. Takže me napadlo zkusit to jako 3D. 
Přikládám ti nas kód kde můžeš vidět process module ktery ukazuje i strukturu našich procesů a jak je nyní zobrazuje v 2D canvasu. 
Potřebuji udělat takový pokus jak to bude fungovat. Budu to dělat v separátní větví jen jako koncept. Potřebuji od tebe detailní bundle se subbundles ktery v první fázi přidá component knihovnu webgl wrapperu se základním systémem pro zobrazení a ovládání podobné jako máme pro canvas. To musí byt univerzální knihovna. Následně codex přidá nový Sandbox projekt pro tu webgl knihovnu. Tam se zobrazí vybraný z našich template procesů.  Ideální bude mít možnost je v sandboxu přepínat. Sandbox musí umožnit nejen zobrazení ale i o v ladani jako je změna pozice nodu nebo jeho připojení apod. 
Codex to musí validovat ze sceenshotů, protože s playwright mcp nejde jednoduše  ovládat canvas a webgl. Bylo by vhodné mit.v.te základní knihovně i nejaky interface pro playwright mcp aby mohl codex testovat i samotné změny ve webgl (třeba přesunout nové, apod).
Je vhodné přidat v bundle xlsx s všemi userstories a funkcemi apod které musí codex vyřešit. Také se hodí rozdělit subbundlss na dané a donutit codex po fazich dělat revizi architektury a případně přidat pohotovostní subbundles kterými to prvně opraví/refaktoruje a poté pokračuje další fázi .
Připrav detailní bundle a dej hodná výstup jako zip.
```
