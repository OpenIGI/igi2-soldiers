using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenIGI.Mods.Igi2Soldiers.Mapping;

/// <summary>What one kind of Project I.G.I. soldier becomes as an I.G.I. 2 soldier.</summary>
/// <param name="AiType">The I.G.I. 2 AI type, a <c>HUMANAI_TYPE_*</c> name: how hard it is and how it fights.</param>
/// <param name="AnimType">The I.G.I. 2 animation type, a <c>HUMANAI_ANIMTYPE_*</c> name.</param>
/// <param name="Weapon">The I.G.I. 2 weapon script its body runs, as a level's <c>Weapon</c> parameter holds one.</param>
/// <param name="Models">The I.G.I. 2 body models it may wear, one picked by the soldier's task id so a level always dresses it alike.</param>
public sealed record SoldierMapping(string AiType, string AnimType, string Weapon, IReadOnlyList<string> Models)
{
    /// <summary>Picks the model a soldier wears.</summary>
    /// <param name="taskId">The soldier's task id.</param>
    /// <returns>The model's name.</returns>
    public string ModelFor(int taskId) => Models[(int)((uint)taskId % (uint)Models.Count)];
}

/// <summary>How well the I.G.I. 2 soldiers see the Project I.G.I. player, as I.G.I. 2 measures its own player.</summary>
/// <param name="Gamma">What half the light is raised to, a level's <c>Visibility Gamma</c>.</param>
/// <param name="Minimum">The least the factor comes to, <c>Visibility Minimum</c>.</param>
/// <param name="Maximum">The most it comes to, <c>Visibility Maximum</c>.</param>
/// <param name="Light">
/// The light the player stands in where the level bakes none into its ground, on I.G.I. 2's scale, where two is as bright as counts;
/// elsewhere the level's own lightmap under the player's feet is read.
/// </param>
/// <remarks>The defaults are I.G.I. 2's first mission's player: 0.8, 0.2 and 0.95.</remarks>
public sealed record PlayerVisibility(float Gamma = 0.8f, float Minimum = 0.2f, float Maximum = 0.95f, float Light = 1f);

/// <summary>The mod's mapping table: which Project I.G.I. soldiers become which I.G.I. 2 soldiers, and how their damage carries.</summary>
/// <param name="Soldiers">
/// Each Project I.G.I. AI type (an <c>AITYPE_*</c> name) and what it becomes; a type mapped to <see langword="null"/> stays a
/// Project I.G.I. soldier, as the story's own characters and the civilians do.
/// </param>
/// <param name="Default">What an AI type the table does not name becomes, or <see langword="null"/> to leave it as it is.</param>
/// <param name="DamageFromIgi1">What a Project I.G.I. round's damage is multiplied by when it strikes an I.G.I. 2 soldier.</param>
/// <param name="DamageFromIgi2">What an I.G.I. 2 round's damage is multiplied by when it strikes a Project I.G.I. human.</param>
/// <param name="Visibility">How well the soldiers see the Project I.G.I. player; <see langword="null"/> for the defaults.</param>
/// <remarks>
/// <para>
/// The two games' damage and health sit on the same scale (a Project I.G.I. AK-47 round does 0.3 of a soldier's health, an
/// I.G.I. 2 one 0.43), so a round does its own game's damage to the other game's body by default; the two factors tune that.
/// </para>
/// <para>
/// Materials need no table: I.G.I. 2's game materials are Project I.G.I.'s twenty-four in the same rows under the same names,
/// and nine more after them, so a material is translated by its name.
/// </para>
/// </remarks>
public sealed record SoldierTable(
    IReadOnlyDictionary<string, SoldierMapping?> Soldiers,
    SoldierMapping? Default,
    float DamageFromIgi1 = 1f,
    float DamageFromIgi2 = 1f,
    PlayerVisibility? Visibility = null)
{
    /// <summary>The name the table ships under, beside the mod's assembly, where a player's own copy overrides it.</summary>
    public const string FileName = "igi2soldiers.json";

    /// <summary>Gets the table the mod ships with.</summary>
    public static SoldierTable Shipped { get; } = Read(ShippedJson());

    /// <summary>What a Project I.G.I. soldier of an AI type becomes.</summary>
    /// <param name="igi1AiType">Its AI type, an <c>AITYPE_*</c> name.</param>
    /// <returns>Its mapping, or <see langword="null"/> when it stays a Project I.G.I. soldier.</returns>
    public SoldierMapping? For(string igi1AiType) =>
        Soldiers.TryGetValue(igi1AiType, out SoldierMapping? mapped) ? mapped : Default;

    /// <summary>Reads a table.</summary>
    /// <param name="json">The table, as JSON.</param>
    /// <returns>The table.</returns>
    /// <exception cref="JsonException">The JSON is not a table.</exception>
    public static SoldierTable Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        SoldierTable table = JsonSerializer.Deserialize(json, SoldierTableJson.Default.SoldierTable)
            ?? throw new JsonException("The soldier table is empty.");
        foreach ((string type, SoldierMapping? mapping) in table.Soldiers)
        {
            if (mapping is { Models.Count: 0 })
            {
                throw new JsonException($"The soldier table gives {type} no model to wear.");
            }
        }

        return table with { Soldiers = new Dictionary<string, SoldierMapping?>(table.Soldiers, StringComparer.Ordinal) };
    }

    /// <summary>Reads the table beside the mod's assembly when there is one, else the shipped one.</summary>
    /// <param name="directory">The mod's directory.</param>
    /// <returns>The table.</returns>
    public static SoldierTable Load(string directory)
    {
        string path = Path.Combine(directory, FileName);
        return File.Exists(path) ? Read(File.ReadAllText(path)) : Shipped;
    }

    private static string ShippedJson()
    {
        using Stream stream = typeof(SoldierTable).Assembly.GetManifestResourceStream(FileName)
            ?? throw new InvalidOperationException($"The mod ships no {FileName}.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>The table's JSON shape, generated so that reading one needs no reflection.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(SoldierTable))]
internal sealed partial class SoldierTableJson : JsonSerializerContext;
