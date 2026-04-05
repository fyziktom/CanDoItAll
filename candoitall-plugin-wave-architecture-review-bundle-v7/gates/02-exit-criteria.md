# Exit criteria

The phase7 refactor may be considered complete only when:

- all hard blockers are closed with code and tests
- the conditional blocker is either solved or the connector wave explicitly forbids outbound side effects
- the hard-gate script passes
- build/test/run validation passes in a real .NET environment
- senior QA confirms that the repeated blockers are actually gone
