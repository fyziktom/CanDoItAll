# Execution principles

1. Characterize before changing.
2. Lock canonical models before persistence.
3. Keep generic transcript behavior backward compatible.
4. Keep product behavior out of `Llm.Conversations`.
5. Keep EF and profile-runtime dependencies out of the domain project.
6. Use the existing provider registry and database runtime identity.
7. Make every paid command idempotent.
8. Model crash gaps explicitly.
9. Test narrow ownership slices during development.
10. Run the full stable Release gate only once.
11. Do not touch UI.
12. Do not claim enterprise-chatbot support beyond the readiness seams documented here.
