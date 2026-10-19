using System.Collections.Generic;

namespace LizardWizard.Items;

/// <summary>
/// Which villages' teleports are awake (see TeleportCrystal and TeleportPad): touching the crystal in a village castle's
/// teleport room wakes that village up, and from then on any teleport room can send the team there. Shared by the team
/// and kept in the save.
/// </summary>
public static class Teleports
{
    private static readonly HashSet<string> Awake = new();

    public static IEnumerable<string> Unlocked => Awake;

    public static bool IsUnlocked(string village) => Awake.Contains(village);

    /// <summary>Wake a village's teleport. Returns false if it was awake already.</summary>
    public static bool Unlock(string village) => Awake.Add(village);

    /// <summary>Loading a save, or a new game (none).</summary>
    public static void Restore(IEnumerable<string> villages)
    {
        Awake.Clear();
        Awake.UnionWith(villages);
    }
}
