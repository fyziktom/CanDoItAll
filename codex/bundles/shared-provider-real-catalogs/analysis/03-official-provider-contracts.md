# External Provider Contract References

These are external documentation URLs, not local proof-artifact paths. Consulted while
repairing the real upstream contract; the installed SDK wire tests and actual final
provider execution are the behavioral proof.

- [OpenAI Chat Completions request contract](https://developers.openai.com/api/reference/resources/chat/subresources/completions/methods/create): documented reasoning option values.
- [OpenAI image generation response](https://developers.openai.com/api/reference/resources/images/methods/generate): image metadata and usage.
- [OpenAI pricing](https://developers.openai.com/api/docs/pricing): supported configured rates; unknown model rates stay absent.
- [Microsoft Agent Framework context providers](https://learn.microsoft.com/en-us/agent-framework/journey/adding-context-providers): context composition. Tests use the installed framework version, not an assumed latest package.
