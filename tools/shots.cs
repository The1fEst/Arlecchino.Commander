#:package SkiaSharp@3.119.0
#:property GenerateDocumentationFile=true

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using SkiaSharp;

const string Size = "200x34";
var repository = Directory.GetCurrentDirectory();
var output = Path.Combine(repository, "assets", "screenshots");

var framework = Path.Combine(Directory.GetParent(repository)!.FullName, "Arlecchino");

var fixture = Path.Combine(Path.GetTempPath(), "arlecchino-shots");
var scratchLeft = Path.Combine(fixture, "project");
var scratchRight = Path.Combine(fixture, "backup");

const string host = "ubuntu";

if (args is ["tape", ..])
{
    Tape();
    return;
}

if (args is ["show", ..])
{
    Show(false);
    return;
}

if (args is ["shoot", ..])
{
    Show(true);
    return;
}

Shots();

/// <summary>
/// Every picture that gets taken, in order. The panels show the two repositories, apart from the scenes
/// that copy, which work on a scratch fixture, so a picture never writes into a repository.
/// </summary>
/// <param name="size">Columns by rows, as the application is told it.</param>
/// <returns>What to shoot, what keys to play first, and what to caption it with.</returns>
(string Name, string Size, string Keys, string Wait, bool Scratch, string Connect, string Caption)[] Scenes(
    string size) =>
    [
        ("panels", size, "", "", false, "", "two panels over a local disk"),
        ("marks", size, "End,Up,Up,Space,Space,Space", "", false, "", "three files marked, counted at the foot of the panel"),
        ("sorted", size, "Tab,s,j", "", false, "", "the right panel sorted by size"),
        ("menu", size, "F9", "", false, "", "the menu, opened by F9"),
        ("file-menu", size, "F9,Down,Enter", "", false, "", "what can be done to what is marked"),
        ("copy", size, "End,Up,Up,Space,Space,F5", "", false, "", "copying asks where to"),
        ("delete", size, "End,Up,Space,F8", "", false, "", "deleting asks first, with no selected"),
        ("viewer", size, "End,Up,Up,Up,F3", "", false, "", "a file read without leaving the panels"),
        ("filter", size, "F4,s,r", "", false, "", "the panel filtered by name"),
        ("palette", size, "Ctrl+K", "", false, "", "everything the application can do, by name"),
        ("hosts", size, "g,n", "", false, "", "hosts read from ~/.ssh/config"),
        ("find", size, "Ctrl+F7,Enter,Enter", "600", false, "", "a walk of the folder, filling in as it goes"),
        ("output", size, ":,l,s,Enter,Ctrl+O", "900", false, "", "everything the commands printed"),
        ("connect", size, "Ctrl+K,c,o,n,n,e,c,t,Enter", "", false, "", "a connection asked for in full"),
        ("help", size, "F1", "", false, "", "the keys screen, which comes with the framework"),
        ("server", size, "", "", false, host, "a panel browsing a server over SFTP"),
        ("ssh", size, "Ctrl+K,s,s,h,Enter,l,s,Enter", "3000", false, host, "a command run on that server"),
        ("progress", size, "Down,Down,F5,Enter", "150", true, "", "a copy running in the background, with a bar and a key to stop"),
        ("notification", size, "Down,Down,F5,Enter,Ctrl+N,Enter", "150", true, "", "the same copy opened in full, with Stop offered"),
        ("done", size, "Down,Down,F5,Enter,Ctrl+N,wait4000", "600", true, "", "the same entry once the copy is over"),
    ];

void Shots()
{
    Directory.CreateDirectory(output);

    Fixture.Lay(fixture, scratchLeft, scratchRight);

    using var paper = new Paper(32f);

    foreach (var scene in Scenes(Size))
    {
        if (scene.Scratch)
        {
            Fixture.Reset(scratchRight);
        }

        var ansi = Capture(
            "--frame",
            scene.Size,
            scene.Keys,
            scene.Wait,
            scene.Scratch ? scratchLeft : framework,
            scene.Scratch ? scratchRight : repository,
            scene.Connect);
        
        var grid = Terminal.Parse(ansi);
        if (grid.Count == 0)
        {
            Console.WriteLine($"{scene.Name}: nothing came back");
            continue;
        }

        var (columns, rows) = Measure(scene.Size);
        var path = Path.Combine(output, $"{scene.Name}.png");

        using var picture = paper.Draw(Fit(grid, columns, rows), scene.Caption);
        using var image = SKImage.FromBitmap(picture);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);

        data.SaveTo(file);

        Console.WriteLine($"{scene.Name}: {grid[0].Count}x{grid.Count} → {path}");
    }
}

