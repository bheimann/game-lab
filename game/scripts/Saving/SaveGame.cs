using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using LizardWizard.Actors;
using LizardWizard.Dungeons;
using LizardWizard.Items;
using LizardWizard.Players;

namespace LizardWizard.Saving;

/// <summary>Something in the world whose state is kept in the save file (a door, a lever, a chest...).</summary>
public interface ISaveable
{
    /// <summary>The state to save, or null if it's still how it started.</summary>
    string? SaveState();

    /// <summary>Put back a saved state, quietly (no "Click!" or "The door opens!").</summary>
    void LoadState(string state);
}

/// <summary>What one player slot was playing, so picking the same character again brings it all back.</summary>
public sealed class SavedPlayer
{
    public string Character { get; set; } = "";

    public string Class { get; set; } = "";

    public string Weapon { get; set; } = "";

    public string? Armor { get; set; }

    /// <summary>Older saves: this player's own herbs (herbs are the team's now, see <see cref="SaveData.Herbs"/>).</summary>
    public int Herbs { get; set; }

    /// <summary>The move a Thief stole last (empty: none).</summary>
    public string StolenMove { get; set; } = "";

    /// <summary>Monster parts fused onto this hero's weapons, guns and armor ("Weapon:Antler", see <see cref="Fusion"/>).</summary>
    public List<string> Fusions { get; set; } = new();

    /// <summary>Which fusion of each kind was in use (kind → index, -1 = none).</summary>
    public Dictionary<string, int> HeldFusions { get; set; } = new();
}

/// <summary>Everything in the save file.</summary>
public sealed class SaveData
{
    public int Version { get; set; } = 1;

    public List<string> BossesBeaten { get; set; } = new();

    public int Keys { get; set; }

    /// <summary>The team's coins (<see cref="Purse"/>).</summary>
    public int Coins { get; set; }

    public int BossCoins { get; set; }

    /// <summary>The team's healing herbs.</summary>
    public int Herbs { get; set; }

    /// <summary>The team's smelling salts and fairies (they revive knocked-out heroes).</summary>
    public int Salts { get; set; }

    public int Fairies { get; set; }

    /// <summary>The team's weapon material (0 = wood, see <see cref="WeaponTier"/>).</summary>
    public int WeaponTier { get; set; }

    /// <summary>The team's monster parts (name → how many).</summary>
    public Dictionary<string, int> Parts { get; set; } = new();

    /// <summary>Armor the moose Tinkerer has crafted.</summary>
    public List<string> CraftedArmor { get; set; } = new();

    /// <summary>Older saves: The Old Burrow was beaten (now in <see cref="CompletedDungeons"/>).</summary>
    public bool DungeonComplete { get; set; }

    /// <summary>Villages whose teleports are awake.</summary>
    public List<string> Teleports { get; set; } = new();

    /// <summary>The last gathering marker anyone reached (a dungeon foyer or a teleport room): where the team starts.
    /// Empty: the village.</summary>
    public string GatherPoint { get; set; } = "";

    /// <summary>The dungeons beaten so far (their area names).</summary>
    public List<string> CompletedDungeons { get; set; } = new();

    /// <summary>Node path (from the scene root) → saved state.</summary>
    public Dictionary<string, string> World { get; set; } = new();

    /// <summary>Things that are gone for good (broken rocks), by node path.</summary>
    public List<string> Removed { get; set; } = new();

    /// <summary>Player slot (0 = P1) → what they were playing.</summary>
    public Dictionary<int, SavedPlayer> Players { get; set; } = new();
}

/// <summary>
/// Saves the game to <c>user://save.json</c> (on Windows: %APPDATA%\Godot\app_userdata\Lizard Wizard) and loads
/// it next time. The game saves by itself: going through a door, beating a boss, a player leaving, and closing
/// the game. Loading puts the world back how it was (doors, levers, chests, broken rocks, bosses, keys, coins, herbs, monster parts, crafted armor, weapon material) and
/// starts the team in Scalebrook Village. Dev runs (autoplay, screenshots) never touch the save, unless they're given
/// their own file with <c>--save-file=user://test_save.json</c>.
/// </summary>
public static class SaveGame
{
    private const string FileFlag = "--save-file=";

