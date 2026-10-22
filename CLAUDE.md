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
4. the **settings window** (shortcuts, the cases of the popup, the word list of the smart cases, a box to try them).

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

## Commands

```powershell
.\scripts\build.ps1                       # tests + dist\app (ReadyToRun) + installer
dotnet test tests\TextCase.Tests          # unit tests (Core + localization)
dotnet run --project src\TextCase         # run (opens the window; --background opens nothing)
powershell -File scripts\make-icons.ps1   # regenerate the icons (Windows PowerShell 5.1); -Preview: the small sizes
```

Environment variables: `TEXTCASE_LANG=en|fr`, `TEXTCASE_THEME=Light|Dark`, `TEXTCASE_NO_MICA=1` (pictures),
`TEXTCASE_NO_HOOK=1` (the shortcuts are seen by nothing), `TEXTCASE_PICTURE_POPUP=<file.png>` (the popup on
`TEXTCASE_PICTURE_TEXT`, or a product name, drawn off screen into the file; no window opens),
`TEXTCASE_SETTINGS=<file.json>` (another settings file, the swap log next to it: the test runs). Settings live in
`%LOCALAPPDATA%\TextCase\settings.json`, the swap log beside them.

## Working on the project

- **Memory of Claude** for this project is in `.local/claude/memory/` (git-ignored, loaded at start through
  `CLAUDE.local.md`; read `MEMORY.md` there first). The author asked that **all** of Claude's notes live in the
  project folder: never in `~/.claude`.
- `.local/` also holds the mock-ups and their scripts, and the test tools (`.local/README.md` lists them):
  `dev.ps1` (test settings with the shortcuts paused, start, picture, page by UI Automation, stop, settings back).
- The author may be using the PC: drive the app through UI Automation or window messages, never keys or the mouse.
  **TextCase's own job is to send keys** (Ctrl+C, Ctrl+V to the app in front): never trigger a swap without his OK.
  Test runs use settings with the shortcuts paused (`Use-TestSettings`), so that his own Ctrl+C never opens a popup.
- Docs (both READMEs, this file) are kept up to date at the end of each work session. The project status (the
  versions installed, his tries, what is left to check) stays out of the repository, in
  `.local/claude/memory/project-status.md`.

### README pictures

`.github/screenshot.png` (English) and `.github/screenshot.fr.png` (French), 1560 × 1000: on the gradient of the
icon, the real settings window (light theme, Shortcuts page) and the real popup opened on a product name selected in
a drawn app. `.local\readme\capture.ps1 -Lang en|fr` takes the pieces, `.local\readme\compose.ps1` renders
`.local\readme\hero.html`. Retake them when the UI changes.

## Framing (October 22, 2026, the author's answers)

- A right-click menu item in every app is impossible (each app draws its own menu): a shortcut and a popup instead,
  the result pasted **without formatting**.
- Triggers: a dedicated shortcut, Ctrl+C twice, direct shortcuts (not Ctrl + right click).
- Cases: all those of the extension, plus alternating case and the clean-ups.
- Title / Sentence case: smart, with a word list.
- Remote desktop: TextCase on the local PC only, through the RDP window; nothing on the server.
- Claude's defaults, not asked: the name, the default shortcuts, 400 ms, the clipboard put back, start with Windows,
  tray icon, the popup never takes the focus.

## Architecture

| Folder | Role |
|--------|------|
| `src/TextCase.Core` | Pure, tested logic: `CaseMode` (the cases, their groups, the keys of the popup rows), `CaseConverter` (every conversion), `CaseRules` (the word list, small words, elisions, Roman numerals), `Shortcut` (modifiers + virtual key, "Ctrl+Alt+C"), `DoublePress`, `SemanticVersion`, `UpdateFeed`. |
| `src/TextCase` | WPF .NET 10. `App` (single instance, tray, hook, the window in front, settings), `CasePopup` (+ `PopupColors`; the Smart switch, key 0), `Toast`, `MainWindow` (pages built in code), `PopupMenu` + `Menus.xaml` (tray menu), `UpdateBar`, `Localization.cs`; `Controls/`: `Ui` (cards), `ShortcutBox` (records a shortcut); `Services/`: `KeyboardHook` (its own thread), `ShortcutMatcher` (what each key does), `TextSwap` (copy → popup → paste), `ClipboardAccess`, `Input` (SendInput), `RemoteDesktop`, `SwapLog` (each step of a swap, never the text), `TrayHost`, `AppSettings`, `WindowsSettings`, `SingleInstance`, `MicaBackdrop`, `MemoryTrim`, `Updater`, `NativeMethods`. |
| `tests/TextCase.Tests` | xUnit: conversions, shortcuts, Ctrl+C twice, update feed, localization key parity. |
| `installer/TextCase.iss` | Inno Setup (English/French), .NET 10 Desktop Runtime check, closes the running app. |
| `scripts/` | `build.ps1`, `make-icons.ps1`. |
| `.github/workflows/build.yml` | Build + tests on push; release on a `v*` tag (pre-release when the tag has a `-`). |

