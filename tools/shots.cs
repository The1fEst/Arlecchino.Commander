#:package SkiaSharp@3.119.0
#:property GenerateDocumentationFile=true

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SkiaSharp;

const int FontSize = 32;
const int Padding = 40;
const int TitleBar = 104;
const int Radius = 24;

var repository = Directory.GetCurrentDirectory();
var output = Path.Combine(repository, "assets", "screenshots");

Directory.CreateDirectory(output);

var fixture = Path.Combine(Path.GetTempPath(), "arlecchino-shots");
var left = Path.Combine(fixture, "project");
var right = Path.Combine(fixture, "backup");

Fixture.Lay(fixture, left, right);

(string Name, string Size, string Keys, string Wait, string Caption)[] scenes =
[
    ("panels", "132x26", "", "", "two panels over a local disk"),
    ("marks", "132x26", "Down,Space,Space,Space", "", "three files marked, counted at the foot of the panel"),
    ("sorted", "132x26", "F9,Down,Down,Down,Down,Enter,Down,Enter", "", "the right panel sorted by size"),
    ("menu", "132x26", "F9", "", "the menu, opened by F9"),
    ("file-menu", "132x26", "F9,Down,Enter", "", "what can be done to what is marked"),
    ("copy", "132x26", "Down,Space,Space,F5", "", "copying asks where to"),
    ("delete", "132x26", "Down,Space,Space,F8", "", "deleting asks first, with no selected"),
    ("progress", "132x26", "Down,F5,Enter", "40", "a copy running in the background, with a bar and Esc to stop"),
    ("notifications", "132x26", "Down,F5,Enter,Ctrl+N", "60", "the same copy on the notifications screen"),
    ("notification", "132x26", "Down,F5,Enter,Ctrl+N,Enter", "60", "opened in full, with Stop offered"),
    ("done", "132x26", "Down,F5,Enter,Ctrl+N,Enter", "4000", "the same entry once the copy is over"),
    ("viewer", "132x26", "End,Up,Up,F3", "", "a file read without leaving the panels"),
    ("filter", "132x26", "F4,c,s", "", "the panel filtered by name"),
    ("hosts", "132x26", "Ctrl+K", "", "hosts read from ~/.ssh/config"),
    ("connect", "132x26", "F9,Enter,Down,Down,Down,Down,Down,Down,Enter", "", "connecting a panel to a server"),
    ("ssh", "132x26", "F9,Down,Down,Enter,Down,Down,Down,Enter", "", "commands run over SSH"),
    ("palette", "132x26", ":", "", "the command palette, which comes with the framework"),
    ("help", "132x32", "F1", "", "the keys screen, which comes with it too"),
];

var typeface = Typeface("JetBrainsMonoNLNerdFontMono-Regular.ttf");
var boldface = Typeface("JetBrainsMonoNLNerdFontMono-Bold.ttf");
var font = new SKFont(typeface, FontSize);
var bold = new SKFont(boldface, FontSize);
var fallbacks = new Dictionary<int, SKFont>();

var cellWidth = font.MeasureText("MMMMMMMMMM") / 10f;
var metrics = font.Metrics;
var cellHeight = MathF.Round(-metrics.Ascent + metrics.Descent + 4f);

foreach (var scene in scenes)
{
    Fixture.Reset(right);

    var ansi = Capture(scene.Size, scene.Keys, scene.Wait);
    var grid = Terminal.Parse(ansi);

    if (grid.Count == 0)
    {
        Console.WriteLine($"{scene.Name}: nothing came back");
        continue;
    }

    var path = Path.Combine(output, $"{scene.Name}.png");
    Paint(grid, scene.Caption, path);

    Console.WriteLine($"{scene.Name}: {grid[0].Count}x{grid.Count} → {path}");
}

string Capture(string size, string keys, string wait)
{
    var arguments = $"run --project src/Arlecchino.Commander --no-build -- " +
                    $"--frame {size} --left \"{left}\" --right \"{right}\"";

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
    };

    start.Environment["COLORTERM"] = "truecolor";
    start.Environment["TERM"] = "xterm-256color";

    using var process = Process.Start(start)!;
    var text = process.StandardOutput.ReadToEnd();
    process.WaitForExit();

    return text;
}

