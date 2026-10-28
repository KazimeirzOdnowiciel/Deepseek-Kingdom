# Contributing

This repository builds and ships Final Fantasy XIV Dalamud plugins: the rulebook, the template, the
deterministic gate, and the sources. What ships here reaches players through this repository's own
custom repository (`repo.json`), so a bug means a broken UI mid-duty - correctness and not crashing
the client outrank elegance, novelty, and clever abstractions.

Rules carry stable ids (`N3`, `A2`, `T1`, …). **Review findings must cite the id** — reviewers are
not allowed to file opinions, only rule violations or defects.

## Scope

| | Status |
| --- | --- |
| **New plugins** | Built from `templates/new-plugin/` under the full strict gate below. |
| **The nine earlier plugins** | `SlotYield`, `RouletteBingo`, `CritSound`, `EnemyListCastBars`, `DesynthLens`, `MechanicWatch`, `GilLedger`, `DutyRecap`, `CrashTrace`. They live in an earlier repository and are **out of scope here**; exempt from the strict gate until each is migrated deliberately. Do not retrofit them as a drive-by change. |

## Two kinds of rules — do not confuse them

| Kind | Enforced by | Failure mode | Disputed in review? |
| --- | --- | --- | --- |
| **Deterministic** — formatting, whitespace, line endings, using order, braces | `.editorconfig` via `dotnet format` | build failure | No. The tool decides. |
| **Deterministic** — nullable, analyzers, unused usings, style diagnostics | `Directory.Build.props` via `dotnet build` | build failure | No. The tool decides. |
| **Judgmental** — conventions, API level, client safety, admission | a reviewer reading the diff | review finding with a rule id | Yes, with a cited rule. |

Consequence for reviewers: **never file a finding about whitespace, brace placement, or using
order.** If `dotnet format --verify-no-changes` passes, formatting is settled. If it fails, that is
a build failure for the coder to fix, not a review comment.

### How strictness is opted into

`Directory.Build.props` applies `Nullable`, `AnalysisLevel=latest-recommended`,
`EnforceCodeStyleInBuild`, and `TreatWarningsAsErrors` **only** to projects that set
`<StrictConventions>true</StrictConventions>`. Every plugin created from the template does. The
nine released plugins do not, which is why adding this file did not change their builds.

`CA1031` (catch general exception types) is suppressed on purpose — see `N4`. It is not a licence to
swallow exceptions silently.

## Non-negotiables

**N1 — Talk to the game in the documented order.** Dalamud service interfaces first
(`IDataManager`, `IPlayerState`, `IClientState`, `ICommandManager`, …), then ClientStructs
(`FFXIVClientStructs.*`, shipped inside Dalamud, no NuGet), then raw signatures/hooks as a last
resort. Do not bundle a custom ClientStructs build.

**N2 — Game data comes from Lumina through `IDataManager`, never from an HTTP API on the game
thread.** `Svc.Data.GetExcelSheet<Item>()`, not `xivapi.com` and not `universalis.com` for sheet
lookups. Universalis is acceptable for market prices only, off the render path, through
`Market/UniversalisClient.cs`. Research during development may use XIVAPI v2
(`https://v2.xivapi.com/api/...`); shipping code may not.

**N3 — Every subscription is unsubscribed in `Dispose()`.** A surviving handler draws into a
disposed `WindowSystem` after `/xldev` reload and faults the client UI. Every `AddHandler`,
`+=`, and `Register` needs its inverse.

**N4 — Anything touching the game fails soft.** Packet reads, addon reads, hook callbacks, Excel
lookups, texture loads and `Draw()` are wrapped, and the failure is **logged** via
`Plugin.Log.Error`/`.Debug` with the exception. A silent `catch { }` violates this rule as surely
as an unguarded call. Never log at `Information`+ level from a per-frame path.

**N5 — Configuration implements `IPluginConfiguration` with a `Version` and a `Migrate()`.** New
fields get defaults in `Migrate`; existing fields are never deleted or renamed without a version
bump, because the user's JSON on disk is not inspectable from the plugin.

**N6 — Window titles end in a stable `###` id.** `"Foo###FooMain"`. Changing the visible text must
never change the ImGui id, or every user's window position and size resets.

