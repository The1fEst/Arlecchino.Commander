# Arlecchino.Commander

A Midnight Commander for the terminal, written on [Arlecchino](https://github.com/The1fEst/Arlecchino):
two panels, the function keys where they have always been, and the same panel over a local disk, an
SFTP server or an FTP one.

```
dotnet run --project src/Arlecchino.Commander -- C:\some\folder C:\another
```

## What it does

- **Two panels.** `Tab` switches, `Enter` opens, `Backspace` goes up, `Space` marks, and the columns
  sort by name, size or date. The panel that has the focus is the one the operations work from.
- **The function keys.** `F3` view, `F4` filter, `F5` copy, `F6` move, `Shift+F6` rename, `F7` make
  folder, `F8` delete, `F9` menu, `F10` quit — and `F1` opens the framework's own key screen.
- **Servers.** A panel connects over SFTP or FTP and browses it exactly as it browses a disk; copying
  between the two panels is the same key whichever side is which.
- **Hosts from `~/.ssh/config`.** `Ctrl+K` lists the `Host` entries and opens one, reusing its
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
