using OpenIGI.Core.Diagnostics;
using OpenIGI.Core.Graphics;
using OpenIGI.Core.Numerics;
using OpenIGI.Presentation;
using OpenIGI.Presentation.Igi2.ViewModel;
using OpenIGI.Rendering;

namespace OpenIGI.Mods.Igi2Soldiers.Presentation;

/// <summary>Draws a mission's I.G.I. 2 soldiers as I.G.I. 2 draws its own: each body posed by its skeleton, its weapon in hand.</summary>
/// <remarks>
/// The bodies are I.G.I. 2's world models (<see cref="Igi2SoldierLevel.Models"/>) of the soldiers in the world, as the tick last
/// posed them, read from the I.G.I. 2
/// packs the soldiers' kit opened and drawn by I.G.I. 2's own renderer of them, which skins them all in one batch. Both games
/// measure 4,096 units to the metre, so the bodies stand in Project I.G.I.'s world as they do in their own.
/// </remarks>
internal sealed class Igi2SoldierPresenter : ITaskPresenter
{
    private readonly Igi2SoldierLevel _level;
    private readonly Igi2ViewModelRenderer _renderer;

    /// <summary>Initializes a new instance of the <see cref="Igi2SoldierPresenter"/> class.</summary>
    /// <param name="device">The device to draw with.</param>
    /// <param name="kit">Where the models and their textures are read from.</param>
    /// <param name="level">The soldiers.</param>
    /// <param name="logger">Where problems go.</param>
    public Igi2SoldierPresenter(IGraphicsDevice device, Igi2SoldierKit kit, Igi2SoldierLevel level, IOpenIgiLogger logger)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(kit);
        _level = level ?? throw new ArgumentNullException(nameof(level));
        _renderer = new Igi2ViewModelRenderer(device, kit.Guest.Resources, logger);
        _renderer.Load(kit.Packs);
    }

    /// <inheritdoc/>
    public void Draw(in Camera3D camera, in Matrix4f projection, float blend) =>
        _renderer.DrawWorld(_level.Models(), camera, projection);

    /// <inheritdoc/>
    /// <remarks>The soldiers stand in the level's light like its own: they shadow the ground under them and the lamps.</remarks>
    public bool CastsShadow => true;

    /// <inheritdoc/>
    public void Dispose() => _renderer.Dispose();
}
