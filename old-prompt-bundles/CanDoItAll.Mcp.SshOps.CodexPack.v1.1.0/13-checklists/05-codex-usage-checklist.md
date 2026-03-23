# Checklist používání z pohledu Codex

- [ ] Codex vždy nejdřív spustí `targets_list` nebo zná target.
- [ ] Před mutací spustí `target_test` nebo `target_audit`.
- [ ] Před deployem spustí `compose_validate`.
- [ ] U dlouhých operací používá `operation_wait` místo slepého opakování.
- [ ] Při chybě čte `operation_logs`.
- [ ] Po deployi provádí `http_probe` nebo `http_wait`.
- [ ] Po TLS změnách provádí `cert_check`.
- [ ] Po IPFS změnách provádí `ipfs_private_validate`.
- [ ] Před risky change vytvoří backup nebo relyuje na revision backup.
- [ ] Nepoužívá raw exec, pokud to není výslovně povolené.
- [ ] Neprovádí souběžné mutace nad stejným targetem.
