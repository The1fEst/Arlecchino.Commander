using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arlecchino.Commander.Files.Trash;

/// <summary>
/// The trash on a Mac, which is not a folder anybody should be moving files into by hand. Finder keeps
/// the record of where each thing came from beside the file itself, and only the system call writes it;
/// a file moved into <c>~/.Trash</c> by hand lands there with no way back, and Put Back is grayed out.
/// So this asks the system, through the same call Finder makes.
///
/// Asking it means the Objective-C runtime, since there is no other door to <c>NSFileManager</c>. Each
/// message is sent through its own declaration of <c>objc_msgSend</c>: the function takes whatever the
/// method takes, and giving it one signature for all of them is how interop like this goes wrong on one
/// architecture while looking right on another.
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
