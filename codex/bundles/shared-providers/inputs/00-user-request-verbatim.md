# Verbatim user request


Ahoj, jsi senior C# architektka. 

# Hlavní cíl 
Sílení providerů z centrální CanDoItAll.  


# Popis funkce 

CanDoItAll aplikace @GitHub  (https://github.com/fyziktom/CanDoItAll/tree/development) umožňuje nastavení providerů AI. Jedná se třeba o nastavení přístupu k openAI api, ollama, comfyui, apod. (další drivery můžou přibývat). 

Nicméně větší firmy budou potřebovat nastavit i jednu sdílenou instanci, která bude mít nastavené jejich přístupy k openAI, apod. a ty pak sdílet zaměstnancům. Pokud je nad CanDoItAll API ještě jejich aplikace, tak většinou budou spíše využívat rovnou simple chats api, apod a ne přímo providery, nicméně pokud nějaký zaměstnanec bude chtít na svém pc používat CanDoItAll i jako aplikaci pro správu projektů, apod. tak by musel všechny providery u sebe také nastavit a tedy mít i třeba api klíče. Proto musíme vylepšit naše providery o driver sdíleného providera a také api bod pro sdílení providerů. 

Obecně to bude fungovat takto:

OpenAI -- | Central     | providers api ---- User1 CanDoItAll local app
Ollama -- | CanDoItAll  | providers api ---- User2 CanDoItAll local app
...       |             |

Tedy centrální CanDoItAll má hlavní připojení k reálným providerům. Ty jsou pak v nastavení označeny jako sdílené (nemusí být sdílené všechny, jen ty u kterých se to explicitně povolí) a tím se zpřístupní na hlavní CanDoItAll provider api skrze unifikovaný api bod sdíleného providera. V aplikaci na straně usera se pak nenastavuje přímo třeba openai, ale sdílený candoitall driver. Ten samozřejmě může reprezentovat třeba sdílený openAI, ale už k nemu není připojený napřímo. 
Musí se jednat o hybridní řešení. Tedy i user si může klidně k tomu nastavit i třeba vlastní openAI provider se svým osobním klíčem. Tedy lze použít sdílené i vlastní drivery pro providery zároveň. 

Protože providerů může mít centrální candoitall nastaveno více, tak bude vhodné mít i api point a zpětně v aplikaci v nastavení providerů akci pro sdílení/načtení seznamu sdílených providerů. Tedy nastavení u usera by spočívalo v tom, že se v providerech přidá "sdílený zdroj providerů" (ip serveru kde jede centrální candoitall) a poté se stáhne seznam dostupných providerů. Uživatel zaklikne ty které chce nastavit a potvrdí. tím se nastaví v jeho instalaci a může je použít pokud je centralní candoitall dostupné.

Bude vhodné api sdíleného provideru přiblížit co nejvíce standardům. Například ollama či openai se také drží určitých standardů api. poskytují skrze "jedno" api různé modely pro různé účely. Neměli bychom tedy tam kde to jde vymýšlet něco nového nestandardního. Lepší je držet se co nejvíce standardů a případně doplnit jen co chybí.

Poznámky:
- Později ještě mezi user a central candoitall app bude "Enterprise Gateway and Control Plane - EGCP", ten ještě umožní routování skrze match s identitou usera, nastavení práv, apod. Nicméně z pohledu sdílených providerů by se nemělo jednat o zásadní změnu. Jen bude vhodné počítat s tím, že request by měl obsahovat i identifikaci určitého "objektu přístupu". Ten asi bude zakrývat informace o userovi, jeho session, referencí jako je třeba externí project id, apod. Protože toto bude potřeba sledovat asi na ostatních api bodech tak předpokládám, že by to měla být jen reference na objekt než že bychom třeba do každého api request dto přidávali všechny potenciální pole které by mohl předávat aby se pak daly dobře trackovat využití/náklady.
Je asi vhodné již nyní toto zahrnout v rámci přípravy architektury.


Potřebuji, abys detailně prostudovala CanDoItAll implementaci především kolem providers, jejich použití a apis. 
Následně podle https://github.com/fyziktom/CanDoItAll.SharedInfo/tree/main/codex/skills/bundles připrav detailní bundle pro codex 5.6. ultra který provede implementaci a otestování. 
Měl by být opatrný s pouštěním unit a integračních testů. trvají velmi dlouho a brzdilo by to implementaci a spotřebovalo zbytečně hrozně moc kreditů. 

V rámci https://github.com/fyziktom/CanDoItAll.SharedInfo/tree/main/codex/skills máme i některé užitečné skilly kolem C# architektury. Použij je pro validace během toho co budeš v bundle navrhovat architekturu. Ujisti se, že nevytváříme nějaké zbytečné obrácené reference, že máme správně izolované dto, helpery, apod. 

Jedná se o rozsáhlejší run a bude potřeba jak zásah do backend tak i do frontend. Je nutné aby než se postoupí k UI byl kvalitní otestovaný backend základ. Samotné testování synchronizace a sdílení providerů se bude muset provést pomocí dvou či tří docker instancí. Jinak by to nešlo skutečně potvrdit. 
Po dokončení práce je musí nechat codex běžet, abych je mohl také ručně otestovat. 
Ke všemu bude nutné připravit i detailní dokumentaci a updatovat informace v SharedInfo, především export OpenAPI description a příslušných skillů. 

Bundle dej na výstup jako zip.
