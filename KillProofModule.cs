using Blish_HUD;
using Blish_HUD.ArcDps.Common;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Gw2Sharp.ChatLinks;
using Gw2Sharp.Models;
using Gw2Sharp.WebApi.V2.Models;
using KillProofModule.Controls;
using KillProofModule.Persistance;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Blish_HUD.GameService;
using Color = Microsoft.Xna.Framework.Color;

namespace KillProofModule
{
    [Export(typeof(Module))]
    public class KillProofModule : Module
    {
        internal static readonly Logger Logger = Logger.GetLogger(typeof(KillProofModule));

        internal static KillProofModule ModuleInstance;

        [ImportingConstructor]
        public KillProofModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) { ModuleInstance = this; }

        protected override void DefineSettings(SettingCollection settings)
        {
            var selfManagedSettings = settings.AddSubCollection("Managed Settings", false, false);
            SmartPingMenuEnabled = selfManagedSettings.DefineSetting("SmartPingMenuEnabled", false);
            AutomaticClearEnabled = selfManagedSettings.DefineSetting("AutomaticClearEnabled", false);

            SPM_DropdownSelection = selfManagedSettings.DefineSetting("SmartPingMenuDropdownSelection", "");
            SPM_WingSelection = selfManagedSettings.DefineSetting("SmartPingMenuWingSelection", "W1");
        }

        #region Constants

        private const int TOP_MARGIN = 0;
        private const int RIGHT_MARGIN = 5;
        private const int BOTTOM_MARGIN = 10;
        private const int LEFT_MARGIN = 8;

        private const string KILLPROOF_API_URL = "https://killproof.me/api/";

        #endregion

        #region Textures

        private Dictionary<int, AsyncTexture2D> ProfessionRenderRepository;
        private Dictionary<int, AsyncTexture2D> EliteRenderRepository;
        private Dictionary<int, AsyncTexture2D> TokenRenderRepository;

        #endregion

        #region Controls

        private List<PlayerButton> _displayedPlayers;
        private Panel _smartPingMenu;
        private WindowTab _killProofTab;
        private Panel _modulePanel;
        private PlayerButton _localPlayerButton;
        private List<KillProofButton> _displayedKillProofs;
        private Panel _squadPanel;

        #endregion

        #region Settings

        private SettingEntry<bool> SmartPingMenuEnabled;
        private SettingEntry<bool> AutomaticClearEnabled;
        private SettingEntry<string> SPM_DropdownSelection;
        private SettingEntry<string> SPM_WingSelection;
        #endregion

        #region Localization

        private string KillProofTabName = "KillProof";

        private string SORTBY_ALL;
        private string SORTBY_KILLPROOF;
        private string SORTBY_TOKEN;
        private string SORTBY_TITLE;
        private string SORTBY_RAID;
        private string SORTBY_FRACTAL;
        //private string SmartPingMenuSettingDisplayName;
        //private string SmartPingMenuSettingDescription
        private string NotificationProfileAvailable;
        private string SmartPingMenuToggleCheckboxText;
        private string SmartPingMenuCheckboxTooltip;
        private string StartedBlishHUDWhileGw2AlreadyRunning;
        private string RefreshMapToSeeYourProfile;
        private string SearchBoxText;
        private string RecentProfileText;
        private string PoweredByText;
        private string LastRefreshText;
        private string KpIdText;
        private string NotYetRegisteredText;
        private string VisitUsAndHelpText;
        private string NoProfileFoundText;
        private string PleaseShareSiteText;
        private string ProfileBackButtonNavTitle;
        private string LoadingLabel;
        private string SmartPingMenuSendButtonTooltip;
        private string SmartPingMenuRandomizeButtonTooltip;
        private string SmartPingMenuRightclickSendMessage;
        private string ClearButtonText;
        private string ClearButtonTooltipText;
        private string ClearCheckboxTooltipText;

        private void ChangeLocalization(object sender, EventArgs e)
        {
            //SmartPingMenuSettingDisplayName = Properties.Resources.Kill_Proof_Smart_Ping_Menu;
            //SmartPingMenuSettingDescription = Properties.Resources.Quick_access_to_ping_kill_proofs_;
            NotificationProfileAvailable = Properties.Resources.profile_available;
            SmartPingMenuToggleCheckboxText = Properties.Resources.Show_Smart_Ping_Menu;
            SmartPingMenuCheckboxTooltip = Properties.Resources.Shows_a_menu_on_the_top_left_corner_of_your_screen_which_allows_you_to_quickly_access_and_ping_your_killproofs_;
            StartedBlishHUDWhileGw2AlreadyRunning = Properties.Resources.You_started_Blish_HUD_while_Guild_Wars_2_was_already_running_;
            RefreshMapToSeeYourProfile = Properties.Resources.Refresh_map_to_see_your_profile_;
            SearchBoxText = Properties.Resources.Account_Name_or_KillProof_me_ID_;
            RecentProfileText = Properties.Resources.Recent_profiles_;
            PoweredByText = Properties.Resources.Powered_by_www_killproof_me;
            LastRefreshText = Properties.Resources.Last_Refresh_;
            KpIdText = Properties.Resources.ID_;
            NotYetRegisteredText = Properties.Resources.Not_yet_registered___;
            VisitUsAndHelpText = Properties.Resources.Visit_www_killproof_me_and_allow_us_to_record_your_KillProofs_for_you_;
            NoProfileFoundText = Properties.Resources.No_profile_for___0___found___;
            PleaseShareSiteText = Properties.Resources.Please__share_www_killproof_me_with_this_player_and_help_expand_our_database_;
            ProfileBackButtonNavTitle = Properties.Resources.Profile;
            LoadingLabel = Properties.Resources.Loading___;
            SmartPingMenuSendButtonTooltip = Properties.Resources.Send_To_Chat_nLeft_Click__Only_send_code_up_to_a_stack_s_worth__250x____nRight_Click__Send_killproof_me_total_amount_;
            SmartPingMenuRandomizeButtonTooltip = Properties.Resources.Random_token_from_selected_wing_when_pressing_Send_To_Chat__nLeft_Click__Toggle_nRight_Click__Iterate_wings;
            SmartPingMenuRightclickSendMessage = Properties.Resources.Total___0__of__1___killproof_me__2__;
            ClearButtonText = Properties.Resources.Clear;
            ClearButtonTooltipText = Properties.Resources.Removes_profiles_of_players_which_are_not_in_squad_;
            ClearCheckboxTooltipText = Properties.Resources.Remove_leavers_automatically_;

            SORTBY_ALL = Properties.Resources.Everything;
            SORTBY_KILLPROOF = Properties.Resources.Progress_Proofs;
            SORTBY_TOKEN = Properties.Resources.Tokens;
            SORTBY_TITLE = Properties.Resources.Titles;
            SORTBY_RAID = Properties.Resources.Raid_Titles;
            SORTBY_FRACTAL = Properties.Resources.Fractal_Titles;
            CurrentSortMethod = SORTBY_ALL;

            Task.Run(async delegate { await LoadResources(); });

            BuildSmartPingMenu();

            _modulePanel?.Dispose();
            _modulePanel = BuildHomePanel(Overlay.BlishHudWindow);

            if (_killProofTab != null)
                Overlay.BlishHudWindow.RemoveTab(_killProofTab);

            _killProofTab = Overlay.BlishHudWindow.AddTab(KillProofTabName, _killProofIconTexture, _modulePanel, 0);
        }

