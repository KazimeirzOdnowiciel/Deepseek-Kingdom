# Icons

One PNG per published plugin, named exactly `<InternalName>.png` — the **InternalName** is the
`AssemblyName` of the project, not the display name.

- 512×512, square, transparent background preferred.
- `build.ps1` copies it to `dist/icons/<InternalName>-<version>.png`, sets `repo.json`'s `IconUrl`
  to that path, **and** embeds the `IconUrl` into the manifest inside the zip.

That last part matters: the installed-plugin list reads the manifest inside the zip, not `repo.json`,
so an icon that is only referenced by `repo.json` vanishes the moment the plugin is installed.

No icon is fine — the entry just gets an empty `IconUrl` and Dalamud shows its default.