/// <summary>
/// Walks the same scenes with nothing between them and the terminal, so each frame is drawn by the
/// terminal itself. Enter goes to the next one and <c>q</c> stops, at whatever size this terminal is.
/// </summary>
/// <param name="shoot">Whether to photograph the window as well as draw in it.</param>
void Show(bool shoot)
{
    if (Console.IsInputRedirected || Console.IsOutputRedirected)
    {
        Console.Error.WriteLine("show needs a terminal of its own: run it without a pipe.");

        return;
    }

    var window = shoot ? Shot.Window() : 0;

    if (shoot && window == 0)
    {
        Console.Error.WriteLine("shoot takes a picture of this window, and only kitty on macOS will say which window that is.");

        return;
    }

    var columns = Math.Max(40, Console.WindowWidth);
    var rows = Math.Max(10, Console.WindowHeight);
    var size = $"{columns}x{rows}";
    var scenes = Scenes(size);
    var taken = new List<string>();

    Fixture.Lay(fixture, scratchLeft, scratchRight);

    if (shoot)
    {
        Directory.CreateDirectory(output);
        Console.Write("\e]2;arlecchino.commander\a");
    }

    Console.Write("\e[?1049h\e[?25l\e[?7l");

    try
    {
        foreach (var scene in scenes)
        {
            if (scene.Scratch)
            {
                Fixture.Reset(scratchRight);
            }

            Console.Write("\e[2J\e[H");
            Console.Write($"{scene.Name}: drawing…");

            var ansi = Capture(
                "--frame",
                size,
                scene.Keys,
                scene.Wait,
                scene.Scratch ? scratchLeft : framework,
                scene.Scratch ? scratchRight : repository,
                scene.Connect);

            Console.Write("\e[2J\e[H");
            Console.Write(ansi.Length == 0 ? $"{scene.Name}: nothing came back" : ansi.TrimEnd('\r', '\n'));
            Console.Out.Flush();

            if (shoot && ansi.Length > 0)
            {
                var picture = Path.Combine(output, $"{scene.Name}.png");

                taken.Add(Shot.Take(window, picture, shadow: true)
                    ? $"{scene.Name}: {size} → {picture}"
                    : $"{scene.Name}: screencapture would not take it");
            }

            if (shoot ? Stopped() : Stop())
            {
                return;
            }
        }
    }
    finally
    {
        Console.Write("\e[?7h\e[?25h\e[?1049l");

        foreach (var line in taken)
        {
            Console.WriteLine(line);
        }
    }
}

/// <summary>Waits for a key, where <c>q</c> and Escape stop and anything else moves on.</summary>
/// <returns><c>true</c> when the walk should end.</returns>
static bool Stop()
{
    var key = Console.ReadKey(intercept: true);

    return key.Key is ConsoleKey.Q or ConsoleKey.Escape;
}

static bool Stopped() => Console.KeyAvailable && Stop();

void Tape()
{
    (string Key, int Hold)[] beats =
    [
        ("", 1500),
        ("Down", 340), ("Space", 280), ("Space", 240), ("Space", 700),
        ("F5", 1600),
        ("Enter", 260), ("", 220), ("", 260),
        ("Ctrl+N", 600),
        ("Enter", 420), ("", 380), ("", 900),
        ("Esc", 700), ("wait2500", 1600),
        ("Esc", 900),
        ("F1", 2400),
    ];

    var steps = new List<string>();

    foreach (var (key, hold) in beats)
    {
        if (key.Length > 0)
        {
            steps.Add(key);
        }

        steps.Add($"shot{hold}");
    }

    steps.Add("Esc");

    var script = string.Join(',', steps);

    if (Console.IsInputRedirected || Console.IsOutputRedirected)
    {
        Console.Error.WriteLine("tape needs a terminal of its own: run it without a pipe.");

        return;
    }

    var window = Shot.Window();

    if (window == 0)
    {
        Console.Error.WriteLine("tape is a recording of this window, and only kitty on macOS will say which window that is.");

        return;
    }

    var size = $"{Math.Max(40, Console.WindowWidth)}x{Math.Max(10, Console.WindowHeight)}";

    Console.WriteLine($"tape: laying the tree and playing the script at {size}…");

    Playground.Lay();

    var ansi = Capture("--tape", size, script, "", Playground.Left, Playground.Right, "");
    var reel = Path.Combine(Path.GetTempPath(), "arlecchino-tape");
    var frames = new List<(string Path, int Hold)>();

    if (Directory.Exists(reel))
    {
        Directory.Delete(reel, true);
    }

    Directory.CreateDirectory(reel);
    Console.Write("\e]2;arlecchino.commander\a");
    Console.Write("\e[?1049h\e[?25l\e[?7l");

    try
    {
        foreach (var (hold, text) in Reel(ansi))
        {
            if (text.Trim().Length == 0)
            {
                continue;
            }

            Console.Write("\e[2J\e[H");
            Console.Write(text.TrimEnd('\r', '\n'));
            Console.Out.Flush();

            var frame = Path.Combine(reel, $"frame{frames.Count:000}.png");

            if (Shot.Take(window, frame, shadow: true))
            {
                frames.Add((frame, hold));
            }
        }
    }
    finally
    {
        Console.Write("\e[?7h\e[?25h\e[?1049l");
    }

    try
    {
        if (frames.Count == 0)
        {
            Console.WriteLine("tape: nothing came back");

            return;
        }

        var target = Path.Combine(repository, "assets", "demo.png");

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        Console.WriteLine(Film.Weave(frames, target)
            ? $"tape: {frames.Count} frames, {new FileInfo(target).Length / 1024d / 1024d:0.0} MB → {target}"
            : "tape: ffmpeg would not weave the frames");
    }
    finally
    {
        Directory.Delete(reel, true);
    }
}