    private static readonly string Path =
        OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith(FileFlag))?[FileFlag.Length..] ?? "user://save.json";

    private static SaveData? _loaded;

    /// <summary>Off for dev runs (autoplay bots, screenshots), so they never touch the real save.</summary>
    public static bool Enabled { get; } =
        OS.GetCmdlineUserArgs().Any(a => a.StartsWith(FileFlag))
        || !OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--autoplay") || a.StartsWith("--screenshot"));

    public static bool Exists => Enabled && FileAccess.FileExists(Path);

    /// <summary>The save file's contents (read once).</summary>
    public static SaveData? Loaded
    {
        get
        {
            if (_loaded is null && Exists)
            {
                using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
                try
                {
                    _loaded = JsonSerializer.Deserialize<SaveData>(file.GetAsText());
                }
                catch (JsonException e)
                {
                    GD.PushWarning($"Couldn't read the save file, starting fresh: {e.Message}");
                }
            }

            return _loaded;
        }
    }

    /// <summary>Things broken since the game started (merged into the save).</summary>
    private static readonly HashSet<string> Removed = new();

    /// <summary>Remember that <paramref name="node"/> is gone for good (a rock was blown up).</summary>
    public static void MarkRemoved(Node node) => Removed.Add(node.GetTree().CurrentScene.GetPathTo(node));

    /// <summary>Put the world back how the save left it. Call once everything in the scene is ready.</summary>
    public static void Apply(Node anyNode)
    {
        var data = Loaded;
        if (data is null)
        {
            Playtest.ApplyNewGame(); // a new game: playtest settings (starting coins...)
            Playtest.ApplyAlways(anyNode);
            return;
        }

        GD.Print($"Loaded the save: {data.World.Count} things changed, {data.Removed.Count} gone, bosses beaten: {string.Join(", ", data.BossesBeaten)}");
        var scene = anyNode.GetTree().CurrentScene;
        foreach (var path in data.Removed)
        {
            Removed.Add(path);
            scene.GetNodeOrNull(path)?.QueueFree();
        }

        foreach (var (path, state) in data.World)
        {
            if (scene.GetNodeOrNull(path) is ISaveable saveable)
            {
                saveable.LoadState(state);
            }
        }

        Unlocks.Restore(data.BossesBeaten);
        Purse.Restore(data.Coins, data.BossCoins);
        Items.Teleports.Restore(data.Teleports);
        Purse.RestoreHerbs(data.Herbs > 0 ? data.Herbs : data.Players.Values.Sum(p => p.Herbs)); // older saves: everyone's herbs pooled
        Purse.RestoreRevivers(data.Salts, data.Fairies);
        Items.WeaponTier.Restore(data.WeaponTier);
        MonsterParts.Restore(data.Parts);
        Armor.RestoreCrafted(data.CraftedArmor);
        if (Dungeon.Find(anyNode) is { } dungeon)
        {
            dungeon.Keys = data.Keys;
            dungeon.RestoreCompleted(data.DungeonComplete ? data.CompletedDungeons.Append(dungeon.DungeonName) : data.CompletedDungeons);
        }

        foreach (var room in scene.FindChildren("*", "", true, false).OfType<DungeonRoom>())
        {
            room.SyncCleared();
        }

        Playtest.ApplyAlways(anyNode);
    }

    /// <summary>Save now (does nothing in dev runs).</summary>
    public static void Save(Node anyNode)
    {
        if (!Enabled || !anyNode.IsInsideTree())
        {
            return;
        }

        var scene = anyNode.GetTree().CurrentScene;
        var data = new SaveData
        {
            BossesBeaten = Unlocks.BeatenBosses.ToList(),
            Coins = Purse.Coins,
            BossCoins = Purse.BossCoins,
            Herbs = Purse.Herbs,
            Salts = Purse.Salts,
            Fairies = Purse.Fairies,
            Teleports = Items.Teleports.Unlocked.ToList(),
            WeaponTier = Items.WeaponTier.Level,
            Parts = MonsterParts.Snapshot(),
            CraftedArmor = Armor.Crafted.ToList(),
            Removed = Removed.ToList(),
            Players = new Dictionary<int, SavedPlayer>(Loaded?.Players ?? new()),
        };

        foreach (var node in scene.FindChildren("*", "", true, false))
        {
            if (node is ISaveable saveable && saveable.SaveState() is { } state)
            {
                data.World[scene.GetPathTo(node)] = state;
            }
        }

        if (Dungeon.Find(anyNode) is { } dungeon)
        {
            data.Keys = dungeon.Keys;
            data.CompletedDungeons = dungeon.CompletedDungeons.ToList();
            data.GatherPoint = dungeon.LastGatherPoint is { } marker ? scene.GetPathTo(marker) : "";
        }

        if (PlayerManager.Find(anyNode) is { } manager)
        {
            for (var i = 0; i < manager.Players.Count; i++)
            {
                if (manager.Players[i] is { } hero)
                {
                    data.Players[i] = new SavedPlayer
                    {
                        Character = hero.DisplayName,
                        Class = hero.CurrentClass,
                        Weapon = hero.CurrentWeapon,
                        Armor = hero.Armor?.Name,
                        StolenMove = hero.GetChildren().OfType<Abilities.StolenMoveAbility>().FirstOrDefault()?.Move ?? "",
                        Fusions = System.Enum.GetValues<FuseKind>().SelectMany(hero.Fusions).Select(f => f.ToString()).ToList(),
                        HeldFusions = System.Enum.GetValues<FuseKind>().ToDictionary(k => k.ToString(), hero.HeldFusion),
                    };
                }
            }
        }

        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        file.StoreString(JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        _loaded = data;
    }

    /// <summary>What this player slot was playing last time, if it was the same character.</summary>
    public static SavedPlayer? PlayerFor(int slot, string character) =>
        Loaded?.Players.GetValueOrDefault(slot) is { } saved && saved.Character == character ? saved : null;

    /// <summary>Throw the save away (new game).</summary>
    public static void Delete()
    {
        if (Exists)
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(Path));
        }

        _loaded = null;
        Removed.Clear();
        Unlocks.Restore(Enumerable.Empty<string>());
        Purse.Restore(0, 0);
        Purse.RestoreHerbs(0);
        Items.Teleports.Restore(Enumerable.Empty<string>());
        Items.WeaponTier.Restore(0);
        MonsterParts.Restore(null);
        Armor.RestoreCrafted(Enumerable.Empty<string>());
    }
}
