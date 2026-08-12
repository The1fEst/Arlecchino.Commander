using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arlecchino.Commander.Files.Trash;

/// <summary>
/// The Recycle Bin, asked for through the old shell file operation, which keeps the record Restore needs and
/// wants nothing but a struct. That struct is laid out twice, one per bitness.
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

    private const ushort Quietly = Silent | NoConfirmation | AllowUndo | NoErrorUi;

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

            return Environment.Is64BitProcess ? PutAligned(listed) : PutPacked(listed);
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

    private static bool PutAligned(IntPtr listed)
    {
        var asked = new Operation
        {
            Function = Delete,
            From = listed,
            Flags = Quietly,
        };

        return Ask(ref asked) == 0 && asked.Aborted == 0;
    }

    private static bool PutPacked(IntPtr listed)
    {
        var asked = new PackedOperation
        {
            Function = Delete,
            From = listed,
            Flags = Quietly,
        };

        return Ask(ref asked) == 0 && asked.Aborted == 0;
    }

    [DllImport("shell32.dll", EntryPoint = "SHFileOperationW", ExactSpelling = true)]
    private static extern int Ask(ref Operation asked);

    [DllImport("shell32.dll", EntryPoint = "SHFileOperationW", ExactSpelling = true)]
    private static extern int Ask(ref PackedOperation asked);

    /// <summary>
    /// What the shell is asked with in a 64-bit process. The fields are in the order the call reads them
    /// and none may be left out, so the ones this never uses stand here too.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
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

    /// <summary>The same fields with the padding squeezed out, which is what a 32-bit process is read as.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct PackedOperation
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