/// <summary>
/// Splits a recording into its frames. Each is announced by a record separator and the milliseconds it is
/// to be held for, so the reel carries the timing the application ran at.
/// </summary>
/// <param name="text">What the headless run wrote.</param>
/// <returns>Each frame and how long to hold it.</returns>
static List<(int Hold, string Ansi)> Reel(string text)
{
    var frames = new List<(int Hold, string Ansi)>();

    foreach (var piece in text.Split('', StringSplitOptions.RemoveEmptyEntries))
    {
        var line = piece.IndexOf('\n');

        if (line < 0 || !int.TryParse(piece[..line].Trim(), out var hold))
        {
            continue;
        }

        frames.Add((hold, piece[(line + 1)..]));
    }

    return frames;
}

/// <summary>Reads a <c>columns x rows</c> size, which every picture is cut or padded to.</summary>
/// <param name="size">The size as it was written.</param>
/// <returns>The two numbers.</returns>
static (int Columns, int Rows) Measure(string size)
{
    var parts = size.Split('x');

    return (int.Parse(parts[0]), int.Parse(parts[1]));
}

static List<List<Cell>> Fit(List<List<Cell>> grid, int columns, int rows)
{
    var blank = new Cell(" ", Terminal.Foreground, Terminal.Background, false);

    while (grid.Count < rows)
    {
        grid.Add([]);
    }

    foreach (var line in grid)
    {
        while (line.Count < columns)
        {
            line.Add(blank);
        }
    }

    return grid;
}

string Capture(string mode, string size, string keys, string wait, string leftPanel, string rightPanel, string connect)
{
    var arguments = $"run --project src/Arlecchino.Commander -c Release --no-build -- " +
                    $"{mode} {size} --left \"{leftPanel}\" --right \"{rightPanel}\"";

    if (connect.Length > 0)
    {
        arguments += $" --connect {connect}";
    }

    if (keys.Length > 0)
    {
        arguments += $" --keys {keys}";
    }

    if (wait.Length > 0)
    {
        arguments += $" --wait {wait}";
    }

    var start = new ProcessStartInfo("dotnet", arguments)
    {
        RedirectStandardOutput = true,
        WorkingDirectory = repository,
        UseShellExecute = false,
        Environment =
        {
            ["COLORTERM"] = "truecolor",
            ["TERM"] = "xterm-256color",
            ["ARLECCHINO_COLOR"] = "truecolor"
        }
    };

    using var process = Process.Start(start)!;
    var text = process.StandardOutput.ReadToEnd();
    process.WaitForExit();

    return Masked(text);
}

/// <summary>
/// Replaces every address in a captured frame, so no picture carries a real one. What goes in its place
/// is padded to the width it stood in, so no row shifts under it.
/// </summary>
/// <param name="text">The frame as it was captured.</param>
/// <returns>The frame with its addresses hidden.</returns>
static string Masked(string text)
{
    const string replace = "***.***.***.***";

    return Regex.Replace(
        text,
        @"\b\d{1,3}(?:\.\d{1,3}){3}\b",
        found => replace.PadRight(found.Length)[..Math.Max(found.Length, replace.Length)]);
}

readonly record struct Cell(string Symbol, SKColor Foreground, SKColor Background, bool Bold);

