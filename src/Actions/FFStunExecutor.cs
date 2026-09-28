using Cthangover.Core.Cards.StatusEffect;
using Cthangover.Core.Characters;
using Cthangover.Core.Utils;

namespace Cthangover.FFBattle.Actions
{
    /// <summary>
    /// Applies a stun status effect to the target for a configurable number of turns.
    /// Reads the turn count from the <c>"Turn"</c> property on the action (default 3).
    /// Stunned characters are skipped during turn iteration in
    /// <see cref="FFBattleCore.TryNextCharacterOrEndTurn"/> and
    /// <see cref="FFBattleCore.RunEnemyTurn"/>. Registered under ID <c>"physics/stun"</c>.
    /// </summary>
    public class FFStunExecutor : ActionBase
    {
        public override string ID => "FFStunExecutor";

        public override ChangedAttributes Execute(ActionCharacter action, Character user, Character target)
        {
            if (target == null || user == null || !CheckRequiredAndUsePoint(action, user))
                return new ChangedAttributes { Result = false };

            if (target.HasPassiveEffect("StunImmune"))
                return new ChangedAttributes { Result = true };

            var turns = action.GetInt("Turn", 3);
            StatusEffectService.Of(target).Add("effect/physics/stun", turns);
            GameLogger.Log("FF_BATTLE",
                $"{user.Name} stuns {target.Name} for {turns} turns",
                LogLevel.Debug);

            return new ChangedAttributes { Result = true };
        }
    }
}
