# Original User Request

> Jsi senior C# architektka.  
> potřebuji abys prošla jaké jsou změny v MAF 1.18 vůči 1.17 který používáme nyní v https://github.com/fyziktom/CanDoItAll/tree/development@GitHub
>
> Na základě toho připrav bundle (viz https://github.com/fyziktom/CanDoItAll.SharedInfo/tree/main/codex/skills/bundles) pro codex 5.6. xhigh který provede update a opraví breaking changes.
>
> Vím, že opravovali paralelní volání toolů. Nicméně zatím bych s tím byl opatrný, protože někdy dost záleží na pořadí.
>
> Určitě nemáme nyní dotažené human in loop v rámci workflows. Toto bychom mohli zvážit opravit hned po tomto update. Pokud je update malinký (myslím, že vyjma těch parellel tools tam nic moc velkého nebylo), tak je možné to přidat již v rámci tohoto bundle.  
> Není to jen o tom doplnit správnou implementaci, ale i doplnit to na api.
>
> projdi vše důkladně a připrav detailní bundle pro codex a dej mi ho jako zip.

## Interpretation recorded during preparation

The 1.18 update is small enough to share one bundle with workflow HITL, but the two concerns must remain separate implementation waves and commits. Parallel tool invocation is an opt-in capability and remains disabled. Workflow HITL is included because the current repository already has most public contracts and an API shell, while the missing piece is a real MAF checkpoint/resume path.
