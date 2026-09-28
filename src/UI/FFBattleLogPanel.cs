using Cthangover.Core.UI;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.FFBattle.UI
{
    /// <summary>
    /// Battle journal docked to the upper right: a window with a header carrying
    /// the round counter and a scrollable list of colour-coded rows. The panel is
    /// a proposal on top of the bare battle screen — it gives damage, shield
    /// absorption, status and death events a readable history that the original
    /// FF-style UI lacked. Node structure lives in
    /// <c>scenes/ff_battle_log_panel.tscn</c>; rows are created at runtime because
    /// they are data, not layout, and are capped to <see cref="MaxRows"/> so a long
    /// battle cannot grow unbounded.
    /// </summary>
    public partial class FFBattleLogPanel : ModWidget
    {
        private const int MaxRows = 80;

        private Label _title;
        private Label _roundLabel;
        private ScrollContainer _scroll;
        private VBoxContainer _rows;

        protected override void Construct()
        {
            CthangoverTheme.Apply(this);

            _title = GetNode<Label>("Window/Column/Header/HeaderRow/TitleLabel");
            _roundLabel = GetNode<Label>("Window/Column/Header/HeaderRow/RoundLabel");
            _scroll = GetNode<ScrollContainer>("Window/Column/Scroll");
            _rows = GetNode<VBoxContainer>("Window/Column/Scroll/Rows");

            var windowStyle = UiKit.Frame(UiTokens.RadiusLg, UiTokens.Alpha(UiTokens.Surface0, 0.84f), UiTokens.Line);
            windowStyle.ContentMarginLeft = 0;
            windowStyle.ContentMarginRight = 0;
            windowStyle.ContentMarginTop = 0;
            windowStyle.ContentMarginBottom = 0;
            windowStyle.ShadowColor = new Color(0f, 0f, 0f, 0.6f);
            windowStyle.ShadowSize = 18;
            windowStyle.ShadowOffset = new Vector2(0f, 8f);
            GetNode<PanelContainer>("Window").AddThemeStyleboxOverride("panel", windowStyle);

            _title.AddThemeColorOverride("font_color", UiTokens.Muted);
            _roundLabel.AddThemeColorOverride("font_color", UiTokens.Faint);
        }

        /// <summary>Updates the round caption in the header.</summary>
        public void SetRound(int round)
            => _roundLabel.Text = string.Format(Translate("ff_battle/log_round"), round);

        /// <summary>Drops every row; used when the battle is cleared or restarted.</summary>
        public void Clear()
        {
            foreach (var child in _rows.GetChildren())
            {
                _rows.RemoveChild(child);
                child.QueueFree();
            }
        }

        /// <summary>
        /// Appends one coloured row and trims the oldest entries past the cap.
        /// The scroll is pushed to the bottom so the newest event is always visible.
        /// </summary>
        public void AddEntry(string text, FFBattleLogKind kind)
        {
            var style = UiKit.Frame(0, BackgroundOf(kind), new Color(0f, 0f, 0f, 0f));
            style.BorderWidthLeft = 2;
            style.BorderColor = BorderOf(kind);
            style.ContentMarginLeft = 16;
            style.ContentMarginRight = 12;
            style.ContentMarginTop = 7;
            style.ContentMarginBottom = 7;

            var row = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.AddThemeStyleboxOverride("panel", style);

            var label = UiKit.Label(text, UiTokens.FontBodySmall, TextOf(kind));
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            row.AddChild(label);

            _rows.AddChild(row);

            while (_rows.GetChildCount() > MaxRows)
            {
                var first = _rows.GetChild(0);
                _rows.RemoveChild(first);
                first.QueueFree();
            }

            _scroll.ScrollVertical = (int)_scroll.GetVScrollBar().MaxValue;
        }

        private static Color BackgroundOf(FFBattleLogKind kind)
            => kind == FFBattleLogKind.Turn ? UiTokens.AccentSoft : new Color(0f, 0f, 0f, 0f);

        private static Color BorderOf(FFBattleLogKind kind)
        {
            switch (kind)
            {
                case FFBattleLogKind.Turn: return UiTokens.Accent;
                case FFBattleLogKind.Damage: return UiTokens.Danger;
                case FFBattleLogKind.Shield: return UiTokens.TypeResource;
                case FFBattleLogKind.Buff: return UiTokens.TypePassive;
                case FFBattleLogKind.Status: return UiTokens.Cooldown;
                case FFBattleLogKind.Dead: return UiTokens.LineFaint;
                default: return UiTokens.Line;
            }
        }

        private static Color TextOf(FFBattleLogKind kind)
        {
            switch (kind)
            {
                case FFBattleLogKind.Turn: return UiTokens.AccentBright;
                case FFBattleLogKind.Damage: return UiTokens.DangerText;
                case FFBattleLogKind.Shield: return UiTokens.Info;
                case FFBattleLogKind.Buff: return UiTokens.SuccessText;
                case FFBattleLogKind.Status: return UiTokens.Cooldown;
                case FFBattleLogKind.Dead: return UiTokens.Faint;
                default: return UiTokens.Muted;
            }
        }

        private static string Translate(string key)
            => TranslationServer.Translate(key).ToString();
    }
}
