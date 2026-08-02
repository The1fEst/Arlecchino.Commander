#:package SkiaSharp@3.119.0
#:property GenerateDocumentationFile=true

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using SkiaSharp;

// Columns by rows, not pixels. A hundred rows is a screenshot nobody can read at a glance.
const string Size = "200x34";
var repository = Directory.GetCurrentDirectory();
var output = Path.Combine(repository, "assets", "screenshots");

var framework = Path.Combine(Directory.GetParent(repository)!.FullName, "Arlecchino");

var fixture = Path.Combine(Path.GetTempPath(), "arlecchino-shots");
var scratchLeft = Path.Combine(fixture, "project");
var scratchRight = Path.Combine(fixture, "backup");

// The host the server scenes use. A `~/.ssh/config` entry, so the shots say nothing about anyone's
// credentials; scenes that name it are skipped when the host does not answer.
const string host = "ubuntu";

if (args is ["tape", ..])
{
    Tape();
    return;
}

if (args is ["show", ..])
{
    Show();
    return;
}

Shots();

// The panels show the two repositories, because a screenshot of a real tree says more than a
// made-up one. The scenes that actually copy work on a scratch fixture instead, so a picture never
// writes into a repository.
(string Name, string Size, string Keys, string Wait, bool Scratch, string Connect, string Caption)[] Scenes(
    string size) =>
    [
        ("panels", size, "", "", false, "", "two panels over a local disk"),
        ("marks", size, "End,Up,Up,Space,Space,Space", "", false, "", "three files marked, counted at the foot of the panel"),
        ("sorted", size, "Tab,F9,Down,Down,Down,Down,Enter,Down,Enter", "", false, "", "the right panel sorted by size"),
        ("menu", size, "F9", "", false, "", "the menu, opened by F9"),
        ("file-menu", size, "F9,Down,Enter", "", false, "", "what can be done to what is marked"),
        ("copy", size, "End,Up,Up,Space,Space,F5", "", false, "", "copying asks where to"),
        ("delete", size, "End,Up,Space,F8", "", false, "", "deleting asks first, with no selected"),
        ("viewer", size, "End,Up,Up,Up,F3", "", false, "", "a file read without leaving the panels"),
        ("filter", size, "F4,s,r", "", false, "", "the panel filtered by name"),
        ("palette", size, "Ctrl+K", "", false, "", "everything the application can do, by name"),
        ("hosts", size, "Alt+K", "", false, "", "hosts read from ~/.ssh/config"),
        ("find", size, "Alt+F7,Enter,Enter", "600", false, "", "a walk of the folder, filling in as it goes"),
        ("output", size, "Ctrl+O", "", false, "", "everything the commands printed"),
        ("connect", size, "Ctrl+K,c,o,n,n,e,c,t,Enter", "", false, "", "a connection asked for in full"),
        ("help", size, "F1", "", false, "", "the keys screen, which comes with the framework"),
        ("server", size, "", "", false, host, "a panel browsing a server over SFTP"),
        ("ssh", size, "Ctrl+K,s,s,h,Enter,l,s,Enter", "3000", false, host, "a command run on that server"),
        ("progress", size, "Down,Down,Down,F5,Enter", "120", true, "", "a copy running in the background, with a bar and Esc to stop"),
        ("notification", size, "Down,Down,Down,F5,Enter,Ctrl+N,Enter", "120", true, "", "the same copy opened in full, with Stop offered"),
        ("done", size, "Down,Down,Down,F5,Enter,Ctrl+N,Enter", "6000", true, "", "the same entry once the copy is over"),
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

// Walks the same scenes with nothing between them and the terminal: each frame is written out as the
// application drew it, Enter goes to the next one, q stops.
//
// The pictures go through a renderer of our own — the ANSI is parsed into cells and painted with
// Skia — and a renderer of our own is a second place for the drawing to be wrong. Here the terminal
// draws it, with the real font, the real glyph widths and the real colours, so a frame that looks
// right here and wrong in the picture says the fault is in the painting rather than in the program.
//
// Every frame is captured at the size of this terminal rather than the fixed one the pictures use,
// because a frame composed for 200 columns and shown in 120 is not the screen anybody would see.
void Show()
{
    if (Console.IsInputRedirected || Console.IsOutputRedirected)
    {
        Console.Error.WriteLine("show needs a terminal of its own: run it without a pipe.");

        return;
    }

    var columns = Math.Max(40, Console.WindowWidth);
    var rows = Math.Max(10, Console.WindowHeight - 1);
    var size = $"{columns}x{rows}";
    var scenes = Scenes(size);

    Fixture.Lay(fixture, scratchLeft, scratchRight);

    Console.Write("\e[?1049h");

    try
    {
        for (var index = 0; index < scenes.Length; index++)
        {
            var scene = scenes[index];

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
            Console.Write(ansi.Length == 0 ? $"{scene.Name}: nothing came back" : ansi);
            Console.Write($"\e[{rows + 1};1H\e[0m{scene.Name} · {scene.Caption} · " +
                          $"{index + 1} of {scenes.Length} · Enter next · q stops");

            if (Stop())
            {
                return;
            }
        }
    }
    finally
    {
        Console.Write("\e[?1049l");
    }
}

// Waits for the one key that means anything here. Anything else moves on, because a reviewer pressing
// space or an arrow means "next" and being told otherwise is a puzzle nobody asked for.
static bool Stop()
{
    var key = Console.ReadKey(intercept: true);

    return key.Key is ConsoleKey.Q or ConsoleKey.Escape;
}

// Records the animation the framework's readme opens with. One run of the application plays the whole
// script and draws a frame wherever the script says `shot`, so the bar that fills and the entry that
// finishes are caught while they happen rather than staged one process at a time.
void Tape()
{
    const string Caption = "arlecchino.commander";

    // How long each frame is held is how long a hand would have waited there: a moment to see where
    // the cursor is, a longer one over a dialog that has to be read, and no two presses exactly alike.
    // A step with no key of its own is the recording watching work that is already running.
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

    Playground.Lay();

    var ansi = Capture("--tape", Size, script, "", Playground.Left, Playground.Right, "");
    var (columns, rows) = Measure(Size);
    using var paper = new Paper(15f);
    var frames = new List<(SKBitmap Image, int Hold)>();

    foreach (var (hold, text) in Reel(ansi))
    {
        var grid = Terminal.Parse(text);

        if (grid.Count == 0)
        {
            continue;
        }

        frames.Add((paper.Draw(Fit(grid, columns, rows), Caption), hold));
    }

    if (frames.Count == 0)
    {
        Console.WriteLine("tape: nothing came back");
        return;
    }

    var target = Path.Combine(Directory.Exists(framework) ? framework : repository, "assets", "demo.gif");
    var shape = $"{frames[0].Image.Width}x{frames[0].Image.Height}";

    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    Gif.Write(target, frames, 128);

    foreach (var (image, _) in frames)
    {
        image.Dispose();
    }

    var written = new FileInfo(target).Length;

    Console.WriteLine($"tape: {frames.Count} frames, {shape}, {written / 1024d / 1024d:0.0} MB → {target}");
}

// Splits a recording into its frames. Each is announced by a record separator and the milliseconds it
// is to be held for, so the reel carries the timing the application ran at rather than a guess.
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

// Every picture is the size its scene asked for, whatever the screen happened to draw: a dialog over
// an otherwise empty screen leaves the rest of the frame blank, and a gallery of pictures that are
// each a different height reads as a mistake.
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
    // Release, named rather than left to the default. Without it the pictures come from whatever was
    // last built in Debug, which is how a screenshot ends up showing a screen that no longer exists.
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

// No picture carries a real address. Every scene that reaches a server draws the host it connected
// to, and that is somebody's machine — so any address in a captured frame is replaced before it is
// drawn, with one from the range set aside for documentation, padded to the width it stood in so no
// row shifts under it.
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
/// Draws a grid of cells as a window: the terminal on a rounded plate, three lamps and a caption above
/// it. The size of the type sets everything else, so the same drawing serves a screenshot at one scale
/// and an animation at another.
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
        ArgumentNullException.ThrowIfNull(grid);

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

    /// <summary>Lets go of the type, fallbacks included.</summary>
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

    // Where a font lives differs per platform, and picking the wrong one is not an error anybody sees
    // as one: Skia hands back a proportional face and the picture comes out with its columns adrift.
    // So every folder the three platforms keep fonts in is searched by name, and only then a family.
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
/// The folders the screenshots are taken of. Shooting the repository itself would put whatever the
/// working copy happens to hold into the pictures, so the panels are pointed at a small tree that is
/// laid out the same way every time and rebuilt between scenes the copies would otherwise change.
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

        for (var index = 0; index < 220; index++)
        {
            File.WriteAllText(Path.Combine(heavy.FullName, $"page{index:000}.md"), new string('x', 40_000));
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

/// <summary>
/// Writes an animation as a GIF. The pictures share one colour table, chosen from what the frames
/// actually hold, and every frame after the first carries only the rectangle that changed — a terminal
/// mostly stands still, so that is what keeps the file small enough for a readme.
/// </summary>
static class Gif
{
    private const int MaximumCodes = 4096;

    /// <summary>Encodes the frames and writes them out.</summary>
    /// <param name="path">Where the file goes.</param>
    /// <param name="frames">The pictures and how long each is held, in milliseconds.</param>
    /// <param name="colors">How many entries the colour table may hold, up to 256.</param>
    public static void Write(string path, IReadOnlyList<(SKBitmap Image, int Hold)> frames, int colors)
    {
        ArgumentNullException.ThrowIfNull(frames);

        var width = frames[0].Image.Width;
        var height = frames[0].Image.Height;
        var palette = Palette.Choose(frames, colors);
        var bits = Bits(palette.Count);

        using var file = File.Create(path);
        using var writer = new BinaryWriter(file);

        Header(writer, width, height, bits, palette);

        byte[]? previous = null;
        var pending = 0;

        for (var index = 0; index < frames.Count; index++)
        {
            var (image, hold) = frames[index];
            var current = palette.Map(image);
            var box = Changed(previous, current, width, height);

            if (box is null)
            {
                pending += hold;
                continue;
            }

            Frame(writer, current, width, box.Value, hold + pending, bits);

            pending = 0;
            previous = current;
        }

        writer.Write((byte)0x3B);
    }

    private static int Bits(int count)
    {
        var bits = 1;

        while (1 << bits < count)
        {
            bits++;
        }

        return Math.Min(8, bits);
    }

    private static void Header(BinaryWriter writer, int width, int height, int bits, Palette palette)
    {
        writer.Write("GIF89a"u8);
        writer.Write((ushort)width);
        writer.Write((ushort)height);
        writer.Write((byte)(0xF0 | (bits - 1)));
        writer.Write((byte)0);
        writer.Write((byte)0);

        for (var index = 0; index < 1 << bits; index++)
        {
            var color = index < palette.Count ? palette[index] : SKColors.Black;

            writer.Write(color.Red);
            writer.Write(color.Green);
            writer.Write(color.Blue);
        }

        writer.Write((byte)0x21);
        writer.Write((byte)0xFF);
        writer.Write((byte)0x0B);
        writer.Write("NETSCAPE2.0"u8);
        writer.Write((byte)0x03);
        writer.Write((byte)0x01);
        writer.Write((ushort)0);
        writer.Write((byte)0);
    }

    private static void Frame(
        BinaryWriter writer,
        byte[] indices,
        int width,
        (int Left, int Top, int Width, int Height) box,
        int hold,
        int bits)
    {
        writer.Write((byte)0x21);
        writer.Write((byte)0xF9);
        writer.Write((byte)0x04);
        writer.Write((byte)0x04);
        writer.Write((ushort)Math.Max(2, hold / 10));
        writer.Write((byte)0);
        writer.Write((byte)0);

        writer.Write((byte)0x2C);
        writer.Write((ushort)box.Left);
        writer.Write((ushort)box.Top);
        writer.Write((ushort)box.Width);
        writer.Write((ushort)box.Height);
        writer.Write((byte)0);

        var cut = new byte[box.Width * box.Height];

        for (var row = 0; row < box.Height; row++)
        {
            Array.Copy(indices, ((box.Top + row) * width) + box.Left, cut, row * box.Width, box.Width);
        }

        var minimum = Math.Max(2, bits);

        writer.Write((byte)minimum);
        Blocks(writer, Lzw(cut, minimum));
        writer.Write((byte)0);
    }

    /// <summary>
    /// The rectangle two frames differ in, or nothing when they are the same picture. Rewriting only
    /// what moved is what a screen of unchanging text costs almost nothing to animate.
    /// </summary>
    private static (int Left, int Top, int Width, int Height)? Changed(byte[]? previous, byte[] current, int width, int height)
    {
        if (previous is null)
        {
            return (0, 0, width, height);
        }

        var left = width;
        var right = -1;
        var top = height;
        var bottom = -1;

        for (var row = 0; row < height; row++)
        {
            var start = row * width;

            for (var column = 0; column < width; column++)
            {
                if (previous[start + column] == current[start + column])
                {
                    continue;
                }

                left = Math.Min(left, column);
                right = Math.Max(right, column);
                top = Math.Min(top, row);
                bottom = Math.Max(bottom, row);
            }
        }

        return right < 0 ? null : (left, top, right - left + 1, bottom - top + 1);
    }

    private static void Blocks(BinaryWriter writer, byte[] data)
    {
        var offset = 0;

        while (offset < data.Length)
        {
            var length = Math.Min(255, data.Length - offset);

            writer.Write((byte)length);
            writer.Write(data, offset, length);

            offset += length;
        }
    }

    private static byte[] Lzw(byte[] indices, int minimum)
    {
        var clear = 1 << minimum;
        var end = clear + 1;
        var next = end + 1;
        var size = minimum + 1;

        var dictionary = new Dictionary<int, int>();
        var bits = new BitWriter();

        bits.Write(clear, size);

        var prefix = -1;

        foreach (var symbol in indices)
        {
            if (prefix < 0)
            {
                prefix = symbol;
                continue;
            }

            var key = (prefix << 8) | symbol;

            if (dictionary.TryGetValue(key, out var known))
            {
                prefix = known;
                continue;
            }

            bits.Write(prefix, size);
            dictionary[key] = next++;

            if (next > MaximumCodes)
            {
                bits.Write(clear, size);
                dictionary.Clear();
                next = end + 1;
                size = minimum + 1;
            }
            else if (next - 1 == 1 << size && size < 12)
            {
                size++;
            }

            prefix = symbol;
        }

        if (prefix >= 0)
        {
            bits.Write(prefix, size);
        }

        bits.Write(end, size);

        return bits.Drain();
    }

    private sealed class BitWriter
    {
        private readonly List<byte> _bytes = [];
        private int _bits;
        private int _count;

        public void Write(int code, int size)
        {
            _bits |= code << _count;
            _count += size;

            while (_count >= 8)
            {
                _bytes.Add((byte)(_bits & 0xFF));
                _bits >>= 8;
                _count -= 8;
            }
        }

        public byte[] Drain()
        {
            if (_count <= 0)
            {
                return [.. _bytes];
            }

            _bytes.Add((byte)(_bits & 0xFF));
            _bits = 0;
            _count = 0;

            return [.. _bytes];
        }
    }

    /// <summary>
    /// The colours the animation is drawn with, cut out of what the frames hold. Text drawn with
    /// smoothed edges carries far more shades than a table can name, so the shades are counted, the
    /// crowd is split until there are as many boxes as entries, and each box gives up its average.
    /// </summary>
    private sealed class Palette
    {
        private readonly SKColor[] _entries;
        private readonly Dictionary<int, byte> _known = [];

        private Palette(SKColor[] entries) => _entries = entries;

        public int Count => _entries.Length;

        public SKColor this[int index] => _entries[index];

        public static Palette Choose(IReadOnlyList<(SKBitmap Image, int Hold)> frames, int wanted)
        {
            var counts = new Dictionary<int, int>();

            foreach (var (image, _) in frames)
            {
                foreach (var pixel in image.Pixels)
                {
                    var key = Key(pixel);
                    counts[key] = counts.TryGetValue(key, out var seen) ? seen + 1 : 1;
                }
            }

            List<List<KeyValuePair<int, int>>> boxes = [[.. counts]];

            while (boxes.Count < wanted)
            {
                var widest = -1;
                var spread = -1;
                var channel = 0;

                for (var index = 0; index < boxes.Count; index++)
                {
                    if (boxes[index].Count < 2)
                    {
                        continue;
                    }

                    var (found, range) = Widest(boxes[index]);

                    if (range <= spread)
                    {
                        continue;
                    }

                    spread = range;
                    widest = index;
                    channel = found;
                }

                if (widest < 0)
                {
                    break;
                }

                var box = boxes[widest];
                box.Sort((left, right) => Channel(left.Key, channel).CompareTo(Channel(right.Key, channel)));

                var half = Half(box);

                boxes[widest] = box[..half];
                boxes.Add(box[half..]);
            }

            var entries = new SKColor[boxes.Count];

            for (var index = 0; index < boxes.Count; index++)
            {
                entries[index] = Average(boxes[index]);
            }

            return new(entries);
        }

        /// <summary>Turns a picture into one index per pixel.</summary>
        /// <param name="image">The picture.</param>
        /// <returns>The indices, row by row.</returns>
        public byte[] Map(SKBitmap image)
        {
            var pixels = image.Pixels;
            var indices = new byte[pixels.Length];

            for (var index = 0; index < pixels.Length; index++)
            {
                var key = Key(pixels[index]);

                if (!_known.TryGetValue(key, out var entry))
                {
                    entry = Nearest(pixels[index]);
                    _known[key] = entry;
                }

                indices[index] = entry;
            }

            return indices;
        }

        private byte Nearest(SKColor color)
        {
            var best = 0;
            var closest = int.MaxValue;

            for (var index = 0; index < _entries.Length; index++)
            {
                var red = color.Red - _entries[index].Red;
                var green = color.Green - _entries[index].Green;
                var blue = color.Blue - _entries[index].Blue;
                var distance = (red * red * 3) + (green * green * 6) + (blue * blue);

                if (distance >= closest)
                {
                    continue;
                }

                closest = distance;
                best = index;
            }

            return (byte)best;
        }

        private static int Key(SKColor color) =>
            ((color.Red >> 3) << 10) | ((color.Green >> 3) << 5) | (color.Blue >> 3);

        private static int Channel(int key, int channel) => channel switch
        {
            0 => (key >> 10) & 0x1F,
            1 => (key >> 5) & 0x1F,
            _ => key & 0x1F,
        };

        private static (int Channel, int Range) Widest(List<KeyValuePair<int, int>> box)
        {
            var found = 0;
            var widest = -1;

            for (var channel = 0; channel < 3; channel++)
            {
                var low = 0x1F;
                var high = 0;

                foreach (var entry in box)
                {
                    var value = Channel(entry.Key, channel);

                    low = Math.Min(low, value);
                    high = Math.Max(high, value);
                }

                if (high - low <= widest)
                {
                    continue;
                }

                widest = high - low;
                found = channel;
            }

            return (found, widest);
        }

        /// <summary>Where a box splits: the point half its pixels lie on either side of.</summary>
        private static int Half(List<KeyValuePair<int, int>> box)
        {
            var total = 0L;

            foreach (var entry in box)
            {
                total += entry.Value;
            }

            var walked = 0L;

            for (var index = 0; index < box.Count - 1; index++)
            {
                walked += box[index].Value;

                if (walked * 2 >= total)
                {
                    return index + 1;
                }
            }

            return box.Count / 2;
        }

        private static SKColor Average(List<KeyValuePair<int, int>> box)
        {
            long red = 0, green = 0, blue = 0, total = 0;

            foreach (var entry in box)
            {
                var weight = entry.Value;

                red += (long)((Channel(entry.Key, 0) << 3) + 4) * weight;
                green += (long)((Channel(entry.Key, 1) << 3) + 4) * weight;
                blue += (long)((Channel(entry.Key, 2) << 3) + 4) * weight;
                total += weight;
            }

            return total == 0
                ? SKColors.Black
                : new((byte)Math.Min(255, red / total),
                    (byte)Math.Min(255, green / total),
                    (byte)Math.Min(255, blue / total));
        }
    }
}
