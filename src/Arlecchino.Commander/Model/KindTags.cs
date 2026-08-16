namespace Arlecchino.Commander.Model;

/// <summary>
/// Which extensions are worth reading as one family, and under what tag. An extension that is not in
/// here is written as it is, which says as much as a tag invented for it would.
/// </summary>
public static class KindTags
{
    /// <summary>The tag a family of extensions shares.</summary>
    /// <param name="extension">The extension, dot and all, in lower case.</param>
    /// <returns>The tag, or <c>null</c> when this extension speaks for itself.</returns>
    public static string? Of(string extension) => extension switch
    {
        ".md" or ".markdown" or ".mdx" => "md",
        ".json" or ".jsonc" or ".yml" or ".yaml" or ".toml" or ".ini" or ".conf" or ".config" or ".props" or
            ".targets" or ".editorconfig" or ".plist" or ".cnf" => "cfg",
        ".zip" or ".gz" or ".tgz" or ".tar" or ".7z" or ".rar" or ".xz" or ".bz2" or ".zst" or ".lz4" or
            ".cab" => "zip",
        ".log" => "log",
        ".pem" or ".key" or ".pfx" or ".p12" or ".crt" or ".cer" or ".gpg" or ".asc" or ".kdbx" => "key",
        ".exe" or ".com" or ".bat" or ".cmd" or ".msi" or ".appimage" => "exe",
        ".dll" or ".so" or ".dylib" or ".a" or ".lib" or ".o" or ".obj" => "lib",
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".tiff" or ".tif" or ".ico" or ".svg" or
            ".heic" or ".avif" or ".qoi" or ".tga" or ".ppm" or ".pgm" or ".pbm" or ".pnm" or ".psd" => "img",
        ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" or ".wmv" or ".flv" or ".m4v" or ".mpg" or
            ".mpeg" => "vid",
        ".mp3" or ".flac" or ".wav" or ".ogg" or ".opus" or ".m4a" or ".aac" or ".wma" or ".mid" or
            ".midi" => "aud",
        ".pdf" or ".doc" or ".docx" or ".odt" or ".rtf" or ".epub" or ".djvu" or ".xls" or ".xlsx" or ".ods" or
            ".ppt" or ".pptx" or ".odp" => "doc",
        ".html" or ".htm" or ".xhtml" or ".css" or ".scss" or ".sass" or ".less" => "web",
        ".sh" or ".bash" or ".zsh" or ".fish" or ".ps1" or ".psm1" => "sh",
        ".sqlite" or ".sqlite3" or ".mdb" or ".db" => "db",
        ".iso" or ".dmg" or ".vhd" or ".vhdx" or ".qcow2" => "iso",
        ".ttf" or ".otf" or ".woff" or ".woff2" or ".eot" => "fnt",
        ".deb" or ".rpm" or ".apk" or ".pkg" or ".msix" or ".nupkg" or ".whl" or ".snap" or ".flatpak" => "pkg",
        ".txt" or ".text" or ".nfo" => "txt",
        _ => null,
    };
}
