using Cthangover.Core.UI;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.FFBattle.UI
{
    /// <summary>
    /// Centered announcement strip ("Player turn", "Enemy turn", escape results).
    /// The node tree lives in <c>scenes/ff_battle_banner.tscn</c>; this class
    /// dresses it with tokens and owns the fade in/hold/fade out tween. The banner
    /// is purely informational, so it ignores the mouse and the core places it on
    /// its own anchored strip between the enemy grid and the party row.
    /// </summary>
    public partial class FFBattleBanner : ModWidget
    {
        private Label _label;
        private Tween _tween;

        protected override void Construct()
        {
            CthangoverTheme.Apply(this);

            _label = GetNode<Label>("Center/Panel/Label");
            _label.AddThemeColorOverride("font_color", UiTokens.InkBright);

            var style = UiKit.Frame(0, UiTokens.Alpha(UiTokens.Page, 0.62f), new Color(0f, 0f, 0f, 0f));
            style.BorderWidthTop = 1;
            style.BorderWidthBottom = 1;
            style.BorderColor = UiTokens.AccentLine;
            style.ContentMarginLeft = 44;
            style.ContentMarginRight = 44;
            style.ContentMarginTop = 12;
            style.ContentMarginBottom = 12;
            GetNode<PanelContainer>("Center/Panel").AddThemeStyleboxOverride("panel", style);

            Modulate = UiTokens.Alpha(UiTokens.White, 0f);
        }

        /// <summary>Shows the given caption and restarts the 1.6s fade cycle.</summary>
        public void Show(string text)
        {
            _label.Text = text;
            _tween?.Kill();
            Modulate = UiTokens.Alpha(UiTokens.White, 0f);

            _tween = CreateTween();
            _tween.TweenProperty(this, "modulate:a", 1f, 0.2f);
            _tween.TweenInterval(1.1f);
            _tween.TweenProperty(this, "modulate:a", 0f, 0.3f);
        }
    }
}
