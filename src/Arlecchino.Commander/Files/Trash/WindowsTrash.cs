using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arlecchino.Commander.Files.Trash;

/// <summary>
/// The Recycle Bin, asked for through the shell rather than written to. What makes a deleted file
/// restorable on Windows is the record the shell keeps beside it; a file moved into the bin folder by
/// hand is a file in a folder with a strange name, and Restore will not offer it.
///
/// The old file operation is used rather than the newer interface, because it wants nothing but a
/// struct: the newer one is COM, and COM from a program published ahead of time means keeping the
/// type registrations alive through the trimmer for no gain here.
///
/// Two shapes of this call catch people out. The path list is double-null-terminated — one terminator
/// ends the path and one ends the list — and the operation reports refusal two ways: a non-zero result,
/// and a flag saying the user or the shell called it off while the result stayed zero. Both are
/// failures, and only checking the first is how a delete that never happened gets reported as done.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsTrash : Trash
{
    /// <summary>The one of these there is.</summary>
    public static readonly WindowsTrash Instance = new();

    private const uint Delete = 0x0003;

    private const ushort Silent = 0x0004;
    private const ushort NoConfirmation = 0x0010;
    private const ushort AllowUndo = 0x0040;
    private const ushort NoErrorUi = 0x0400;

    /// <inheritdoc/>
    public override bool Works => true;

    /// <inheritdoc/>
    public override bool TryPut(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var listed = IntPtr.Zero;

        try
        {
            listed = Marshal.StringToHGlobalUni(Path.GetFullPath(path) + '\0');

            var asked = new Operation
            {
                Function = Delete,
                From = listed,
                Flags = Silent | NoConfirmation | AllowUndo | NoErrorUi,
            };

            return Ask(ref asked) == 0 && asked.Aborted == 0;
        }
        catch (Exception error) when (error is IOException or ArgumentException or NotSupportedException)
        {
            return false;
        }
        finally
        {
            if (listed != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(listed);
            }
        }
    }

    [DllImport("shell32.dll", EntryPoint = "SHFileOperationW", ExactSpelling = true)]
    private static extern int Ask(ref Operation asked);

    /// <summary>
    /// What the shell is asked with. The fields are in the order the call reads them and none may be
    /// left out, which is why the ones this never uses are here at all.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Operation
    {
        public IntPtr Window;
        public uint Function;
        public IntPtr From;
        public IntPtr To;
        public ushort Flags;
        public int Aborted;
        public IntPtr NameMappings;
        public IntPtr ProgressTitle;
    }
}
