namespace Cthangover.FFBattle.UI
{
    /// <summary>
    /// Semantic categories of <see cref="FFBattleLogPanel"/> entries. The kind
    /// selects the row colour and the left border accent: turn separators are
    /// gold, damage red, absorbed shield blue, buffs green, statuses amber and
    /// deaths grey. <see cref="Info"/> is the neutral narration fallback.
    /// </summary>
    public enum FFBattleLogKind
    {
        Info,
        Turn,
        Damage,
        Shield,
        Buff,
        Status,
        Dead
    }
}
