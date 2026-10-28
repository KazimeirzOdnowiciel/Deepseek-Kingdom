# New plugin template

A compiling, format-clean, warnings-as-errors clean Dalamud plugin that already conforms to
`CONTRIBUTING.md`. Copy it, rename it, delete what you do not need.

Verified 2026-10-28: builds with `0 Warning(s)` under `TreatWarningsAsErrors`, passes
`dotnet format --verify-no-changes`, and its 6 unit tests pass.

## Instantiate

For a plugin called `FooBar` (copy **both** projects — the tests project is a sibling, never
nested inside the plugin folder, because the SDK's default `**/*.cs` glob would otherwise compile
test sources into the plugin):

```powershell
$Name = 'FooBar'
Copy-Item -Recurse templates/new-plugin/PluginName      "src/$Name"
Copy-Item -Recurse templates/new-plugin/PluginName.Tests "src/$Name.Tests"
Remove-Item "src/$Name.Tests/bin","src/$Name.Tests/obj","src/$Name/bin","src/$Name/obj" -Recurse -ErrorAction SilentlyContinue
```

Then rename. Every occurrence, and nothing else:

| From | To |
| --- | --- |
| `PluginName.csproj` | `FooBar.csproj` |
| `PluginName.Tests.csproj` | `FooBar.Tests.csproj` |
| `namespace PluginName`, `PluginName.Windows`, `PluginName.Services`, `PluginName.Tests` | same with `FooBar` |
| `<Compile Include="..\PluginName\Services\**\*.cs" …>` in the tests csproj | `..\FooBar\Services\**\*.cs` |
| `new WindowSystem("PluginName")` | `new WindowSystem("FooBar")` |
| `"/newplugin"` (`CommandName`) | `"/foobar"` |
| `###PluginNameMain`, `###PluginNameConfig` | `###FooBarMain`, `###FooBarConfig` |
| `public static string DisplayName => "Plugin Name"` | `"Foo Bar"` |
| `"Plugin Name###...` window titles | `"Foo Bar###...` |
| csproj `<Name>`, `<Punchline>`, `<Description>`, `<Tags>`, `<Version>` | real values |

`FooBar` becomes the **InternalName** (from `AssemblyName`) and therefore the config folder, log
tag and plugin identity. It cannot change after release.

Do **not** add the new plugin to the `$plugins` array in `build.ps1` until it is ready to publish;
until then it is simply unlisted and CI ignores it.

## The UI is Glasswork, and that is not optional dressing

The template draws its windows with [Glasswork](https://github.com/XeldarAlz/FFXIV-Glasswork) (MIT,
`0.1.0-beta.2`, API 15) instead of stock ImGui: real refracting glass behind the pane, `Gw` widgets
in place of ImGui's controls, and a `Look` that restyles everything from one assignment.

- `Plugin.cs` sets `Host.Look = Look.Eorzea()`. Other families: `Neutral`, `Liquid`, `Minimal`,
  `HighContrast`, `Retro`, `Classic`, `ClearBlue/Grey/Green/Pink`, `Aurora`, `Catppuccin`, `Nord`,
  each with a `Light` variant. `Look.FromBase16(...)` takes a scheme.
- `Gw.Toggle/Slider/Button/Toast/BeginSection/BeginRow` keep ImGui's cursor flow and return values,
  so a window converts one control at a time and still works at every step.
- Windows use `ImGuiWindowFlags.NoBackground` + `host.DrawWindowBackground(radius, tint, isLight)`,
  with a matching `ImGuiStyleVar.WindowRounding` pushed in `PreDraw` and popped in `PostDraw`.

Two build facts that bite: the four `PackageReference`s are a prerelease, so they are **pinned**; and
the `TerraFX.Interop.Windows` `Reference` is **not optional** — the glass renderer compiles against
the copy Dalamud ships, and `Private="false"` keeps that 28 MB file out of the plugin output. The
four Glasswork assemblies add roughly 900 KB to the shipped zip.

**You cannot see this without the game.** Configuration and behaviour are testable; pixels are not.
Glasswork ships a headless renderer for exactly this —
`dotnet tool install Glasswork.Preview --prerelease` then
`dotnet glasswork-preview --scene widgets --out preview.png` — which is the intended way to check a
window's appearance and, with a golden image, to catch a regression in CI. Wiring that up is worth
doing on the first real plugin; the template does not carry a `.Previews` project yet.

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
