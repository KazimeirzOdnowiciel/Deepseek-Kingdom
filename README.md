# Deepseek-Kingdom

A workshop for building new Final Fantasy XIV **Dalamud** plugins: a rulebook, a starting template,
and a deterministic gate. Finished plugins ship from a separate distributor repository - this repo
exists so a new plugin does not start with nine different ideas about project layout.

| Path | What it is |
| --- | --- |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | The rulebook. Every rule has an id (`N4`, `A2`, `T1`, `P4`) so a review finding can cite one instead of paraphrasing. |
| [`AI-USAGE.md`](AI-USAGE.md) | The declared AI level (Copilot), the five human gates, and the honest limits. |
| [`templates/new-plugin/`](templates/new-plugin/TEMPLATE.md) | A compiling plugin plus its unit-test project, already conforming to the rulebook. |
| [`scripts/check.ps1`](scripts/check.ps1) | The gate: build, tests, format. One command, JSON on the last line. |
| `.editorconfig`, `Directory.Build.props` | The machine half of the rulebook: formatting, nullable, analyzers, warnings-as-errors. |

## Start a plugin

```powershell
$Name = 'FooBar'
Copy-Item -Recurse templates/new-plugin/PluginName      "src/$Name"
Copy-Item -Recurse templates/new-plugin/PluginName.Tests "src/$Name.Tests"
```

Then rename every token in the table in [`templates/new-plugin/TEMPLATE.md`](templates/new-plugin/TEMPLATE.md).
The plugin becomes the **InternalName** (config folder, log tag, plugin identity), so pick it once and
never change it after release.

## Check it

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check.ps1 -Name FooBar
```

It prints `{"plugin":"FooBar","build":true,"tests":"passed","format":"clean","ok":true}` and exits 0
only when all three pass. Formatting is enforced by `dotnet format`, so never argue style in review -
and always scope it to one project, or it will reformat every plugin in the tree.

The gate proves that it **compiles, passes its unit tests, and is formatted**. It proves nothing about
behaviour. Nothing in this repository renders a window; in-game behaviour is the human's checklist.

## What the template gives you

A working plugin on [Glasswork](https://github.com/XeldarAlz/FFXIV-Glasswork) (MIT, API 15): real
refracting glass behind the pane, themed `Gw` widgets instead of stock ImGui controls, a `Look` that
restyles everything from one assignment, `IPluginConfiguration` with a `Migrate()`, a `WindowSystem`
with stable `###` window ids, a slash command whose help enumerates its subcommands, and a
`Services/` folder whose contents the test project compiles **by path** - so a Dalamud type in
"pure" logic fails the test build rather than relying on a reviewer to notice.

## Seeing the UI without the game

Glasswork ships a headless renderer:

```powershell
dotnet tool install --global Glasswork.Preview --prerelease
glasswork-preview --scene widgets --warp --out preview.png
```

It draws real ImGui on its own D3D11 device (WARP on CPU) using your Dalamud fonts, and it can diff
against a golden image - the only UI regression test available here. A `.Previews` project per plugin
is the intended next step and does not exist yet.

## Status

The template builds with **0 warnings** under `TreatWarningsAsErrors`, its **6 unit tests pass**, and
`dotnet format --verify-no-changes` is clean. A copy instantiated into `src/` and renamed passes
`scripts/check.ps1` end to end. The nine released plugins are unaffected by any of this - they live
elsewhere and do not opt in to the strict settings.

Not verified, and not verifiable here: anything that needs the game running.
