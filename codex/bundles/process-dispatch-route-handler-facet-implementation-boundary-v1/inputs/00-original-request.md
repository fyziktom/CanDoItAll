# Raw Request

codex už to dodělal. je to pushnuté ve větevi maf-processes-refactor. 
Podívej se jestli je vše splněné, co je potřeba opravit, vylepšit. 
Stále bych netlačil na oddělování process core pokud už vyloženě neusoudíš, že můžeme s tím začít. Obecně si myslím, že to bude potřebovat ještě další kroky postupné izolace, které k tomu process core bude směřovat. Bylo by vhodné jich naplánovat více, ale samozřejmě bezpečně tak aby si je pak codex nezjednodušil. nesmíme vypustit žádné původní funkcionality systému. děláme jen refaktoring a zlepšení architektury.
Hlavně některé services jsou pořád obrovské a je lepší je postupně rozebírat. Ideálně vytvářet balíčky které budou řešit jejich izolaci (abstraction, apod) a když jsou ready, tak postupně je pak využít pro izolaci určité konkrétní části. 

Rozděl je do jednotlivých fází, aby codex mohl pracovat déle. Nyní to měl hotové rychle a jsou to celkem malé změny. Naplánuj jich více ať může pracovat pár hodin. Nicméně je potřeba vynucovat co několik subbndlů ještě další refaktoring. díky tomu je možné naplánovat více kroků. 

připrav bundle a dej ho jako zip.
