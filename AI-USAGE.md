# AI usage

This records how AI tooling is used to build the plugins in this repository, and what level of
involvement that amounts to. Read it before opening a pull request or trusting a plugin blindly.

These plugins ship from a custom repository, so the [official Dalamud repository's AI
policy](https://dalamud.dev/plugin-publishing/ai-policy/) does not bind them. This repository holds
to that policy anyway: it is the community's standard for what honest AI use looks like, and a
plugin nobody is forced to audit should be one that can survive an audit.

## Declared level

Dalamud's policy uses six levels adapted from [AI-DECLARATION.md](https://ai-declaration.md/):
None, Hint, Assist, Pair, Copilot, Auto.

**This repository declares Copilot.** AI implements; the human plans, reviews, tests in game and
owns the result.

That is the honest label rather than the lowest one that could be argued. Inside a task the
implementation runs unattended — which reads as Auto if you look only at that step — but it is
bounded on both ends by a human who decides what gets built and whether it ships, which is what
Copilot describes. If the owner considers the level higher, change this paragraph; nothing else in
the repository depends on it.

## How the work actually happens

- **Planned by the human.** What to build, the rules it must keep, and how it will be checked are
  written down before any agent runs. `CONTRIBUTING.md` is that rulebook, and its rules carry ids
  (`N4`, `A2`, `T1`) so a finding can cite one instead of paraphrasing.
- **Scaffolded from a template.** `templates/new-plugin/` is the only starting point for a new
  plugin. It compiles, passes its unit tests and passes the format gate on day zero, so a task never
  begins by re-deciding project layout.
- **Implemented by three agents, integrated and reviewed by a fourth.** One
  lead writes the spec, splits the work into three disjoint file sets with a contract between them,
  and scaffolds from the template. Three implementers then write their own part in parallel — pure
  logic, the view, the wiring — and the lead integrates what they produced, reviews its own diff
  through the four lenses (suite conventions, API correctness, client safety, official-repo
  admission) and runs the gate.

  **The lead reviews its own work, and that is a real reduction in independence.** Independent
  reviewers are what found the two defects this template has had so far (a config write inside the
  draw pass, and per-frame `Error` logging in the window catches). For a plugin that will be
  published rather than tried, add a read-only review of the diff by an agent that wrote none of it.
- **Verified by machines, then by a human in game.** `scripts/check.ps1` settles build, unit tests
  and formatting, and the UI golden settles layout. It settles nothing about behaviour.

The shape is written down in the `dalamud-new-plugin` skill; `references/pipeline.js` is the same
shape as a script, for runs that must happen unattended. The `.editorconfig` and the golden are
enforced by tools, not by an agent's opinion.

## The human gates, none skippable

1. **The plan comes first.** No agent starts on an unwritten plan.
2. **The diff is read.** Output is reviewed before it lands, not after users find the problem.
3. **It is tested in the game before release.** On a real client, at least one supervised run end to
   end, watched the whole way, when the change touches anything the player sees or anything that
   writes to the game. A clean build and a green test run do not replace this.
4. **Ownership transfers.** Every merged line is the owner's to defend, explain and fix. "The AI
   did it" is not an answer to why something is written the way it is.
5. **Feedback is taken on its merits.** AI-assisted work invites sharper review. The answer to a
   review comment is a fix or a reason, never a defence of the tooling.

## What this does not change

Nothing here lowers a standard. AI-assisted code meets the same bar as any other change: the
`CONTRIBUTING.md` rules, the `.editorconfig` style, the deterministic gate, and one concern per
commit. A reviewer cannot tell which lines came from where, and that is the point.

What AI use does change is **where the verification effort goes**. AI gets Dalamud and
FFXIVClientStructs APIs wrong often enough that any call into either is suspect until it has run in
game. A call that compiles says nothing about whether the service answers it the same way at
runtime. The deterministic gate catches the cheap half of this (it is the same reason `A2` bans
API-16-fragile patterns and `A3` says never to trust a remembered signature across an API bump);
only the client catches the other half.

## If you contribute

1. **Declare your level in the pull request description** if you went beyond autocomplete or inline
   suggestions. Use Dalamud's six level names so the vocabulary matches theirs. None and Hint need
   no declaration.
2. **Test your change in game yourself** before opening the PR.
3. **Be able to explain your code.** If you cannot say why something is written the way it is, it is
   not ready.

Nobody is judged for the level they declare. An undeclared one is the problem.

## Gotchas

- **Nothing enforces this, which is the whole point.** A custom repository has no review queue and
  no ban list, so every rule here holds because the project chose it. Standards that only survive
  enforcement are not standards.
- **Commits carry no co-author trailers or "generated with" footers.** Disclosure lives in this
  document and in pull request descriptions. That is why this document has to exist: without it,
  nothing in the history would say how the code was written.
- **A clean build is not a test.** AI output compiles far more reliably than it works. Window flows,
  addon reads and market data differ per client language and per world, so a fix proven in one
  language or on one world says nothing about another.

## Related

- [CONTRIBUTING.md](CONTRIBUTING.md) — the rulebook, the review lenses and the release checklist.
- [templates/new-plugin/TEMPLATE.md](templates/new-plugin/TEMPLATE.md) — how a new plugin starts.
- [scripts/check.ps1](scripts/check.ps1) — the deterministic gate.
