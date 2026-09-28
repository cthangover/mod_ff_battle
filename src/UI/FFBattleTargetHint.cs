using Cthangover.Core.UI;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.FFBattle.UI
{
    /// <summary>
    /// Toast-style directive shown while the player picks a battle target
    /// ("Select a target · RMB / Esc — cancel"). It is a deliberate addition over
    /// the bare FF UI: the original mod highlighted widgets without ever telling
    /// the player what the highlight meant, which is easy to miss on a large enemy
    /// grid. The node tree lives in <c>scenes/ff_battle_target_hint.tscn</c>; the
    /// class dresses it with tokens and toggles visibility.
    /// </summary>
    public partial class FFBattleTargetHint : ModWidget
    {
        private Label _icon;
        private Label _text;

        protected override void Construct()
        {
            CthangoverTheme.Apply(this);

            _icon = GetNode<Label>("Center/Toast/Row/IconLabel");
            _text = GetNode<Label>("Center/Toast/Row/TextLabel");
            _icon.AddThemeColorOverride("font_color", UiTokens.AccentBright);
            _text.AddThemeColorOverride("font_color", UiTokens.Ink);

            var style = UiKit.Frame(UiTokens.RadiusMd, UiTokens.ToastBg, UiTokens.Line);
            style.ContentMarginLeft = 14;
            style.ContentMarginRight = 14;
            style.ContentMarginTop = 10;
            style.ContentMarginBottom = 10;
            style.ShadowColor = new Color(0f, 0f, 0f, 0.5f);
            style.ShadowSize = 10;
            style.ShadowOffset = new Vector2(0f, 4f);
            GetNode<PanelContainer>("Center/Toast").AddThemeStyleboxOverride("panel", style);
        }

        /// <summary>Shows the hint with the given caption.</summary>
        public void ShowHint(string text)
        {
            _text.Text = text;
            Visible = true;
        }

        /// <summary>Hides the hint; called when a target is confirmed or selection is cancelled.</summary>
        public void HideHint()
            => Visible = false;
    }
}