/// <summary>
/// Draws a grid of cells as a window: the terminal on a rounded plate, three lamps and a caption above it.
/// The size of the type sets everything else, so one drawing serves both scales.
/// </summary>
sealed class Paper : IDisposable
{
    private readonly SKTypeface _typeface;
    private readonly SKTypeface _boldface;
    private readonly SKFont _font;
    private readonly SKFont _bold;
    private readonly Dictionary<int, SKFont> _fallbacks = [];
    private readonly SKFontMetrics _metrics;
    private readonly float _size;
    private readonly float _padding;
    private readonly float _titleBar;
    private readonly float _radius;
    private readonly float _cellWidth;
    private readonly float _cellHeight;

    /// <summary>Sets the type up and measures the cell everything is laid out on.</summary>
    /// <param name="size">Type size in pixels.</param>
    public Paper(float size)
    {
        _typeface = Face("JetBrainsMonoNLNerdFontMono-Regular.ttf");
        _boldface = Face("JetBrainsMonoNLNerdFontMono-Bold.ttf");
        _font = new(_typeface, size);
        _bold = new(_boldface, size);

        _size = size;
        _padding = MathF.Round(size * 1.25f);
        _titleBar = MathF.Round(size * 3.25f);
        _radius = MathF.Round(size * 0.75f);

        _metrics = _font.Metrics;
        _cellWidth = _font.MeasureText("MMMMMMMMMM") / 10f;
        _cellHeight = MathF.Round(-_metrics.Ascent + _metrics.Descent + (size / 8f));
    }

    /// <summary>Draws one frame.</summary>
    /// <param name="grid">The cells, already padded to the size the scene asked for.</param>
    /// <param name="caption">What the title bar says.</param>
    /// <returns>The picture, for the caller to encode or dispose.</returns>
    public SKBitmap Draw(IReadOnlyList<List<Cell>> grid, string caption)
    {
        var columns = grid[0].Count;
        var width = (int)MathF.Ceiling(columns * _cellWidth) + (int)(_padding * 2);
        var height = (int)(grid.Count * _cellHeight) + (int)(_padding * 2) + (int)_titleBar;

        var bitmap = new SKBitmap(width, height);

        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.Transparent);

        using var window = new SKPaint();
        window.Color = Terminal.Background;
        window.IsAntialias = true;
        canvas.DrawRoundRect(new(0, 0, width, height), _radius, _radius, window);

        DrawTitleBar(canvas, width, caption);

        using var ink = new SKPaint();
        ink.IsAntialias = true;

        for (var row = 0; row < grid.Count; row++)
        {
            var line = grid[row];
            var top = _padding + _titleBar + (row * _cellHeight);

            for (var column = 0; column < line.Count; column++)
            {
                var cell = line[column];
                var left = _padding + (column * _cellWidth);

                if (cell.Background != Terminal.Background)
                {
                    ink.Color = cell.Background;
                    canvas.DrawRect(new(left, top, left + _cellWidth + 0.6f, top + _cellHeight), ink);
                }

                if (cell.Symbol is " " or "")
                {
                    continue;
                }

                ink.Color = cell.Foreground;

                if (Lines.Draw(canvas, ink, cell.Symbol, left, top, _cellWidth, _cellHeight))
                {
                    continue;
                }

                canvas.DrawText(cell.Symbol,
                    left,
                    top - _metrics.Ascent + (_size / 16f),
                    SKTextAlign.Left,
                    Pick(cell.Symbol, cell.Bold),
                    ink);
            }
        }

