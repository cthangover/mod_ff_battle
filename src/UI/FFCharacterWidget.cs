using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Battle;
using Cthangover.Core.Cards.StatusEffect;
using Cthangover.Core.Characters;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Theming;
using Godot;

using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Mods.Interface.Theming;
namespace Cthangover.FFBattle.UI
{
    /// <summary>
    /// Battle card of a single combatant, shared by the party and enemy panels.
    /// The node tree lives in <c>scenes/ff_character_widget.tscn</c>; this class
    /// only fetches the nodes, dresses them with <see cref="UiTokens"/> styles and
    /// refreshes their content. Visually the card follows the unified style guide:
    /// a team accent line on top, a sprite well, status badges in the upper right
    /// and an info block holding name, HP numbers, the layered health bar
    /// (<see cref="UiKit.StyleShieldedHealthBar"/>: track → health fill → blue
    /// defence segment drawn past the health edge while
    /// <see cref="CharacterAttributes.Defence"/> absorbs damage), action point pips
    /// (party) or attack caption (enemies) and the defence shield chip.
    /// <para/>
    /// The public surface is animation-facing, not layout-facing:
    /// <see cref="Highlight"/> paints the selection frame, <see cref="Flash"/> and
    /// <see cref="Shake"/> give impact feedback, <see cref="PlayDeathAnimation"/>
    /// dissolves the card. Battle code never touches labels directly — it mutates
    /// the <see cref="Character"/> and calls <see cref="UpdateInfo"/>, which
    /// re-reads health, defence, points and stun in one place. Panels must assign
    /// <see cref="IsPlayer"/> before <see cref="Init"/>, because the team colour is
    /// applied during initialisation.
    /// </summary>
    public partial class FFCharacterWidget : ModWidget
    {
        /// <summary>Diameter of one action point pip.</summary>
        private const int PipSize = 9;

        private TextureRect _art;
        private ColorRect _teamLine;
        private Panel _selection;
        private Panel _teamDot;
        private Label _nameLabel;
        private Label _hpNumLabel;
        private ProgressBar _shieldBar;
        private ProgressBar _hpBar;
        private HBoxContainer _pipsBox;
        private Label _atkLabel;
        private PanelContainer _shieldChip;
        private Label _shieldValue;
        private VBoxContainer _statusBox;
        private PanelContainer _stunBadge;
        private TextureRect _stunIcon;
        private Label _stunLabel;
        private Tween _animTween;
        private VBoxContainer _gaugesBox;
        private bool _gaugesSubscribed;
        private static Shader _dissolveShader;

        /// <summary>Whether this widget belongs to the player party (affects highlight/targeting logic).</summary>
        public bool IsPlayer { get; set; }
        /// <summary>Whether the character is dead; set by <see cref="PlayDeathAnimation"/>.</summary>
        public bool IsDead { get; set; }
        /// <summary>The character data model backing this widget: stats, actions, status effects.</summary>
        public Character Card { get; set; }
        /// <summary>Original scale captured at initialisation; used to restore after death animation.</summary>
        public Vector2 BaseScale { get; private set; }
        /// <summary>Exposes this widget as a <see cref="Godot.Control"/> for layout calculations.</summary>
        public Control ControlNode => this;

