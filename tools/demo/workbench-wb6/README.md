# Frozen Workbench WB6 candidate

These PowerShell 7.4+ scripts operate the owned Docker Desktop candidate described by an
absolute or repository-relative `candidate.json`. Run them from the repository root. Docker
must already contain the exact application and PostgreSQL images. They never build, pull,
replace an unrelated listener, delete volumes or alter the ordinary port-5032 installation.

```powershell
$candidate = 'artifacts/workbench-completion-wb6/demo-candidate/candidate.json'
./tools/demo/workbench-wb6/Start-Demo.ps1 -CandidatePath $candidate
./tools/demo/workbench-wb6/Check-Demo.ps1 -CandidatePath $candidate
./tools/demo/workbench-wb6/Stop-Demo.ps1 -CandidatePath $candidate
```

Start validates the frozen Compose hashes, exact image IDs, host binding and loopback port.
Check verifies the owned app and database health, exact local operator ingress and HTTP health.
Stop uses only inspected candidate container IDs and preserves data. Calling Start on the
healthy candidate, or Stop on the stopped candidate, is safe.

The ignored delivery directory contains the actual manifest, frozen Compose files and
snapshot receipts. The original private environment file is outside source. The sanitized
`candidate.env.example` describes its keys; it is not a replacement for existing private
configuration. Preserve the original database password file, key volume, secret vault and
host binding together. The base Compose file has a development default; the frozen override
explicitly selects Production and the inspected loopback Docker gateway. The gateway trust is
valid only with the checked loopback binding. See [container operations](../../../docs/operations/containers.md).

Backup stops application writes, creates a PostgreSQL custom-format dump and storage/key
archives, then stops the database. It hashes all three archives and refuses an existing
target. Start the candidate explicitly afterwards.

```powershell
./tools/demo/workbench-wb6/Backup-Demo.ps1 -CandidatePath $candidate -SnapshotPath 'artifacts/workbench-completion-wb6/demo-candidate/snapshot-next'
./tools/demo/workbench-wb6/Start-Demo.ps1 -CandidatePath $candidate
```

Restore requires a new child directory, separate unused loopback port and non-overlapping
canonical IPv4 /28 networks. It verifies snapshot identity/hashes, creates new named volumes,
restores database/storage/keys and starts the exact application image. It preserves the host
binding needed to decrypt the copied keys and sets the local operator ingress to the new
frontend gateway. It never overwrites the original candidate or a previous recovery target.

```powershell
./tools/demo/workbench-wb6/Restore-Demo.ps1 -CandidatePath $candidate -SnapshotPath 'artifacts/workbench-completion-wb6/demo-candidate/snapshot-03' -TargetDirectory 'artifacts/workbench-completion-wb6/demo-candidate/recovery-next' -Port 55562 -FrontendSubnet '10.231.240.160/28' -BackendSubnet '10.231.240.176/28'
```

The example networks and port must still be free when used; conflicts fail explicitly.
If a command fails, inspect the exact owned container and retained log, correct the cause and
use a new backup/restore target. No command silently manufactures credentials or removes a
failed attempt. Run only trusted local manifests: these are operator scripts, not an importer
for untrusted manifests or archives.

The [Czech runbook](../../../docs/operations/DEMO_RUNBOOK_CS.md) identifies the preserved project,
actual model results, blocked journeys and tested recovery targets. Structural completion,
runnable delivery and customer-demo readiness are separate results.