        return bitmap;
    }

    /// <summary>Releases the type, fallbacks included.</summary>
    public void Dispose()
    {
        _font.Dispose();
        _bold.Dispose();
        _typeface.Dispose();
        _boldface.Dispose();

        foreach (var fallback in _fallbacks.Values)
        {
            fallback.Dispose();
        }
    }

    private SKFont Pick(string symbol, bool heavy)
    {
        var chosen = heavy ? _bold : _font;
        var point = char.ConvertToUtf32(symbol, 0);

        if ((heavy ? _boldface : _typeface).GetGlyph(point) != 0)
        {
            return chosen;
        }

        if (_fallbacks.TryGetValue(point, out var known))
        {
            return known;
        }

        var face = SKFontManager.Default.MatchCharacter(point);
        var fallback = face is null ? chosen : new(face, _size);

        _fallbacks[point] = fallback;
        return fallback;
    }

    private void DrawTitleBar(SKCanvas canvas, int width, string caption)
    {
        SKColor[] lamps = [new(0xC9, 0x38, 0x2B), new(0xD0, 0x8A, 0x2C), new(0x8A, 0x81, 0x89)];

        using var lamp = new SKPaint();
        lamp.IsAntialias = true;

        for (var i = 0; i < lamps.Length; i++)
        {
            lamp.Color = lamps[i];
            canvas.DrawCircle(_padding + (_size / 2f) + (i * _size * 1.5f), _titleBar / 2f, _size * 0.44f, lamp);
        }

        using var text = new SKPaint();
        text.Color = new(0xC5, 0xBC, 0xB0);
        text.IsAntialias = true;
        using var small = new SKFont(_boldface, _size * 1.05f);

        canvas.DrawText(caption.ToUpperInvariant(),
            width / 2f,
            (_titleBar / 2f) + (small.Size / 3f),
            SKTextAlign.Center,
            small,
            text);
    }

    /// <summary>
    /// The monospaced face to draw with. Every folder the three platforms keep fonts in is searched by
    /// file name first, and only then a family, since a proportional face draws the columns adrift.
    /// </summary>
    /// <param name="file">The font file to look for.</param>
    /// <returns>The face.</returns>
    private static SKTypeface Face(string file) =>
        Found(file) ??
        Found("CascadiaMono.ttf") ??
        SKTypeface.FromFamilyName("JetBrainsMono Nerd Font Mono") ??
        SKTypeface.FromFamilyName("Menlo") ??
        SKTypeface.FromFamilyName("Consolas") ??
        throw new InvalidOperationException($"no monospaced font found for {file}");

    private static SKTypeface? Found(string file)
    {
        foreach (var folder in Fonts())
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(folder, file, SearchOption.AllDirectories))
            {
                if (SKTypeface.FromFile(path) is { } face)
                {
                    return face;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> Fonts()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        if (windows.Length > 0)
        {
            yield return Path.Combine(windows, "Fonts");
        }

        yield return Path.Combine(home, "Library", "Fonts");
        yield return "/Library/Fonts";
        yield return "/System/Library/Fonts";
        yield return Path.Combine(home, ".local", "share", "fonts");
        yield return Path.Combine(home, ".fonts");
        yield return "/usr/share/fonts";
    }
}

/// <summary>
/// The tree the animation runs over. It sits in a folder of its own, well away from any repository,
/// because the recording copies it: a demo that writes has to write somewhere it is welcome to.
/// </summary>
static class Playground
{
    private const int Pages = 2400;
    private const int PageSize = 30_000;

    public static string Root { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Playground");

    public static string Left { get; } = Path.Combine(Root, "project");

    public static string Right { get; } = Path.Combine(Root, "backup");

    /// <summary>Lays the tree out from scratch, so a second recording starts where the first did.</summary>
    public static void Lay()
    {
        if (Directory.Exists(Left))
        {
            Directory.Delete(Left, true);
        }

        if (Directory.Exists(Right))
        {
            Directory.Delete(Right, true);
        }

        Directory.CreateDirectory(Left);
        Directory.CreateDirectory(Right);

        Fill(Path.Combine(Left, "assets"), Pages);
        Fill(Path.Combine(Left, "docs"), Pages);
        Fill(Path.Combine(Left, "src"), Pages);

        (string Name, int Size)[] files =
        [
            ("Arlecchino.slnx", 640), ("Directory.Build.props", 1_180), ("README.md", 7_400),
            ("CHANGELOG.md", 12_600), ("LICENSE", 1_100), ("banner.svg", 14_200), ("notes.txt", 210),
        ];

        foreach (var (name, size) in files)
        {
            File.WriteAllText(Path.Combine(Left, name), new('x', size));
        }

        File.WriteAllText(Path.Combine(Right, "one.txt"), "kept");
    }

    private static void Fill(string folder, int count)
    {
        var made = Directory.CreateDirectory(folder);

        for (var index = 0; index < count; index++)
        {
            File.WriteAllText(Path.Combine(made.FullName, $"part{index:000}.cs"), new('x', PageSize));
        }
    }
}

/// <summary>
/// The folders the screenshots are taken of, which is a small tree rather than the repository itself. It
/// is laid out the same way every time, and rebuilt between the scenes whose copies would change it.
/// </summary>
static class Fixture
{
    private static readonly (string Name, int Size)[] Files =
    [
        ("Program.cs", 2_400), ("Commander.slnx", 320), ("README.md", 5_900), ("LICENSE", 1_100),
        ("appsettings.json", 780), ("banner.svg", 14_200), ("notes.txt", 210), ("changelog.md", 9_400),
        ("build.yml", 1_020), ("icon.png", 62_000),
    ];

    private static readonly string[] Folders = [".github", "assets", "docs", "src", "tests"];

