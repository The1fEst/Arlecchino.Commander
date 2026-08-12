using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arlecchino.Commander.Files.Trash;

/// <summary>
/// The trash on a Mac, asked for through the same call Finder makes, since only that call writes the record
/// Put Back needs. It goes through the Objective-C runtime, one declaration per message sent.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacTrash : Trash
{
    /// <summary>The one of these there is.</summary>
    public static readonly MacTrash Instance = new();

    private const string Runtime = "/usr/lib/libobjc.A.dylib";

    /// <inheritdoc/>
    public override bool Works => true;

    /// <inheritdoc/>
    public override bool TryPut(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var manager = Send(Class("NSFileManager"), Selector("defaultManager"));
        var text = SendText(Class("NSString"), Selector("stringWithUTF8String:"), path);

        if (manager == IntPtr.Zero || text == IntPtr.Zero)
        {
            return false;
        }

        var url = SendPointer(Class("NSURL"), Selector("fileURLWithPath:"), text);

        return url != IntPtr.Zero &&
               SendTrash(manager, Selector("trashItemAtURL:resultingItemURL:error:"), url, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport(Runtime, EntryPoint = "objc_getClass", ExactSpelling = true)]
    private static extern IntPtr Class([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(Runtime, EntryPoint = "sel_registerName", ExactSpelling = true)]
    private static extern IntPtr Selector([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(Runtime, EntryPoint = "objc_msgSend", ExactSpelling = true)]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

    [DllImport(Runtime, EntryPoint = "objc_msgSend", ExactSpelling = true)]
    private static extern IntPtr SendText(
        IntPtr receiver,
        IntPtr selector,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    [DllImport(Runtime, EntryPoint = "objc_msgSend", ExactSpelling = true)]
    private static extern IntPtr SendPointer(IntPtr receiver, IntPtr selector, IntPtr argument);

    [DllImport(Runtime, EntryPoint = "objc_msgSend", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool SendTrash(
        IntPtr receiver,
        IntPtr selector,
        IntPtr url,
        IntPtr resulting,
        IntPtr error);
}
