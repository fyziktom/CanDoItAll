# N005: normal local Simple Chats access

User feedback, 2026-08-27:

> I wanted to test it via simple chat that I will create, but in
> http://localhost:5212/agents?tab=simple-chats
> it tells me this:
> Access required
> Definitions are unavailable
> Read Simple Chats permission is required to view reusable definitions
> It is some bug. because any instance should be able to allow creation of simple chats.
> analyze it and repair it.

This reopens normal desktop UI acceptance. Earlier scoped-token browser proof remains
valid for shared-provider routing, but did not test the user's anonymous local browser.
