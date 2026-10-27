using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cthangover.Core.Battle;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Cards.StatusEffect;
using Cthangover.Core.Characters;
using Cthangover.Core.Items;
using Cthangover.Core.Settings;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI;
using Cthangover.Core.UI.CharacterPanel;
using Cthangover.Core.Utils;
using Cthangover.FFBattle.UI;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.FFBattle
{
    /// <summary>
    /// Core orchestration class for the Final Fantasy-style turn-based battle system.
    /// Manages the full battle lifecycle: player turn with menu-driven action selection,
    /// enemy AI turn execution, target selection mode, escape mechanics, and win/loss
    /// condition checks. Coordinates between <see cref="FFPlayerPanel"/>,
    /// <see cref="FFEnemyPanel"/>, <see cref="FFMenuPanel"/>, and
    /// <see cref="FFBattleController"/> for UI interaction. Registered via
    /// <see cref="IBattleCore.Id"/> as <c>"ff_battle"</c>.
    /// </summary>
    public class FFBattleCore : IBattleCore, IBattleAutoPlayable
    {
        /// <summary>Unique identifier for this battle system implementation, used by the core engine to locate it.</summary>
        public string Id => "ff_battle";
        /// <summary>Provides the set of action executors (damage, defence, stun, item) registered for this battle system.</summary>
        public IActionExecutorProvider ActionProvider { get; } = new Actions.FFActionProvider();

        private Character[] _playerChars;
        private Character[] _enemyChars;
        private IBattleContext _ctx;

        private FFPlayerPanel _playerPanel;
        private FFEnemyPanel _enemyPanel;
        private FFMenuPanel _menuPanel;
        private FFActionDetailPanel _detailPanel;
        private FFBattleHud _hud;
        private FFBattleLogPanel _logPanel;
        private FFBattleBanner _banner;
        private FFBattleTargetHint _targetHint;
        private FFBattleController _controller;
        private Control _toolPanel;

        private Button _endTurnButton;

        private FFCharacterWidget _currentCharacter;
        private FFMenuEntry _currentMenuAction;
        private bool _isPlayerTurn;
        private bool _targetSelectMode;
        private bool _targetAlly;
        private System.Action<FFCharacterWidget> _onTargetSelected;
        private int _round;
        private int _detailKind;

        private const int DetailMain = 0;
        private const int DetailActions = 1;
        private const int DetailItems = 2;

        private const float PLAYER_SCALE = 0.85f;

        /// <summary>
        /// Initialises the battle UI panels, controller, and event wiring. Copies enemy
        /// <see cref="Character"/> instances via <c>Copy()</c> so mutations during battle
        /// do not affect the original data. Registers the <see cref="ActionProvider"/>
        /// with <see cref="ActionExecutorHub"/> and constructs all UI widgets inside
        /// <paramref name="ctx"/>'s root node.
        /// </summary>
        /// <param name="playerChars">The player party characters, consumed directly (not copied).</param>
        /// <param name="enemyChars">Enemy characters — each is shallow-copied for battle isolation.</param>
        /// <param name="ctx">Battle context providing root node and end-battle callback.</param>
        public void Init(Character[] playerChars, Character[] enemyChars, IBattleContext ctx)
        {
            _playerChars = playerChars;
            _ctx = ctx;

            _enemyChars = new Character[enemyChars.Length];
            for (int i = 0; i < enemyChars.Length; i++)
                _enemyChars[i] = enemyChars[i]?.Copy() ?? enemyChars[i];

            ActionExecutorHub.Instance.SetActiveProvider(ActionProvider);

            var root = ctx.RootNode as Node;
            var panel = root.GetNodeOrNull<Control>("Panel") ?? root;
            _toolPanel = root.GetNodeOrNull<Control>("ToolPanel");

            _playerPanel = new FFPlayerPanel { Name = "FFPlayerPanel" };
            _playerPanel.EnsureConstructed();
            panel.AddChild(_playerPanel);

            _enemyPanel = new FFEnemyPanel { Name = "FFEnemyPanel" };
            _enemyPanel.EnsureConstructed();
            panel.AddChild(_enemyPanel);

            _menuPanel = (FFMenuPanel)TscnScenes.LoadAndBuild("scenes/ff_menu_panel.tscn");
            _menuPanel.Name = "FFMenuPanel";
            _menuPanel.EnsureConstructed();
            _menuPanel.HideMenu();
            panel.AddChild(_menuPanel);

            _detailPanel = (FFActionDetailPanel)TscnScenes.LoadAndBuild("scenes/ff_action_detail_panel.tscn");
            _detailPanel.Name = "FFActionDetailPanel";
            _detailPanel.EnsureConstructed();
            _detailPanel.HidePassport();
            panel.AddChild(_detailPanel);

            _hud = (FFBattleHud)TscnScenes.LoadAndBuild("scenes/ff_battle_hud.tscn");
            _hud.Name = "FFBattleHud";
            _hud.EnsureConstructed();
            panel.AddChild(_hud);

            _logPanel = (FFBattleLogPanel)TscnScenes.LoadAndBuild("scenes/ff_battle_log_panel.tscn");
            _logPanel.Name = "FFBattleLogPanel";
            _logPanel.EnsureConstructed();
            panel.AddChild(_logPanel);

            _banner = (FFBattleBanner)TscnScenes.LoadAndBuild("scenes/ff_battle_banner.tscn");
            _banner.Name = "FFBattleBanner";
            _banner.EnsureConstructed();
            panel.AddChild(_banner);

            _targetHint = (FFBattleTargetHint)TscnScenes.LoadAndBuild("scenes/ff_battle_target_hint.tscn");
            _targetHint.Name = "FFBattleTargetHint";
            _targetHint.EnsureConstructed();
            _targetHint.HideHint();
            panel.AddChild(_targetHint);

            _endTurnButton = new Button();
            _endTurnButton.Text = TranslationServer.Translate("ff_battle/end_turn");
            _endTurnButton.Pressed += OnEndTurnPressed;
            _endTurnButton.Visible = false;
            _endTurnButton.FocusMode = Control.FocusModeEnum.None;
            _toolPanel?.AddChild(_endTurnButton);

            _controller = new FFBattleController { Name = "FFBattleController" };
            panel.AddChild(_controller);
            _controller.PlayerPanel = _playerPanel;
            _controller.EnemyPanel = _enemyPanel;
            _controller.MenuPanel = _menuPanel;
            _controller.SubMenuPanel = _menuPanel;
            _controller.OnCharacterSelected += OnCharacterSelected;
            _controller.OnMenuCancel += OnMenuCancel;
            _controller.OnTargetSelected += OnTargetSelected;

            _playerPanel.OnWidgetClicked += OnWidgetClicked;
            _enemyPanel.OnEnemyClicked += OnEnemyClicked;
            _enemyPanel.OnEnemyDead += OnEnemyDied;
            _enemyPanel.OnEnemyDefeated += OnEnemyDefeated;
            _menuPanel.OnItemSelected += OnMenuItemSelected;
            _menuPanel.OnSelectionChanged += OnMenuSelectionChanged;
            _menuPanel.OnCancelled += OnMenuCancel;
        }

        /// <summary>
        /// Begins the battle sequence. Calculates layout positions and scales for
        /// player/enemy panels based on viewport size. Subscribes to
        /// <see cref="BattleSceneContext.OnBattleCleared"/> for cleanup.
        /// Immediately transitions into the first player turn.
        /// </summary>
        public void Start()
        {
            var root = _ctx.RootNode as Node;
            var viewportSize = root.GetViewport().GetVisibleRect().Size;

            var enemyScale = FFEnemyPanel.CalculateScale(_enemyChars.Length,
                new Vector2(viewportSize.X * 0.9f, viewportSize.Y * 0.5f));

            _enemyPanel.Position = new Vector2(viewportSize.X * 0.05f, 0);
            _enemyPanel.Size = new Vector2(viewportSize.X * 0.9f, viewportSize.Y * 0.5f);

            _playerPanel.Position = new Vector2(0, viewportSize.Y * 0.55f);
            _playerPanel.Size = new Vector2(viewportSize.X * 0.65f, viewportSize.Y * 0.45f);

            _menuPanel.Position = new Vector2(viewportSize.X * 0.66f, viewportSize.Y * 0.58f);
            _menuPanel.Size = new Vector2(viewportSize.X * 0.3f, 160f);

            _detailPanel.Position = new Vector2(viewportSize.X * 0.66f - 320f, viewportSize.Y * 0.58f);
            _detailPanel.Size = new Vector2(300f, 190f);

            _hud.Position = new Vector2(26f, 20f);

            _logPanel.Position = new Vector2(viewportSize.X - 466f, 64f);
            _logPanel.Size = new Vector2(440f, 440f);

            _banner.Position = new Vector2(0f, viewportSize.Y * 0.43f);
            _banner.Size = new Vector2(viewportSize.X, 70f);

            _targetHint.Position = new Vector2(0f, viewportSize.Y * 0.48f);
            _targetHint.Size = new Vector2(viewportSize.X, 40f);

            if (_endTurnButton != null)
                _endTurnButton.Position = new Vector2(viewportSize.X - 180f, viewportSize.Y * 0.92f);

            _playerPanel.Init(_playerChars, PLAYER_SCALE);
            _enemyPanel.Init(_enemyChars, enemyScale);

            BattleSceneContext.Instance.OnBattleCleared += OnBattleCleared;

            _isPlayerTurn = true;
            _round = 0;
            UpdateHud();
            _logPanel.AddEntry(Translate("ff_battle/log_battle_start"), FFBattleLogKind.Turn);
            StartPlayerTurn();
        }

        private void StartPlayerTurn()
        {
            GameLogger.Log("FF_BATTLE", "Player turn started", LogLevel.Debug);

            _isPlayerTurn = true;
            BattleSceneContext.Instance.IsWait = false;

            _currentCharacter = null;
            _targetSelectMode = false;
            _menuPanel.HideMenu();
            _detailPanel.HidePassport();
            _targetHint.HideHint();
            BattleSceneContext.Instance.ActiveCharacter = null;

            foreach (var widget in _playerPanel.Widgets)
            {
                if (!widget.IsDead)
                {
                    StatusEffectService.Of(widget.Card).OnTurnStart();
                    widget.Card.Attributes.Point.Value = widget.Card.Attributes.Point.BaseValue;
                }
                widget.UpdateInfo();
            }

            _endTurnButton.Visible = true;
            _controller.State = FFBattleState.Idle;

            foreach (var widget in _enemyPanel.Widgets)
                widget.ClearHighlight();

            foreach (var widget in _playerPanel.Widgets)
                widget.ClearHighlight();

            _round += 1;
            UpdateHud();
            _hud.SetPhase(true);
            _banner.Show(Translate("ff_battle/banner_player"));
            _logPanel.SetRound(_round);
            _logPanel.AddEntry(string.Format(Translate("ff_battle/log_turn_player"), _round), FFBattleLogKind.Turn);
        }

        private void OnWidgetClicked(FFCharacterWidget widget)
        {
            if (!_isPlayerTurn)
                return;

            if (_targetSelectMode)
            {
                if (widget.IsDead)
                    return;

                if (_targetAlly && !_playerPanel.Widgets.Contains(widget))
                    return;

                if (!_targetAlly && _playerPanel.Widgets.Contains(widget))
                    return;

                _targetSelectMode = false;
                foreach (var w in _enemyPanel.Widgets) w.ClearHighlight();
                foreach (var w in _playerPanel.Widgets) w.ClearHighlight();
                _targetHint.HideHint();

                ExecuteMenuAction(widget);
                return;
            }

            _controller.SelectCharacter(widget);
        }

        private void OnCharacterSelected(FFCharacterWidget widget)
        {
            if (widget.IsDead || widget.Card?.Attributes?.Point?.Value <= 0)
                return;

            _currentCharacter = widget;
            BattleSceneContext.Instance.ActiveCharacter = widget.Card;
            ShowMainMenu();
        }

        private void ShowMainMenu()
        {
            var entries = new List<FFMenuEntry>
            {
                new FFMenuEntry
                {
                    Key = "attack",
                    Label = TranslationServer.Translate("ff_battle/menu_attack"),
                    Enabled = HasAvailableActions()
                },
                new FFMenuEntry
                {
                    Key = "items",
                    Label = TranslationServer.Translate("ff_battle/menu_items"),
                    Enabled = HasUsableItems()
                },
                new FFMenuEntry
                {
                    Key = "defend",
                    Label = TranslationServer.Translate("ff_battle/menu_defend"),
                    Enabled = HasDefendAction()
                },
                new FFMenuEntry
                {
                    Key = "escape",
                    Label = TranslationServer.Translate("ff_battle/menu_escape"),
                    Enabled = true
                }
            };

            _currentMenuAction = null;
            _detailKind = DetailMain;
            _menuPanel.ShowMenu(entries, string.Format(Translate("ff_battle/menu_caption"),
                ActorName(), _currentCharacter.Card.Attributes.Point.Value, _currentCharacter.Card.Attributes.Point.BaseValue));
            _controller.State = FFBattleState.MenuOpen;

            foreach (var widget in _playerPanel.Widgets)
                widget.ClearHighlight();
            _currentCharacter?.Highlight(UiTokens.Accent);
        }

        private void OnMenuItemSelected(int index)
        {
            if (_menuPanel.Entries == null || index >= _menuPanel.Entries.Count)
                return;

            var entry = _menuPanel.Entries[index];
            if (!entry.Enabled)
                return;

            MatchMenuAction(entry);
        }

        private void MatchMenuAction(FFMenuEntry entry)
        {
            switch (entry.Key)
            {
                case "attack":
                    ShowActionSubMenu();
                    break;

                case "items":
                    ShowItemSubMenu();
                    break;

                case "defend":
                    DoDefendAction();
                    break;

                case "escape":
                    DoEscapeAction();
                    break;

                case "action":
                    _currentMenuAction = entry;
                    var action = entry.Data as ActionCharacter;
                    if (action != null)
                    {
                        if (action.Type == ActionCharacterType.ToEnemy)
                            StartTargetSelect(false);
                        else if (action.Type == ActionCharacterType.ToAlias)
                            StartTargetSelect(true);
                        else if (action.Type == ActionCharacterType.ToSelf)
                        {
                            _targetSelectMode = false;
                            ExecuteMenuAction(_currentCharacter);
                        }
                    }
                    break;

                case "item":
                    _currentMenuAction = entry;
                    var item = entry.Data as IItem;
                    if (item != null)
                    {
                        if (item.ItemType.HasFlag(ItemType.TargetUsed))
                            StartTargetSelect(true);
                        else
                            DoItemAction(item);
                    }
                    break;

                case "back":
                    ShowMainMenu();
                    break;

                case "_empty":
                    break;
            }
        }

        private void ShowActionSubMenu()
        {
            if (_currentCharacter?.Card?.Actions == null)
                return;

            var entries = new List<FFMenuEntry>();
            foreach (var action in _currentCharacter.Card.Actions)
            {
                if (!action.Type.UseInBattle())
                    continue;
                var cost = action.GetInt("RequiredPoint", 1);
                var canUse = _currentCharacter.Card.Attributes.Point.Value >= cost;

                entries.Add(new FFMenuEntry
                {
                    Key = "action",
                    Label = $"{TranslationServer.Translate(action.Name)}  ({cost}P)",
                    Enabled = canUse,
                    Data = action
                });
            }

            if (entries.Count == 0)
            {
                entries.Add(new FFMenuEntry
                {
                    Key = "_empty",
                    Label = TranslationServer.Translate("ff_battle/no_actions"),
                    Enabled = false
                });
            }

            entries.Add(new FFMenuEntry
            {
                Key = "back",
                Label = TranslationServer.Translate("ff_battle/back"),
                Enabled = true
            });

            _detailKind = DetailActions;
            _menuPanel.ShowMenu(entries, ActorName() + " · " + Translate("ff_battle/menu_actions"));
            _controller.State = FFBattleState.SubMenuOpen;
        }

        private void ShowItemSubMenu()
        {
            var inventory = GameData.Instance.Runtime.Inventory;
            var entries = new List<FFMenuEntry>();

            foreach (var container in inventory.Items)
            {
                var item = container.Item;
                if (item.ItemType.HasFlag(ItemType.Used) && container.Count > 0)
                {
                    entries.Add(new FFMenuEntry
                    {
                        Key = "item",
                        Label = $"{TranslationServer.Translate(item.Name)}  x{container.Count}",
                        Enabled = _currentCharacter.Card.Attributes.Point.Value >= 1,
                        Data = item
                    });
                }
            }

            if (entries.Count == 0)
            {
                entries.Add(new FFMenuEntry
                {
                    Key = "_empty",
                    Label = TranslationServer.Translate("ff_battle/no_items"),
                    Enabled = false
                });
            }

            entries.Add(new FFMenuEntry
            {
                Key = "back",
                Label = TranslationServer.Translate("ff_battle/back"),
                Enabled = true
            });

            _detailKind = DetailItems;
            _menuPanel.ShowMenu(entries, ActorName() + " · " + Translate("ff_battle/menu_items"));
            _controller.State = FFBattleState.SubMenuOpen;
        }

        private void StartTargetSelect(bool targetAlly)
        {
            _targetSelectMode = true;
            _targetAlly = targetAlly;
            _menuPanel.HideMenu();
            _controller.State = FFBattleState.TargetSelect;
            _targetHint.ShowHint(Translate(targetAlly ? "ff_battle/hint_ally" : "ff_battle/hint_target"));

            foreach (var widget in _playerPanel.Widgets)
                widget.ClearHighlight();
            foreach (var widget in _enemyPanel.Widgets)
                widget.ClearHighlight();

            if (targetAlly)
            {
                foreach (var widget in _playerPanel.Widgets)
                    if (!widget.IsDead)
                        widget.Highlight(UiTokens.Success);
            }
            else
            {
                foreach (var widget in _enemyPanel.Widgets)
                    if (!widget.IsDead)
                        widget.Highlight(UiTokens.Danger);
            }

            _onTargetSelected = null;
        }

        private void OnEnemyClicked(FFCharacterWidget widget)
        {
            if (!_isPlayerTurn || !_targetSelectMode || widget.IsDead)
                return;

            if (_targetAlly)
                return;

            _targetSelectMode = false;
            foreach (var w in _enemyPanel.Widgets) w.ClearHighlight();
            foreach (var w in _playerPanel.Widgets) w.ClearHighlight();
            _targetHint.HideHint();

            ExecuteMenuAction(widget);
        }

        private void OnTargetSelected(FFCharacterWidget widget)
        {
            if (!_isPlayerTurn || !_targetSelectMode || widget.IsDead)
                return;

            if (_targetAlly && _enemyPanel.Widgets.Contains(widget))
                return;

            if (!_targetAlly && _playerPanel.Widgets.Contains(widget))
                return;

            _targetSelectMode = false;
            foreach (var w in _enemyPanel.Widgets) w.ClearHighlight();
            foreach (var w in _playerPanel.Widgets) w.ClearHighlight();
            _targetHint.HideHint();

            ExecuteMenuAction(widget);
        }

        private async void ExecuteMenuAction(FFCharacterWidget target)
        {
            if (_currentMenuAction == null)
                return;

            _controller.State = FFBattleState.AnimationPlaying;
            _endTurnButton.Visible = false;
            _detailPanel.HidePassport();
            _targetHint.HideHint();

            var source = _currentCharacter;
            var actionName = string.Empty;
            var hpBefore = target.Card.Attributes.Health.Value;
            var defBefore = target.Card.Attributes.Defence.Value;
            var stunBefore = CountStunTurns(target.Card);

            switch (_currentMenuAction.Key)
            {
                case "action":
                {
                    var action = _currentMenuAction.Data as ActionCharacter;
                    actionName = TranslationServer.Translate(action.Name).ToString();
                    LogActionDeclared(source, actionName, target);
                    IBattleAction anim = null;

                    if (action.Type == ActionCharacterType.ToEnemy)
                        anim = new Actions.FFAttackAnimation(_currentCharacter, target, action);
                    else if (action.Type == ActionCharacterType.ToAlias)
                        anim = new Actions.FFDefendAnimation(_currentCharacter, target, action);
                    else if (action.Type == ActionCharacterType.ToSelf)
                        anim = new Actions.FFDefendAnimation(_currentCharacter, _currentCharacter, action);
                    else
                        GameLogger.Log("FF_BATTLE", $"Unknown action type: {action.Type}", LogLevel.Warning);

                    await RunAnimation(anim);
                    break;
                }

                case "item":
                {
                    var item = _currentMenuAction.Data as IItem;
                    actionName = TranslationServer.Translate(item.Name).ToString();
                    LogActionDeclared(source, actionName, target);
                    var dummyAction = new ActionCharacter { ID = "ff/item" };
                    var anim = new Actions.FFItemAnimation(_currentCharacter, target, dummyAction, item);
                    await RunAnimation(anim);
                    break;
                }
            }

            LogOutcome(source, target, actionName, hpBefore, defBefore, stunBefore);

            _currentMenuAction = null;
            _endTurnButton.Visible = true;

            CheckEndCondition();
        }

        private void DoDefendAction()
        {
            var defendAction = _currentCharacter?.Card?.Actions
                ?.FirstOrDefault(a => a.Type == ActionCharacterType.ToAlias);
            if (defendAction == null)
                return;

            _currentMenuAction = new FFMenuEntry
            {
                Key = "action",
                Data = defendAction
            };

            _targetSelectMode = false;
            ExecuteMenuAction(_currentCharacter);
        }

        private void DoEscapeAction()
        {
            _menuPanel.HideMenu();
            _detailPanel.HidePassport();
            _controller.State = FFBattleState.Idle;

            var escapeRoll = (float)GD.RandRange(0, 100);
            if (escapeRoll < 70f)
            {
                GameLogger.Log("FF_BATTLE", "Escape succeeded", LogLevel.Debug);
                _banner.Show(Translate("ff_battle/escape_success"));
                _logPanel.AddEntry(Translate("ff_battle/log_escape_success"), FFBattleLogKind.Turn);
                _ctx.EndBattle(BattleSide.Player);
            }
            else
            {
                GameLogger.Log("FF_BATTLE", "Escape failed", LogLevel.Debug);
                _currentCharacter.Card.Attributes.Point.Value -= 1;
                _currentCharacter.UpdateInfo();
                _banner.Show(Translate("ff_battle/escape_fail"));
                _logPanel.AddEntry(string.Format(Translate("ff_battle/log_escape_failed"), NameOf(_currentCharacter)), FFBattleLogKind.Status);
                _currentCharacter = null;
                BattleSceneContext.Instance.ActiveCharacter = null;
                TryNextCharacterOrEndTurn();
            }
        }

        private void DoItemAction(IItem item)
        {
            _currentMenuAction = new FFMenuEntry
            {
                Key = "item",
                Data = item
            };

            _targetSelectMode = false;
            ExecuteMenuAction(_currentCharacter);
        }

        private async Task RunAnimation(IBattleAction anim)
        {
            if (anim == null)
                return;

            var root = (Node)_ctx.RootNode;

            anim.DoStart();
            while (!anim.DoAction())
                await root.ToSignal(root.GetTree(), "process_frame");
            anim.DoEnd();
        }

        private void OnMenuCancel()
        {
            if (_targetSelectMode)
            {
                _targetSelectMode = false;
                foreach (var w in _enemyPanel.Widgets) w.ClearHighlight();
                foreach (var w in _playerPanel.Widgets) w.ClearHighlight();
                _targetHint.HideHint();
                ShowMainMenu();
                return;
            }

            if (_controller.State == FFBattleState.SubMenuOpen)
            {
                ShowMainMenu();
                return;
            }

            _menuPanel.HideMenu();
            _detailPanel.HidePassport();
            _controller.State = FFBattleState.Idle;
            _currentCharacter = null;
            _currentMenuAction = null;
            BattleSceneContext.Instance.ActiveCharacter = null;
            foreach (var w in _playerPanel.Widgets) w.ClearHighlight();
        }

        private void CheckEndCondition()
        {
            if (!_isPlayerTurn)
                return;

            if (!_enemyPanel.HasAlive())
            {
                GameLogger.Log("FF_BATTLE", "All enemies dead — player wins", LogLevel.Debug);
                _ctx.EndBattle(BattleSide.Player);
                return;
            }

            if (!_playerPanel.HasAlive())
            {
                GameLogger.Log("FF_BATTLE", "All players dead — enemy wins", LogLevel.Debug);
                _ctx.EndBattle(BattleSide.Enemy);
                return;
            }

            TryNextCharacterOrEndTurn();
        }

        private void TryNextCharacterOrEndTurn()
        {
            foreach (var widget in _playerPanel.Widgets)
            {
                if (widget.IsDead || widget.Card?.Attributes?.Point?.Value <= 0)
                    continue;

                if (StatusEffectService.Of(widget.Card).HasStun())
                {
                    GameLogger.Log("FF_BATTLE", $"{widget.Card.Name} is stunned, skipping", LogLevel.Debug);
                    widget.Card.Attributes.Point.Value = 0;
                    widget.UpdateInfo();
                    _logPanel.AddEntry(string.Format(Translate("ff_battle/log_stun_skip"), NameOf(widget)), FFBattleLogKind.Status);
                    continue;
                }

                _currentCharacter = widget;
                BattleSceneContext.Instance.ActiveCharacter = widget.Card;
                ShowMainMenu();
                return;
            }

            _logPanel.AddEntry(Translate("ff_battle/log_no_points"), FFBattleLogKind.Info);
            OnPlayerTurnEnd();
        }

        /// <summary>
        /// Ends the player's turn, hides all UI, and transitions to the enemy turn.
        /// Runs a brief delay before invoking <c>RunEnemyTurn()</c> which iterates
        /// over all alive enemies, each picking a random <see cref="ActionCharacter"/>
        /// and a random valid target, executing the action via the appropriate
        /// animation subclass. Checks for player defeat after each enemy action.
        /// If all enemies are dead, calls <see cref="IBattleContext.EndBattle"/>.
        /// Otherwise starts a new player turn.
        /// </summary>
        public async void OnPlayerTurnEnd()
        {
            GameLogger.Log("FF_BATTLE", "Player turn ended", LogLevel.Debug);

            _isPlayerTurn = false;
            _endTurnButton.Visible = false;
            _menuPanel.HideMenu();
            _detailPanel.HidePassport();
            _targetHint.HideHint();
            _controller.State = FFBattleState.AnimationPlaying;
            BattleSceneContext.Instance.IsWait = true;

            foreach (var widget in _playerPanel.Widgets)
                widget.ClearHighlight();
            foreach (var widget in _enemyPanel.Widgets)
                widget.ClearHighlight();

            _hud.SetPhase(false);
            _banner.Show(Translate("ff_battle/banner_enemy"));
            _logPanel.AddEntry(string.Format(Translate("ff_battle/log_turn_enemy"), _round), FFBattleLogKind.Turn);

            var root = (Node)_ctx.RootNode;
            await root.ToSignal(root.GetTree(), "process_frame");
            await root.ToSignal(root.GetTree().CreateTimer(0.5f), "timeout");

            await RunEnemyTurn();
        }

        private async Task RunEnemyTurn()
        {
            GameLogger.Log("FF_BATTLE", "Enemy turn started", LogLevel.Debug);

            var root = (Node)_ctx.RootNode;

            foreach (var enemyWidget in _enemyPanel.Widgets.ToList())
            {
                if (enemyWidget.IsDead || enemyWidget.Card?.Actions == null || enemyWidget.Card.Actions.Count == 0)
                    continue;

                StatusEffectService.Of(enemyWidget.Card).OnTurnStart();
                enemyWidget.UpdateInfo();

                if (StatusEffectService.Of(enemyWidget.Card).HasStun())
                {
                    GameLogger.Log("FF_BATTLE", $"{enemyWidget.Card.Name} is stunned, skipping", LogLevel.Debug);
                    _logPanel.AddEntry(string.Format(Translate("ff_battle/log_stun_skip"), NameOf(enemyWidget)), FFBattleLogKind.Status);
                    continue;
                }

                enemyWidget.Card.Attributes.Point.Value = enemyWidget.Card.Attributes.Point.BaseValue;

                var action = PickEnemyAction(enemyWidget);
                if (action == null)
                    continue;

                var target = PickEnemyTarget(enemyWidget, action);
                if (target == null)
                    continue;

                GameLogger.Log("FF_BATTLE",
                    $"Enemy: {enemyWidget.Card.Name} → {action.Name} → {target.Card.Name}",
                    LogLevel.Debug);

                var actionName = TranslationServer.Translate(action.Name).ToString();
                LogActionDeclared(enemyWidget, actionName, target);
                var hpBefore = target.Card.Attributes.Health.Value;
                var defBefore = target.Card.Attributes.Defence.Value;
                var stunBefore = CountStunTurns(target.Card);

                var anim = CreateEnemyAnimation(enemyWidget, target, action);
                if (anim != null)
                {
                    anim.DoStart();
                    while (!anim.DoAction())
                        await root.ToSignal(root.GetTree(), "process_frame");
                    anim.DoEnd();
                }

                LogOutcome(enemyWidget, target, actionName, hpBefore, defBefore, stunBefore);

                await root.ToSignal(root.GetTree().CreateTimer(0.2f), "timeout");

                if (!_playerPanel.HasAlive())
                {
                    GameLogger.Log("FF_BATTLE", "All players dead — enemy wins", LogLevel.Debug);
                    _ctx.EndBattle(BattleSide.Enemy);
                    return;
                }
            }

            if (!_playerPanel.HasAlive())
            {
                _ctx.EndBattle(BattleSide.Enemy);
                return;
            }

            StartPlayerTurn();
        }

        private ActionCharacter PickEnemyAction(FFCharacterWidget widget)
        {
            var actions = widget.Card.Actions;
            if (actions == null || actions.Count == 0)
                return null;

            var random = new Random();
            return actions[random.Next(actions.Count)];
        }

        private FFCharacterWidget PickEnemyTarget(FFCharacterWidget source, ActionCharacter action)
        {
            if (action.Type == ActionCharacterType.ToEnemy)
            {
                var alivePlayers = _playerPanel.Widgets
                    .Where(w => !w.IsDead && w.Card?.Attributes?.Health?.Value > 0)
                    .ToList();

                if (alivePlayers.Count == 0)
                    return null;
                return alivePlayers[new Random().Next(alivePlayers.Count)];
            }

            if (action.Type == ActionCharacterType.ToAlias || action.Type == ActionCharacterType.ToSelf)
                return source;

            return null;
        }

        private Actions.FFAbstractAnimation CreateEnemyAnimation(
            FFCharacterWidget source, FFCharacterWidget target, ActionCharacter action)
        {
            if (action.Type == ActionCharacterType.ToEnemy)
                return new Actions.FFAttackAnimation(source, target, action, 0.8f);

            if (action.Type == ActionCharacterType.ToAlias || action.Type == ActionCharacterType.ToSelf)
                return new Actions.FFDefendAnimation(source, target, action, 0.8f);

            GameLogger.Log("FF_BATTLE", $"Unknown action type for enemy: {action.Type}", LogLevel.Warning);
            return null;
        }

        private void OnEndTurnPressed()
        {
            if (!_isPlayerTurn)
                return;

            OnPlayerTurnEnd();
        }

        private void OnEnemyDefeated(FFCharacterWidget widget)
        {
            var ctx = BattleSceneContext.Instance;
            if (ctx != null && widget?.Card != null)
            {
                ctx.RecordEnemyDefeated(widget.Card);
                ctx.NotifyCharacterDied(widget.Card);
            }
        }

        private void OnEnemyDied(FFCharacterWidget widget)
        {
            UpdateHudCounts();

            if (!_enemyPanel.HasAlive())
            {
                GameLogger.Log("FF_BATTLE", "All enemies dead — player wins", LogLevel.Debug);
                _ctx.EndBattle(BattleSide.Player);
            }
        }

        private bool HasAvailableActions()
        {
            if (_currentCharacter?.Card?.Actions == null)
                return false;

            foreach (var action in _currentCharacter.Card.Actions)
            {
                if (!action.Type.UseInBattle())
                    continue;
                var cost = action.GetInt("RequiredPoint", 1);
                if (_currentCharacter.Card.Attributes.Point.Value >= cost)
                    return true;
            }
            return false;
        }

        private bool HasUsableItems()
        {
            var inventory = GameData.Instance.Runtime.Inventory;
            foreach (var container in inventory.Items)
            {
                if (container.Item.ItemType.HasFlag(ItemType.Used) && container.Count > 0)
                    return true;
            }
            return false;
        }

        private bool HasDefendAction()
        {
            if (_currentCharacter?.Card?.Actions == null)
                return false;

            return _currentCharacter.Card.Actions.Any(a =>
                a.Type == ActionCharacterType.ToAlias
                && _currentCharacter.Card.Attributes.Point.Value >= a.GetInt("RequiredPoint", 1));
        }

        private void OnBattleCleared()
        {
            _playerPanel?.ClearAll();
            _enemyPanel?.ClearAll();
            _menuPanel?.HideMenu();
            _detailPanel?.HidePassport();
            _targetHint?.HideHint();
            _logPanel?.Clear();
            if (_endTurnButton != null)
                _endTurnButton.Visible = false;
        }

        private void OnMenuSelectionChanged(int index)
        {
            if (_menuPanel.Entries == null || index < 0 || index >= _menuPanel.Entries.Count)
            {
                _detailPanel.HidePassport();
                return;
            }

            var entry = _menuPanel.Entries[index];
            _detailPanel.ShowPassport();

            if (_detailKind == DetailActions)
            {
                if (entry.Data is ActionCharacter action)
                    _detailPanel.ShowAction(action, BuildActionNote(action));
                else
                    _detailPanel.ShowHint(Translate("ff_battle/detail_back"));
                return;
            }

            if (_detailKind == DetailItems)
            {
                if (entry.Data is IItem item)
                    _detailPanel.ShowItem(item, item.ItemType.HasFlag(ItemType.TargetUsed), BuildItemNote(item));
                else
                    _detailPanel.ShowHint(Translate("ff_battle/detail_back"));
                return;
            }

            _detailPanel.ShowHint(MainHintFor(entry.Key));
        }

        private string BuildActionNote(ActionCharacter action)
        {
            if (action.Properties == null)
                return TranslationServer.Translate("ff_battle/detail_action").ToString();

            var cost = action.GetInt("RequiredPoint", 1);
            var attack = action.GetFloat("Attack", 0f);
            if (attack > 0f)
            {
                var attacker = _currentCharacter?.Card?.Attributes?.Attack?.Value ?? 0;
                return string.Format(Translate("ff_battle/detail_damage"),
                    Mathf.RoundToInt(attack * attacker), attacker, attack.ToString("0.0"), cost);
            }

            var turns = action.GetInt("Turn", 0);
            if (turns > 0)
                return string.Format(Translate("ff_battle/detail_stun"), turns, cost);

            var defence = action.GetInt("Defence", 0);
            if (defence > 0)
                return string.Format(Translate("ff_battle/detail_defence"), defence, cost);

            var description = ActionCharacterUi.StripMarkup(ActionCharacterUi.Translate(action.Description));
            return !string.IsNullOrEmpty(description) ? description : Translate("ff_battle/detail_action");
        }

        private static string BuildItemNote(IItem item)
        {
            var description = ActionCharacterUi.StripMarkup(ActionCharacterUi.Translate(item.Description));
            return !string.IsNullOrEmpty(description)
                ? description
                : TranslationServer.Translate("ff_battle/detail_item").ToString();
        }

        private static string MainHintFor(string key)
        {
            switch (key)
            {
                case "attack": return TranslationServer.Translate("ff_battle/detail_attack").ToString();
                case "items": return TranslationServer.Translate("ff_battle/detail_items").ToString();
                case "defend": return TranslationServer.Translate("ff_battle/detail_defend").ToString();
                case "escape": return TranslationServer.Translate("ff_battle/detail_escape").ToString();
                default: return TranslationServer.Translate("ff_battle/detail_back").ToString();
            }
        }

        private void LogActionDeclared(FFCharacterWidget source, string actionName, FFCharacterWidget target)
        {
            var entry = source == target
                ? string.Format(Translate("ff_battle/log_action_self"), NameOf(source), actionName)
                : string.Format(Translate("ff_battle/log_action"), NameOf(source), actionName, NameOf(target));
            _logPanel.AddEntry(entry, FFBattleLogKind.Info);
        }

        private void LogOutcome(FFCharacterWidget source, FFCharacterWidget target, string actionName,
            int hpBefore, int defBefore, int stunBefore)
        {
            var hpAfter = target.Card.Attributes.Health.Value;
            var defAfter = target.Card.Attributes.Defence.Value;
            var stunAfter = CountStunTurns(target.Card);

            if (defAfter > defBefore)
                _logPanel.AddEntry(string.Format(Translate("ff_battle/log_defence"),
                    NameOf(target), defAfter - defBefore, defAfter), FFBattleLogKind.Buff);

            var shieldLost = defBefore - defAfter;
            if (shieldLost > 0)
                _logPanel.AddEntry(string.Format(Translate("ff_battle/log_shield"),
                    NameOf(target), shieldLost, defAfter), FFBattleLogKind.Shield);

            if (stunAfter > stunBefore)
                _logPanel.AddEntry(string.Format(Translate("ff_battle/log_stun"),
                    NameOf(target), stunAfter), FFBattleLogKind.Status);

            if (hpAfter > hpBefore)
                _logPanel.AddEntry(string.Format(Translate("ff_battle/log_heal"),
                    actionName, NameOf(target), hpAfter - hpBefore), FFBattleLogKind.Buff);

            if (hpAfter < hpBefore)
                _logPanel.AddEntry(string.Format(Translate("ff_battle/log_damage"),
                    actionName, NameOf(source), NameOf(target), hpBefore - hpAfter), FFBattleLogKind.Damage);

            if (hpAfter <= 0 && hpBefore > 0)
                _logPanel.AddEntry(string.Format(Translate("ff_battle/log_death"), NameOf(target)), FFBattleLogKind.Dead);

            UpdateHudCounts();
        }

        private static int CountStunTurns(Character character)
        {
            var stun = StatusEffectService.Of(character)?.ActiveStatusEffects?
                .FirstOrDefault(effect => effect.EffectType == StatusEffectType.Stun);
            return stun?.RemainingTurns ?? 0;
        }

        private string ActorName()
            => _currentCharacter?.Card == null
                ? string.Empty
                : TranslationServer.Translate(_currentCharacter.Card.Name).ToString();

        private static string NameOf(FFCharacterWidget widget)
            => widget?.Card == null
                ? string.Empty
                : TranslationServer.Translate(widget.Card.Name).ToString();

        private void UpdateHud()
        {
            if (_hud == null)
                return;
            _hud.SetRound(_round);
            UpdateHudCounts();
        }

        private void UpdateHudCounts()
        {
            if (_hud == null)
                return;
            _hud.SetCounts(
                _enemyPanel.Widgets.Count(w => !w.IsDead),
                _playerPanel.Widgets.Count(w => !w.IsDead));
        }

        private static string Translate(string key)
            => TranslationServer.Translate(key).ToString();

        /// <summary>
        /// <c>true</c> while the core accepts a player turn advance; flips to
        /// <c>false</c> synchronously at the start of <see cref="OnPlayerTurnEnd"/>.
        /// </summary>
        public bool IsWaitingForPlayerInput => _isPlayerTurn;

        /// <summary>
        /// Automation hook: ends the player's turn through the same path the
        /// end-turn button uses.
        /// </summary>
        public void AdvancePlayerTurn() => OnPlayerTurnEnd();
    }
}



