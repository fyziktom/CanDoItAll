# Plugin Shop, Package, Install, And Trust Architecture

## Shop Contract

A future public plugin server can expose catalog metadata. Local instances can browse and decide what to install.

Minimum remote catalog fields:

- plugin id;
- package id;
- display name;
- vendor;
- version;
- min/max compatible app version;
- capabilities;
- executor summary;
- settings schema;
- required secrets/scopes;
- package URL or package source reference;
- SHA-256 hash;
- signature metadata;
- release date;
- deprecation/security advisory state.

## Local Install State

Persist local install state separately from remote catalog:

- plugin id;
- installed version;
- enabled flag;
- source kind;
- source reference;
- manifest snapshot JSON;
- trust decision;
- installed at/by;
- last health/check status.

## MVP Rule

Bundled/static plugins can be installed/enabled because code is already part of the application. Remote catalog entries may be displayed and selected only as metadata until package trust/runtime loading is implemented.

## Dynamic Code Loading Review Topics

Before loading external assemblies:

- signature validation and publisher trust;
- package hash verification;
- dependency resolution;
- assembly load context and unloadability;
- version compatibility;
- ability to disable/remove plugin cleanly;
- renderer component trust;
- outbound network policy;
- file/storage/secret capability policy;
- audit and revocation;
- security advisory update flow.

## Do Not Implement In MVP

- downloading executable assemblies from the shop and invoking them;
- loading unsigned DLLs;
- remote Razor component execution;
- package auto-updates;
- marketplace payments/licensing.
