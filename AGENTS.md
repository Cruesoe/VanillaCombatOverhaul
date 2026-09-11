# Project workflow

- Treat `C:\Users\crues\source\repos\VanillaCombat` as the authoritative repository.
- Treat `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\VanillaCombatOverhaul` as the local Steam upload/deployment folder, not as a repository mirror.
- The local Steam mod folder must contain only files required to run and upload the mod: `1.6`, `About`, `Languages`, and `LoadFolders.xml`. Preserve Steam's `About\PublishedFileId.txt` when it is present so uploads continue to update the existing Workshop item.
- Never deploy repository-only files or directories, including `.git`, `.agents`, `.codex`, `Source`, `Docs`, `Tools`, `Dist`, `AGENTS.md`, `CLAUDE.md`, `README.md`, `modkit.json`, IDE metadata, or build intermediates.
- After every completed mod change, successfully build the mod and run `Tools\package-steam.ps1 -InstallToMods` to stage and deploy the allowed upload content to the local Steam mod folder. Remove any files there that are not present in the allowed repository content, except the preserved `About\PublishedFileId.txt`.
- Verify deployment by comparing relative file paths and file hashes for the allowed content; completion requires zero missing or mismatched files and zero extra files other than `About\PublishedFileId.txt` in the local Steam mod folder.