        #endregion

        #region Persistance

        private Resources _resources;
        private KillProof _myKillProof;
        private List<KillProof> _cachedKillProofs;
        private KillProof _currentProfile;

        #endregion

        #region Delegates

        private EventHandler<MouseEventArgs> _localPlayerButtonDelegate;

        #endregion
        
        private readonly Regex Gw2AccountName = new Regex(@"[^a-zA-Z0-9.\s]|^\.*$", RegexOptions.Compiled);

        private readonly Point LABEL_BIG = new Point(400, 40);
        private readonly Point LABEL_SMALL = new Point(400, 30);
        private string CurrentSortMethod;

        private DateTimeOffset _smartPingCooldownSend = DateTimeOffset.Now;
        private DateTimeOffset _smartPingHotButtonTimeSend = DateTimeOffset.Now;
        private int _smartPingCurrentReduction = 0;
        private int _smartPingCurrentValue = 0;

        protected override void Initialize()
        {
            TokenRenderRepository = new Dictionary<int, AsyncTexture2D>();
            EliteRenderRepository = new Dictionary<int, AsyncTexture2D>();
            ProfessionRenderRepository = new Dictionary<int, AsyncTexture2D>();
            _displayedKillProofs = new List<KillProofButton>();
            _displayedPlayers = new List<PlayerButton>();
            _cachedKillProofs = new List<KillProof>();

            LoadTextures();
        }


        private void LoadTextures()
        {
            _killProofIconTexture = ContentsManager.GetTexture("killproof_icon.png");
            _killProofMeLogoTexture = ContentsManager.GetTexture("killproof_logo.png");

            _deletedItemTexture = ContentsManager.GetTexture("deleted_item.png");

            _sortByWorldBossesTexture = ContentsManager.GetTexture("world-bosses.png");
            _sortByTokenTexture = ContentsManager.GetTexture("icon_token.png");
            _sortByTitleTexture = ContentsManager.GetTexture("icon_title.png");
            _sortByRaidTexture = ContentsManager.GetTexture("icon_raid.png");
            _sortByFractalTexture = ContentsManager.GetTexture("icon_fractal.png");

            _notificationBackroundTexture = ContentsManager.GetTexture("ns-button.png");
        }


        protected override async Task LoadAsync()
        {
            await LoadResources();
            await LoadProfessionIcons();
            await LoadEliteIcons();
        }


        private async Task LoadResources()
        {
            await TaskUtil.GetJsonResponse<Resources>(KILLPROOF_API_URL + "resources?lang=" + Overlay.UserLocale.Value)
                .ContinueWith(async result =>
                {
                    if (!result.IsCompleted || !result.Result.Item1)
                    {
                        using (var fs = ContentsManager.GetFileStream("resources.json")) {
                            fs.Position = 0;
                            using (var jsonReader = new JsonTextReader(new StreamReader(fs)))
                            {
                                var serializer = new JsonSerializer();
                                _resources = serializer.Deserialize<Resources>(jsonReader);
                            }
                        }
                    } else {
                        _resources = result.Result.Item2;
                    }
                    await LoadTokenIcons();
                });
        }


        protected override void OnModuleLoaded(EventArgs e)
        {
            ChangeLocalization(null, null);

            Overlay.UserLocaleChanged += ChangeLocalization;

            SmartPingMenuEnabled.SettingChanged += OnSmartPingMenuEnabledSettingChanged;

            ArcDps.Common.Activate();
            ArcDps.Common.PlayerAdded += PlayerAddedEvent;
            ArcDps.Common.PlayerRemoved += PlayerLeavesEvent;

            GameIntegration.IsInGameChanged += OnIsInGameChanged;
            Gw2Mumble.UI.IsMapOpenChanged += OnIsMapOpenChanged;

            // Base handler must be called
            base.OnModuleLoaded(e);
        }


        private void DoSmartPing(Token token) {

            var chatLink = new ItemChatLink();
            chatLink.ItemId = token.Id;

            var totalAmount = _myKillProof.GetToken(token.Id)?.Amount ?? 0;
            if (totalAmount <= 250) {
                chatLink.Quantity = Convert.ToByte(totalAmount);
                GameIntegration.Chat.Send(chatLink.ToString());
                return;
            }

            var hotButtonCooldownTime  = DateTimeOffset.Now.Subtract(_smartPingHotButtonTimeSend);
            if (hotButtonCooldownTime.TotalMilliseconds > 500) 
            {
                _smartPingCurrentReduction = 0;
                _smartPingCurrentValue = 0;
            }

            var rest = totalAmount - (_smartPingCurrentValue % totalAmount);
            if (rest > 250)
            {
                var tempAmount = 250 - _smartPingCurrentReduction;
                if (RandomUtil.GetRandom(0, 10) > 5) 
                {
                    _smartPingCurrentValue += tempAmount;
                    _smartPingCurrentReduction++;
                }
                chatLink.Quantity = Convert.ToByte(tempAmount);

            } else {

                chatLink.Quantity = Convert.ToByte(rest);
                _smartPingCurrentReduction = 0;
                _smartPingCurrentValue = 0;
                _smartPingCooldownSend = DateTimeOffset.Now;
            }
            GameIntegration.Chat.Send(chatLink.ToString());
            _smartPingHotButtonTimeSend = DateTimeOffset.Now;
        }


        private bool IsUiAvailable() => Gw2Mumble.IsAvailable && GameIntegration.IsInGame && !Gw2Mumble.UI.IsMapOpen;

        private void OnIsMapOpenChanged(object o, ValueEventArgs<bool> e) => ToggleSmartPingMenu(!e.Value, 0.45f);
        private void OnIsInGameChanged(object o, ValueEventArgs<bool> e) => ToggleSmartPingMenu(e.Value, 0.1f);
        private void OnSmartPingMenuEnabledSettingChanged(object o, ValueChangedEventArgs<bool> e) => ToggleSmartPingMenu(e.NewValue, 0.1f);

        private void ToggleSmartPingMenu(bool enabled, float tDuration) {
            if (enabled)
                BuildSmartPingMenu();
            else if (_smartPingMenu != null)
                Animation.Tweener.Tween(_smartPingMenu, new {Opacity = 0.0f}, tDuration).OnComplete(() => _smartPingMenu?.Dispose());
        }

        /// <inheritdoc />
        protected override void Unload()
        {
            SmartPingMenuEnabled.SettingChanged -= OnSmartPingMenuEnabledSettingChanged;
            Overlay.UserLocaleChanged -= ChangeLocalization;
            ArcDps.Common.PlayerAdded -= PlayerAddedEvent;
            ArcDps.Common.PlayerRemoved -= PlayerLeavesEvent;
            Gw2Mumble.UI.IsMapOpenChanged -= OnIsMapOpenChanged;
            GameIntegration.IsInGameChanged -= OnIsInGameChanged;
            _smartPingMenu?.Dispose();
            _squadPanel?.Dispose();
            _localPlayerButton?.Dispose();

            foreach (var c in _displayedKillProofs) c?.Dispose();

            _displayedPlayers.Clear();
            Overlay.BlishHudWindow.RemoveTab(_killProofTab);
            // All static members must be manually unset
            ModuleInstance = null;
        }

