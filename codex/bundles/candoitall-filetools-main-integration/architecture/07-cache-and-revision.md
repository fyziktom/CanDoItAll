# Cache And File-Catalog Revision

## Layers

1. FileTools session retention: UI navigation only. Use Disabled for live project/process/workspace roots unless a story explicitly proves bounded retention.
2. Host listing cache: optional outer decorator using HybridCache memory primary.
3. File-catalog revision: process-local monotonic semantic revision in the first delivery.

## Typed Settings

`StorageBrowseCacheSettings` lives under `StorageProviderConfiguration` in `ConfigJson` and includes `Enabled`, typed mode (`Disabled`, `Memory`, future `Hybrid`), TTL/hard maximum, max page size/items, force-refresh permission, and immutable-version policy. Missing/legacy config becomes Disabled. Invalid combinations fail validation; Hybrid fails when durable/shared revision is absent.

## Key Inputs

Hash canonical bounded values:

- runtime/database generation;
- storage binding/source ID;
- semantic scope ID and include-subprojects/descendants state;
- ordered source-set fingerprint;
- catalog revision or driver-proven immutable version;
- authorization-scope fingerprint for post-authorization entries;
- normalized query/filter/sort/metadata/page fingerprint;
- cache schema version.

Never use raw secrets, paths, handles, signed URLs, streams, content, or unbounded metadata as key/value data.

## Cache Models

- Authorization-scoped entry: key includes actor/grant revision and contains filtered descriptive facts.
- Raw-provider entry: contains only safe provider facts and is reauthorized/mapped on every hit.

Choose one model per decorator/entry. Do not mix them.

## Revision Producers

After successful persistence only: FileInteraction save, storage placement, project structure file/source mutation, project/subproject source-set change, resource promotion/config change, observed workspace receipt, or authorized refresh that proves change. Failed/cancelled operations do not publish.

## Policy Matrix

| Source | Host cache | Session retention |
| --- | --- | --- |
| managed/process/agent live folder | Disabled | Disabled |
| ordinary filesystem binding | Disabled by default | Disabled |
| immutable IPFS CID/DAG | optional long-lived by proven version | Bounded optional |
| mutable IPFS MFS | Disabled/conservative bounded | Disabled |
| FTP | conservative bounded only when configured | Disabled by default |
| project/resource aggregate | optional memory cache with revision/source fingerprint | bounded only when story proves it |

## Distributed Gate

No distributed secondary in this bundle. A future bundle must add durable/shared monotonic revision, multi-node authorization revision, backplane/version proof, and runtime-profile isolation before Hybrid mode can enable it.
