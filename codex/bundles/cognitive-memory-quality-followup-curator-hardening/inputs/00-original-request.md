# Original User Request

The implementation agent claims it has implemented the previous Cognitive Memory quality bundle. Review the current code again, identify weak spots, incomplete implementation, necessary refactoring, and prepare a follow-up bundle for Codex.

The review must include the newly added curator mechanism. Curator is a special learning mode where the user acts as source of truth, similar to a student talking with a professor. The goal is to avoid overwhelming the user with many manual approve/reject proposals while still allowing the memory to learn correctly. The professor-provided information should be remembered for some time, compared against existing memories, used to improve clusters and aggregated thoughts, and eventually fade when the memory has truly internalized the knowledge through other derived memories.

Do not include economic memory governance models yet. Focus on making the basic memory foundation work well.