void Paint(IReadOnlyList<List<Cell>> grid, string caption, string path)
{
    var columns = grid[0].Count;
    var width = (int)MathF.Ceiling(columns * cellWidth) + (Padding * 2);
    var height = (int)(grid.Count * cellHeight) + (Padding * 2) + TitleBar;

    using var bitmap = new SKBitmap(width, height);
    using var canvas = new SKCanvas(bitmap);

    canvas.Clear(SKColors.Transparent);

    using var window = new SKPaint { Color = Terminal.Background, IsAntialias = true };
    canvas.DrawRoundRect(new SKRect(0, 0, width, height), Radius, Radius, window);

    DrawTitleBar(canvas, width, caption);

    using var ink = new SKPaint { IsAntialias = true };

    for (var row = 0; row < grid.Count; row++)
    {
        var line = grid[row];
        var top = Padding + TitleBar + (row * cellHeight);

        for (var column = 0; column < line.Count; column++)
        {
            var cell = line[column];
            var left = Padding + (column * cellWidth);

            if (cell.Background != Terminal.Background)
            {
                ink.Color = cell.Background;
                canvas.DrawRect(new SKRect(left, top, left + cellWidth + 0.6f, top + cellHeight), ink);
            }

            if (cell.Symbol is " " or "")
            {
                continue;
            }

            ink.Color = cell.Foreground;

            if (Lines.Draw(canvas, ink, cell.Symbol, left, top, cellWidth, cellHeight))
            {
                continue;
            }

            canvas.DrawText(cell.Symbol, left, top - metrics.Ascent + 2f, SKTextAlign.Left,
                Pick(cell.Symbol, cell.Bold), ink);
        }
    }

    using var image = SKImage.FromBitmap(bitmap);
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    using var file = File.Create(path);

    data.SaveTo(file);
}

SKFont Pick(string symbol, bool heavy)
{
    var chosen = heavy ? bold : font;
    var point = char.ConvertToUtf32(symbol, 0);

    if ((heavy ? boldface : typeface).GetGlyph(point) != 0)
    {
        return chosen;
    }

    if (fallbacks.TryGetValue(point, out var known))
    {
        return known;
    }

    var face = SKFontManager.Default.MatchCharacter(point);
    var fallback = face is null ? chosen : new SKFont(face, FontSize);

    fallbacks[point] = fallback;
    return fallback;
}

void DrawTitleBar(SKCanvas canvas, int width, string caption)
{
    SKColor[] lamps = [new(0xC9, 0x38, 0x2B), new(0xD0, 0x8A, 0x2C), new(0x8A, 0x81, 0x89)];

    using var lamp = new SKPaint { IsAntialias = true };

    for (var i = 0; i < lamps.Length; i++)
    {
        lamp.Color = lamps[i];
        canvas.DrawCircle(Padding + 16 + (i * 48), TitleBar / 2f, 14, lamp);
    }

    using var text = new SKPaint { Color = new(0xC5, 0xBC, 0xB0), IsAntialias = true };
    using var small = new SKFont(boldface, FontSize * 1.05f);

    canvas.DrawText(caption.ToUpperInvariant(), width / 2f, (TitleBar / 2f) + (small.Size / 3f), SKTextAlign.Center,
        small, text);
}

static SKTypeface Typeface(string file) =>
    SKTypeface.FromFile(FontPath(file))
    ?? SKTypeface.FromFile(FontPath("CascadiaMono.ttf"))
    ?? SKTypeface.FromFamilyName("Consolas");

static string FontPath(string file) =>
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", file);

readonly record struct Cell(string Symbol, SKColor Foreground, SKColor Background, bool Bold);

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

    private static readonly string[] Folders = ["src", "docs", "assets", "tests", ".github"];

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

        if (i + 2 < codes.Length && codes[i + 1] == "5")
        {
            var entry = int.Parse(codes[i + 2]);
            i += 2;
            return entry < Ansi.Length ? Ansi[entry] : Foreground;
        }

        return Foreground;
    }
}
