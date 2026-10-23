using OpenIGI.Core.Graphics;
using OpenIGI.Core.Numerics;
using OpenIGI.Engine.Vfs;
using OpenIGI.Game.Igi2.Systems;
using OpenIGI.Game.Player;
using OpenIGI.Game.World;
using OpenIGI.Presentation;
using OpenIGI.Presentation.Igi2.Hud;
using OpenIGI.Presentation.Igi2.ViewModel;
using OpenIGI.Rendering;

namespace OpenIGI.Mods.Igi2Soldiers;

/// <summary>I.G.I. 2's HUD over I.G.I. 2's player in a Project I.G.I. mission, its art read from I.G.I. 2's data, not the mission's.</summary>
/// <param name="hud">I.G.I. 2's HUD.</param>
/// <param name="content">The I.G.I. 2 content the player is made of.</param>
internal sealed class Igi2GuestHud(Igi2HudRenderer hud, Igi2GuestContent content) : ITitleHudPresenter
{
    /// <inheritdoc/>
    /// <remarks>The host offers the mission's files; the HUD's are I.G.I. 2's.</remarks>
    public void Load(VirtualFileSystem files) => hud.Load(content.Guest.FileSystem);

    /// <inheritdoc/>
    public void Draw(SpriteBatch batch, ITitlePlayer player, int screenWidth, int screenHeight, int requestedScale = UiScale.Automatic) =>
        hud.Draw(batch, player, screenWidth, screenHeight, requestedScale);

    /// <inheritdoc/>
    public void Dispose() => hud.Dispose();
}

/// <summary>
/// I.G.I. 2's player's arms and what it holds, and its body in the world, read from I.G.I. 2's packs, not the mission's.
/// </summary>
/// <param name="arms">I.G.I. 2's view-model renderer.</param>
/// <param name="content">The I.G.I. 2 content the player is made of.</param>
internal sealed class Igi2GuestArms(Igi2ViewModelRenderer arms, Igi2GuestContent content) : ITitleViewModelPresenter, ITitleWorldModelPresenter
{
    /// <inheritdoc/>
    public ObjectShading Shading { get => arms.Shading; set => arms.Shading = value; }

    /// <inheritdoc/>
    /// <remarks>The host offers the mission; the models are I.G.I. 2's.</remarks>
    public bool Load(LoadedMission mission) => arms.Load(content.Packs);

    /// <inheritdoc/>
    public void Draw(TitleFirstPersonView view, in Camera3D camera, in Matrix4f projection) => arms.Draw(view, camera, projection);

    /// <inheritdoc/>
    public void DrawWorld(IReadOnlyList<TitleWorldModel> models, in Camera3D camera, in Matrix4f projection, float blend = 1f) =>
        arms.DrawWorld(models, camera, projection, blend);

    /// <inheritdoc/>
    public void DrawWorldSprites(IReadOnlyList<TitleWorldSprite> sprites, in Camera3D camera, in Matrix4f projection) =>
        arms.DrawWorldSprites(sprites, camera, projection);

    /// <inheritdoc/>
    public void DrawGroundDecals(IReadOnlyList<TitleGroundDecal> decals, in Camera3D camera, in Matrix4f projection) =>
        arms.DrawGroundDecals(decals, camera, projection);

    /// <inheritdoc/>
    public void Dispose() => arms.Dispose();
}
