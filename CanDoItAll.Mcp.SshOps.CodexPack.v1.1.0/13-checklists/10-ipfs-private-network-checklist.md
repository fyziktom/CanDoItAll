# Private IPFS checklist

- [ ] Kubo uses persistent storage in the selected deployment lane.
- [ ] `swarm.key` is delivered securely.
- [ ] Public bootstrap peers are removed.
- [ ] Only controlled private peers are configured, or the topology is explicitly single-node.
- [ ] API is internal and not publicly exposed.
- [ ] Gateway is not publicly exposed unless explicitly required.
- [ ] If multi-host swarm is required, port 4001 is routed correctly.
- [ ] `ipfs_private_validate` confirms private mode.
- [ ] The app communicates with IPFS through the selected internal endpoint.
- [ ] Monitoring includes peer count and repo size.
- [ ] A peer count of `0` is accepted for a single-node private validation host when no additional peers are intentionally attached.
