# Current user request — preserved input

```text
už začínáme být postupně ready na tyto simple chaty. nyní je v https://github.com/fyziktom/CanDoItAll/tree/development@GitHub 
pushnutá verze která už jede i na linuxu. 
Nyní potřebuji, abys připravila bundle, kterým implementujeme tyto jednodušší chaty. 
Určitě to budeme muset nějak rozčlenit a nedělat vše najednou. měli bychom začít potřebnými třídami, enumy,helpery, apod. v podstatě vše na úrovni backendu. 
následně provedme kontroly a testování přes api, apod. a až poté začneme dělat fázi propojení s UI, protože než se pustíme do UI uděláme samostatný bundle pro vylepšení izolace společných komponent, apod. a pak až uděláme ten bundle pro integraci těchto simple chatů i v ui hlavní apky. 
Vesměs to budou fáze A a B, ale je potřeba abys udělala revizi a kontrolu jestli nám to opravdu pokryje potřebné cases (včetně i budoucího rozšíření na běžného chatbota, protože některé enterprises chtějí aby to umělo i to). 
V https://github.com/fyziktom/CanDoItAll.SharedInfo/tree/main/codex/skills máme různé skily kolem návrhu architektury, aby byla zajištěná modularita, apod. použij je pro doplnění svých znalostí o enterprise architekturách. 

musíme být u bundles opatrní s neustálým během testů. máme jich hodně a žere to hrozně moc času. codex nesmí pouštět pořád celé suites. Na konci dává smysl pustit vše, ale ne v průběhu. 

bundle dej na výstup jako zip.
```
