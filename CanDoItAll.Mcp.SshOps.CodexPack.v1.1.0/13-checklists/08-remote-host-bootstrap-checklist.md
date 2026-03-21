# Remote host bootstrap checklist

- [ ] Actual distribution name and version are detected and recorded, not assumed from operator description.
- [ ] CPU architecture is recorded.
- [ ] glibc, OpenSSL, and ICU baselines are checked before choosing a publish/runtime strategy.
- [ ] SSH user exists.
- [ ] Sudo policy is verified.
- [ ] Docker Engine and Compose are verified, or the host is explicitly routed into the native-service lane.
- [ ] systemd availability is verified for native service deployment.
- [ ] The selected lane is recorded as either `standard-host` or `legacy-arm-host`.
- [ ] Required directories for state, stack files, and artifacts exist under an allowed root.
- [ ] Port availability is checked for 22, 80, 443, and the chosen IPFS ports.
- [ ] Disk space is sufficient.
- [ ] DNS or direct-IP access mode is explicitly chosen for validation.
- [ ] Host key pinning is recorded before any mutating operation.
