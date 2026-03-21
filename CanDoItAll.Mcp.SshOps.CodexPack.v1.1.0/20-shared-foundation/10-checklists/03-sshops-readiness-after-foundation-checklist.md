# SSH Ops readiness checklist po shared foundation

- [ ] `CanDoItAll.Mcp.SshOps` referencuje `CanDoItAll.Mcp.Core`.
- [ ] SSH server nevytváří vlastní response envelope.
- [ ] SSH server nevytváří vlastní common redactor.
- [ ] SSH server používá shared mutation gate.
- [ ] SSH server používá shared operation primitives.
- [ ] Tooly `targets_list` a `target_test` vracejí shared envelope.
- [ ] Duplicitní common helpery nejsou přidány do projektu.
