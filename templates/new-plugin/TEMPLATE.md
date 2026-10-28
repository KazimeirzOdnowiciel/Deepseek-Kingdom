# New plugin template

A compiling, format-clean, warnings-as-errors clean Dalamud plugin that already conforms to
`CONTRIBUTING.md`. Copy it, rename it, delete what you do not need.

Verified 2026-10-28: builds with `0 Warning(s)` under `TreatWarningsAsErrors`, passes
`dotnet format --verify-no-changes`, its 6 unit tests pass, and its UI golden image compares clean.
Renaming it to a different plugin and changing one button label made the gate fail
(`previews: mismatch`), which is the point of the golden.

## Instantiate

For a plugin called `FooBar`, copy **three** projects. All three are siblings under `src/` — the tests
project is never nested inside the plugin folder, because the SDK's default `**/*.cs` glob would then
compile test sources into the plugin.

```powershell
$Name = 'FooBar'
Copy-Item -Recurse templates/new-plugin/PluginName         "src/$Name"
Copy-Item -Recurse templates/new-plugin/PluginName.Tests    "src/$Name.Tests"
Copy-Item -Recurse templates/new-plugin/PluginName.Previews  "src/$Name.Previews"
Remove-Item "src/$Name/bin","src/$Name/obj","src/$Name.Tests/bin","src/$Name.Tests/obj","src/$Name.Previews/bin","src/$Name.Previews/obj" -Recurse -ErrorAction SilentlyContinue
```

Then rename. Every occurrence, and nothing else:

| From | To |
| --- | --- |
| `*.csproj` file names | `FooBar.csproj`, `FooBar.Tests.csproj`, `FooBar.Previews.csproj` |
| `namespace PluginName`, `.Windows`, `.Services`, `.Views`, `.Tests`, `.Previews` | same with `FooBar` |
| `<Compile Include="..\PluginName\Services\**\*.cs" …>` in the tests csproj | `..\FooBar\Services\**\*.cs` |
| `<ProjectReference Include="..\PluginName\PluginName.csproj" />` in the previews csproj | `..\FooBar\FooBar.csproj` |
| `<InternalsVisibleTo Include="PluginName.Previews" />` in the plugin csproj | `FooBar.Previews` |
| `new WindowSystem("PluginName")` | `new WindowSystem("FooBar")` |
| `"/newplugin"` (`CommandName`) | `"/foobar"` |
| `###PluginNameMain`, `###PluginNameConfig` | `###FooBarMain`, `###FooBarConfig` |
| `public static string DisplayName => "Plugin Name"` | `"Foo Bar"` |
| `"Plugin Name###...` window titles | `"Foo Bar###...` |
| csproj `<Name>`, `<Punchline>`, `<Description>`, `<Tags>`, `<Version>` | real values |

`FooBar` becomes the **InternalName** (from `AssemblyName`) and therefore the config folder, log
tag and plugin identity. It cannot change after release.

### Re-render the golden, once

