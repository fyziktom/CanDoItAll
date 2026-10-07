# Embedded browser

The original `CanDoItAll.AppComponents.EmbeddedBrowser` renderer, isolated with its scoped
CSS and BaseLib dependency. The original AppComponents assembly forwards the public type.
Consumers provide the URL and embedding decision; this component retains the existing
scheme, loopback, cross-origin sandbox and external-link restrictions. It owns no reads,
processes, navigation state or project authority.