### Technical choices (and why)

- **A low-level keyboard hook, not RegisterHotKey**: the remote desktop client (mstsc) installs its own hook when
  its window is active and sends the keys to the remote PC before any shortcut of this PC is seen. The last hook
  installed is called first: TextCase installs its hook again when a remote desktop window comes to the front (and a
  moment later, and on every change of the window in front: Windows removes a hook that once answered too slowly).
  The hook runs on a thread of its own with its own message loop; `ShortcutMatcher` (in Core, tested with a clock of
  the tests) only reads arrays and raises events, the work is done on the UI thread. While a remote desktop window is
  in front, the hook is installed again **every second** (the client may set its own again later, then every key goes
  to the remote PC unseen); the log says once per visit whether keys reach TextCase there.
- **Missed releases**: the keys held are followed from the hook's events (in the remote desktop window Windows' own
  key state never sees them). A key pressed again more than 1.1 s after its last press is a new press (not an
  auto-repeat), and a modifier seen more than 2 s ago that `GetAsyncKeyState` says is up is forgotten: otherwise one
  missed release (Win + L, the remote desktop client taking the keys for a moment) stopped every shortcut.
- **AltGr is not Ctrl+Alt**: AltGr sends left Ctrl + right Alt; with them, no shortcut matches (AZERTY and many
  keyboards type characters with AltGr). A shortcut with Alt or Win is followed by an unassigned key (0xE8), so that
  the release of Alt does not open the menu bar, nor Win the Start menu.
- **The popup never takes the focus** (`WS_EX_NOACTIVATE`, `MA_NOACTIVATE`): the app keeps its selection, the remote
  desktop window its keyboard. Its keys come from the hook while it is open (1–9, A–Z, arrows, Enter, Esc; any other
  key closes it and goes to the app); a click elsewhere closes it (a mouse hook only while it is open).
- **The swap**: wait until the user lets go of the shortcut's keys, keep the clipboard (text, HTML, RTF, files,
  image: not the private formats of an app, rendering all of Excel's takes seconds), send Ctrl+C, wait for the
  clipboard to change (0.8 s, 2.5 s for a remote PC), popup, plain text into the clipboard (kept out of the clipboard
  history), Ctrl+V, the clipboard put back 0.7 s later (2.5 s remote) if nothing else was copied meanwhile. For a
  remote PC the clipboard gets 0.4 s to cross before the paste.
- **Keys sent with their scan code**: the remote desktop client forwards scan codes; a key without one does nothing on
  the remote PC.
- **Smart cases** (the Smart switch, on by default; off, `SimpleTitle` capitalizes every word and `SimpleSentence`
  the start of each sentence): words of the list (case-insensitive), words in mixed case, letters against a digit, Roman numerals
  (II to XX) and, in a line not in capitals, words in capitals (acronyms) are kept. A line counts as "in capitals"
  when fewer than a quarter of its letters are lowercase. French elisions start a new word (l'Appareil), English
  contractions do not (Don't).
- **Same stack as the sister projects**: WPF .NET 10 Fluent theme, Mica, resx EN/FR, settings JSON, Inno Setup,
  updater from GitHub Releases.

### Localization

English (default) and French, following the Windows display language or chosen in General. `Strings.Get("Mode" +
value)` exists for every `CaseMode` (a test checks). Installer messages in `installer/TextCase.iss`.

## Known pitfalls

- **The shell of the Claude desktop app sees a redirected `%LOCALAPPDATA%`** (writes go to a private copy; reads fall
  through to the real file while no copy exists). A file Claude writes there is not the one the author's apps read:
  test runs use `TEXTCASE_SETTINGS` (`.local\dev.ps1` → `.local\test-settings.json`), never his `settings.json`; his
  real files are read or reset by a `.cmd` run through Explorer (`.local\read-real-settings.cmd`,
  `reset-real-settings.cmd`).
- One TextCase per session (mutex): a test run stops his TextCase; `Start-TextCaseForUser` starts it again through
  Explorer, with his settings. Explorer starts an exe without its arguments (`--background` is lost).

- An app run as administrator gets no keys from TextCase (Windows blocks the input of a normal app to an elevated
  one): the copy fails, "No text selected" shows.
- Copy and paste through the remote desktop need the clipboard redirection of the connection (on by default).
- `PrintWindow` renders Mica as the desktop behind it: pictures of the settings window use `TEXTCASE_NO_MICA=1`. A
  second start of TextCase wakes the running one (single instance): stop it fully before a start with other settings.
- `RenderTargetBitmap` of an element with a margin draws it at its margin: the popup's picture adds the shadow room
  on both sides.

## To do

- [ ] Turn the key trace off (`Matcher.Trace`) once the shortcuts are confirmed; the next version number for it.