        #region Service Managers

        internal SettingsManager SettingsManager => ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => ModuleParameters.Gw2ApiManager;

        #endregion

        #region Cached Textures

        internal Texture2D _killProofIconTexture;
        internal Texture2D _killProofMeLogoTexture;

        internal Texture2D _deletedItemTexture;

        internal Texture2D _sortByWorldBossesTexture;
        internal Texture2D _sortByTokenTexture;
        internal Texture2D _sortByTitleTexture;
        internal Texture2D _sortByRaidTexture;
        internal Texture2D _sortByFractalTexture;

        internal Texture2D _notificationBackroundTexture;

        #endregion

        #region Module Logic

        #region Render Getters

        private async Task LoadTokenIcons()
        {
            var tokenRenderUrlRepository = _resources.GetAllTokens();
            foreach (var token in tokenRenderUrlRepository)
            {
                TokenRenderRepository.Add(token.Id, new AsyncTexture2D());

                var renderUri = token.Icon;
                await Gw2ApiManager.Gw2ApiClient.Render.DownloadToByteArrayAsync(renderUri)
                    .ContinueWith(textureDataResponse =>
                    {
                        if (textureDataResponse.Exception != null) {
                            Logger.Warn(textureDataResponse.Exception, $"Request to render service for {renderUri} failed.");
                            return;
                        }
                        using (var textureStream = new MemoryStream(textureDataResponse.Result))
                        {
                            var loadedTexture =
                                Texture2D.FromStream(Graphics.GraphicsDevice, textureStream);

                            TokenRenderRepository[token.Id].SwapTexture(loadedTexture);
                        }
                    });
            }
        }


        private async Task<IReadOnlyList<Profession>> LoadProfessions()
        {
            return await Gw2ApiManager.Gw2ApiClient.V2.Professions.ManyAsync(Enum.GetValues(typeof(ProfessionType)).Cast<ProfessionType>());
        }


        private async Task LoadProfessionIcons()
        {
            var professions = await LoadProfessions();
            foreach (var profession in professions)
            {
                var id = (int) Enum.GetValues(typeof(ProfessionType)).Cast<ProfessionType>().ToList()
                    .Find(x => x.ToString().Equals(profession.Id, StringComparison.InvariantCultureIgnoreCase));

                ProfessionRenderRepository.Add(id, new AsyncTexture2D());

                var renderUri = (string) profession.IconBig;
                await Gw2ApiManager.Gw2ApiClient.Render.DownloadToByteArrayAsync(renderUri)
                    .ContinueWith(textureDataResponse =>
                    {
                        if (textureDataResponse.Exception != null) {
                            Logger.Warn(textureDataResponse.Exception, $"Request to render service for {renderUri} failed.");
                            return;
                        }
                        using (var textureStream = new MemoryStream(textureDataResponse.Result))
                        {
                            var loadedTexture =
                                Texture2D.FromStream(Graphics.GraphicsDevice, textureStream);

                            ProfessionRenderRepository[id].SwapTexture(loadedTexture);
                        }
                    });
            }
        }


        private async Task LoadEliteIcons()
        {
            var ids = await Gw2ApiManager.Gw2ApiClient.V2.Specializations.IdsAsync();
            var specializations = await Gw2ApiManager.Gw2ApiClient.V2.Specializations.ManyAsync(ids);
            foreach (var specialization in specializations)
            {
                if (!specialization.Elite) continue;

                EliteRenderRepository.Add(specialization.Id, new AsyncTexture2D());

                var renderUri = (string) specialization.ProfessionIconBig;
                await Gw2ApiManager.Gw2ApiClient.Render.DownloadToByteArrayAsync(renderUri)
                    .ContinueWith(textureDataResponse =>
                    {
                        if (textureDataResponse.Exception != null) {
                            Logger.Warn(textureDataResponse.Exception, $"Request to render service for {renderUri} failed.");
                            return;
                        }
                        using (var textureStream = new MemoryStream(textureDataResponse.Result))
                        {
                            var loadedTexture =
                                Texture2D.FromStream(Graphics.GraphicsDevice, textureStream);

                            EliteRenderRepository[specialization.Id].SwapTexture(loadedTexture);
                        }
                    });
            }
        }

        private AsyncTexture2D GetProfessionRender(CommonFields.Player player)
        {
            return ProfessionRenderRepository[(int) player.Profession];
        }
        private AsyncTexture2D GetEliteRender(CommonFields.Player player)
        {
            if (player.Elite == 0) return GetProfessionRender(player);
            return EliteRenderRepository[(int) player.Elite];
        }
        private AsyncTexture2D GetTokenRender(int key)
        {
            return TokenRenderRepository[key];
        }

        #endregion

        private async Task<KillProof> GetKillProofContent(string account)
        {
            if (_cachedKillProofs.Any(x => x.AccountName.Equals(account, StringComparison.InvariantCultureIgnoreCase)))
                return _cachedKillProofs.FirstOrDefault(x =>
                    x.AccountName.Equals(account, StringComparison.InvariantCultureIgnoreCase));

            var (responseSuccess, killProof) = await TaskUtil.GetJsonResponse<KillProof>(KILLPROOF_API_URL + $"kp/{account}?lang=" + Overlay.UserLocale.Value)
                .ConfigureAwait(false);

            if (responseSuccess && killProof?.Error == null)
            {
                _cachedKillProofs.Add(killProof);
                return killProof;
            }
            return null;
        }

        #region Panel Related Stuff

        private async void LoadMyKillProof()
        {
            if (_myKillProof != null) return;
            var player = ArcDps.Common.PlayersInSquad.First(x => x.Value.Self).Value;
            await GetKillProofContent(player.AccountName).ContinueWith((result) =>
            {
                if (!result.IsCompleted) return;
                _myKillProof = result.Result;
                BuildSmartPingMenu();
            });
        }


        private async Task<bool> ProfileAvailable(string account)
        {
            var (responseSuccess, optionalKillProof) =
                await TaskUtil.GetJsonResponse<KillProof>(KILLPROOF_API_URL + $"kp/{account}?lang=" + Overlay.UserLocale.Value);

            return responseSuccess && optionalKillProof?.Error == null;
        }


        private void PlayerLeavesEvent(CommonFields.Player player)
        {
            if (!AutomaticClearEnabled.Value) return;
            var profileBtn = _displayedPlayers.FirstOrDefault(x => x.Player.AccountName.Equals(player.AccountName));
            _displayedPlayers.Remove(profileBtn);
            profileBtn?.Dispose();
        }


