# Deepseek-Kingdom — strict plugin workshop

The second repo: where new plugins are built from `templates/new-plugin/` under the full gate.
`CONTRIBUTING.md` is canonical; this file is the working summary. Workspace context is in
`D:\Deepseek\AGENTS.md`.

## Start a new plugin

One command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\new-plugin.ps1 -Name FooBar
```

It copies the three projects as siblings under `src/` (never nested — the SDK would compile tests
into the plugin), renames every template token, re-renders the golden and runs the gate. Add
`-DisplayName "Foo Bar" -Punchline ... -Description ... -Tags ...` when you already know the manifest
text. It never edits `build.ps1` and never commits.

Load skill `dalamud-new-plugin` for the pipeline, and `glasswork-ui` for any UI.

## Gate

`scripts/check.ps1 -Name <Name>` runs all four steps and prints JSON; it exits 0 only when all pass.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check.ps1 -Name TerritoryCard
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-all.ps1
```

Manual equivalent, in this order:

```powershell
dotnet build "src/<Name>/<Name>.csproj" -c Release
dotnet run --project "src/<Name>.Tests/<Name>.Tests.csproj" --no-build
dotnet format "src/<Name>/<Name>.csproj" --verify-no-changes
glasswork-preview --assembly "src/<Name>.Previews/bin/Release/<Name>.Previews.dll" --compare "src/<Name>.Previews/goldens" --warp
```

- Use `dotnet run`, not `dotnet test` (VSTest cannot open its handle in this sandbox).
- Scope `dotnet format` to the project; unscoped it rewrites the whole tree.
- `check.ps1 -Base templates/new-plugin` validates the template itself.

## Rules that bite

- **T1** Testable logic lives in `Services/`, Dalamud-free; tests compile those files by path.
- **T2** Never claim in-game verification. Previews prove layout only; behaviour is the human's checklist.
- **T6** A moved golden is intentional only after looking at the image; commit it with the change.
- **T7** `Views/` draw from their state object only — no services, no `DateTime.Now`, no file reads.
- **N10** One plugin per folder, tests in a sibling `src/<Name>.Tests/`.
- **N11** Glasswork host: `BeginFrame` first, `EndFrame` last (in `finally`), dispose last.
- Review findings cite a rule id (`N3`, `A2`, `T1`, …). "Looks good" is not a review.

## Publishing

1. Add the plugin to the `$plugins` array in `build.ps1` (being in `src/` publishes nothing).
2. Drop `icons/<InternalName>.png`.
3. Bump `<Version>` in the csproj.
4. Run `build.ps1`, then commit `dist/` **and** `repo.json` in the same commit.
5. Keep `LICENSE` + `THIRD-PARTY-NOTICES.md` embedded; packaging refuses without both (P5).

## Do not

- Retrofit the nine shipped plugins from `PluginRepo` — they are out of scope here.
- Edit `dist/` or `repo.json` by hand.
- Update a golden just to turn the gate green.
- Weaken `.editorconfig` or `Directory.Build.props` to silence a finding.