    private const int Pages = 3_000;

    private const int PageSize = 24_000;

    public static void Lay(string root, string left, string right)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }

        Directory.CreateDirectory(left);
        Directory.CreateDirectory(right);

        foreach (var folder in Folders)
        {
            var made = Directory.CreateDirectory(Path.Combine(left, folder));

            for (var index = 0; index < 4; index++)
            {
                File.WriteAllText(Path.Combine(made.FullName, $"part{index}.cs"), new string('x', 900 + (index * 40)));
            }
        }

        var heavy = Directory.CreateDirectory(Path.Combine(left, "docs", "manual"));
        var page = new string('x', PageSize);

        for (var index = 0; index < Pages; index++)
        {
            File.WriteAllText(Path.Combine(heavy.FullName, $"page{index:0000}.md"), page);
        }

        foreach (var (name, size) in Files)
        {
            File.WriteAllText(Path.Combine(left, name), new string('x', size));
        }

        File.WriteAllText(Path.Combine(right, "one.txt"), "kept");
    }

    public static void Reset(string right)
    {
        foreach (var found in new DirectoryInfo(right).EnumerateFileSystemInfos())
        {
            if (found.Name == "one.txt")
            {
                continue;
            }

            if (found is DirectoryInfo folder)
            {
                folder.Delete(true);
                continue;
            }

            found.Delete();
        }
    }
}

/// <summary>
/// Binds the captured frames into one animation, which FFmpeg does as an APNG rather than as a GIF, whose
/// 256 colors carry neither the shades nor the shadow. Every frame is held for as long as it was recorded.
/// </summary>
static class Film
{
    private const int Widest = 1400;

    /// <summary>Weaves the frames, in order, into one file.</summary>
    /// <param name="frames">Each picture and how long it is to be held, in milliseconds.</param>
    /// <param name="target">Where the animation goes.</param>
    /// <returns>Whether FFmpeg wrote it.</returns>
    public static bool Weave(IReadOnlyList<(string Path, int Hold)> frames, string target)
    {
        var folder = Path.GetDirectoryName(frames[0].Path)!;
        var reel = Path.Combine(folder, "reel.txt");
        var lines = new List<string> { "ffconcat version 1.0" };

        foreach (var (path, hold) in frames)
        {
            lines.Add($"file '{Path.GetFileName(path)}'");
            lines.Add($"duration {hold / 1000d:0.000}");
        }

        File.WriteAllLines(reel, lines);

        var start = new ProcessStartInfo("ffmpeg")
        {
            ArgumentList =
            {
                "-y", "-hide_banner", "-loglevel", "error",
                "-f", "concat", "-safe", "0", "-i", reel,
                "-vf", $"scale='min({Widest},iw)':-1:flags=lanczos,format=rgba",
                "-plays", "0",
                "-final_delay", $"{frames[^1].Hold / 1000d:0.000}",
                "-f", "apng", target
            },
            RedirectStandardError = true,
            UseShellExecute = false
        };

        try
        {
            using var process = Process.Start(start)!;
            var complaint = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                return true;
            }

            Console.Error.WriteLine(complaint.Trim());

            return false;
        }
        catch (Win32Exception)
        {
            Console.Error.WriteLine("tape needs ffmpeg on the path to weave the frames.");

            return false;
        }
    }
}

/// <summary>
/// The terminal window as macOS sees it. A frame drawn by kitty is the real font, the real glyph widths
/// and the real colors, and the system hands the window back with its shadow.
/// </summary>
static class Shot
{
    private static readonly TimeSpan Drawn = TimeSpan.FromMilliseconds(250);