        private void PlayerAddedEvent(CommonFields.Player player) {
            if (player.Self && _localPlayerButton != null)
            {
                _localPlayerButton.BasicTooltipText = "";
                _localPlayerButton.Player = player;
                _localPlayerButton.Icon = GetEliteRender(player);
                _localPlayerButton.Click -= _localPlayerButtonDelegate;
                _localPlayerButtonDelegate = delegate
                {
                    Overlay.BlishHudWindow.Navigate(BuildKillProofPanel(Overlay.BlishHudWindow, player));
                };
                _localPlayerButton.Click += _localPlayerButtonDelegate;
                LoadMyKillProof();
                return;
            }

            if (_displayedPlayers.Any(x => x.Player.AccountName.Equals(player.AccountName, StringComparison.InvariantCultureIgnoreCase)) || !ProfileAvailable(player.AccountName).Result) return;

            PlayerNotification.ShowNotification(player.AccountName, GetEliteRender(player), NotificationProfileAvailable, 10);

            var optionalButton = _displayedPlayers.FirstOrDefault(x => x.Player.AccountName.Equals(player.AccountName, StringComparison.InvariantCultureIgnoreCase));

            if (optionalButton == null) {
                var playerButton = new PlayerButton
                {
                    Parent = _squadPanel,
                    Player = player,
                    Icon = GetEliteRender(player),
                    Font = Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size16, ContentService.FontStyle.Regular)
                };
                playerButton.Click += delegate
                {
                    playerButton.IsNew = false;
                    Overlay.BlishHudWindow.Navigate(BuildKillProofPanel(Overlay.BlishHudWindow, playerButton.Player));
                };
                _displayedPlayers.Add(playerButton);

            } else {

                optionalButton.Player = player;
                optionalButton.Icon = GetEliteRender(player);

            }

