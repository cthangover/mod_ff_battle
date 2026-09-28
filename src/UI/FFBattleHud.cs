using Cthangover.Core.UI;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.FFBattle.UI
{
    /// <summary>
    /// Top-left battle HUD: title, phase badge and the round / roster counters.
    /// The node tree comes from <c>scenes/ff_battle_hud.tscn</c>; this class only
    /// fetches nodes, dresses them with <see cref="UiTokens"/> styles and exposes
    /// narrow setters so <see cref="FFBattleCore"/> never assembles controls by
    /// hand. The phase badge reuses the type-accent palette: blue for the player
    /// turn, red for the enemy turn.
    /// </summary>
    public partial class FFBattleHud : ModWidget
    {
        private Label _title;
        private PanelContainer _phaseBadge;
        private Label _phaseLabel;
        private Label _roundValue;
        private Label _enemiesValue;
        private Label _alliesValue;

        protected override void Construct()
        {
            CthangoverTheme.Apply(this);

            _title = GetNode<Label>("Panel/Column/TopRow/TitleLabel");
            _phaseBadge = GetNode<PanelContainer>("Panel/Column/TopRow/PhaseBadge");
            _phaseLabel = GetNode<Label>("Panel/Column/TopRow/PhaseBadge/PhaseLabel");
            _roundValue = GetNode<Label>("Panel/Column/MetaRow/RoundValue");
            _enemiesValue = GetNode<Label>("Panel/Column/MetaRow/EnemiesValue");
            _alliesValue = GetNode<Label>("Panel/Column/MetaRow/AlliesValue");

            var panelStyle = UiKit.Frame(UiTokens.RadiusLg, UiTokens.Alpha(UiTokens.Surface0, 0.86f), UiTokens.Line);
            panelStyle.ContentMarginLeft = 18;
            panelStyle.ContentMarginRight = 18;
            panelStyle.ContentMarginTop = 12;
            panelStyle.ContentMarginBottom = 13;
            panelStyle.ShadowColor = new Color(0f, 0f, 0f, 0.6f);
            panelStyle.ShadowSize = 18;
            panelStyle.ShadowOffset = new Vector2(0f, 8f);
            GetNode<PanelContainer>("Panel").AddThemeStyleboxOverride("panel", panelStyle);

            _title.AddThemeColorOverride("font_color", UiTokens.InkBright);

            foreach (var name in new[] { "RoundCaption", "EnemiesCaption", "AlliesCaption" })
                GetNode<Label>("Panel/Column/MetaRow/" + name).AddThemeColorOverride("font_color", UiTokens.Muted);

            foreach (var name in new[] { "RoundValue", "EnemiesValue", "AlliesValue" })
                GetNode<Label>("Panel/Column/MetaRow/" + name).AddThemeColorOverride("font_color", UiTokens.AccentBright);

            ApplyPhaseStyle(true);
        }

        /// <summary>Switches the phase badge between the player and enemy turns.</summary>
        public void SetPhase(bool playerTurn)
        {
            _phaseLabel.Text = Translate(playerTurn ? "ff_battle/phase_player" : "ff_battle/phase_enemy");
            ApplyPhaseStyle(playerTurn);
        }

        /// <summary>Updates the round counter.</summary>
        public void SetRound(int round)
            => _roundValue.Text = round.ToString();

        /// <summary>Updates living combatant counters.</summary>
        public void SetCounts(int enemies, int allies)
        {
            _enemiesValue.Text = enemies.ToString();
            _alliesValue.Text = allies.ToString();
        }

        private void ApplyPhaseStyle(bool playerTurn)
        {
            var accent = playerTurn ? UiTokens.TypeSelf : UiTokens.TypeEnemy;
            var style = UiKit.Frame(UiTokens.RadiusXs, UiTokens.BadgeFill(accent), UiTokens.BadgeLine(accent));
            style.ContentMarginLeft = 7;
            style.ContentMarginRight = 7;
            style.ContentMarginTop = 2;
            style.ContentMarginBottom = 2;
            _phaseBadge.AddThemeStyleboxOverride("panel", style);
            _phaseLabel.AddThemeColorOverride("font_color", accent);
        }

        private static string Translate(string key)
            => TranslationServer.Translate(key).ToString();
    }
}
