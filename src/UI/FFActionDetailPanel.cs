using Cthangover.Core.Characters;
using Cthangover.Core.Items;
using Cthangover.Core.UI;
using Cthangover.Core.UI.CharacterPanel;
using Cthangover.Core.UI.Theming;
using Godot;

using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Mods.Interface.Theming;
namespace Cthangover.FFBattle.UI
{
    /// <summary>
    /// Action/item passport shown to the left of the command menu. It mirrors the
    /// action-card component of the unified style guide: a type accent stripe, the
    /// action icon, name and ID, type/cost/cooldown badges, property chips and a
    /// note line with the computed effect preview. The panel exists so the small
    /// FF-style menu can stay a compact list while the player still sees what the
    /// highlighted command actually does. Node structure lives in
    /// <c>scenes/ff_action_detail_panel.tscn</c>; the class fills it and reuses
    /// <see cref="ActionCharacterUi"/> for badges and properties so the battle and
    /// the character panel can never disagree about an action's presentation.
    /// </summary>
    public partial class FFActionDetailPanel : ModWidget
    {
        private PanelContainer _card;
        private ColorRect _stripe;
        private TextureRect _icon;
        private Label _nameLabel;
        private Label _idLabel;
        private HFlowContainer _badges;
        private HFlowContainer _props;
        private PanelContainer _note;
        private Label _noteLabel;

        /// <summary>Builds the action detail passport panel (description plate shown when an action is inspected).</summary>
        protected override void Construct()
        {
            CthangoverTheme.Apply(this);

            _card = GetNode<PanelContainer>("Box/Card");
            _stripe = GetNode<ColorRect>("Box/Card/CardRow/Stripe");
            _icon = GetNode<TextureRect>("Box/Card/CardRow/IconMargin/CardIcon");
            _nameLabel = GetNode<Label>("Box/Card/CardRow/Body/NameRow/NameLabel");
            _idLabel = GetNode<Label>("Box/Card/CardRow/Body/NameRow/IdLabel");
            _badges = GetNode<HFlowContainer>("Box/Card/CardRow/Body/Badges");
            _props = GetNode<HFlowContainer>("Box/Card/CardRow/Body/Props");
            _note = GetNode<PanelContainer>("Box/Note");
            _noteLabel = GetNode<Label>("Box/Note/NoteLabel");

            var cardStyle = UiKit.Frame(UiTokens.RadiusMd, UiTokens.Alpha(UiTokens.Surface1, 0.92f), UiTokens.LineCard);
            cardStyle.ContentMarginLeft = 0;
            cardStyle.ContentMarginRight = 0;
            cardStyle.ContentMarginTop = 0;
            cardStyle.ContentMarginBottom = 0;
            _card.AddThemeStyleboxOverride("panel", cardStyle);

            var noteStyle = UiKit.Frame(UiTokens.RadiusMd, UiTokens.Alpha(UiTokens.Surface2, 0.9f), UiTokens.Line);
            noteStyle.ContentMarginLeft = 12;
            noteStyle.ContentMarginRight = 12;
            noteStyle.ContentMarginTop = 9;
            noteStyle.ContentMarginBottom = 9;
            _note.AddThemeStyleboxOverride("panel", noteStyle);

            _nameLabel.AddThemeColorOverride("font_color", UiTokens.InkBright);
            _idLabel.AddThemeColorOverride("font_color", UiTokens.Faint);
            _noteLabel.AddThemeColorOverride("font_color", UiTokens.Muted);
        }

        /// <summary>
        /// Fills the card with an action passport. <paramref name="note"/> is a
        /// ready localised effect preview built by the core, because only the core
        /// knows the acting character's attack value for the damage estimate.
        /// </summary>
        public void ShowAction(ActionCharacter action, string note)
        {
            if (action == null)
            {
                ShowHint(note);
                return;
            }

            _stripe.Color = ActionCharacterUi.AccentColor(action.Type);
            _icon.Texture = action.Image;
            _nameLabel.Text = ActionCharacterUi.Translate(action.Name);
            _idLabel.Text = action.ID ?? string.Empty;
            _card.Visible = true;

            ResetBody();

            AddBadge(ActionCharacterUi.Translate(ActionCharacterUi.TypeLabelKey(action.Type)), ActionCharacterUi.AccentColor(action.Type));

            var costText = ActionCharacterUi.CostText(action);
            if (!string.IsNullOrEmpty(costText))
                AddBadge(costText, UiTokens.TypeResource);

            var turns = action.GetInt("Turn", 0);
            if (turns > 0)
                AddBadge(string.Format(Translate("ff_battle/badge_turns"), turns), UiTokens.Cooldown);

            foreach (var pair in ActionCharacterUi.SortedProperties(action))
            {
                if (pair.Key == "RequiredPoint")
                    continue;
                _props.AddChild(ActionCharacterUi.MakePropertyChip(pair.Key, pair.Value));
            }

            SetNote(note);
        }

        /// <summary>Fills the card with an inventory item passport for the item sub-menu.</summary>
        public void ShowItem(IItem item, bool targetAlly, string note)
        {
            if (item == null)
            {
                ShowHint(note);
                return;
            }

            _stripe.Color = UiTokens.TypeUsed;
            _icon.Texture = item.Sprite;
            _nameLabel.Text = ActionCharacterUi.Translate(item.Name);
            _idLabel.Text = item.ID ?? string.Empty;
            _card.Visible = true;

            ResetBody();

            if (item.ItemType.HasFlag(ItemType.Used))
                AddBadge(ActionCharacterUi.Translate("ui/inventory/type_used"), UiTokens.TypeUsed);
            AddBadge(ActionCharacterUi.Translate(targetAlly ? "ui/inventory/type_target" : "ff_battle/badge_self"),
                targetAlly ? UiTokens.TypeTarget : UiTokens.TypeSelf);

            SetNote(note);
        }

        /// <summary>Shows a plain directive note under the card, used for highlighted menu hints.</summary>
        public void ShowHint(string note)
        {
            ResetBody();
            _card.Visible = false;
            SetNote(note);
        }

        /// <summary>
        /// Hides the whole passport; the core calls it when the menu closes. The
        /// inherited <see cref="CanvasItem.Hide"/> semantics are enough, so the
        /// method is only a named alias for readability at call sites.
        /// </summary>
        public void HidePassport()
            => Visible = false;

        /// <summary>Shows the panel; the core calls it before filling any content.</summary>
        public void ShowPassport()
            => Visible = true;

        private void ResetBody()
        {
            foreach (var child in _badges.GetChildren())
            {
                _badges.RemoveChild(child);
                child.QueueFree();
            }
            foreach (var child in _props.GetChildren())
            {
                _props.RemoveChild(child);
                child.QueueFree();
            }
        }

        private void AddBadge(string text, Color color)
            => _badges.AddChild(ActionCharacterUi.MakeBadge(text, color));

        private void SetNote(string note)
        {
            var hasNote = !string.IsNullOrEmpty(note);
            _note.Visible = hasNote;
            _noteLabel.Text = note ?? string.Empty;
        }

        private static string Translate(string key)
            => TranslationServer.Translate(key).ToString();
    }
}