            RepositionPlayers();
        }

        private Panel BuildHomePanel(WindowBase wndw)
        {
            var hPanel = new Panel
            {
                CanScroll = false,
                Size = wndw.ContentRegion.Size
            };
            /* ###################
            /      <HEADER>
            / ################### */
            var header = new Panel
            {
                Parent = hPanel,
                Size = new Point(hPanel.Width, 200),
                Location = new Point(0, 0),
                CanScroll = false
            };

            var imgKillproof = new Image(_killProofMeLogoTexture)
            {
                Parent = header,
                Size = new Point(128, 128),
                Location = new Point(LEFT_MARGIN + 10, TOP_MARGIN + 5)
            };
            var labAccountName = new Label
            {
                Parent = header,
                Size = new Point(300, 30),
                Location = new Point(header.Width / 2 - 100, header.Height / 2 + 30 + TOP_MARGIN),
                StrokeText = true,
                ShowShadow = true,
                Text = SearchBoxText
            };
            var tbAccountName = new TextBox
            {
                Parent = header,
                Size = new Point(200, 30),
                Location = new Point(header.Width / 2 - 100, labAccountName.Bottom + TOP_MARGIN),
                PlaceholderText = "Player.0000"
            };
            tbAccountName.EnterPressed += delegate
            {
                if (!string.Equals(tbAccountName.Text, "") && Gw2AccountName.IsMatch(tbAccountName.Text))
                    wndw.Navigate(BuildKillProofPanel(wndw, new CommonFields.Player(null, tbAccountName.Text, 0, 0, false)));
                tbAccountName.Focused = false;
            };
            var labSquadPanel = new Label
            {
                Parent = header,
                Size = new Point(300, 40),
                Font = Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size24, ContentService.FontStyle.Regular),
                StrokeText = true,
                Location = new Point(LEFT_MARGIN, header.Bottom - 40),
                Text = RecentProfileText
            };
            /* ###################
            /      </HEADER>
            / ###################
            / ###################
            /      <FOOTER>
            / ################### */
            var footer = new Panel
            {
                Parent = hPanel,
                Size = new Point(hPanel.Width, 50),
                Location = new Point(0, hPanel.Height - 50),
                CanScroll = false
            };
            var creditLabel = new Label
            {
                Parent = footer,
                Size = LABEL_SMALL,
                HorizontalAlignment = HorizontalAlignment.Center,
                Location = new Point(footer.Width / 2 - LABEL_SMALL.X / 2, footer.Height / 2 - LABEL_SMALL.Y / 2),
                StrokeText = true,
                ShowShadow = true,
                Text = PoweredByText
            };

            /* ###################
            /      </FOOTER>
            / ################### */
            _squadPanel = new Panel
            {
                Parent = hPanel,
                Size = new Point(header.Size.X, hPanel.Height - header.Height - footer.Height),
                Location = new Point(0, header.Bottom),
                ShowBorder = true,
                CanScroll = true,
                ShowTint = true
            };

            // Features only available when ArcDps is installed.
            if (ArcDps.Loaded)
            {
                var selfButtonPanel = new Panel
                {
                    Parent = header,
                    Size = new Point(335, 114),
                    ShowBorder = true,
                    ShowTint = true,
                    Location =
                        new Point(header.Right - 335 - RIGHT_MARGIN, TOP_MARGIN + 15)
                };

                var _smartPingCheckBox = new Checkbox
                {
                    Parent = header,
                    Location = new Point(selfButtonPanel.Location.X + LEFT_MARGIN, selfButtonPanel.Bottom),
                    Size = new Point(selfButtonPanel.Width, 30),
                    Text = SmartPingMenuToggleCheckboxText,
                    BasicTooltipText = SmartPingMenuCheckboxTooltip,
                    Checked = SmartPingMenuEnabled.Value
                };

                _smartPingCheckBox.CheckedChanged += delegate(object o, CheckChangedEvent e) { SmartPingMenuEnabled.Value = e.Checked; };

                _localPlayerButton = new PlayerButton
                {
                    Parent = selfButtonPanel,
                    Player = new CommonFields.Player(StartedBlishHUDWhileGw2AlreadyRunning,RefreshMapToSeeYourProfile, 0, 0, true),
                    Icon = Content.GetTexture("common/733268"),
                    IsNew = false,
                    Location = new Point(0, 0),
                    Font = Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size16, ContentService.FontStyle.Regular),
                    BasicTooltipText = RefreshMapToSeeYourProfile
                };

                var clearButton = new StandardButton()
                {
                    Parent = hPanel,
                    Size = new Point(100, 30),
                    Location = new Point(_squadPanel.Location.X + _squadPanel.Width - 100 - RIGHT_MARGIN, _squadPanel.Location.Y + _squadPanel.Height + BOTTOM_MARGIN),
                    Text = ClearButtonText,
                    BasicTooltipText = ClearButtonTooltipText
                };

                var clearCheckbox = new Checkbox()
                {
                    Parent = hPanel,
                    Size = new Point(20, 30),
                    Location = new Point(clearButton.Location.X - 20 - RIGHT_MARGIN, clearButton.Location.Y),
                    Text = "",
                    BasicTooltipText = ClearCheckboxTooltipText,
                    Checked = AutomaticClearEnabled.Value
                };

                clearCheckbox.CheckedChanged += delegate(object o, CheckChangedEvent e) 
                {
                    AutomaticClearEnabled.Value = e.Checked;
                };

                clearButton.Click += delegate
                {
                    foreach (var c in _displayedPlayers.ToArray())
                    {
                        if (c == null)
                            _displayedPlayers.Remove(null);
                        else if (!ArcDps.Common.PlayersInSquad.Any(p => p.Value.AccountName.Equals(c.Player.AccountName)))
                        {
                            _displayedPlayers.Remove(c);
                            c.Dispose();
                        }
                    }
                };
            }
            return hPanel;
        }


        private void MousePressedSortButton(object sender, MouseEventArgs e)
        {
            var bSortMethod = (Control) sender;
            bSortMethod.Size = new Point(bSortMethod.Size.X - 4, bSortMethod.Size.Y - 4);
        }


        private void MouseLeftSortButton(object sender, MouseEventArgs e)
        {
            var bSortMethod = (Control) sender;
            bSortMethod.Size = new Point(32, 32);
        }


        private void FinishLoadingKillProofPanel(WindowBase wndw, Panel hPanel, CommonFields.Player player, KillProof currentAccount)
        {
            if (currentAccount != null)
            {
                /* ###################
                /      <HEADER>
                / ################### */
                var header = new Panel
                {
                    Parent = hPanel,
                    Size = new Point(hPanel.Width, 200),
                    Location = new Point(0, 0),
                    CanScroll = false
                };
                var currentAccountName = new Label
                {
                    Parent = header,
                    Size = LABEL_BIG,
                    Location = new Point(LEFT_MARGIN, 100 - BOTTOM_MARGIN),
                    ShowShadow = true,
                    Font = Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size36, ContentService.FontStyle.Regular),
                    Text = ""
                };
                var currentAccountLastRefresh = new Label
                {
                    Parent = header,
                    Size = LABEL_SMALL,
                    Location = new Point(LEFT_MARGIN, currentAccountName.Bottom + BOTTOM_MARGIN),
                    Text = ""
                };
                var sortingsMenu = new Panel
                {
                    Parent = header,
                    Size = new Point(260, 32),
                    Location = new Point(header.Right - 310 - RIGHT_MARGIN, currentAccountLastRefresh.Location.Y),
                    ShowTint = true
                };
                var bSortByAll = new Image
                {
                    Parent = sortingsMenu,
                    Size = new Point(32, 32),
                    Location = new Point(RIGHT_MARGIN, 0),
                    Texture = Content.GetTexture("255369"),
                    BackgroundColor = Color.Transparent,
                    BasicTooltipText = SORTBY_ALL
                };
                bSortByAll.LeftMouseButtonPressed += UpdateSort;
                bSortByAll.LeftMouseButtonPressed += MousePressedSortButton;
                bSortByAll.LeftMouseButtonReleased += MouseLeftSortButton;
                bSortByAll.MouseLeft += MouseLeftSortButton;
                var bSortByKillProof = new Image
                {
                    Parent = sortingsMenu,
                    Size = new Point(32, 32),
                    Location = new Point(bSortByAll.Right + 20 + RIGHT_MARGIN, 0),
                    Texture = _sortByWorldBossesTexture,
                    BasicTooltipText = SORTBY_KILLPROOF
                };
                bSortByKillProof.LeftMouseButtonPressed += UpdateSort;
                bSortByKillProof.LeftMouseButtonPressed += MousePressedSortButton;
                bSortByKillProof.LeftMouseButtonReleased += MouseLeftSortButton;
                bSortByKillProof.MouseLeft += MouseLeftSortButton;
                var bSortByToken = new Image
                {
                    Parent = sortingsMenu,
                    Size = new Point(32, 32),
                    Location = new Point(bSortByKillProof.Right + RIGHT_MARGIN, 0),
                    Texture = _sortByTokenTexture,
                    BasicTooltipText = SORTBY_TOKEN
                };
                bSortByToken.LeftMouseButtonPressed += UpdateSort;
                bSortByToken.LeftMouseButtonPressed += MousePressedSortButton;
                bSortByToken.LeftMouseButtonReleased += MouseLeftSortButton;
                bSortByToken.MouseLeft += MouseLeftSortButton;
                var bSortByTitle = new Image
                {
                    Parent = sortingsMenu,
                    Size = new Point(32, 32),
                    Location = new Point(bSortByToken.Right + 20 + RIGHT_MARGIN, 0),
                    Texture = _sortByTitleTexture,
                    BasicTooltipText = SORTBY_TITLE
                };
                bSortByTitle.LeftMouseButtonPressed += UpdateSort;
                bSortByTitle.LeftMouseButtonPressed += MousePressedSortButton;
                bSortByTitle.LeftMouseButtonReleased += MouseLeftSortButton;
                bSortByTitle.MouseLeft += MouseLeftSortButton;
                var bSortByRaid = new Image
                {
                    Parent = sortingsMenu,
                    Size = new Point(32, 32),
                    Location = new Point(bSortByTitle.Right + RIGHT_MARGIN, 0),
                    Texture = _sortByRaidTexture,
                    BasicTooltipText = SORTBY_RAID
                };
                bSortByRaid.LeftMouseButtonPressed += UpdateSort;
                bSortByRaid.LeftMouseButtonPressed += MousePressedSortButton;
                bSortByRaid.LeftMouseButtonReleased += MouseLeftSortButton;
                bSortByRaid.MouseLeft += MouseLeftSortButton;
                var bSortByFractal = new Image
                {
                    Parent = sortingsMenu,
                    Size = new Point(32, 32),
                    Location = new Point(bSortByRaid.Right + RIGHT_MARGIN, 0),
                    Texture = _sortByFractalTexture,
                    BasicTooltipText = SORTBY_FRACTAL
                };
                bSortByFractal.LeftMouseButtonPressed += UpdateSort;
                bSortByFractal.LeftMouseButtonPressed += MousePressedSortButton;
                bSortByFractal.LeftMouseButtonReleased += MouseLeftSortButton;
                bSortByFractal.MouseLeft += MouseLeftSortButton;
                /* ###################
                /      </HEADER>
                / ###################
                / ###################
                /      <FOOTER>
                / ################### */
                var footer = new Panel
                {
                    Parent = hPanel,
                    Size = new Point(hPanel.Width, 50),
                    Location = new Point(0, hPanel.Height - 50),
                    CanScroll = false
                };
                var currentAccountKpId = new Label
                {
                    Parent = footer,
                    Size = LABEL_SMALL,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Location = new Point(LEFT_MARGIN, footer.Height / 2 - LABEL_SMALL.Y / 2),
                    Font = Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size11,
                        ContentService.FontStyle.Regular),
                    Text = ""
                };
                var currentAccountProofUrl = new Label
                {
                    Parent = footer,
                    Size = LABEL_SMALL,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Location = new Point(LEFT_MARGIN, currentAccountKpId.Location.Y + BOTTOM_MARGIN + 2),
                    Font = Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size11,
                        ContentService.FontStyle.Regular),
                    Text = ""
                };
                var creditLabel = new Label
                {
                    Parent = footer,
                    Size = LABEL_SMALL,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Location = new Point(footer.Width / 2 - LABEL_SMALL.X / 2, footer.Height / 2 - LABEL_SMALL.Y / 2),
                    StrokeText = true,
                    ShowShadow = true,
                    Text = PoweredByText
                };
                /* ###################
                /      </FOOTER>
                / ################### */
                var contentPanel = new Panel
                {
                    Parent = hPanel,
                    Size = new Point(header.Size.X, hPanel.Height - header.Height - footer.Height),
                    Location = new Point(0, header.Bottom),
                    ShowBorder = true,
                    CanScroll = true,
                    ShowTint = true
                };
                currentAccountName.Text = currentAccount.AccountName;
                currentAccountLastRefresh.Text = LastRefreshText + $" {currentAccount.LastRefresh:dddd, d. MMMM yyyy - HH:mm:ss}";
                currentAccountKpId.Text = KpIdText + ' ' + currentAccount.KpId;
                currentAccountProofUrl.Text = currentAccount.ProofUrl;

                if (currentAccount.Killproofs != null)
                {
                    foreach (var killproof in currentAccount.Killproofs)
                    {
                        var killProofButton = new KillProofButton
                        {
                            Parent = contentPanel,
                            Icon = GetTokenRender(killproof.Id),
                            Font = Content.GetFont(ContentService.FontFace.Menomonia,
                                ContentService.FontSize.Size16, ContentService.FontStyle.Regular),
                            Text = killproof.Name,
                            BottomText = killproof.Amount.ToString()
                        };
                        _displayedKillProofs.Add(killProofButton);
                    }
                }
                else
                {
                    // TODO: Show button indicating that killproofs were explicitly hidden
                    Logger.Info($"Player '{currentAccount.AccountName}' has LI details explicitly hidden.");
                }

                if (currentAccount.Tokens != null)
                {
                    foreach (var token in currentAccount.Tokens)
                    {
                        var killProofButton = new KillProofButton
                        {
                            Parent = contentPanel,
                            Icon = GetTokenRender(token.Id),
                            Font = Content.GetFont(ContentService.FontFace.Menomonia,
                                ContentService.FontSize.Size16, ContentService.FontStyle.Regular),
                            Text = token.Name,
                            BottomText = token.Amount.ToString()
                        };

                        _displayedKillProofs.Add(killProofButton);
                    }
                } else {
                    // TODO: Show button indicating that tokens were explicitly hidden
                    Logger.Info($"Player '{currentAccount.AccountName}' has tokens explicitly hidden.");
                }

                if (currentAccount.Titles != null)
                    foreach (var title in currentAccount.Titles)
                    {
                        var titleButton = new KillProofButton
                        {
                            Parent = contentPanel,
                            Font = Content.DefaultFont16,
                            Text = title.Name,
                            BottomText = title.Mode.ToString(),
                            IsTitleDisplay = true
                        };

                        switch (title.Mode)
                        {
                            case Mode.Raid:
                                titleButton.Icon = _sortByRaidTexture;
                                break;
                            case Mode.Fractal:
                                titleButton.Icon = _sortByFractalTexture;
                                break;
                        }

                        _displayedKillProofs.Add(titleButton);
                    }
                else // TODO: Show text indicating that titles were explicitly hidden
                    Logger.Info($"Player '{currentAccount.AccountName}' has titles and achievements explicitly hidden.");

                RepositionKillProofs();

                if (!player.Self && !player.AccountName.Equals(_localPlayerButton.Player.AccountName)
                                 && !_displayedPlayers.Any(x =>
                                     x.Player.AccountName.Equals(player.AccountName)))
                {
                    var newPlayer = new CommonFields.Player(null, player.AccountName, 0, 0, false);
                    var playerButton = new PlayerButton
                    {
                        Parent = _squadPanel,
                        Player = newPlayer,
                        Icon = Content.GetTexture("common/733268"),
                        Font = Content.GetFont(ContentService.FontFace.Menomonia,
                            ContentService.FontSize.Size16, ContentService.FontStyle.Regular),
                        IsNew = false
                    };
                    playerButton.LeftMouseButtonPressed += delegate
                    {
                        Overlay.BlishHudWindow.Navigate(BuildKillProofPanel(Overlay.BlishHudWindow, player));
                    };
                    _displayedPlayers.Add(playerButton);
                }
            }
            else
            {
                var tintPanel = new Panel
                {
                    Parent = hPanel,
                    Size = new Point(hPanel.Size.X - 150, hPanel.Size.Y - 150),
                    Location = new Point(75, 75),
                    ShowBorder = true,
                    ShowTint = true
                };
                var labNothingHere = new Label
                {
                    Parent = hPanel,
                    Size = hPanel.Size,
                    Location = new Point(0, -20),
                    ShowShadow = true,
                    StrokeText = true,
                    Font = Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size36, ContentService.FontStyle.Regular),
                    Text = NotYetRegisteredText,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Middle
                };
                var labVisitUs = new Label
                {
                    Parent = hPanel,
                    Size = hPanel.Size,
                    Location = new Point(0, -20),
                    ShowShadow = true,
                    StrokeText = true,
                    Font = Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size24, ContentService.FontStyle.Regular),
                    Text = "\n\n" + VisitUsAndHelpText,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Middle
                };
                if (!player.Self && !player.AccountName.Equals(_localPlayerButton.Player.AccountName,
                    StringComparison.InvariantCultureIgnoreCase))
                {
                    labNothingHere.Text = NoProfileFoundText.Replace("{0}", player.AccountName);
                    labVisitUs.Text = "\n\n" + PleaseShareSiteText;
                }
            }

            var backButton = new BackButton(wndw)
            {
                Text = KillProofTabName,
                NavTitle = ProfileBackButtonNavTitle,
                Parent = hPanel,
                Location = new Point(20, 20)
            };

            backButton.LeftMouseButtonReleased += delegate
            {
                wndw.NavigateHome();
                wndw.ActivePanel = Overlay.BlishHudWindow.Panels[_killProofTab];
                RepositionPlayers();
                hPanel.Dispose();
            };

            _currentProfile = currentAccount;
        }


        public Panel BuildKillProofPanel(WindowBase wndw, CommonFields.Player player)
        {
            var hPanel = new Panel
            {
                CanScroll = false,
                Size = wndw.ContentRegion.Size
            };

            var pageLoading = new LoadingSpinner
            {
                Parent = hPanel
            };

            pageLoading.Location = new Point(hPanel.Size.X / 2 - pageLoading.Size.X / 2, hPanel.Size.Y / 2 - pageLoading.Size.Y / 2);

            foreach (var e1 in _displayedKillProofs) e1.Dispose();
            _displayedKillProofs.Clear();

            GetKillProofContent(player.AccountName).ContinueWith(kpResult =>
            {
                FinishLoadingKillProofPanel(wndw, hPanel, player, kpResult.Result);
                pageLoading.Dispose();
            });

            return hPanel;
        }


        private void UpdateSort(object sender, EventArgs e)
        {
            if (sender != null) CurrentSortMethod = ((Control) sender).BasicTooltipText;
            if (CurrentSortMethod.Equals(SORTBY_ALL, StringComparison.InvariantCultureIgnoreCase)) {
                _displayedKillProofs.Sort((e1, e2) =>
                {
                    var result = e1.IsTitleDisplay.CompareTo(e2.IsTitleDisplay);
                    if (result != 0) return result;
                    return string.Compare(e1.BottomText, e2.BottomText,
                        StringComparison.InvariantCultureIgnoreCase);
                });
                foreach (var e1 in _displayedKillProofs) e1.Visible = true;
            } else if (CurrentSortMethod.Equals(SORTBY_KILLPROOF, StringComparison.InvariantCultureIgnoreCase)) {
                    _displayedKillProofs.Sort((e1, e2) =>
                        string.Compare(e1.BottomText, e2.BottomText, StringComparison.InvariantCultureIgnoreCase));
                    foreach (var e1 in _displayedKillProofs)
                        e1.Visible = _currentProfile.Killproofs != null &&
                                     _currentProfile.Killproofs.Any(x => x.Name.Equals(e1.Text, StringComparison.InvariantCultureIgnoreCase));
            } else if (CurrentSortMethod.Equals(SORTBY_TOKEN, StringComparison.InvariantCultureIgnoreCase)) {
                    _displayedKillProofs.Sort((e1, e2) =>
                        string.Compare(e1.BottomText, e2.BottomText, StringComparison.InvariantCultureIgnoreCase));
                    foreach (var e1 in _displayedKillProofs)
                        e1.Visible = _currentProfile.Tokens != null &&
                                     _currentProfile.Tokens.Any(x => x.Name.Equals(e1.Text, StringComparison.InvariantCultureIgnoreCase));
            } else if (CurrentSortMethod.Equals(SORTBY_TITLE, StringComparison.InvariantCultureIgnoreCase)) {
                    _displayedKillProofs.Sort((e1, e2) =>
                        string.Compare(e1.BottomText, e2.BottomText, StringComparison.InvariantCultureIgnoreCase));
                    foreach (var e1 in _displayedKillProofs) e1.Visible = e1.IsTitleDisplay;
            } else if (CurrentSortMethod.Equals(SORTBY_FRACTAL, StringComparison.InvariantCultureIgnoreCase)) {
                    _displayedKillProofs.Sort((e1, e2) =>
                        string.Compare(e1.Text, e2.Text, StringComparison.InvariantCultureIgnoreCase));
                    foreach (var e1 in _displayedKillProofs) e1.Visible = e1.BottomText.ToLower().Contains("fractal");
            } else if (CurrentSortMethod.Equals(SORTBY_RAID, StringComparison.InvariantCultureIgnoreCase)) {
                    _displayedKillProofs.Sort((e1, e2) =>
                        string.Compare(e1.Text, e2.Text, StringComparison.InvariantCultureIgnoreCase));
                    foreach (var e1 in _displayedKillProofs) e1.Visible = e1.BottomText.ToLower().Contains("raid");
            }
            RepositionKillProofs();
        }


        private void RepositionKillProofs()
        {
            var pos = 0;
            foreach (var e in _displayedKillProofs)
            {
                var x = pos % 3;
                var y = pos / 3;
                e.Location = new Point(x * (e.Width + 8), y * (e.Height + 8));

                ((Panel) e.Parent).VerticalScrollOffset = 0;
                e.Parent.Invalidate();
                if (e.Visible) pos++;
            }
        }


        private void RepositionPlayers()
        {
            var sorted = from player in _displayedPlayers
                orderby player.IsNew descending
                select player;

            var pos = 0;
            foreach (var e in sorted)
            {
                var x = pos % 3;
                var y = pos / 3;
                e.Location = new Point(x * (e.Width + 8), y * (e.Height + 8));

                ((Panel) e.Parent).VerticalScrollOffset = 0;
                e.Parent.Invalidate();
                if (e.Visible) pos++;
            }
        }


        private void BuildSmartPingMenu()
        {
            _smartPingMenu?.Dispose();

            if (!SmartPingMenuEnabled.Value || !IsUiAvailable() || _myKillProof == null) return;

            _smartPingMenu = new Panel
            {
                Parent = Graphics.SpriteScreen,
                Location = new Point(10, 38),
                Size = new Point(400, 40),
                Opacity = 0.0f,
                ShowBorder = true
            };

            _smartPingMenu.Resized += delegate { _smartPingMenu.Location = new Point(10, 38); };

            _smartPingMenu.MouseEntered += delegate { Animation.Tweener.Tween(_smartPingMenu, new {Opacity = 1.0f}, 0.45f); };

            var leftBracket = new Label
            {
                Parent = _smartPingMenu,
                Size = _smartPingMenu.Size,
                Font = Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size20, ContentService.FontStyle.Regular),
                Text = "[",
                Location = new Point(0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            var quantity = new Label
            {
                Parent = _smartPingMenu,
                Size = new Point(30, 30),
                Location = new Point(10, -2)
            };
            var dropdown = new Dropdown
            {
                Parent = _smartPingMenu,
                Size = new Point(260, 20),
                Location = new Point(quantity.Right + 2, 3),
                SelectedItem = LoadingLabel
            };
            _smartPingMenu.MouseLeft += delegate
            {
                //TODO: Check for when dropdown IsExpanded
                Animation.Tweener.Tween(_smartPingMenu, new {Opacity = 0.4f}, 0.45f);
            };
            var rightBracket = new Label
            {
                Parent = _smartPingMenu,
                Size = new Point(10, _smartPingMenu.Height),
                Font = Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size20, ContentService.FontStyle.Regular),
                Text = "]",
                Location = new Point(dropdown.Right, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            var tokenStringSorted = new List<string>();
            foreach (var token in _myKillProof.GetAllTokens())
            {
                var wing = _resources.GetWing(token);
                if (wing != null)
                    tokenStringSorted.Add($"W{_resources.GetAllWings().ToList().IndexOf(wing) + 1} | {token.Name}");
                else
                    tokenStringSorted.Add(token.Name);
            }
            tokenStringSorted.Sort((e1, e2) => string.Compare(e1, e2, StringComparison.InvariantCultureIgnoreCase));
            foreach (var tokenString in tokenStringSorted) {
                dropdown.Items.Add(tokenString);
            }

            dropdown.ValueChanged += delegate (object o, ValueChangedEventArgs e)
            {
                quantity.Text = _myKillProof?.GetToken(e.CurrentValue)?.Amount.ToString() ?? "";
                SPM_DropdownSelection.Value = e.CurrentValue;
            };

            var oldSelection = dropdown.Items.FirstOrDefault(x => x.Equals(SPM_DropdownSelection.Value, StringComparison.InvariantCultureIgnoreCase));
            dropdown.SelectedItem = oldSelection ?? (dropdown.Items.Count > 0 ? dropdown.Items[0] : "");

            var sendButton = new Image
            {
                Parent = _smartPingMenu,
                Size = new Point(24, 24),
                Location = new Point(rightBracket.Right + 1, 0),
                Texture = Content.GetTexture("784268"),
                SpriteEffects = SpriteEffects.FlipHorizontally,
                BasicTooltipText = SmartPingMenuSendButtonTooltip
            };
            var randomizeButton = new StandardButton
            {
                Parent = _smartPingMenu,
                Size = new Point(29, 24),
                Location = new Point(sendButton.Right + 7, 0),
                Text = SPM_WingSelection.Value,
                BackgroundColor = Color.Gray,
                BasicTooltipText = SmartPingMenuRandomizeButtonTooltip
            };

            randomizeButton.PropertyChanged += delegate (object o, PropertyChangedEventArgs e) {
                if (!e.PropertyName.Equals(nameof(StandardButton.Text))) return;
                SPM_WingSelection.Value = randomizeButton.Text;
            };

            randomizeButton.LeftMouseButtonPressed += delegate
            {
                randomizeButton.Size = new Point(27, 22);
                randomizeButton.Location = new Point(sendButton.Right + 5, 2);
            };

            randomizeButton.LeftMouseButtonReleased += delegate
            {
                randomizeButton.Size = new Point(29, 24);
                randomizeButton.Location = new Point(sendButton.Right + 7, 0);
                randomizeButton.BackgroundColor =
                    randomizeButton.BackgroundColor == Color.Gray ? Color.LightGreen : Color.Gray;
            };
            randomizeButton.RightMouseButtonPressed += delegate
            {
                randomizeButton.Size = new Point(27, 22);
                randomizeButton.Location = new Point(sendButton.Right + 5, 2);
            };
            randomizeButton.RightMouseButtonReleased += delegate
            {
                randomizeButton.Size = new Point(29, 24);
                randomizeButton.Location = new Point(sendButton.Right + 7, 0);
                var allWings = _resources.GetAllWings().ToList();
                var current = _resources.GetWing(randomizeButton.Text);
                var wingIndex = allWings.IndexOf(current) + 1;
                var next = wingIndex + 1 <= allWings.Count() ? wingIndex + 1 : 1;
                randomizeButton.Text = $"W{next}";
            };
            sendButton.LeftMouseButtonPressed += delegate
            {
                sendButton.Size = new Point(22, 22);
                sendButton.Location = new Point(rightBracket.Right + 3, 2);
            };

            sendButton.LeftMouseButtonReleased += delegate
            {
                sendButton.Size = new Point(24, 24);
                sendButton.Location = new Point(rightBracket.Right + 1, 0);

                if (Gw2Mumble.UI.IsTextInputFocused) return;

                var cooldown = DateTimeOffset.Now.Subtract(_smartPingCooldownSend);
                if (cooldown.TotalSeconds < 1) {
                    ScreenNotification.ShowNotification("Your total has been reached. Cooling down.", ScreenNotification.NotificationType.Error);
                    return;
                }

                if (randomizeButton.BackgroundColor == Color.LightGreen)
                {
                    var wing = _resources.GetWing(randomizeButton.Text);
                    var wingTokens = wing.GetTokens();
                    var tokenSelection = _myKillProof.GetAllTokens().Where(x => wingTokens.Any(y => y.Id.Equals(x.Id))).ToList();
                    if (tokenSelection.Count == 0) return;

                    DoSmartPing(tokenSelection.ElementAt(RandomUtil.GetRandom(0, tokenSelection.Count - 1)));

                } else {

                    var token = _myKillProof.GetToken(dropdown.SelectedItem);
                    if (token == null) return;
                    DoSmartPing(token);
                }
            };

            sendButton.RightMouseButtonPressed += delegate
            {
                sendButton.Size = new Point(22, 22);
                sendButton.Location = new Point(rightBracket.Right + 3, 2);
            };

            var timeOutRightSend = new Dictionary<int, DateTimeOffset>();

            sendButton.RightMouseButtonReleased += delegate
            {
                sendButton.Size = new Point(24, 24);
                sendButton.Location = new Point(rightBracket.Right + 1, 0);

                if (_myKillProof == null) return;

                var chatLink = new ItemChatLink();

                if (randomizeButton.BackgroundColor == Color.LightGreen)
                {
                    var wing = _resources.GetWing(randomizeButton.Text);
                    var wingTokens = wing.GetTokens();
                    var tokenSelection = _myKillProof.GetAllTokens().Where(x => wingTokens.Any(y => y.Id.Equals(x.Id))).ToList();
                    if (tokenSelection.Count == 0) return;
                    var singleRandomToken = tokenSelection.ElementAt(RandomUtil.GetRandom(0, tokenSelection.Count - 1));
                    chatLink.ItemId = singleRandomToken.Id;
                    if (timeOutRightSend.Any(x => x.Key == chatLink.ItemId))
                    {
                        var cooldown = DateTimeOffset.Now.Subtract(timeOutRightSend[chatLink.ItemId]);
                        if (cooldown.TotalMinutes < 2)
                        {
                            var timeLeft = TimeSpan.FromMinutes(2 - cooldown.TotalMinutes);
                            var minuteWord = timeLeft.TotalSeconds > 119 ? $" {timeLeft:%m} minutes and" :
                                timeLeft.TotalSeconds > 59 ? $" {timeLeft:%m} minute and" : "";
                            var secondWord = timeLeft.Seconds > 9 ? $"{timeLeft:ss} seconds" :
                                timeLeft.Seconds > 1 ? $"{timeLeft:%s} seconds" : $"{timeLeft:%s} second";
                            ScreenNotification.ShowNotification(
                                $"You can't send your {singleRandomToken.Name} total\nwithin the next{minuteWord} {secondWord} again.",
                                ScreenNotification.NotificationType.Error);
                            return;
                        }

                        timeOutRightSend[chatLink.ItemId] = DateTimeOffset.Now;
                    } else {
                        timeOutRightSend.Add(chatLink.ItemId, DateTimeOffset.Now);
                    }

                    chatLink.Quantity = Convert.ToByte(1);
                    GameIntegration.Chat.Send($"Total: {_myKillProof.GetToken(singleRandomToken.Id)?.Amount ?? 0} of {chatLink} (killproof.me/{_myKillProof.KpId})");

                } else {

                    var token = _myKillProof.GetToken(dropdown.SelectedItem);
                    if (token == null) return;
                    chatLink.ItemId = token.Id;
                    if (timeOutRightSend.Any(x => x.Key == chatLink.ItemId))
                    {
                        var cooldown = DateTimeOffset.Now.Subtract(timeOutRightSend[chatLink.ItemId]);
                        if (cooldown.TotalMinutes < 2)
                        {
                            var timeLeft = TimeSpan.FromMinutes(2 - cooldown.TotalMinutes);
                            var minuteWord = timeLeft.TotalSeconds > 119 ? $" {timeLeft:%m} minutes and" :
                                timeLeft.TotalSeconds > 59 ? $" {timeLeft:%m} minute and" : "";
                            var secondWord = timeLeft.Seconds > 9 ? $"{timeLeft:ss} seconds" :
                                timeLeft.Seconds > 1 ? $"{timeLeft:%s} seconds" : $"{timeLeft:%s} second";
                            ScreenNotification.ShowNotification(
                                $"You can't send your {token.Name} total\nwithin the next{minuteWord} {secondWord} again.",
                                ScreenNotification.NotificationType.Error);
                            return;
                        }

                        timeOutRightSend[chatLink.ItemId] = DateTimeOffset.Now;

                    } else {

                        timeOutRightSend.Add(chatLink.ItemId, DateTimeOffset.Now);

                    }

                    chatLink.Quantity = Convert.ToByte(1);
                    GameIntegration.Chat.Send(SmartPingMenuRightclickSendMessage.Replace("{0}", token.Amount.ToString()).Replace("{1}", chatLink.ToString()).Replace("{2}", _myKillProof.KpId));
                }
            };
            _smartPingMenu.Disposed += delegate { Animation.Tweener.Tween(_smartPingMenu, new {Opacity = 0.0f}, 0.2f); };
            quantity.Text = _myKillProof.GetToken(dropdown.SelectedItem)?.Amount.ToString() ?? "0";

            Animation.Tweener.Tween(_smartPingMenu, new {Opacity = 0.4f}, 0.35f);
            return;
        }

        #endregion

        #endregion
    }
}