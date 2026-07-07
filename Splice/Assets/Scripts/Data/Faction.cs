namespace Splice.Data
{
    // The 4 top-level visual/lore families a faction rolls up into (see splice-faction-design.md).
    // Sub-factions themselves are DATA assets (FactionSO), not an enum — so a new faction is added with
    // zero code changes (create a FactionSO, drop it in the FactionRegistry).
    public enum FactionFamily
    {
        Human,
        Galax,
        Natural,   // Beast, Elf, Thorn, Swarm
        Darkside   // Undead, Demon
    }
}