**N7 — `AssemblyName` is frozen after first release.** It is the InternalName, the config folder
(`%APPDATA%\XIVLauncher\pluginConfigs\<InternalName>\`), and the log tag. Renaming it orphans every
user's configuration.

**N8 — Slash commands are registered through `ICommandManager` with a `HelpMessage` that enumerates
every subcommand.** `"Opens the window"` is not a help message. Unknown subcommands print an error
and a suggestion, they do not silently do nothing.

**N9 — UI comes from Glasswork, not raw ImGui or inline literals.** Widgets are `Gw.*`; colour is a
`Look` token read from `host.Look`, never a literal `Vector4`; a window's pane is
`host.DrawWindowBackground(...)`, with `ImGuiWindowFlags.NoBackground` and a `WindowRounding` push
matching the radius. Where no `Gw` equivalent exists, plain ImGui is fine — but do not paint your own
control backgrounds or hand-pick colours. Changing `host.Look` must restyle the whole plugin.

**N10 — One plugin per `src/<Name>/` folder, one sibling `src/<Name>.Tests/`.** The tests project is
never nested inside the plugin folder: the SDK's default `**/*.cs` glob would compile test sources
into the plugin assembly.

**N11 — The Glasswork host outlives every window and is disposed last.** `Host.BeginFrame()` is the
first call in `UiBuilder.Draw`, `Host.EndFrame()` the last, and in `Dispose()` the `Draw`
subscription is removed **before** `Host.Dispose()`. A glass callback recorded after the layer is
gone faults the client. The renderer falls back to a flat fill when the device is not ready or the
GPU budget is spent, so no code may assume glass actually rendered.

Two traps this rule has already caught in real code:

- `Host.EndFrame()` belongs in a `finally`. A window that throws must not leave the frame the host
  began open forever.
- `Window.OnOpen` and `OnClose` are invoked from `WindowSystem.Draw`, **outside** the guard around
  `Window.Draw`. A config write there goes through `Plugin.SaveConfigSafe`, never a bare `Save()`:
  an exception in the save aborts the whole draw pass and skips `EndFrame`.

## API level

**A1 — Pin `Dalamud.NET.Sdk/15.0.0` (API 15 / .NET 10) for a new plugin today.** API 16 ships with
patch 8.0; do not target it speculatively.

**A2 — Write API 16-ready from the first commit.** New code must avoid, even on API 15:

- mutable `SeString` and payload mutation → `ReadOnlySeString`, `SeStringBuilder`, `RentedSeStringBuilder`
- `On`-prefixed event names
- `EventHandler<T>` where the action form exists
- comparing `RowRef<T>` instances → compare `RowId`

**A3 — Never trust a remembered signature across an API bump.** Check `dalamud.dev/api` or the
installed XML docs. API 15 removed MinHook and FASM: `HookBackend` parameters and the
`string[] AsmHook` constructor no longer exist.

**A4 — Before building a large feature, check whether it depends on something API 16 removes.**

## Testing

**T1 — Logic that can be tested has no Dalamud dependency, and the compiler enforces it.** Pure
helpers live in `Services/`, which the tests project compiles by path
(`..\<Name>\Services\**\*.cs`) rather than referencing the plugin. A Dalamud type used there fails
the test build with `CS0246`. If a helper needs a service or ImGui to be exercised, that is a design
smell: extract the decision from the effect.

**T2 — Do not claim in-game verification.** No test can run FFXIV, drive the game, or observe a sound
playing; in-game behaviour is verified by the human against a written checklist. Window *appearance*
is the one exception — it is rendered headless and compared with a golden (`T6`), so a layout
regression fails the gate. That still proves nothing about whether the glass refracts, whether a
control does what its label says in the client, or whether the plugin survives a duty.

**T3 — The deterministic gate for a plugin is exactly this, in this order:**

```powershell
dotnet build "src/<Name>/<Name>.csproj" -c Release        # warnings are errors
dotnet run --project "src/<Name>.Tests/<Name>.Tests.csproj" --no-build
dotnet format "src/<Name>/<Name>.csproj" --verify-no-changes
glasswork-preview --assembly "src/<Name>.Previews/bin/Release/<Name>.Previews.dll" `
                  --compare "src/<Name>.Previews/goldens" --warp
```

or `powershell -File scripts/check.ps1 -Name <Name>`, which runs all four and reports structured
results. The previews step is skipped only when the plugin has no previews project.

**T4 — `dotnet test` does not work in the sandboxed agent environment** (the VSTest testhost cannot
open a process handle, and Microsoft.Testing.Platform cannot open its named pipe). Run the test
project as an executable with `dotnet run --project … --no-build`. On a normal machine and in
GitHub Actions, `dotnet test` is fine.

**T5 — Always scope `dotnet format` to the project being changed.** Run from the repository root
without a project argument it will reformat every plugin in the tree.

**T6 — A plugin with a previews project keeps its goldens committed and current.** `src/<Name>.Previews`
renders the main view headless on WARP and compares it with `goldens/<Name>.png`. When a UI change is
intended, re-render with `--update`, **look at the image**, and commit the new golden in the same
commit as the change. Never re-render to turn a red gate green without looking at what moved — the
red diff is the review.

This is why the template splits a window into a `Windows/` shell and a `Views/` body: the view reads
only its state object, so it can be drawn with fixed sample data and no game. A view that reaches for
a service or the wall clock breaks the golden's determinism (`T7`).

**T7 — A view draws from its state and nothing else.** No service, no `DateTime.Now`, no file reads,
no machine-dependent formatting inside `Views/`. Read time from `Frame.Time`, numbers from the state
object. Anything else makes the golden differ between runs for reasons that are not the layout.

## Style

Delegated to `.editorconfig`. Named here only so nobody argues about it in review:

**S1** file-scoped namespaces, 4-space indent, LF, UTF-8, trailing newline.
**S2** `System.*` usings first, outside the namespace, unused usings removed (they are errors).
**S3** braces always, `var` only where the type is apparent from the right-hand side.

## Review

Four independent lenses. A finding's `severity` is one of:

| Severity | Meaning |
| --- | --- |
| `blocker` | Wrong, crashes, corrupts config, violates `N1`–`N4` or `A2`, or makes the plugin unload dirty. Must not ship. |
| `major` | A user-visible defect, or a non-negotiable bent without justification. |
| `minor` | Maintainability, a rule bent in a way that is not a defect. |
| `nit` | Taste. Must cite a rule id, or it may not be filed. |

The four lenses:

1. **Suite conventions** — `N3`, `N5`–`N10`.
2. **API correctness** — `A1`–`A4`; wrong signatures, removed members, deprecated patterns.
3. **Client safety** — `N4` above all; unguarded game access, per-frame allocations, log spam,
   hooks without restore.
4. **Admission** (`DalamudPluginsD17`) — the official-repo bar below.

A review with no findings against a lens must say `pass` for that lens. "Looks good" is not a
finding and is not a review.

## Publishing

**Publication happens in this repository.** `build.ps1` builds every listed plugin, copies each zip
into `dist/`, and regenerates `repo.json` with version-pinned links under
`https://raw.githubusercontent.com/KazimeirzOdnowiciel/Deepseek-Kingdom/main`.
`.github/workflows/release.yml` runs the gate first, then `build.ps1`, then commits `dist/` and
`repo.json` only when a version actually changed.

**P1 — A plugin ships only when it is listed in the `$plugins` array in `build.ps1`.** Being in
`src/` does not publish it. Promotion is: add the entry, drop `icons/<InternalName>.png`, bump
`<Version>` in the csproj, run `build.ps1`, commit `dist/` **and** `repo.json` in the same commit.

**P2 — Never commit regenerated `dist/` or `repo.json` without an `AssemblyVersion` change.** Every
run rewrites `LastUpdate` and the description line endings; artifact churn without a version bump is
noise that hides real changes.

**P3 — `AssemblyVersion` is the third and fourth component** (`0.3.8.3`); bump the last one for a
fix, the third for a feature. Users see the version in `/xlplugins`.

**P4 — The version has four parts, always.** `0.3.8.3`, never `0.3.8`. The SDK stamps the shipped
manifest from the assembly version, .NET pads that to four parts, and Dalamud refuses the install
when the manifest and `repo.json` disagree (`distributed: 2.1.0.0 repo: 2.1.0`). A three-part
version does not fail the build; it fails the install.

**P5 — Everything shipped inside a plugin zip is listed in `THIRD-PARTY-NOTICES.md`.** MIT and
similar licences require the notice to travel with every copy, so `build.ps1` embeds that file in
each package and refuses to package without it. A dependency that is referenced but not copied —
Dalamud itself, its ImGui bindings, TerraFX, Lumina — is resolved from the player's installation and
must not be listed as redistributed. Adding a package that ships DLLs into a plugin means adding an
entry here in the same commit.

## Official repository admission (`DalamudPluginsD17`)

The custom repo is unreviewed and not bound by these; the official repo is. Check before promoting
anything that might be submitted there:

**D1 — Disclose AI use** at the level actually used (Assist / Pair / Copilot / Auto). Undisclosed AI
use in an AI-written submission is a ban; entirely AI-generated submissions are auto-rejected; the
disclosure and PR text must be human-written. This repository's own declared level, pipeline and
human gates are in [AI-USAGE.md](AI-USAGE.md).

**D2 — No automation against the game servers**, no combat augmentation beyond your own party's
information, no parsers/DPS meters, no collecting other players' Content/Account IDs. Automated
crafting, autoroll, cutscene skipping and PvP advantages are non-starters.

**D3** — Slot Yield's repricing and listing automation is custom-repo-only by construction.

## Never

**X1** Rewrite source files with `Get-Content -replace | Set-Content`. It produced `CS1061`/`CS0176`
errors and unreadable diffs in this repository before. Edit, and let the diff be the record.

**X2** Edit another plugin's folder while doing unrelated work.

**X3** Commit `bin/`, `obj/`, or build output.

**X4** Add `<TreatWarningsAsErrors>` or `StrictConventions` to a released plugin as a side effect of
an unrelated change.

## Licence

MIT — see [`LICENSE`](LICENSE). By contributing you agree your contributions are licensed the same
way. Every published package also carries `LICENSE` and
[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) inside its zip, because it bundles Glasswork's MIT
assemblies and their notice has to travel with them (`P5`).
