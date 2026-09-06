# Settled refresh diagnostic

One Razor and one C# edit, each with undo, per lane. These include a deliberate settlement interval and are not replacement performance measurements. The earlier calibration and original 27-cycle benchmark remain separate.

| Host | Refresh | Razor | C# | Document changes | Process restarts | Undo |
|---|---|---|---|---:|---:|---|
| fullapp | on | enhanced-navigation | enhanced-navigation | 0 | 0 | both pass |
| fullapp | off | hot-reload | hot-reload | 0 | 0 | both pass |
| parity | on | enhanced-navigation | enhanced-navigation | 0 | 0 | both pass |
| parity | off | hot-reload | hot-reload | 0 | 0 | both pass |
| fast | on | enhanced-navigation | enhanced-navigation | 0 | 0 | both pass |
| fast | off | hot-reload | hot-reload | 0 | 0 | both pass |
