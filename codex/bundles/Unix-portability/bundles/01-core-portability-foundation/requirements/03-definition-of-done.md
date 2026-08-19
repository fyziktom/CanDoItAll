# Core definition of done

Core portability is done only when Gate C4 is GO and:

- all core P0 requirements are `Solved`;
- active Windows, Ubuntu, and macOS CI gates are green;
- the Web host publishes, starts headlessly, reports readiness, shuts down, and restarts from clean output;
- existing Windows logical paths, profiles, Data Protection/DPAPI fixtures, and control-plane passwords have a proven migration/read path;
- Linux/macOS production profiles use a secure, supported secret/key strategy;
- storage/control-plane writes are atomic, link-safe, deterministic, and permission-hardened;
- foreign absolute paths are unresolved/rebound rather than reinterpreted;
- rollback/recovery has been rehearsed;
- the exact C4 commit and evidence are recorded for B00.
