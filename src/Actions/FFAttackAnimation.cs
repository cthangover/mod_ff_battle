using Cthangover.Core.Battle;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;
using Cthangover.TurnBattle;
using Cthangover.FFBattle.UI;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.FFBattle.Actions
{
    /// <summary>
    /// Animation for offensive actions against enemies. Implements a three-phase
    /// sequence: (1) MoveForward — source character dashes toward the target with
    /// ease-in-out interpolation, stopping 30px short, (2) Impact — applies damage
    /// via <see cref="ActionExecutorHub"/> when 30% through the phase, triggering
    /// flash, shake, and floating damage text, (3) MoveBack — returns the source
    /// to its original position with ease-out.
    /// Used for player attacks and enemy attacks alike.
    /// </summary>
    public class FFAttackAnimation : FFAbstractAnimation
    {
        private enum Phase { MoveForward, Impact, MoveBack, Done }

        private Phase _phase;
        private bool _damageApplied;
        private Vector2 _midPoint;

        /// <summary>Creates an attack animation for source→target with the given action descriptor and speed.</summary>
        public FFAttackAnimation(FFCharacterWidget source, FFCharacterWidget target, ActionCharacter action, float speed = 1f)
            : base(source, target, action, speed) { }

        /// <summary>Starts the attack sequence: compute the stop point short of the target and reset the phase clock.</summary>
        protected override void DoInternalStart()
        {
            Timestamp = Godot.Time.GetTicksUsec() / 1_000_000.0;
            _damageApplied = false;
            _phase = Phase.MoveForward;

            var dir = (TargetPos - SourcePos).Normalized();
            _midPoint = TargetPos - dir * 30f;
        }

        /// <summary>Advances move-in → impact (damage via <see cref="ActionExecutorHub"/>, hit flash/shake, damage popup) → move-back.</summary>
        protected override bool DoInternalAction()
        {
            var elapsed = (float)(Godot.Time.GetTicksUsec() / 1_000_000.0 - Timestamp) * Speed * (float)Godot.Engine.TimeScale;

            switch (_phase)
            {
                case Phase.MoveForward:
                {
                    var progress = Mathf.Clamp(elapsed / 0.35f, 0f, 1f);
                    Source.GlobalPosition = SourcePos.Lerp(_midPoint, EaseInOutQuad(progress));

                    if (progress >= 1f)
                    {
                        _phase = Phase.Impact;
                        Timestamp = Godot.Time.GetTicksUsec() / 1_000_000.0;
                    }
                    break;
                }

                case Phase.Impact:
                {
                    var progress = Mathf.Clamp(elapsed / 0.2f, 0f, 1f);

                    if (!_damageApplied && progress >= 0.3f)
                    {
                        _damageApplied = true;
                        var result = ActionExecutorHub.Instance.Execute(Action, Source.Card, Target.Card);
                        Source.UpdateInfo();
                        Target.UpdateInfo();

                        if (result.Result)
                        {
                            Target.Flash(UiTokens.Danger, 0.2f);
                            Target.Shake(4f, 0.25f);

                            if (result.Target.Damage > 0)
                                ShowDamageBehaviour.SpawnDamage(result.Target.Damage, Target, Target.GlobalPosition);

                            var defenceLost = -result.Target.Defence;
                            if (defenceLost > 0)
                                ShowDamageBehaviour.SpawnDefence(defenceLost, Target, Target.GlobalPosition);
                        }
                    }

                    if (progress >= 1f)
                    {
                        _phase = Phase.MoveBack;
                        Timestamp = Godot.Time.GetTicksUsec() / 1_000_000.0;
                    }
                    break;
                }

                case Phase.MoveBack:
                {
                    var progress = Mathf.Clamp(elapsed / 0.4f, 0f, 1f);
                    Source.GlobalPosition = _midPoint.Lerp(SourcePos, EaseOutQuad(progress));

                    if (progress >= 1f)
                    {
                        _phase = Phase.Done;
                        return true;
                    }
                    break;
                }
            }

            return false;
        }

        /// <summary>Returns the attacker to its original position and refreshes both widgets.</summary>
        protected override void DoInternalEnd()
        {
            if (IsAlive(Source))
                Source.GlobalPosition = SourcePos;
            Source.UpdateInfo();
            Target.UpdateInfo();
        }
    }
}

