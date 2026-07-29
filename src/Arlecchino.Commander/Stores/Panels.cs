using System;
using System.Collections.Generic;
using System.IO;
using Arlecchino.Atoms;
using Arlecchino.Commander.Files;
using Arlecchino.Commander.Model;

namespace Arlecchino.Commander.Stores;

public sealed class Panels : IArlecchinoStore, IDisposable
{
    public PanelState Left { get; } = new(new LocalSource(), Directory.GetCurrentDirectory());

    public PanelState Right { get; } = new(new LocalSource(), Listing.Home());

    public Atom<bool> RightIsActive { get; } = new LocalAtom<bool>(false);

    public Atom<string> Viewing { get; } = new LocalAtom<string>("");

    public IFileSource ViewingSource { get; set; } = new LocalSource();

    public long ViewingSize { get; set; }

    /// <summary>Folders kept by hand for jumping straight back to them.</summary>
    public List<string> Hotlist { get; } = [];

    public void Dispose()
    {
        Left.Source.Dispose();
        Right.Source.Dispose();
    }

    public void Start(string left, string right)
    {
        if (Directory.Exists(left))
        {
            Left.GoTo(Path.GetFullPath(left));
        }

        if (Directory.Exists(right))
        {
            Right.GoTo(Path.GetFullPath(right));
        }
    }
}