The committed `goldens/main.png` is a render of the template's placeholder text. Renaming changes
that text, so the golden no longer matches:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\render-goldens.ps1 -Name FooBar
```

Look at the PNG it writes, then commit it. From then on the golden is yours and the gate checks it.

Do **not** add the new plugin to the `$plugins` list in `build.ps1` until it is ready to publish;
until then it is simply unlisted.

## Layout, and why the UI is split into a view

```
src/FooBar/
├── Plugin.cs            entry point, services, command, host lifecycle
├── Configuration.cs     IPluginConfiguration + Migrate
├── Views/               draws from a state object, no service, no wall clock
│   ├── MainView.cs
│   └── MainViewState.cs
├── Windows/             ImGui window shell: flags, rounding, gathers state, applies results
│   └── MainWindow.cs
└── Services/            Dalamud-free logic the tests compile by path
```

`MainWindow` reads the game and the config into `MainViewState`, calls `MainView`, then applies what
came back. The view touches nothing from Dalamud. That split is not decoration: it is what lets
`FooBar.Previews` draw the *same* view with fixed sample data, which is how a UI regression is caught
without launching the game.

## The UI is Glasswork, and that is not optional dressing

The template draws its windows with [Glasswork](https://github.com/XeldarAlz/FFXIV-Glasswork) (MIT,
`0.1.0-beta.2`, API 15) instead of stock ImGui: real refracting glass behind the pane, `Gw` widgets
in place of ImGui's controls, and a `Look` that restyles everything from one assignment.

- `Plugin.cs` sets `Host.Look = Look.Eorzea()`. Other families: `Neutral`, `Liquid`, `Minimal`,
  `HighContrast`, `Retro`, `Classic`, `ClearBlue/Grey/Green/Pink`, `Aurora`, `Catppuccin`, `Nord`,
  each with a `Light` variant. `Look.FromBase16(...)` takes a scheme.
- `Gw.Toggle/Slider/Button/Toast/BeginSection/BeginRow/BeginCard` keep ImGui's cursor flow and return
  values, so a window converts one control at a time and still works at every step.
- The second `Gw.BeginSection` argument is a **footer**, drawn at the bottom of the section — not a
  subtitle. It is the right place for a hint, the wrong place for the section's title.
- Windows use `ImGuiWindowFlags.NoBackground` + `host.DrawWindowBackground(radius, tint, isLight)`,
  with a matching `ImGuiStyleVar.WindowRounding` pushed in `PreDraw` and popped in `PostDraw`.

Two build facts that bite: the four `PackageReference`s are a prerelease, so they are **pinned**; and
the `TerraFX.Interop.Windows` `Reference` is **not optional** — the glass renderer compiles against
the copy Dalamud ships, and `Private="false"` keeps that 28 MB file out of the plugin output. The
four Glasswork assemblies add roughly 900 KB to the shipped zip.

## The golden image

`FooBar.Previews` builds an `IPreviewScreen` and `glasswork-preview` renders it headless on WARP —
its own Direct3D 11 device, real ImGui, your Dalamud fonts — then compares it with
`goldens/main.png`. `scripts/check.ps1` runs it as step 4, so a UI change that nobody meant fails the
gate in CI, with the moved pixels highlighted in red under `artifacts/previews/`.

What the golden covers: layout, spacing, typography, control states and the card panel — because the
view draws a real `Gw.BeginCard()`. What it does not cover: the glass itself, which is the host's and
never runs in the preview. So the golden will not catch "the pane stopped refracting", but it will
catch "the button moved and now overlaps the label".

The tool is pinned in `dotnet-tools.json`, so `dotnet tool restore` then
`dotnet glasswork-preview …` works anywhere. If a local manifest restore is refused (Windows
marks the file), install it globally once: `dotnet tool install --global Glasswork.Preview --prerelease`.

## What to delete

- `Services/Throttle.cs` and `ThrottleTests.cs` — a worked example of a pure, testable helper.
- `ProbeItemSheet()` in `MainWindow.cs` — demonstrates Lumina/`IDataManager` access.
- Any `[PluginService]` you do not use. An unused one is not free: it forces the service to
  resolve at load, and fails the plugin load if that service is unavailable.

## Why the tests project compiles the plugin's source instead of referencing it

The tests csproj lists `..\FooBar\Services\**\*.cs` as `Compile` items and deliberately does **not**
have a `ProjectReference`. Two consequences, both wanted:

- a Dalamud type used anywhere under `Services/` fails the **test** build with `CS0246`, so
  `CONTRIBUTING.md` T1 is enforced by the compiler rather than by a reviewer's opinion;
- a new file dropped into `Services/` is covered automatically, with no csproj edit.

That is also why `[PluginService]` usage, `Configuration` and anything touching ImGui must stay
outside `Services/` — they cannot be compiled into the test assembly.

## What not to delete

- `<StrictConventions>true</StrictConventions>` in the plugin csproj.
- The `try`/`catch` in every `Draw()` and around every game read.
- `Configuration.CurrentVersion` / `Migrate()`.
- The `###` stable id in window titles.
- `Dispose()` unhooking every handler it subscribed, and calling `Host.Dispose()` after that.
- The `Host.BeginFrame()` / `Host.EndFrame()` pair around `WindowSystem.Draw`.
- The Glasswork `PackageReference`s and the `TerraFX.Interop.Windows` `Reference`.
- The view/state split, or the previews project stops being able to draw the window.
- `goldens/main.png`, unless you are replacing it with a deliberate re-render.
