# Implement the feature or function

Make the minimal code and test changes that satisfy the validation contract. Follow local project patterns, keep state explicit, and block instead of guessing when dependencies or architecture constraints are missing.

When you create or repair tests, keep them deterministic and bounded. Do not write loops that depend only on mutable product state without a maximum iteration guard and an assertion that each state transition succeeded. Use a `workspace_dotnet_test` timeout of 300 seconds or less for focused generated-app tests unless a current diagnostic proves the test suite needs more time.
