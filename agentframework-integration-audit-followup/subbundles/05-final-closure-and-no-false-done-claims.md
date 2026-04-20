# Subbundle 05 — Final Closure And No False Done Claims

## Covers

Přísnější override pro původní subbundle:

- `12-data-backfill-cleanup-refactor-gates-and-final-closure`

## Objective

Uzavřít initiative až po skutečném technical, product a QA closure.

## Mandatory closure tasks

1. **Remove placeholders and temporary copy**
   - `/agents` už nesmí obsahovat placeholder wording
   - žádné `future`, `planned imports`, `later subbundles`, `to be filled`

2. **Fix known defects before final claim**
   - opravit známý `Unknown role` bug z execution reportu
   - zavřít residual risks nebo je explicitně downgradeovat s approval note

3. **Finalize data migration**
   - backfill provider a agent binding dat
   - cleanup transitional bridges

4. **Finalize tests**
   - unit + integration + Playwright + scenario validation
   - proof logs a screenshots v repu

5. **Triple review**
   - senior QA
   - senior Development manager
   - senior C# architect
   - každý review musí mít explicitní signoff nebo reopen reason

6. **Truthfulness check**
   - teprve po průchodu všech closure checks je dovolené změnit `Execution state` na `Completed`

## Mandatory grep checks before completion

```bash
rg -n "Execution state: `In progress`|Pending implementation|To be filled|not honestly closable yet" agentframework-full-integration
rg -n "Integrated agent module foundation|Planned imports|Later subbundles|future integrated surfaces|deferred" src/CanDoItAll.Modules.AgentFramework
```

Oba příkazy musí vrátit 0 matches.

## Final acceptance

- všechny subbundles 01–12 closed,
- zero placeholder copy,
- zero pending proof references,
- reálné scenario evidence v repu,
- žádný duplicity-based source of truth,
- žádný obcházející run start bez launch planning approval.

## Explicit prohibition

Nesmí se zopakovat situace, kdy někdo prohlásí práci za hotovou, zatímco vlastní execution report v repu říká opak.
