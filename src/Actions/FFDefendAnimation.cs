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
    /// Animation for defensive/support actions (buff, self-target, ally heal).
    /// Phases: (1) Bounce — source character bobs in place with a sine-based
    /// vertical oscillation, (2) Glow — applies the effect via
    /// <see cref="ActionExecutorHub"/> at 30% phase progress, triggering a green
    /// flash on the target and floating defence/damage indicators, (3) Recover —
    /// smoothly returns the source to its original position with ease-out
    /// interpolation. Unlike <see cref="FFAttackAnimation"/>, the source does not
    /// move toward the target.
    /// </summary>
    public class FFDefendAnimation : FFAbstractAnimation
    {
        private enum Phase { Bounce, Glow, Recover, Done }

        private Phase _phase;
        private bool _effectApplied;
        private Vector2 _bounceOffset;

        /// <summary>Creates a defend/buff animation for source→target with the given action descriptor and speed.</summary>
        public FFDefendAnimation(FFCharacterWidget source, FFCharacterWidget target, ActionCharacter action, float speed = 1f)
            : base(source, target, action, speed) { }

        /// <summary>Starts the defend sequence from the bounce phase and captures the glow state.</summary>
        protected override void DoInternalStart()
        {
            Timestamp = Godot.Time.GetTicksUsec() / 1_000_000.0;
            _effectApplied = false;
            _phase = Phase.Bounce;
            _bounceOffset = new Vector2(0, -12f);
        }

        /// <summary>Advances bounce → glow (defence application with a green flash) → settle.</summary>
        protected override bool DoInternalAction()
        {
            var elapsed = (float)(Godot.Time.GetTicksUsec() / 1_000_000.0 - Timestamp) * Speed * (float)Godot.Engine.TimeScale;

            switch (_phase)
            {
                case Phase.Bounce:
                {
                    var progress = Mathf.Clamp(elapsed / 0.25f, 0f, 1f);
                    var bounce = Mathf.Sin(progress * Mathf.Pi * 2f) * 8f;
                    Source.GlobalPosition = SourcePos + new Vector2(0, -Mathf.Abs(bounce));

                    if (progress >= 1f)
                    {
                        _phase = Phase.Glow;
                        Timestamp = Godot.Time.GetTicksUsec() / 1_000_000.0;
                    }
                    break;
                }

                case Phase.Glow:
                {
                    var progress = Mathf.Clamp(elapsed / 0.4f, 0f, 1f);

                    if (!_effectApplied && progress >= 0.3f)
                    {
                        _effectApplied = true;
                        var result = ActionExecutorHub.Instance.Execute(Action, Source.Card, Target.Card);
                        Source.UpdateInfo();
                        Target.UpdateInfo();

                        if (result.Result)
                        {
                            Target.Flash(UiTokens.Success, 0.4f);

                            if (result.Target.Defence > 0)
                                ShowDamageBehaviour.SpawnDefence(result.Target.Defence, Target, Target.GlobalPosition);

                            if (result.Target.Damage > 0)
                                ShowDamageBehaviour.SpawnDamage(result.Target.Damage, Target, Target.GlobalPosition, true);
                        }
                    }

                    if (progress >= 1f)
                    {
                        _phase = Phase.Recover;
                        Timestamp = Godot.Time.GetTicksUsec() / 1_000_000.0;
                    }
                    break;
                }

                case Phase.Recover:
                {
                    var progress = Mathf.Clamp(elapsed / 0.3f, 0f, 1f);
                    Source.GlobalPosition = (SourcePos + _bounceOffset).Lerp(SourcePos, EaseOutQuad(progress));

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

        /// <summary>Returns the defender to its original position and refreshes both widgets.</summary>
        protected override void DoInternalEnd()
        {
            if (IsAlive(Source))
                Source.GlobalPosition = SourcePos;
            Source.UpdateInfo();
            Target.UpdateInfo();
        }
    }
}

