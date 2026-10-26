# CLAUDE.md

Guidance for Claude Code and contributors working on this repository. `README.md` (English) and
`README.fr.md` (French) describe the project for users.

## The project

**TextCase**: a small open-source (MIT) Windows 11 app that changes the case of the selected text **in any app,
also through the Remote Desktop window** (mstsc), like the Chrome extension *Change Case* does in a browser:

1. a **shortcut** (Ctrl+Alt+C) copies the selection, opens a **popup** that shows the result of each case, and pastes
   the chosen one back over the selection **as plain text**;
2. **Ctrl+C twice** (as DeepL) opens the popup on what was just copied;
3. **direct shortcuts** convert at once (Ctrl+Alt+U / L / T / S by default, one per case possible);
4. the **settings window** (shortcuts, the cases of the popup, the word list of the smart cases, a box to try them);
5. a **developer mode** (About): a log, and the popup kept open for a screenshot.

UI in English and French, light/dark/system theme. Author: [QuentinPerou](https://github.com/quentinperou). Sister
projects with the same stack and conventions: NowPlaying (the copy TextCase started from: tray, single instance,
updater, settings, card builders, installer, workflow), ScreenClip, PageCompose, FolderTools, CropPdf. Credits
wording: "Developed by QuentinPerou, with Claude Code (Anthropic)".

**Talk to the author in French.** Code, comments, commit messages and these docs are in English.

**Performance comes first**: the app waits in the notification area all day. At rest: no timer, no polling; the
keyboard hook answers each key with a few array reads; the popup and the settings window are built when they open
and closed after (`MemoryTrim`). Measure before and after a change that runs at every key.

**Git**: commits only in the author's name (repository-local identity `QuentinPerou`, his GitHub noreply address),
**no `Co-Authored-By` line**. **Never push** without his explicit OK: he pushes from his own PC.
Remote `origin`: the public `github.com/quentinperou/TextCase`. A version goes out as `main`, then its tag alone
(`git push origin vX.Y.Z`: the tags are lightweight, `--follow-tags` skips them); the tag's workflow publishes the
release with the installer. The Store version goes out **by hand**, as NowPlaying's: `scripts\build-store.ps1`, then
in Partner Center a new submission (it copies the last one): the new `.msix`, the "What's new" of each listing, Submit.

## Commands

```powershell
.\scripts\build.ps1                       # tests + dist\app (ReadyToRun) + installer
.\scripts\build-store.ps1                 # tests + dist\store\TextCase_<version>.0_x64.msix (the Store version)
dotnet test tests\TextCase.Tests          # unit tests (Core + localization)
dotnet run --project src\TextCase         # run (opens the window; --background opens nothing)
powershell -File scripts\make-icons.ps1   # regenerate the icons (Windows PowerShell 5.1); -Preview: the small sizes
explorer.exe .local\install-build.cmd     # install the newest dist\TextCase-Setup-*.exe for him (see Known pitfalls)
```

Environment variables: `TEXTCASE_LANG=en|fr`, `TEXTCASE_THEME=Light|Dark`, `TEXTCASE_NO_MICA=1` (pictures),
`TEXTCASE_NO_HOOK=1` (the shortcuts are seen by nothing), `TEXTCASE_PICTURE_POPUP=<file.png>` (the popup on
`TEXTCASE_PICTURE_TEXT`, or a product name, drawn off screen into the file; no window opens),
`TEXTCASE_SETTINGS=<file.json>` (another settings file, the swap log next to it: the test runs). Settings live in
`%LOCALAPPDATA%\TextCase\settings.json`, the swap log (developer mode) beside them.

The Store version in development: `Add-AppxPackage -Register dist\store\layout\AppxManifest.xml` (Developer Mode; no
other TextCase running, or both answer each shortcut; from an NTFS drive: on the exFAT USB key, copy the layout to
`%USERPROFILE%\TextCase-store-test` first), started from the Start menu or `shell:AppsFolder\<family>!App`;
removed by `Get-AppxPackage QuentinPerou.TextCasebyQuentinPerou | Remove-AppxPackage` (its data goes with it).

## Working on the project

- **Memory of Claude** for this project is in `.local/claude/memory/` (git-ignored, loaded at start through
  `CLAUDE.local.md`; read `MEMORY.md` there first). The author asked that **all** of Claude's notes live in the
  project folder: never in `~/.claude`.
- `.local/` also holds the mock-ups and their scripts, and the test tools (`.local/README.md` lists them):
  `dev.ps1` (test settings in a file of their own, start, picture, page by UI Automation, stop, his TextCase started
  again), and the `.cmd` files run through Explorer (install, read or reset his real settings).
- The author may be using the PC: drive the app through UI Automation or window messages, never keys or the mouse.
  **TextCase's own job is to send keys** (Ctrl+C, Ctrl+V to the app in front): never trigger a swap without his OK.
  Test runs use their own settings file with the shortcuts paused, so that his own Ctrl+C never opens a popup there.
- To understand what happened, read his log yourself (`%LOCALAPPDATA%\TextCase\swap.log`, developer mode on; reads
  from Claude's shell see his real file). A popup closed without a choice is often his own doing: ask before calling
  it a bug.
- Docs (both READMEs, this file) are kept up to date at the end of each work session. The project status (the
  versions installed, his tries, what is left to check) stays out of the repository, in
  `.local/claude/memory/project-status.md`.

### README pictures

`.github/screenshot.png` (English) and `.github/screenshot.fr.png` (French), 1560 × 810, also the screenshots of the
Store listing: on the gradient of the icon, the real settings window (light theme, Shortcuts page) and the real popup
(the four cases of the default, the Smart switch on) opened on a product name selected in a drawn app.
`.local\readme\capture.ps1 -Lang en|fr` takes the pieces, `.local\readme\compose.ps1` renders `.local\readme\hero.html`.
Retake them when the UI changes (last taken October 26, 2026, from 0.2.0).

## Framing (October 22, 2026, the author's answers)

- A right-click menu item in every app is impossible (each app draws its own menu): a shortcut and a popup instead,
  the result pasted **without formatting**.
- Triggers: a dedicated shortcut, Ctrl+C twice, direct shortcuts (not Ctrl + right click).
- Cases: all those of the extension, plus alternating case and the clean-ups.
- Title / Sentence case: smart, with a word list.
- Remote desktop: TextCase on the local PC only, through the RDP window; nothing on the server.
- Claude's defaults, not asked: the name, the default shortcuts, the delay of Ctrl+C twice (400 ms, 500 since), the
  clipboard put back, start with Windows, tray icon, the popup never takes the focus.
- His later wishes, after trying it: the minimum on by default (the popup with the four basic cases, the Smart switch
  off), a developer mode.

## Architecture

| Folder | Role |
|--------|------|
| `src/TextCase.Core` | Pure, tested logic: `CaseMode` (the cases, their groups, the keys of the popup rows), `CaseConverter` (every conversion, smart and simple), `CaseRules` (the word list, small words, elisions, Roman numerals, the Smart switch), `Shortcut` (modifiers + virtual key, "Ctrl+Alt+C") and `DoublePress`, `ShortcutMatcher` (what each key does: shortcuts, Ctrl+C twice, the popup's keys, missed releases; with `KeyEvent`, `Trigger`, `PopupKey`), `FileNames` (the case of a file or folder name, its extension kept), `SemanticVersion`, `UpdateFeed`. |
| `src/TextCase` | WPF .NET 10. `App` (single instance, tray, hook, the window in front, settings, developer mode), `CasePopup` (+ `PopupColors`; the Smart switch, key 0; beside Start), `Toast`, `MainWindow` (pages built in code), `PopupMenu` + `Menus.xaml` (tray menu), `UpdateBar`, `Localization.cs`; `Controls/`: `Ui` (cards), `ShortcutBox` (records a shortcut); `Services/`: `KeyboardHook` (its own thread), `TextSwap` (copy → popup → paste), `ClipboardAccess` (+ the listener of copies), `Input` (SendInput), `RemoteDesktop`, `ShellWindows` (Start and its search box), `ExplorerMenu` (the menu of File Explorer, `--rename`), `SwapLog` (developer mode: each step, never the text), `TrayHost`, `AppSettings`, `WindowsSettings`, `AppPackage` (the Store version), `SingleInstance`, `MicaBackdrop`, `MemoryTrim`, `Updater`, `NativeMethods`. |
| `tests/TextCase.Tests` | xUnit: conversions, shortcuts and the key logic (a clock of the tests), Ctrl+C twice, update feed, localization key parity. |
| `installer/TextCase.iss` | Inno Setup (English/French), .NET 10 Desktop Runtime check (a silent install, winget or the updater, installs it without asking: no message box), closes the running app. |
| `store/` | The Microsoft Store package: `AppxManifest.xml` (identity of Partner Center, the app and its startup task), `Assets/` (logos, by `make-icons.ps1`) and `listing.md` (what goes into Partner Center, English and French). |
| `scripts/` | `build.ps1`, `build-store.ps1`, `make-icons.ps1`. |
| `.github/workflows/build.yml` | Build + tests on push; release on a `v*` tag (pre-release when the tag has a `-`). |

### Technical choices (and why)

- **A low-level keyboard hook, not RegisterHotKey**: the remote desktop client (mstsc) installs its own hook when
  its window is active and sends the keys to the remote PC before any shortcut of this PC is seen. The last hook
  installed is called first: TextCase installs its hook again on every change of the window in front (Windows also
  removes a hook that once answered too slowly), and every second while a remote desktop window is in front. The
  hook runs on a thread of its own with its own message loop; `ShortcutMatcher` only reads arrays and raises events,
  the work is done on the UI thread.
- **Missed releases**: the keys held are followed from the hook's events (in the remote desktop window Windows' own
  key state never sees them). A key pressed again more than 1.1 s after its last press is a new press (not an
  auto-repeat), and a modifier seen more than 2 s ago that `GetAsyncKeyState` says is up is forgotten: otherwise one
  missed release (Win + L, the remote desktop client taking the keys for a moment) stops every shortcut.
- **AltGr is not Ctrl+Alt**: AltGr sends left Ctrl + right Alt; with them, no shortcut matches (AZERTY and many
  keyboards type characters with AltGr). A shortcut with Alt or Win is followed by an unassigned key (0xE8), so that
  the release of Alt does not open the menu bar, nor Win the Start menu.
- **The popup never takes the focus** (`WS_EX_NOACTIVATE`, `MA_NOACTIVATE`): the app keeps its selection, the remote
  desktop window its keyboard. Its keys come from the hook while it is open (1–9, A–Z, 0 for Smart, arrows, Enter,
  Esc); any other key, a click elsewhere (a mouse hook only while it is open) or another window in front closes it,
  except in developer mode. It puts itself back on top every 250 ms while open.
- **Start and its search box** are drawn by Windows in a layer above every window of an app, even topmost, even owned
  by the taskbar (tried): the popup opens beside them. Above them would need a signed `uiAccess` build.
- **The swap**: wait until the user lets go of the shortcut's keys, keep the clipboard (text, HTML, RTF, files,
  image: not the private formats of an app, rendering all of Excel's takes seconds), send Ctrl+C, wait for the
  clipboard to change (0.8 s, 2.5 s for a remote PC), popup, plain text into the clipboard (kept out of the clipboard
  history), Ctrl+V, the clipboard put back 0.7 s later (2.5 s remote) if nothing else was copied meanwhile. For a
  remote PC the clipboard gets 0.4 s to cross before the paste. **Ctrl+C twice**: the app copies by itself; the
  hidden window of the tray listens to the clipboard, so the popup opens at once when the copy is there, else waits
  for it (and once more for a remote PC whose text is still on its way).
- **Keys sent with their scan code**: the remote desktop client forwards scan codes; a key without one does nothing on
  the remote PC.
- **The menu of File Explorer**: static verbs in `HKCU\Software\Classes\*\shell\TextCase` and `Directory\shell\…`
  (`SubCommands` empty, the cases under `shell`, `MultiSelectModel=Player`), written again at each start of the
  **installed** app (this exe, the language, the cases of the popup; a dev build never writes it, so that no menu
  points to it), removed when the switch is off and by the uninstaller. Each case starts `TextCase.exe --rename Upper
  "path"` once per selected item: that process renames and quits, before the single instance (many items at once
  would race through its one file of arguments). `MoveFileEx`, then `SHChangeNotify` (Explorer may miss a change of
  case only). A name taken by another item is never overwritten.
- **Smart cases** (the Smart switch, off by default; off, `SimpleTitle` capitalizes every word and `SimpleSentence`
  the start of each sentence): words of the list (case-insensitive), words in mixed case, letters against a digit,
  Roman numerals (II to XX) and, in a line not in capitals, words in capitals (acronyms) are kept. A line counts as
  "in capitals" when fewer than a quarter of its letters are lowercase. French elisions start a new word
  (l'Appareil), English contractions do not (Don't).
- **Same stack as the sister projects**: WPF .NET 10 Fluent theme, Mica, resx EN/FR, settings JSON, Inno Setup,
  updater from GitHub Releases.
- **Store version** (as NowPlaying's): one code, one exe, built twice. `build-store.ps1` publishes it self-contained
  with `-p:StoreBuild=true`: the target framework `net10.0-windows10.0.19041.0` (the Windows SDK projection, some
  25 MB, for the startup task) and `STORE` defined; the GitHub build has neither. At run time
  `AppPackage.IsPackaged` (`GetCurrentPackageFullName`) changes the start with Windows (a package's HKCU writes go to
  its own copy, never read at sign-in: the `windows.startupTask` of the manifest and the `StartupTask` API; launched
  by it, `ActivationKind.StartupTask`, it opens no window), the updates (none: the Store forbids it; the Updates page
  opens the Store) and the menu of File Explorer (none, its switch hidden: Explorer never reads the package's copy of
  the registry; the author's choice, October 25, 2026; a Windows 11 menu through an `IExplorerCommand`
  DLL, as FolderTools, would bring it back). It offers once to uninstall the GitHub version (two TextCase at once
  would both answer each shortcut; the uninstaller through Explorer, out of the package; it closes every TextCase).
  Only `runFullTrust`. In the package, a file of `%LOCALAPPDATA%` that does not exist yet is written to the package's
  folder (`Packages\<family>\LocalCache`).

### Localization

English (default) and French, following the Windows display language or chosen in General. `Strings.Get("Mode" +
value)` exists for every `CaseMode` (a test checks). Installer messages in `installer/TextCase.iss`.

## Known pitfalls

- **The shell of the Claude desktop app sees a redirected `%LOCALAPPDATA%`** (writes go to a private copy; reads fall
  through to the real file while no copy exists), and what it starts runs in that context too. A file Claude writes
  there is not the one the author's apps read: test runs use `TEXTCASE_SETTINGS` (`.local\dev.ps1` →
  `.local\test-settings.json`), never his `settings.json`; his real files are read or reset by a `.cmd` run through
  Explorer (`.local\read-real-settings.cmd`, `read-real-state.cmd`, `reset-real-settings.cmd`); a build is installed
  for him through Explorer too (`.local\install-build.cmd`), so that the app the installer starts again reads his
  real settings.
- One TextCase per session (mutex): a test run stops his TextCase; `Start-TextCaseForUser` starts it again through
  Explorer (the installed one if there is one), with his settings. Explorer starts an exe without its arguments
  (`--background` is lost).
- An app run as administrator gets no keys from TextCase (Windows blocks the input of a normal app to an elevated
  one): the copy fails, "No text selected" shows.
- Copy and paste through the remote desktop need the clipboard redirection of the connection (on by default).
- `PrintWindow` renders Mica as the desktop behind it: pictures of the settings window use `TEXTCASE_NO_MICA=1`. A
  second start of TextCase wakes the running one (single instance): stop it fully before a start with other settings.
- `RenderTargetBitmap` of an element with a margin draws it at its margin: the popup's picture adds the shadow room
  on both sides.
- **FAT and exFAT** (USB keys) take a rename that changes only the case for no change at all (`MoveFileEx`
  succeeds, the name stays): `ExplorerMenu.MoveCase` goes through a temporary name. NTFS takes it in place.
- The registry of Claude's shell is redirected too (like `%LOCALAPPDATA%`): the menu of File Explorer is checked in
  his session only (`.local\read-real-state.cmd` for the Run key; `reg query` the same way for the menu).
- A .NET file-based program (`dotnet run x.cs`) has reflection-based JSON off: add
  `#:property JsonSerializerIsReflectionEnabledByDefault=true` and `#:property PublishAot=false`.
- A package cannot be registered from FAT or exFAT (0x80073CFD, "cannot deploy to the path layout on the file system
  type exFAT"): copy the layout to an NTFS drive (not under `%LOCALAPPDATA%`, which Claude's shell sees redirected).
- The certification kit cannot install an unsigned MSIX: register `dist\store\layout` first, then
  `appcert.exe test -packagefullname <full name> -reportoutputpath <xml>` in an administrator prompt.
- `makepri createconfig` puts each scale in a PRI file of its own (`<packaging>`, meant for a bundle): in a single
  MSIX the scale-200 logos would be lost; `build-store.ps1` removes that section. Run makepri from PowerShell: Git
  Bash turns its `/cf` options into paths.
- GitHub creates no push event for more than three tags pushed at once (no workflow, no release): push a version's tag
  alone.

## To do

- [ ] Later, if he uses it: the menu in the short menu of Windows 11 (option 2: an `IExplorerCommand` DLL and a sparse
      package, as FolderTools).
- [ ] **Code signing with SignPath Foundation**, once the repository is public (see "Code signing (planned)" below).
- [ ] **winget** (from 0.1.5, whose installer asks nothing when silent): manifests for `QuentinPerou.TextCase`
      (`InstallerType: inno`, machine scope, `ElevationRequirement: elevatesSelf`, `ProductCode`
      `{4D4C29C7-FEEA-4F84-8A74-1DB539E852D1}_is1`, dependency `Microsoft.DotNet.DesktopRuntime.10`, locales en-US and
      fr-FR), tested in Windows Sandbox, a pull request to `microsoft/winget-pkgs` from his account; once accepted, a
      step of the release job opens the pull request of each new version.
- [ ] **Microsoft Store** (prepared in 0.2.0, not submitted; product "TextCase by QuentinPerou", shown as "Change Text
      Case", Store ID `9N6FJCHCW5XW`, identity set in `store/AppxManifest.xml`): the package registered and tried
      (startup task, the offer to uninstall the GitHub version, a real swap, the Updates page); the certification kit;
      the submission by hand.

## Code signing (planned)

Where **Smart App Control** is on, an unsigned app does not start (and once turned off it only comes back with a
reinstall of Windows). It lets run only what is signed by a certificate of a provider Microsoft trusts; a self-signed
certificate does not count, and there is no exception list. The choice is a **free** one: **SignPath Foundation**
(free for open source), not Artifact Signing (≈ $10 a month, in Europe for companies only) nor a bought certificate.
Not started: SignPath needs the public repository and a release first.

- **The author's steps**: push the repository (public), publish a release, enable MFA on GitHub and SignPath, apply on
  signpath.org (OSI license: MIT; own code only; actively maintained; uninstall tool: the installer has one). Once
  accepted: add the GitHub.com trusted build system to the SignPath project, an API token (secret
  `SIGNPATH_API_TOKEN`), and tell Claude the organization id, project slug, signing policy and artifact configuration
  slugs. Each release then needs his approval in SignPath (two per release, see below).
- **README**: a "Code signing policy" section (both languages) on the home page: "Free code signing provided by
  SignPath.io, certificate by SignPath Foundation" (both names linked), the roles (committers and reviewers,
  approvers: QuentinPerou), and a privacy statement (the only network access is the update check on GitHub, which
  can be turned off; the template sentence starts "This program will not transfer any information to other
  networked systems unless specifically requested by the user…"). Product name and version must be set in every
  signed binary (they are: `Directory.Build.props`).
- **Workflow** (on a `v*` tag, GitHub-hosted runners only): build and test; ISCC once with a Sign Tool that only
  copies the uninstaller out (`uninst.e32.tmp`; this compile then stops, as expected); upload `dist\app` + that
  uninstaller with `actions/upload-artifact`, sign them with `signpath/github-action-submit-signing-request`
  (approval 1, the artifact configuration deep-signs every `.exe`, `.dll` and the `.e32`); ISCC again with a Sign
  Tool that copies the signed uninstaller over `$f`; upload the setup, sign it (approval 2); release.
- **Inno Setup fact** (tested October 22, 2026 with a logging Sign Tool): with `SignedUninstaller=yes`, ISCC writes
  `uninst.e32.tmp` in `SignedUninstallerDir`, runs the Sign Tool on it, then **refuses to go on** ("returned an exit
  code of 0, but the file does not have a digital signature") if it is still unsigned. So the signing must be done
  (or a signed copy put in place) by the Sign Tool command itself.
- The same recipe will serve his other apps (ScreenClip, PageCompose, FolderTools, CropPdf, NowPlaying).
