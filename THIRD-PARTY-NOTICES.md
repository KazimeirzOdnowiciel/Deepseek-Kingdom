# Third-party notices

The plugin code in this repository is MIT licensed — see [`LICENSE`](LICENSE). That licence covers
our own code; this file covers the third-party components that get bundled *inside* a plugin.

Every plugin published here ships inside its zip the assemblies listed under **Redistributed**. MIT
and similar licences require their copyright notice and permission text to accompany every copy of
the software, so `build.ps1` embeds this file **and** `LICENSE` in each package (next to the plugin's
own DLLs) and refuses to package without them.

**Add an entry here before shipping anything new inside a plugin.** A dependency referenced but not
copied is not redistributed and does not belong here — see the last section.

## Redistributed

### Glasswork (Glasswork, Glasswork.Backend, Glasswork.Glass, Glasswork.Dalamud)

https://github.com/XeldarAlz/FFXIV-Glasswork — MIT

```
MIT License

Copyright (c) 2026 XeldarAlz and the Glasswork contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Referenced, not redistributed

These are resolved at run time from the Dalamud installation on the player's machine, and the build
keeps them out of the package on purpose (`Private="false"` on the `TerraFX.Interop.Windows`
`Reference`; Dalamud's own SDK does the same for its assemblies). Shipping them again would be both a
licensing problem and a version conflict with whichever Dalamud the player runs.

- **Dalamud** and **Dalamud.Bindings.ImGui** — © goatcorp and the Dalamud contributors.
- **TerraFX.Interop.Windows** — © TerraFX contributors. The glass renderer compiles against Dalamud's
  copy.
- **Dalamud's ImGui bindings, generated runtime and the native `cimgui`** — copyleft, and not this
  repository's to redistribute.
- **Lumina** and **Lumina.Excel** — © goatcorp. Game data is read through `IDataManager` from the
  player's own client.

`Glasswork.Preview` (the headless renderer used by the gate) is a build-time tool, pinned in
`dotnet-tools.json`. It is not shipped inside a plugin and therefore needs no notice there, but the
same MIT terms cover it.
