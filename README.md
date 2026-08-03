<p align="center">
  <img src="assets/commander-banner.svg" alt="Arlecchino Commander" width="820">
</p>

<p align="center">
  <a href="https://github.com/The1fEst/Arlecchino.Commander/releases/latest"><img src="https://img.shields.io/github/v/release/The1fEst/Arlecchino.Commander?logo=github&color=C9382B&labelColor=141317" alt="Release"></a>
  <a href="https://github.com/The1fEst/Arlecchino.Commander/releases"><img src="https://img.shields.io/github/downloads/The1fEst/Arlecchino.Commander/total?color=C9382B&labelColor=141317" alt="Downloads"></a>
  <a href="https://github.com/The1fEst/Arlecchino.Commander/actions/workflows/build.yml"><img src="https://github.com/The1fEst/Arlecchino.Commander/actions/workflows/build.yml/badge.svg" alt="Build"></a>
  <img src="https://img.shields.io/badge/windows%20%7C%20macos%20%7C%20linux-EDE6D9?labelColor=141317" alt="Platforms">
  <a href="https://github.com/The1fEst/Arlecchino"><img src="https://img.shields.io/badge/built%20on-Arlecchino-C9382B?labelColor=141317" alt="Built on Arlecchino"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-EDE6D9?labelColor=141317" alt="MIT"></a>
</p>

A Midnight Commander for the terminal, written on [Arlecchino](https://github.com/The1fEst/Arlecchino):
two panels, the function keys where they have always been, and the same panel over a local disk, an
SFTP server or an FTP one.

```
dotnet run --project src/Arlecchino.Commander -- C:\some\folder C:\another
```

![Two panels over a local disk](assets/screenshots/panels.png)

<details>
<summary><b>More screens</b> — marks, the menu, operations, servers, SSH, notifications</summary>

![Three files marked, counted at the foot of the panel](assets/screenshots/marks.png)

![The menu, opened by F9](assets/screenshots/menu.png)

![Copying asks where to](assets/screenshots/copy.png)

![A copy running in the background, with a bar and Esc to stop](assets/screenshots/progress.png)

![The same copy opened in full, with Stop offered](assets/screenshots/notification.png)

![Hosts read from ~/.ssh/config](assets/screenshots/hosts.png)

![A panel browsing a server over SFTP](assets/screenshots/server.png)

![A command run on that server](assets/screenshots/ssh.png)

![A file read without leaving the panels](assets/screenshots/viewer.png)

</details>

## Getting it

Every tag builds a native binary for Windows, macOS and Linux on both architectures, compiled ahead
of time — one file, nothing to install, no .NET on the machine required. They are on the
[releases page](https://github.com/The1fEst/Arlecchino.Commander/releases).

## What it does

- **Two panels.** `Tab` switches, `Enter` opens, `Backspace` goes up, `Space` marks, and the columns
  sort by name, size or date. The panel that has the focus is the one the operations work from.
- **The function keys.** `F2` tabs, `F3` view, `F4` filter, `F5` copy, `F6` move, `Shift+F6` rename,
  `F7` make folder, `F8` delete, `F9` menu, `F10` quit — and `F1` opens the framework's own key screen.
- **Tabs.** Each one holds two panels of its own, so a second pair of folders — or a server on one
  side — is a tab away rather than a place you have to navigate back to. `Alt+T` opens one, `Alt+W`
  closes it, `Alt+PgDn` and `Alt+PgUp` step between them, and the band along the top shows what each
  one is connected to and takes a click on it, on its `×`, or on the `+` at the end. `F2` lists them
  all, and the palette finds one by name — typing the name of a server goes to the tab that is on it.
- **Getting around the way Midnight Commander does.** `Ctrl+S` searches as you type, `+` and `-` mark
  and unmark by shell pattern, `*` inverts the marks, `Alt+G` `Alt+R` `Alt+J` jump to the top, the
  middle and the bottom, `Ctrl+PgUp` and `Ctrl+PgDn` leave and enter a folder, `Alt+H` lists the
  folders the panel has been in with `Alt+Y` and `Alt+U` stepping through them, `Ctrl+B` keeps a
  hotlist, `Alt+I` sends the other panel here and `Alt+O` sends it into the folder under the cursor.
- **Find file.** `Alt+F7` walks down from the panel — over SFTP as readily as over a disk — matching
  names against a shell pattern and, when asked for one, the text inside the files. Results fill in
  while the walk runs, `F3` stops it, and `Enter` sends the panel to the file it found.
- **Everything in one list.** `Ctrl+K` opens a palette holding every menu entry, every tab and every
  key the screen answers to, narrowing as you type — so nothing has to be found by remembering which
  menu it was filed under.
- **The operations behind `Ctrl+X`.** `Ctrl+X C` sets permissions — through SFTP's own request, FTP's
  `SITE CHMOD`, or the file mode on a Unix disk — `Ctrl+X O` hands a `chown` to the shell where the
  panel is looking, `Ctrl+X S` and `Ctrl+X L` make a symbolic or a hard link into the other panel,
  and `Ctrl+X D` marks in both panels every file the other one does not have the same of.
- **A command line under the panels.** Typing goes to it while the panel keeps the cursor, `Enter`
  runs it where the panel is looking — on the server itself when that panel is connected — `cd` moves
  the panel instead of a shell that would forget it, `Alt+P` and `Alt+N` walk the history, `Alt+Enter`
  puts the name under the cursor on the line, `Ctrl+X P` the path, `Ctrl+X T` the marked names, and
  `Ctrl+O` reads back everything the commands printed.
- **Servers.** A panel connects over SFTP or FTP and browses it exactly as it browses a disk; copying
  between the two panels is the same key whichever side is which.
- **Hosts from `~/.ssh/config`.** `Alt+K` lists the `Host` entries and opens one, reusing its
  `HostName`, `User`, `Port` and `IdentityFile`, or the default keys in `~/.ssh` when it names none.
- **Commands over SSH.** The `Command` menu runs one on the connected host and shows what it said.
- **Work that does not freeze the screen.** Copy, move and delete run in the background with a bar,
  the file being worked on, and `Esc` to stop; each reports itself as a notification that turns into
  what came of it, with the errors readable in full behind `Enter`.

## Reading a frame without a terminal

The application renders one frame and exits, which is what the screenshots and the CI check are made
of:

```
dotnet run --project src/Arlecchino.Commander -- --frame 120x30 --left . --right src
```

`--keys F9,Enter` plays keys before the frame is drawn, `--connect sftp://user@host/path?key=…` (or
`--connect myhost` for a `~/.ssh/config` entry) opens a panel on a server first, and `--wait 2000`
gives the network time to answer.

## Building

```
dotnet build Arlecchino.Commander.slnx --configuration Release
```

The screenshots in the framework's README are rendered by `tools/shots.cs`:

```
dotnet run tools/shots.cs
```

## License

MIT.
