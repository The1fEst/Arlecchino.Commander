using System.Collections.Generic;
using System.Threading.Tasks;
using Arlecchino.Commander.Files.Disks;

namespace Arlecchino.Commander.Tests.Support;

/// <summary>
/// Disks a test says the machine has, and a mounting that writes down what it was asked for. A test
/// host is a machine of its own, and what it happens to be holding is nothing to assert against.
/// </summary>
public sealed class NotedDisks : Disks
{
    private readonly List<string> _mountedDevices = [];

    /// <summary>What the machine is to be holding, which a test fills in before it opens the list.</summary>
    public List<Place> Places { get; } = [];

    /// <summary>Whether this one mounts anything, which a test turns off to stand for Windows.</summary>
    public bool CanMount { get; set; } = true;

    /// <summary>Where a mounting is to land, which is what makes one succeed or be refused.</summary>
    public string Landing { get; set; } = "";

    /// <summary>Every device a mounting was asked for, in the order it was asked.</summary>
    public IReadOnlyList<string> MountedDevices => _mountedDevices;

    /// <inheritdoc/>
    public override bool Mounts => CanMount;

    /// <inheritdoc/>
    public override Task<IReadOnlyList<Place>> ReadAsync() => Task.FromResult<IReadOnlyList<Place>>(Places);

    /// <inheritdoc/>
    public override Task<string> TryMountAsync(Place place)
    {
        _mountedDevices.Add(place.Device);

        return Task.FromResult(Landing);
    }
}
