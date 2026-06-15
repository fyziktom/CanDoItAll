# Validate first build and test discovery

Run restore/build and a targeted test command or discovery command. Capture the command, exit code, warnings, and any remaining setup risks.

Keep this as setup validation. Do not launch runtime, start a web app, run browser proof, edit feature behavior, or replace generated starter UI/content. Runtime and browser proof belong to downstream validation steps unless the parent process defines a separate runtime-proof setup step.