    /// <summary>Asks kitty which window this program is running in.</summary>
    /// <returns>The number macOS knows that window by, or zero when nothing can be asked.</returns>
    public static int Window()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return 0;
        }

        var listing = Ask("kitten", "@ ls");

        if (listing.Length == 0)
        {
            return 0;
        }

        try
        {
            using var reply = JsonDocument.Parse(listing);

            foreach (var window in reply.RootElement.EnumerateArray())
            {
                if (Ours(window) && window.TryGetProperty("platform_window_id", out var id))
                {
                    return id.GetInt32();
                }
            }
        }
        catch (JsonException)
        {
            return 0;
        }

        return 0;
    }

    /// <summary>Photographs one window.</summary>
    /// <param name="window">The number the system knows the window by.</param>
    /// <param name="path">Where the picture goes.</param>
    /// <param name="shadow">
    /// Whether to keep the shadow the system draws under a window. A still picture wants it; a frame
    /// bound for an animation does not, because what the shadow is drawn on is transparency and a GIF
    /// has no such color to give it.
    /// </param>
    /// <returns>Whether a file was written.</returns>
    public static bool Take(int window, string path, bool shadow)
    {
        Thread.Sleep(Drawn);

        var start = new ProcessStartInfo("screencapture")
        {
            ArgumentList = { "-x", "-t", "png", "-l", window.ToString() },
            RedirectStandardError = true,
            UseShellExecute = false
        };

        if (!shadow)
        {
            start.ArgumentList.Add("-o");
        }

        start.ArgumentList.Add(path);

        try
        {
            using var process = Process.Start(start)!;

            process.StandardError.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0 && File.Exists(path);
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static bool Ours(JsonElement window)
    {
        if (!window.TryGetProperty("tabs", out var tabs))
        {
            return false;
        }

        foreach (var tab in tabs.EnumerateArray())
        {
            foreach (var pane in tab.GetProperty("windows").EnumerateArray())
            {
                if (pane.TryGetProperty("is_self", out var self) && self.GetBoolean())
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string Ask(string program, string arguments)
    {
        var start = new ProcessStartInfo(program, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        try
        {
            using var process = Process.Start(start)!;
            var text = process.StandardOutput.ReadToEnd();

            process.StandardError.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0 ? text : string.Empty;
        }
        catch (Win32Exception)
        {
            return string.Empty;
        }
    }
}

static class Lines
{
    private const float Thickness = 0.09f;

    public static bool Draw(SKCanvas canvas, SKPaint ink, string symbol, float left, float top, float width, float height)
    {
        if (symbol.Length != 1)
        {
            return false;
        }

        var stroke = MathF.Max(2f, height * Thickness);
        var middleX = left + (width / 2f);
        var middleY = top + (height / 2f);
        var right = left + width + 0.6f;
        var bottom = top + height;

        switch (symbol[0])
        {
            case '█':
                canvas.DrawRect(new(left, top, right, bottom), ink);
                return true;
            case '▓':
                return Shade(canvas, ink, left, top, right, bottom, 0.75f);
            case '▒':
                return Shade(canvas, ink, left, top, right, bottom, 0.5f);
            case '░':
                return Shade(canvas, ink, left, top, right, bottom, 0.28f);
            case '─':
                canvas.DrawRect(new(left, middleY - (stroke / 2f), right, middleY + (stroke / 2f)), ink);
                return true;
            case '│':
                canvas.DrawRect(new(middleX - (stroke / 2f), top, middleX + (stroke / 2f), bottom), ink);
                return true;
            case '╭':
                Corner(canvas, ink, middleX, middleY, right, bottom, stroke, true, true);
                return true;
            case '╮':
                Corner(canvas, ink, middleX, middleY, left, bottom, stroke, false, true);
                return true;
            case '╰':
                Corner(canvas, ink, middleX, middleY, right, top, stroke, true, false);
                return true;
            case '╯':
                Corner(canvas, ink, middleX, middleY, left, top, stroke, false, false);
                return true;
            case '├':
                canvas.DrawRect(new(middleX - (stroke / 2f), top, middleX + (stroke / 2f), bottom), ink);
                canvas.DrawRect(new(middleX, middleY - (stroke / 2f), right, middleY + (stroke / 2f)), ink);
                return true;
            case '┤':
                canvas.DrawRect(new(middleX - (stroke / 2f), top, middleX + (stroke / 2f), bottom), ink);
                canvas.DrawRect(new(left, middleY - (stroke / 2f), middleX, middleY + (stroke / 2f)), ink);
                return true;
            default:
                return false;
        }
    }

    private static bool Shade(SKCanvas canvas, SKPaint ink, float left, float top, float right, float bottom, float alpha)
    {
        var solid = ink.Color;

        ink.Color = solid.WithAlpha((byte)(solid.Alpha * alpha));
        canvas.DrawRect(new(left, top, right, bottom), ink);
        ink.Color = solid;

        return true;
    }

    private static void Corner(
        SKCanvas canvas,
        SKPaint ink,
        float middleX,
        float middleY,
        float toX,
        float toY,
        float stroke,
        bool rightwards,
        bool downwards)
    {
        var horizontal = rightwards
            ? new SKRect(middleX, middleY - (stroke / 2f), toX, middleY + (stroke / 2f))
            : new SKRect(toX, middleY - (stroke / 2f), middleX, middleY + (stroke / 2f));

        var vertical = downwards
            ? new SKRect(middleX - (stroke / 2f), middleY, middleX + (stroke / 2f), toY)
            : new SKRect(middleX - (stroke / 2f), toY, middleX + (stroke / 2f), middleY);

        canvas.DrawRect(horizontal, ink);
        canvas.DrawRect(vertical, ink);
    }
}

static class Terminal
{
    private const char Escape = '';

    public static readonly SKColor Background = new(0x14, 0x13, 0x17);
    public static readonly SKColor Foreground = new(0xED, 0xE6, 0xD9);

    private static readonly SKColor[] Ansi =
    [
        new(0x14, 0x13, 0x17), new(0xC9, 0x38, 0x2B), new(0x7A, 0x9E, 0x5E), new(0xD0, 0x8A, 0x2C),
        new(0x4E, 0x7C, 0xA8), new(0x9B, 0x5D, 0x8E), new(0x4E, 0x9E, 0x9E), new(0xED, 0xE6, 0xD9),
        new(0x2E, 0x2B, 0x33), new(0xE0, 0x53, 0x45), new(0x8F, 0xB5, 0x70), new(0xE6, 0xA1, 0x45),
        new(0x63, 0x92, 0xBE), new(0xB3, 0x72, 0xA6), new(0x63, 0xB5, 0xB5), new(0xFF, 0xFF, 0xFF),
    ];

    public static List<List<Cell>> Parse(string text)
    {
        var rows = new List<List<Cell>>();
        var line = new List<Cell>();

        var foreground = Foreground;
        var background = Background;
        var bold = false;
        var index = 0;

        while (index < text.Length)
        {
            var symbol = text[index];

            if (symbol == Escape && index + 1 < text.Length && text[index + 1] == '[')
            {
                var end = index + 2;
                while (end < text.Length && !char.IsLetter(text[end]))
                {
                    end++;
                }

                if (end < text.Length && text[end] == 'm')
                {
                    Apply(text[(index + 2)..end], ref foreground, ref background, ref bold);
                }

                index = end + 1;
                continue;
            }

            if (symbol == '\n')
            {
                rows.Add(line);
                line = [];
                index++;
                continue;
            }

            if (symbol == '\r')
            {
                index++;
                continue;
            }

            var length = char.IsHighSurrogate(symbol) && index + 1 < text.Length ? 2 : 1;

            line.Add(new(text.Substring(index, length), foreground, background, bold));
            index += length;
        }

        if (line.Count > 0)
        {
            rows.Add(line);
        }

        return Trimmed(rows);
    }

    private static List<List<Cell>> Trimmed(List<List<Cell>> rows)
    {
        while (rows.Count > 0 && Blank(rows[^1]))
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return rows;
    }

    private static bool Blank(List<Cell> row)
    {
        foreach (var cell in row)
        {
            if (cell.Symbol is not " " || cell.Background != Background)
            {
                return false;
            }
        }

        return true;
    }

    private static void Apply(string parameters, ref SKColor foreground, ref SKColor background, ref bool bold)
    {
        var codes = parameters.Split(';');

        for (var i = 0; i < codes.Length; i++)
        {
            if (!int.TryParse(codes[i], out var code))
            {
                continue;
            }

            switch (code)
            {
                case 0:
                    foreground = Foreground;
                    background = Background;
                    bold = false;
                    break;
                case 1:
                    bold = true;
                    break;
                case 22:
                    bold = false;
                    break;
                case 39:
                    foreground = Foreground;
                    break;
                case 49:
                    background = Background;
                    break;
                case >= 30 and <= 37:
                    foreground = Ansi[code - 30];
                    break;
                case >= 90 and <= 97:
                    foreground = Ansi[code - 90 + 8];
                    break;
                case >= 40 and <= 47:
                    background = Ansi[code - 40];
                    break;
                case >= 100 and <= 107:
                    background = Ansi[code - 100 + 8];
                    break;
                case 38 or 48:
                    var exact = Exact(codes, ref i);
                    if (code == 38)
                    {
                        foreground = exact;
                    }
                    else
                    {
                        background = exact;
                    }

                    break;
            }
        }
    }

    private static SKColor Exact(string[] codes, ref int i)
    {
        if (i + 1 < codes.Length && codes[i + 1] == "2" && i + 4 < codes.Length)
        {
            var red = byte.Parse(codes[i + 2]);
            var green = byte.Parse(codes[i + 3]);
            var blue = byte.Parse(codes[i + 4]);

            i += 4;
            return new(red, green, blue);
        }

        if (i + 2 >= codes.Length || codes[i + 1] != "5")
        {
            return Foreground;
        }

        var entry = int.Parse(codes[i + 2]);
        i += 2;

        return entry < Ansi.Length ? Ansi[entry] : Foreground;
    }
}
