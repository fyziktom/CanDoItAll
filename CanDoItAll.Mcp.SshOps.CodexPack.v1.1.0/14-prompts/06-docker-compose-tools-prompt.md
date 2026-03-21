# Prompt: Docker and Compose tools

Implementuj:
- `docker_network_ensure`
- `docker_volume_ensure`
- `compose_validate`
- `compose_apply`
- `compose_ps`
- `compose_logs`
- `compose_exec`
- `compose_down`
- `stack_rollback`

Požadavky:
- nepoužívej generické shell stringy jako veřejné API,
- stavěj příkazy ze strukturovaných DTO,
- `compose_apply` musí podporovat detached mode,
- před apply vždy validuj compose config,
- použij health-aware následné wait kroky,
- logy rediguj,
- `compose_exec` drž v whitelistu bezpečných patternů.

Nakonec:
- přidej testy pro locking, detached mode a rollback.
