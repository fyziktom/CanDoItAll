# Driver Readiness Position

The original architecture discussion included process helper drivers such as generic, SW-development, .NET, Rust, Office, and business-analysis driver packs.

This bundle should **not** implement those drivers yet.

However, this bundle should prepare a semantic map that future drivers can use:

- tool evidence families: build, test, browser proof, file mutation, project structure, image generation, storage, Office/document analysis;
- evidence satisfaction categories: required tool executed, equivalent tool executed, carried proof allowed, process mock substitution allowed, provider-native proof accepted;
- risk categories: validation, mutation, external side effect, read-only, current-attempt-only;
- future manager verification mode: read-only/ephemeral checks can satisfy validation facts without mutating process state.

Why now:

- Tool validation is where future drivers will eventually report equivalent evidence.
- A documentation-only semantic map reduces later driver guesswork without creating unstable APIs.

Why not production drivers now:

- Process Core is not extracted.
- Tool validation helpers are still module-local.
- Driver APIs would likely bind to unstable dispatcher DTOs if introduced now.