        /// <summary>Builds the character widget: portrait frame, HP bar and status chips.</summary>
        protected override void Construct()
        {
            CthangoverTheme.Apply(this);

            _teamLine = GetNode<ColorRect>("TeamLine");
            _art = GetNode<TextureRect>("Art");
            _nameLabel = GetNode<Label>("Info/Column/NameRow/NameLabel");
            _teamDot = GetNode<Panel>("Info/Column/NameRow/TeamDot");
            _hpNumLabel = GetNode<Label>("Info/Column/NameRow/HpNumLabel");
            _shieldBar = GetNode<ProgressBar>("Info/Column/HpBarStack/ShieldBar");
            _hpBar = GetNode<ProgressBar>("Info/Column/HpBarStack/HpBar");
            _pipsBox = GetNode<HBoxContainer>("Info/Column/Foot/PipsBox");
            _atkLabel = GetNode<Label>("Info/Column/Foot/AtkLabel");
            _shieldChip = GetNode<PanelContainer>("Info/Column/Foot/ShieldChip");
            _shieldValue = GetNode<Label>("Info/Column/Foot/ShieldChip/ShieldRow/ShieldValue");
            _statusBox = GetNode<VBoxContainer>("StatusBox");
            _stunBadge = GetNode<PanelContainer>("StatusBox/StunBadge");
            _stunIcon = GetNode<TextureRect>("StatusBox/StunBadge/StunRow/StunIcon");
            _stunLabel = GetNode<Label>("StatusBox/StunBadge/StunRow/StunLabel");
            _selection = GetNode<Panel>("Selection");

            // Extra bars of other mods (mana of battle_formulas…) go under the health bar.
            var column = GetNode<VBoxContainer>("Info/Column");
            _gaugesBox = new VBoxContainer { Name = "Gauges", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
            _gaugesBox.AddThemeConstantOverride("separation", 1);
            column.AddChild(_gaugesBox);
            column.MoveChild(_gaugesBox, GetNode<Control>("Info/Column/HpBarStack").GetIndex() + 1);

            ApplyStyles();
        }

        /// <summary>
        /// Binds the widget to a <see cref="Character"/>: sprite, localised name,
        /// team colour and per-side info layout. The team colour is derived from
        /// <see cref="IsPlayer"/>, which is why callers set it before calling here.
        /// </summary>
        public void Init(Character character)
        {
            Card = character;
            BaseScale = Scale;
            IsDead = false;

            if (_art != null && character?.Image != null)
                _art.Texture = character.Image;

            if (_nameLabel != null)
                _nameLabel.Text = TranslationServer.Translate(character?.Name ?? string.Empty).ToString();

            var teamColor = IsPlayer ? UiTokens.Info : UiTokens.Danger;
            if (_teamLine != null)
                _teamLine.Color = UiTokens.Alpha(teamColor, 0.55f);
            if (_teamDot != null)
                _teamDot.AddThemeStyleboxOverride("panel", UiKit.Frame(4, UiTokens.Alpha(teamColor, 0.9f)));
            if (_pipsBox != null)
                _pipsBox.Visible = IsPlayer;
            if (_atkLabel != null)
                _atkLabel.Visible = !IsPlayer;

            if (!_gaugesSubscribed)
            {
                _gaugesSubscribed = true;
                BattleUnitGauges.Changed += OnGaugesChanged;
                TreeExiting += () => BattleUnitGauges.Changed -= OnGaugesChanged;
            }

            UpdateInfo();
        }

        /// <summary>
        /// Single refresh entry point for all mutable character state: layered
        /// HP/defence bar and numbers with threshold colours, action point pips,
        /// shield chip, attack caption and the stun badge with its remaining
        /// turns. Called by battle code and by animations after each resolved
        /// action.
        /// </summary>
        public void UpdateInfo()
        {
            // A lethal hit frees the widget through the death animation while
            // the attack animation and Health.OnChange subscribers still hold it.
            if (!IsInstanceValid(this) || IsQueuedForDeletion() || Card?.Attributes == null)
                return;

            var health = Card.Attributes.Health;
            var percent = Mathf.Clamp(health.Percent, 0f, 1f);
            var defence = Card.Attributes.Defence?.Value ?? 0;
            var fillColor = percent > 0.5f ? UiTokens.HpOk : percent > 0.25f ? UiTokens.Warning : UiTokens.HpLow;
            var textColor = percent > 0.5f ? UiTokens.HpText : percent > 0.25f ? UiTokens.Warning : UiTokens.DangerText;

            if (_hpBar != null)
            {
                _hpBar.Value = percent * 100f;
                _hpBar.AddThemeStyleboxOverride("fill", UiKit.Frame(UiTokens.RadiusSm, fillColor));
            }

            // Blue defence segment on top of the health fill; collapses onto
            // the health edge when the shield is gone.
            UiKit.UpdateShieldedHealthBar(_shieldBar, percent, defence, health.BaseValue);

            if (_hpNumLabel != null)
            {
                _hpNumLabel.Text = $"{health.Value}/{health.BaseValue}";
                _hpNumLabel.AddThemeColorOverride("font_color", textColor);
            }

            if (_pipsBox != null && IsPlayer)
                RefreshPips();

            if (_atkLabel != null && !IsPlayer)
                _atkLabel.Text = string.Format("{0} {1}", Translate("ui/character_panel/stat_atk"), Card.Attributes.Attack.Value);

            if (_shieldChip != null)
            {
                _shieldChip.Visible = defence > 0;
                if (_shieldValue != null)
                    _shieldValue.Text = defence.ToString();
            }

            RefreshStun();
            RefreshGauges();
        }

        private void OnGaugesChanged(string unitId)
        {
            if (unitId == null || unitId == Card?.ID)
                RefreshGauges();
        }

        /// <summary>
        /// Draws the <see cref="BattleUnitGauges"/> of a party member, one thin bar with its label
        /// and numbers per gauge; rows are reused by position.
        /// </summary>
        private void RefreshGauges()
        {
            if (_gaugesBox == null || !IsInstanceValid(this) || IsQueuedForDeletion())
                return;
            var gauges = IsPlayer && Card != null ? BattleUnitGauges.Read(Card) : new List<BattleGauge>();
            _gaugesBox.Visible = gauges.Count > 0;
            while (_gaugesBox.GetChildCount() > gauges.Count)
            {
                var extra = _gaugesBox.GetChild(_gaugesBox.GetChildCount() - 1);
                _gaugesBox.RemoveChild(extra);
                extra.QueueFree();
            }
            for (var i = 0; i < gauges.Count; i++)
            {
                var row = i < _gaugesBox.GetChildCount() ? (HBoxContainer)_gaugesBox.GetChild(i) : CreateGaugeRow();
                var gauge = gauges[i];
                row.GetNode<Label>("Key").Text = TranslationServer.Translate(gauge.Label ?? gauge.Id ?? string.Empty).ToString();
                var bar = row.GetNode<ProgressBar>("Bar");
                bar.Value = Mathf.Clamp(gauge.Current / (float)gauge.Max, 0f, 1f) * 100f;
                var color = string.IsNullOrWhiteSpace(gauge.Color) ? UiTokens.Info : Color.FromString(gauge.Color, UiTokens.Info);
                bar.AddThemeStyleboxOverride("fill", UiKit.Frame(UiTokens.RadiusSm, color));
                row.GetNode<Label>("Value").Text = $"{gauge.Current}/{gauge.Max}";
            }
        }

        private HBoxContainer CreateGaugeRow()
        {
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 4);
            var key = new Label { Name = "Key", MouseFilter = MouseFilterEnum.Ignore };
            key.AddThemeFontSizeOverride("font_size", UiTokens.FontTiny);
            key.AddThemeColorOverride("font_color", UiTokens.Muted);
            var bar = new ProgressBar
            {
                Name = "Bar",
                ShowPercentage = false,
                MaxValue = 100,
                CustomMinimumSize = new Vector2(0, 5),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            bar.AddThemeStyleboxOverride("background", UiKit.Frame(UiTokens.RadiusSm, UiTokens.StatBg));
            var value = new Label { Name = "Value", MouseFilter = MouseFilterEnum.Ignore };
            value.AddThemeFontSizeOverride("font_size", UiTokens.FontTiny);
            value.AddThemeColorOverride("font_color", UiTokens.Info);
            row.AddChild(key);
            row.AddChild(bar);
            row.AddChild(value);
            _gaugesBox.AddChild(row);
            return row;
        }

        /// <summary>Shows a coloured selection frame over the card with a 0.15s fade.</summary>
        public void Highlight(Color color)
        {
            if (_selection == null)
                return;

            _animTween?.Kill();
            _animTween = CreateTween();
            _animTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
            _animTween.TweenProperty(_selection, "modulate", color, 0.15f);
        }

        /// <summary>Fades the selection frame out via a 0.15s tween.</summary>
        public void ClearHighlight()
        {
            if (_selection == null)
                return;

            _animTween?.Kill();
            _animTween = CreateTween();
            _animTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
            _animTween.TweenProperty(_selection, "modulate", UiTokens.Alpha(UiTokens.White, 0f), 0.15f);
        }

        /// <summary>Rapidly oscillates the widget with random offsets to simulate impact feedback.</summary>
        public void Shake(float intensity, float duration)
        {
            var originalPos = Position;
            _animTween?.Kill();
            _animTween = CreateTween();

            for (int i = 0; i < 6; i++)
            {
                var offset = new Vector2(
                    (float)GD.RandRange(-intensity, intensity),
                    (float)GD.RandRange(-intensity, intensity)
                );
                _animTween.TweenProperty(this, "position", originalPos + offset, duration / 12f);
                _animTween.TweenProperty(this, "position", originalPos, duration / 12f);
            }
        }

        /// <summary>Briefly tints the whole card with a colour pulse (30% rise, 70% fall).</summary>
        public void Flash(Color color, float duration)
        {
            var originalModulate = Modulate;
            _animTween?.Kill();
            _animTween = CreateTween();
            _animTween.TweenProperty(this, "modulate", color, duration * 0.3f);
            _animTween.TweenProperty(this, "modulate", originalModulate, duration * 0.7f);
        }

        /// <summary>
        /// Plays a dissolve-and-shrink death sequence. The sprite gets the
        /// <c>scene_transition</c> shader with a growing dissolve progress while the
        /// card fades and squashes; the info block fades with it. Invokes
        /// <paramref name="onComplete"/> when finished so the parent panel can
        /// remove the widget and re-grid the survivors.
        /// </summary>
        public void PlayDeathAnimation(Action onComplete)
        {
            if (IsDead)
            {
                onComplete?.Invoke();
                return;
            }

            IsDead = true;
            MouseFilter = MouseFilterEnum.Ignore;
            ClearHighlight();

            _animTween?.Kill();
            var duration = 0.7f;

            if (_dissolveShader == null)
                _dissolveShader = Shaders.Resolve("scene_transition");

            if (_dissolveShader != null && _art?.Texture != null)
            {
                var material = new ShaderMaterial { Shader = _dissolveShader };
                material.SetShaderParameter("noise_scale", 5f);
                material.SetShaderParameter("smoothness", 0.25f);
                material.SetShaderParameter("distortion", 0.092f);
                material.SetShaderParameter("glow_intensity", 0f);
                material.SetShaderParameter("hue_shift", 0f);
                material.SetShaderParameter("invert_direction", false);
                material.SetShaderParameter("progress", 0f);
                _art.Material = material;

                CreateTween().TweenMethod(
                    Callable.From<float>(t => material.SetShaderParameter("progress", t)),
                    0f, 1f, duration);
            }

            var tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(this, "modulate:a", 0f, duration * 0.5f);
            tween.TweenProperty(this, "scale", BaseScale * new Vector2(0.1f, 1.2f), duration);
            tween.TweenProperty(_shieldBar, "modulate:a", 0f, duration);
            tween.TweenProperty(_hpBar, "modulate:a", 0f, duration);
            tween.TweenProperty(_hpNumLabel, "modulate:a", 0f, duration);
            tween.TweenProperty(_nameLabel, "modulate:a", 0f, duration);
            tween.TweenProperty(_pipsBox, "modulate:a", 0f, duration);
            tween.TweenProperty(_atkLabel, "modulate:a", 0f, duration);
            tween.TweenProperty(_shieldChip, "modulate:a", 0f, duration);
            tween.TweenProperty(_statusBox, "modulate:a", 0f, duration);

            tween.Chain().TweenProperty(this, "scale", Vector2.Zero, 0.15f);

            tween.Finished += () =>
            {
                onComplete?.Invoke();
            };
        }

        private void ApplyStyles()
        {
            _teamLine.Color = UiTokens.Alpha(UiTokens.Danger, 0.55f);

            var infoStyle = UiKit.Frame(UiTokens.RadiusMd, UiTokens.Alpha(UiTokens.Surface2, 0.93f), UiTokens.Line);
            infoStyle.CornerRadiusTopLeft = 0;
            infoStyle.CornerRadiusTopRight = 0;
            infoStyle.ContentMarginLeft = 8;
            infoStyle.ContentMarginRight = 8;
            infoStyle.ContentMarginTop = 6;
            infoStyle.ContentMarginBottom = 8;
            GetNode<PanelContainer>("Info").AddThemeStyleboxOverride("panel", infoStyle);

            _nameLabel.AddThemeColorOverride("font_color", UiTokens.InkBright);
            _hpNumLabel.AddThemeColorOverride("font_color", UiTokens.HpText);
            _atkLabel.AddThemeColorOverride("font_color", UiTokens.Muted);
            _teamDot.AddThemeStyleboxOverride("panel", UiKit.Frame(4, UiTokens.Danger));

            var hpFill = UiKit.Frame(UiTokens.RadiusSm, UiTokens.HpOk);
            _hpBar.AddThemeStyleboxOverride("fill", hpFill);
            // Layered pie: the shield bar owns the track and the blue defence
            // fill; the health bar paints only its fill on top of it.
            UiKit.StyleShieldedHealthBar(_shieldBar, _hpBar);

            var shieldStyle = UiKit.Frame(UiTokens.RadiusXs, UiTokens.StatBg, UiTokens.StatLine);
            shieldStyle.ContentMarginLeft = 7;
            shieldStyle.ContentMarginRight = 7;
            shieldStyle.ContentMarginTop = 2;
            shieldStyle.ContentMarginBottom = 2;
            _shieldChip.AddThemeStyleboxOverride("panel", shieldStyle);
            GetNode<Label>("Info/Column/Foot/ShieldChip/ShieldRow/ShieldKey").AddThemeColorOverride("font_color", UiTokens.Muted);
            _shieldValue.AddThemeColorOverride("font_color", UiTokens.Info);

            var stunStyle = UiKit.Frame(UiTokens.RadiusXs, UiTokens.CooldownSoft, UiTokens.CooldownLine);
            stunStyle.ContentMarginLeft = 5;
            stunStyle.ContentMarginRight = 8;
            stunStyle.ContentMarginTop = 3;
            stunStyle.ContentMarginBottom = 3;
            _stunBadge.AddThemeStyleboxOverride("panel", stunStyle);
            _stunLabel.AddThemeColorOverride("font_color", UiTokens.Cooldown);

            var selectionStyle = UiKit.Frame(UiTokens.RadiusMd, new Color(1f, 1f, 1f, 0.07f), Colors.White, 2);
            _selection.AddThemeStyleboxOverride("panel", selectionStyle);
            _selection.Modulate = UiTokens.Alpha(UiTokens.White, 0f);
        }

        private void RefreshPips()
        {
            foreach (var child in _pipsBox.GetChildren())
            {
                _pipsBox.RemoveChild(child);
                child.QueueFree();
            }

            var basePoints = Mathf.Max(0, Card.Attributes.Point.BaseValue);
            var points = Card.Attributes.Point.Value;
            for (var i = 0; i < basePoints; i++)
            {
                var on = i < points;
                var pip = new Panel
                {
                    MouseFilter = MouseFilterEnum.Ignore,
                    CustomMinimumSize = new Vector2(PipSize, PipSize),
                };
                pip.AddThemeStyleboxOverride("panel", UiKit.Frame(
                    PipSize / 2,
                    on ? UiTokens.AccentBright : UiTokens.LineDisabled,
                    on ? UiTokens.Accent : UiTokens.LineFaint));
                _pipsBox.AddChild(pip);
            }
        }

        private void RefreshStun()
        {
            if (_stunBadge == null)
                return;

            var stun = StatusEffectService.Of(Card)?.ActiveStatusEffects?
                .FirstOrDefault(effect => effect.EffectType == StatusEffectType.Stun);

            if (stun == null)
            {
                _stunBadge.Visible = false;
                return;
            }

            _stunBadge.Visible = true;
            if (_stunIcon != null)
                _stunIcon.Texture = stun.Icon;
            if (_stunLabel != null)
                _stunLabel.Text = string.Format(Translate("ff_battle/status_stun"), stun.RemainingTurns);
        }

        private static string Translate(string key)
            => TranslationServer.Translate(key).ToString();
    }
}
