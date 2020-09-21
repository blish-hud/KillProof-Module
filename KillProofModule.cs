using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.ArcDps.Common;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Flurl.Http;
using Gw2Sharp.ChatLinks;
using Gw2Sharp.Models;
using Gw2Sharp.WebApi.V2.Models;
using KillProofModule.Controls;
using KillProofModule.Persistance;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Color = Microsoft.Xna.Framework.Color;

namespace KillProofModule
{
    [Export(typeof(Module))]
    public class KillProofModule : Module
    {
        private const int TOP_MARGIN = 0;
        private const int RIGHT_MARGIN = 5;
        private const int BOTTOM_MARGIN = 10;
        private const int LEFT_MARGIN = 8;

        // Max profile buttons on SquadPanel before dequeuing FiFo behavior.
        private const int MAX_PLAYERS = 15;

        private const string KILLPROOF_API_URL = "https://killproof.me/api/";

        private static readonly Logger Logger = Logger.GetLogger(typeof(KillProofModule));

        internal static KillProofModule ModuleInstance;

        private List<KillProof> _cachedKillProofs;
        private KillProof _currentProfile;
        private List<KillProofButton> _displayedKillProofs;

        private Queue<PlayerButton> _displayedPlayers;
        private Panel _killProofQuickMenu;

        private SettingEntry<bool> _killProofQuickMenuEnabled;

        private WindowTab _killProofTab;
        private Panel _modulePanel;
        private PlayerButton _localPlayerButton;
        private KillProof _myKillProof;

        private Resources _resources;
        private Checkbox _smartPingCheckBox;

        private Panel _squadPanel;
        private string CurrentSortMethod;
        private Dictionary<int, AsyncTexture2D> EliteRenderRepository;
        private readonly Point LABEL_BIG = new Point(400, 40);
        private readonly Point LABEL_SMALL = new Point(400, 30);
        private Dictionary<int, AsyncTexture2D> ProfessionRenderRepository;

        // Caches
        private Dictionary<int, AsyncTexture2D> TokenRenderRepository;

        [ImportingConstructor]
        public KillProofModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters)
        {
            ModuleInstance = this;
        }

        protected override void DefineSettings(SettingCollection settings)
        {
            _killProofQuickMenuEnabled = settings.DefineSetting("KillProofQuickMenuEnabled", false);
        }

        #region Localization
        private string SORTBY_ALL;
        private string SORTBY_KILLPROOF;
        private string SORTBY_TOKEN;
        private string SORTBY_TITLE;
        private string SORTBY_RAID;
        private string SORTBY_FRACTAL;
        private string SmartPingMenuSettingDisplayName;
        private string SmartPingMenuSettingDescription;
        private string KillProofTabName;
        private string NewVersionFound;
        private string NotificationProfileAvailable;
        private string SmartPingMenuToggleCheckboxText;
        private string SmartPingMenuCheckboxTooltip;
        private string StartedBlishHUDWhileGw2AlreadyRunning;
        private string RefreshMapToSeeYourProfile;
        private string SearchBoxText;
        private string RecentProfileText;
        private string PoweredByText;
        private string LastRefreshText;
        private string UpdateAvailableVisitText;
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
        private void ChangeLocalization(object sender, EventArgs e)
        {
            SmartPingMenuSettingDisplayName = Properties.Resources.Kill_Proof_Smart_Ping_Menu;
            SmartPingMenuSettingDescription = Properties.Resources.Quick_access_to_ping_kill_proofs_;
            KillProofTabName = Properties.Resources.KillProof;
            NewVersionFound = Properties.Resources.A_new_version_of_the_KillProof_module_was_found_;
            NotificationProfileAvailable = Properties.Resources.profile_available;
            SmartPingMenuToggleCheckboxText = Properties.Resources.Show_Smart_Ping_Menu;
            SmartPingMenuCheckboxTooltip = Properties.Resources.Shows_a_menu_on_the_top_left_corner_of_your_screen_which_allows_you_to_quickly_access_and_ping_your_killproofs_;
            StartedBlishHUDWhileGw2AlreadyRunning = Properties.Resources.You_started_Blish_HUD_while_Guild_Wars_2_was_already_running_;
            RefreshMapToSeeYourProfile = Properties.Resources.Refresh_map_to_see_your_profile_;
            SearchBoxText = Properties.Resources.Account_Name_or_KillProof_me_ID_;
            RecentProfileText = Properties.Resources.Recent_profiles_;
            PoweredByText = Properties.Resources.Powered_by_www_killproof_me;
            LastRefreshText = Properties.Resources.Last_Refresh_;
            UpdateAvailableVisitText = Properties.Resources.Update_available__Visit_killproof_me_addons;
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

            SORTBY_ALL = Properties.Resources.Everything;
            SORTBY_KILLPROOF = Properties.Resources.KillProof;
            SORTBY_TOKEN = Properties.Resources.Tokens;
            SORTBY_TITLE = Properties.Resources.Titles;
            SORTBY_RAID = Properties.Resources.Raid_Titles;
            SORTBY_FRACTAL = Properties.Resources.Fractal_Titles;
            CurrentSortMethod = SORTBY_ALL;

            LoadResources();

            _killProofQuickMenu?.Dispose();
            if (_killProofQuickMenuEnabled.Value && _myKillProof != null)
                _killProofQuickMenu = BuildKillProofQuickMenu();

            _modulePanel?.Dispose();
            _modulePanel = BuildHomePanel(GameService.Overlay.BlishHudWindow);

            if (_killProofTab != null)
                GameService.Overlay.BlishHudWindow.RemoveTab(_killProofTab);

            _killProofTab = GameService.Overlay.BlishHudWindow.AddTab("KillProof", _killProofIconTexture, _modulePanel, 0);
        }
        #endregion

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

        protected override void Initialize()
        {
            TokenRenderRepository = new Dictionary<int, AsyncTexture2D>();
            EliteRenderRepository = new Dictionary<int, AsyncTexture2D>();
            ProfessionRenderRepository = new Dictionary<int, AsyncTexture2D>();
            _displayedKillProofs = new List<KillProofButton>();
            _displayedPlayers = new Queue<PlayerButton>();
            _cachedKillProofs = new List<KillProof>();

            LoadTextures();
            GameService.Overlay.UserLocaleChanged += ChangeLocalization;

            GameService.ArcDps.Common.Activate();
        }

        protected override async Task LoadAsync()
        {
            await Task.Run(LoadResources);
            await Task.Run(LoadProfessionIcons);
            await Task.Run(LoadEliteIcons);
        }

        private async void LoadResources()
        {
            await GetJsonResponse<Resources>(KILLPROOF_API_URL + "resources?lang=" + GameService.Overlay.UserLocale.Value)
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
                    await Task.Run(LoadTokenIcons);
                });
        }
        protected override void OnModuleLoaded(EventArgs e)
        {
            ChangeLocalization(null, null);
            GameService.ArcDps.Common.PlayerAdded += PlayerAddedEvent;

            // Base handler must be called
            base.OnModuleLoaded(e);
        }

        protected override void Update(GameTime gameTime)
        {
            if (_localPlayerButton != null) _localPlayerButton.Parent.Visible = GameService.ArcDps.RenderPresent;
            if (_smartPingCheckBox != null) _smartPingCheckBox.Visible = GameService.ArcDps.RenderPresent;
            if (_killProofQuickMenu != null)
                _killProofQuickMenu.Visible = GameService.GameIntegration.IsInGame &&
                                              GameService.ArcDps.Common.PlayersInSquad.Count != 0;
        }

        private async Task<(bool, T)> GetJsonResponse<T>(string request)
        {
            try
            {
                var rawJson = await request.AllowHttpStatus(HttpStatusCode.NotFound).GetStringAsync();

                return (true, JsonConvert.DeserializeObject<T>(rawJson));
            }
            catch (FlurlHttpTimeoutException ex)
            {
                Logger.Warn(ex, $"Request '{request}' timed out.");
            }
            catch (FlurlHttpException ex)
            {
                Logger.Warn(ex, $"Request '{request}' was not successful.");
            }
            catch (JsonReaderException ex)
            {
                Logger.Warn(ex, $"Failed to read JSON response returned by request '{request}' which returned ''");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Unexpected error while requesting '{request}'.");
            }

            return (false, default);
        }

        /// <inheritdoc />
        protected override void Unload()
        {
            GameService.Overlay.UserLocaleChanged -= ChangeLocalization;
            _killProofQuickMenu?.Dispose();
            _squadPanel?.Dispose();
            _localPlayerButton?.Dispose();
            foreach (var c in _displayedKillProofs) c?.Dispose();
            GameService.Overlay.BlishHudWindow.RemoveTab(_killProofTab);
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

        private async Task<bool> IsLatestVersion()
        {
            var (responseSuccess, remoteManifest) =
                await GetJsonResponse<Manifest>(
                    "https://raw.githubusercontent.com/blish-hud/KillProof-Module/master/manifest.json");
            if (responseSuccess)
            {
                if (ModuleInstance.Version >= remoteManifest.Version)
                    return true;
                Logger.Warn(NewVersionFound + ' ' + remoteManifest.Version.Clean());
            }
            else
            {
                Logger.Info("Failed to check for new version.");
            }
            return false;
        }

        #region Render Getters

        private async void LoadTokenIcons()
        {
            var tokenRenderUrlRepository = _resources.GetAllTokens();
            foreach (var token in tokenRenderUrlRepository)
            {
                var renderUri = token.Icon;
                if (TokenRenderRepository.Any(x => x.Key == token.Id))
                    try
                    {
                        var textureDataResponse =
                            await Gw2ApiManager.Gw2ApiClient.Render.DownloadToByteArrayAsync(renderUri);

                        using (var textureStream = new MemoryStream(textureDataResponse))
                        {
                            var loadedTexture =
                                Texture2D.FromStream(GameService.Graphics.GraphicsDevice, textureStream);

                            TokenRenderRepository[token.Id].SwapTexture(loadedTexture);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"Request to render service for {renderUri} failed.", renderUri);
                    }
                else
                    TokenRenderRepository.Add(token.Id, GameService.Content.GetRenderServiceTexture(renderUri));
            }
        }

        private async Task<IReadOnlyList<Profession>> LoadProfessions()
        {
            return await Gw2ApiManager.Gw2ApiClient.V2.Professions.ManyAsync(Enum.GetValues(typeof(ProfessionType))
                .Cast<ProfessionType>());
        }

        private async void LoadProfessionIcons()
        {
            var professions = await LoadProfessions();
            foreach (var profession in professions)
            {
                var renderUri = (string) profession.IconBig;
                var id = (int) Enum.GetValues(typeof(ProfessionType)).Cast<ProfessionType>().ToList()
                    .Find(x => x.ToString().Equals(profession.Id, StringComparison.InvariantCultureIgnoreCase));
                if (ProfessionRenderRepository.Any(x => x.Key == id))
                    try
                    {
                        var textureDataResponse =
                            await Gw2ApiManager.Gw2ApiClient.Render.DownloadToByteArrayAsync(renderUri);

                        using (var textureStream = new MemoryStream(textureDataResponse))
                        {
                            var loadedTexture =
                                Texture2D.FromStream(GameService.Graphics.GraphicsDevice, textureStream);

                            ProfessionRenderRepository[id].SwapTexture(loadedTexture);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"Request to render service for {renderUri} failed.", renderUri);
                    }
                else
                    ProfessionRenderRepository.Add(id, GameService.Content.GetRenderServiceTexture(renderUri));
            }
        }

        private async void LoadEliteIcons()
        {
            var ids = await Gw2ApiManager.Gw2ApiClient.V2.Specializations.IdsAsync();
            var specializations = await Gw2ApiManager.Gw2ApiClient.V2.Specializations.ManyAsync(ids);
            foreach (var specialization in specializations)
            {
                if (!specialization.Elite) continue;
                if (EliteRenderRepository.Any(x => x.Key == specialization.Id))
                {
                    var renderUri = (string) specialization.ProfessionIconBig;
                    try
                    {
                        var textureDataResponse =
                            await Gw2ApiManager.Gw2ApiClient.Render.DownloadToByteArrayAsync(renderUri);

                        using (var textureStream = new MemoryStream(textureDataResponse))
                        {
                            var loadedTexture =
                                Texture2D.FromStream(GameService.Graphics.GraphicsDevice,
                                    textureStream);

                            EliteRenderRepository[specialization.Id].SwapTexture(loadedTexture);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"Request to render service for {renderUri} failed.", renderUri);
                    }
                }
                else
                {
                    EliteRenderRepository.Add(specialization.Id,
                        GameService.Content.GetRenderServiceTexture(specialization.ProfessionIconBig));
                }
            }
        }

        private AsyncTexture2D GetProfessionRender(CommonFields.Player player)
        {
            if (!ProfessionRenderRepository.Any(x => x.Key.Equals((int) player.Profession)))
            {
                var render = new AsyncTexture2D();
                ProfessionRenderRepository.Add((int) player.Profession, render);
            }

            return ProfessionRenderRepository[(int) player.Profession];
        }

        private AsyncTexture2D GetEliteRender(CommonFields.Player player)
        {
            if (player.Elite == 0) return GetProfessionRender(player);
            if (!EliteRenderRepository.Any(x => x.Key.Equals((int) player.Elite)))
            {
                var render = new AsyncTexture2D();
                try
                {
                    EliteRenderRepository.Add((int) player.Elite, render);
                }
                catch (ArgumentException e)
                {
                    Logger.Warn(e.Message + e.StackTrace);
                }
            }

            return EliteRenderRepository[(int) player.Elite];
        }

        private AsyncTexture2D GetTokenRender(int key)
        {
            if (TokenRenderRepository.All(x => x.Key != key))
            {
                var render = new AsyncTexture2D();
                try
                {
                    TokenRenderRepository.Add(key, render);
                }
                catch (ArgumentException e)
                {
                    Logger.Warn(e.Message + e.StackTrace);
                }
            }

            return TokenRenderRepository[key];
        }

        #endregion

        private async Task<KillProof> GetKillProofContent(string account)
        {
            if (_cachedKillProofs.Any(x => x.AccountName.Equals(account, StringComparison.InvariantCultureIgnoreCase)))
                return _cachedKillProofs.FirstOrDefault(x =>
                    x.AccountName.Equals(account, StringComparison.InvariantCultureIgnoreCase));

            var (responseSuccess, killProof) = await GetJsonResponse<KillProof>(KILLPROOF_API_URL + $"kp/{account}?lang=" + GameService.Overlay.UserLocale.Value)
                .ConfigureAwait(false);

            if (responseSuccess && killProof?.Error == null)
            {
                _cachedKillProofs.Add(killProof);
                return killProof;
            }
            return null;
        }

        #region Panel Related Stuff

        private async Task<bool> ProfileAvailable(string account)
        {
            var (responseSuccess, optionalKillProof) =
                await GetJsonResponse<KillProof>(KILLPROOF_API_URL + $"kp/{account}?lang=" + GameService.Overlay.UserLocale.Value);

            return responseSuccess && optionalKillProof?.Error == null;
        }

        private void PlayerAddedEvent(CommonFields.Player player)
        {
            if (player.Self && _localPlayerButton != null)
            {
                _localPlayerButton.Player = player;
                _localPlayerButton.Icon = GetEliteRender(player);
                _localPlayerButton.LeftMouseButtonPressed += delegate
                {
                    GameService.Overlay.BlishHudWindow.Navigate(
                        BuildKillProofPanel(GameService.Overlay.BlishHudWindow, player));
                };
                if (_myKillProof == null) LoadMyKillProof();
                return;
            }

            if (_displayedPlayers.Any(x =>
                    x.Player.AccountName.Equals(player.AccountName, StringComparison.InvariantCultureIgnoreCase))
                || !ProfileAvailable(player.AccountName).Result) return;

            PlayerNotification.ShowNotification(player.AccountName, GetEliteRender(player), NotificationProfileAvailable, 10);

            var optionalButton = _displayedPlayers.FirstOrDefault(x =>
                x.Player.AccountName.Equals(player.AccountName, StringComparison.InvariantCultureIgnoreCase));

            if (optionalButton == null)
            {
                if (_displayedPlayers.Count() == MAX_PLAYERS) _displayedPlayers.Dequeue().Dispose();

                var playerButton = new PlayerButton
                {
                    Parent = _squadPanel,
                    Player = player,
                    Icon = GetEliteRender(player),
                    Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia,
                        ContentService.FontSize.Size16, ContentService.FontStyle.Regular)
                };
                playerButton.LeftMouseButtonPressed += delegate
                {
                    playerButton.IsNew = false;
                    GameService.Overlay.BlishHudWindow.Navigate(BuildKillProofPanel(GameService.Overlay.BlishHudWindow,
                        playerButton.Player));
                };
                _displayedPlayers.Enqueue(playerButton);
            }
            else
            {
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

            if (GameService.ArcDps.Loaded)
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
                _smartPingCheckBox = new Checkbox
                {
                    Parent = header,
                    Location = new Point(selfButtonPanel.Location.X + LEFT_MARGIN, selfButtonPanel.Bottom),
                    Size = new Point(selfButtonPanel.Width, 30),
                    Text = SmartPingMenuToggleCheckboxText,
                    BasicTooltipText = SmartPingMenuCheckboxTooltip,
                    Checked = _killProofQuickMenuEnabled.Value
                };
                _smartPingCheckBox.CheckedChanged += delegate(object sender, CheckChangedEvent e)
                {
                    _killProofQuickMenuEnabled.Value = e.Checked;
                    if (e.Checked && _myKillProof != null)
                        _killProofQuickMenu = BuildKillProofQuickMenu();
                    else
                        _killProofQuickMenu?.Dispose();
                };
                _localPlayerButton = new PlayerButton
                {
                    Parent = selfButtonPanel,
                    Player = new CommonFields.Player(StartedBlishHUDWhileGw2AlreadyRunning,RefreshMapToSeeYourProfile, 0, 0, true),
                    Icon = GameService.Content.GetTexture("common/733268"),
                    IsNew = false,
                    Location = new Point(0, 0),
                    Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia,
                        ContentService.FontSize.Size16,
                        ContentService.FontStyle.Regular)
                };
            }

            var imgKillproof = new Image(_killProofMeLogoTexture)
            {
                Parent = header,
                Size = new Point(128, 128),
                Location = new Point(LEFT_MARGIN + 10, TOP_MARGIN + 5)
            };
            var labAccountName = new Label
            {
                Parent = header,
                Size = new Point(200, 30),
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
                if (!string.Equals(tbAccountName.Text, "") &&
                    !Regex.IsMatch(tbAccountName.Text, @"[^a-zA-Z0-9.\s]|^\.*$"))
                    wndw.Navigate(BuildKillProofPanel(wndw,
                        new CommonFields.Player(null, tbAccountName.Text, 0, 0, false)));
                tbAccountName.Focused = false;
            };
            var labSquadPanel = new Label
            {
                Parent = header,
                Size = new Point(300, 40),
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size24,
                    ContentService.FontStyle.Regular),
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
            var checkUpdate = Task.Run(() => IsLatestVersion());
            checkUpdate.Wait();
            var versionLabel = new Label
            {
                Parent = footer,
                Size = footer.Size,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                StrokeText = true,
                ShowShadow = true,
                Text = checkUpdate.Result
                    ? ModuleInstance.Version.Clean()
                    : UpdateAvailableVisitText,
                TextColor = checkUpdate.Result ? Color.White : Color.Red
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

        private void FinishLoadingKillProofPanel(WindowBase wndw, Panel hPanel, CommonFields.Player player,
            KillProof currentAccount)
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
                    Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia,
                        ContentService.FontSize.Size36, ContentService.FontStyle.Regular),
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
                    Texture = GameService.Content.GetTexture("255369"),
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
                    Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size11,
                        ContentService.FontStyle.Regular),
                    Text = ""
                };
                var currentAccountProofUrl = new Label
                {
                    Parent = footer,
                    Size = LABEL_SMALL,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Location = new Point(LEFT_MARGIN, currentAccountKpId.Location.Y + BOTTOM_MARGIN + 2),
                    Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size11,
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
                currentAccountLastRefresh.Text =
                    LastRefreshText + $" {currentAccount.LastRefresh:dddd, d. MMMM yyyy - HH:mm:ss}";
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
                            Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia,
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
                            Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia,
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
                            Font = GameService.Content.DefaultFont16,
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

                if (!player.Self && !player.AccountName.Equals(_localPlayerButton.Player.AccountName,
                                     StringComparison.InvariantCultureIgnoreCase)
                                 && !_displayedPlayers.Any(x =>
                                     x.Player.AccountName.Equals(player.AccountName,
                                         StringComparison.InvariantCultureIgnoreCase)))
                {
                    if (_displayedPlayers.Count == MAX_PLAYERS) _displayedPlayers.Dequeue().Dispose();

                    var newPlayer = new CommonFields.Player(null, player.AccountName, 0, 0, false);
                    var playerButton = new PlayerButton
                    {
                        Parent = _squadPanel,
                        Player = newPlayer,
                        Icon = GameService.Content.GetTexture("733268"),
                        Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia,
                            ContentService.FontSize.Size16, ContentService.FontStyle.Regular),
                        IsNew = false
                    };
                    playerButton.LeftMouseButtonPressed += delegate
                    {
                        GameService.Overlay.BlishHudWindow.Navigate(
                            BuildKillProofPanel(GameService.Overlay.BlishHudWindow, player));
                    };
                    _displayedPlayers.Enqueue(playerButton);
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
                    Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia,
                        ContentService.FontSize.Size36, ContentService.FontStyle.Regular),
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
                    Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia,
                        ContentService.FontSize.Size24, ContentService.FontStyle.Regular),
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
                wndw.ActivePanel = GameService.Overlay.BlishHudWindow.Panels[_killProofTab];
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

            pageLoading.Location = new Point(hPanel.Size.X / 2 - pageLoading.Size.X / 2,
                hPanel.Size.Y / 2 - pageLoading.Size.Y / 2);

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

        private async void LoadMyKillProof()
        {
            var player = GameService.ArcDps.Common.PlayersInSquad.First(x => x.Value.Self).Value;
            await GetKillProofContent(player.AccountName).ContinueWith((result) =>
            {
                if (!result.IsCompleted) return;
                _myKillProof = result.Result;
                if (_killProofQuickMenuEnabled.Value) _killProofQuickMenu = BuildKillProofQuickMenu();
            });
        }
        private Panel BuildKillProofQuickMenu()
        {
            var bgPanel = new Panel
            {
                Parent = GameService.Graphics.SpriteScreen,
                Location = new Point(10, 38),
                Size = new Point(400, 40),
                Opacity = 0.4f,
                Visible = false,
                ShowBorder = true
            };
            bgPanel.Resized += delegate { bgPanel.Location = new Point(10, 38); };
            bgPanel.MouseEntered += delegate
            {
                GameService.Animation.Tweener.Tween(bgPanel, new {Opacity = 1.0f}, 0.45f);
            };
            var leftBracket = new Label
            {
                Parent = bgPanel,
                Size = bgPanel.Size,
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size20,
                    ContentService.FontStyle.Regular),
                Text = "[",
                Location = new Point(0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            var quantity = new Label
            {
                Parent = bgPanel,
                Size = new Point(30, 30),
                Location = new Point(10, -2)
            };
            var dropdown = new Dropdown
            {
                Parent = bgPanel,
                Size = new Point(260, 20),
                Location = new Point(quantity.Right + 2, 3),
                SelectedItem = LoadingLabel
            };
            bgPanel.MouseLeft += delegate
            {
                //TODO: Check for when dropdown IsExpanded
                GameService.Animation.Tweener.Tween(bgPanel, new {Opacity = 0.4f}, 0.45f);
            };
            var rightBracket = new Label
            {
                Parent = bgPanel,
                Size = new Point(10, bgPanel.Height),
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size20,
                    ContentService.FontStyle.Regular),
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
            dropdown.ValueChanged += delegate
            {
                quantity.Text = _myKillProof?.GetToken(dropdown.SelectedItem)?.Amount.ToString() ?? "0";
            };
            dropdown.SelectedItem = dropdown.Items[0];
            var sendButton = new Image
            {
                Parent = bgPanel,
                Size = new Point(24, 24),
                Location = new Point(rightBracket.Right + 1, 0),
                Texture = GameService.Content.GetTexture("784268"),
                SpriteEffects = SpriteEffects.FlipHorizontally,
                BasicTooltipText = SmartPingMenuSendButtonTooltip
            };
            var randomizeButton = new StandardButton
            {
                Parent = bgPanel,
                Size = new Point(29, 24),
                Location = new Point(sendButton.Right + 7, 0),
                Text = "W1",
                BackgroundColor = Color.Gray,
                BasicTooltipText = SmartPingMenuRandomizeButtonTooltip
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
            var cooldownSend = DateTimeOffset.Now;
            var hotButtonTimeSend = DateTimeOffset.Now;
            var reduction = -1;
            var currentValue = 0;
            sendButton.LeftMouseButtonReleased += delegate
            {
                sendButton.Size = new Point(24, 24);
                sendButton.Location = new Point(rightBracket.Right + 1, 0);

                if (_myKillProof == null) return;

                var chatLink = new ItemChatLink();

                if (randomizeButton.BackgroundColor == Color.LightGreen)
                {
                    var cooldown = DateTimeOffset.Now.Subtract(cooldownSend);
                    if (cooldown.TotalSeconds < 2) {
                        ScreenNotification.ShowNotification("Your total has been reached. Cooling down.", ScreenNotification.NotificationType.Error);
                        return;
                    }
                    var wing = _resources.GetWing(randomizeButton.Text);
                    var wingTokens = wing.GetTokens();
                    var tokenSelection = _myKillProof.GetAllTokens().Where(x => wingTokens.Any(y => y.Id.Equals(x.Id))).ToList();
                    if (tokenSelection.Count == 0) return;
                    var singleRandomToken = tokenSelection.ElementAt(RandomUtil.GetRandom(0, tokenSelection.Count - 1));
                    chatLink.ItemId = singleRandomToken.Id;
                    var amount = _myKillProof.GetToken(singleRandomToken.Id)?.Amount ?? 0;
                    if (amount <= 250) {
                        chatLink.Quantity = Convert.ToByte(amount);
                        GameService.GameIntegration.Chat.Send(chatLink.ToString());
                        return;
                    }

                    var reductionTimes = amount / 250 - 1;
                    cooldown = DateTimeOffset.Now.Subtract(hotButtonTimeSend);

                    if (cooldown.TotalMilliseconds > 500) 
                    {
                        reduction = -1;
                        currentValue = 0;
                    }

                    if (reduction < reductionTimes)
                    {
                        reduction++;
                        amount = 250 - reduction;
                        chatLink.Quantity = Convert.ToByte(amount);
                        currentValue += amount;

                    } else {

                        chatLink.Quantity = Convert.ToByte(amount % currentValue);
                        reduction = -1;
                        currentValue = 0;
                        cooldownSend = DateTimeOffset.Now;
                    }
                    GameService.GameIntegration.Chat.Send(chatLink.ToString());
                    hotButtonTimeSend = DateTimeOffset.Now;
                }
                else
                {
                    var cooldown = DateTimeOffset.Now.Subtract(cooldownSend);
                    if (cooldown.TotalSeconds < 2) {
                        ScreenNotification.ShowNotification("Your total has been reached. Cooling down.", ScreenNotification.NotificationType.Error);
                        return;
                    }
                    var token = _myKillProof.GetToken(dropdown.SelectedItem);
                    if (token == null) return;
                    chatLink.ItemId = token.Id;
                    var amount = token.Amount;
                    if (amount <= 250) {
                        chatLink.Quantity = Convert.ToByte(amount);
                        GameService.GameIntegration.Chat.Send(chatLink.ToString());
                        return;
                    }

                    var reductionTimes = amount / 250 - 1;
                    cooldown = DateTimeOffset.Now.Subtract(hotButtonTimeSend);

                    if (cooldown.TotalMilliseconds > 500) 
                    {
                        reduction = -1;
                        currentValue = 0;
                    }

                    if (reduction < reductionTimes)
                    {
                        reduction++;
                        amount = 250 - reduction;
                        chatLink.Quantity = Convert.ToByte(amount);
                        currentValue += amount;

                    } else {

                        chatLink.Quantity = Convert.ToByte(amount % currentValue);
                        reduction = -1;
                        currentValue = 0;
                        cooldownSend = DateTimeOffset.Now;
                    }
                    GameService.GameIntegration.Chat.Send(chatLink.ToString());
                    hotButtonTimeSend = DateTimeOffset.Now;
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
                    }
                    else
                    {
                        timeOutRightSend.Add(chatLink.ItemId, DateTimeOffset.Now);
                    }

                    chatLink.Quantity = Convert.ToByte(1);
                    GameService.GameIntegration.Chat.Send(
                        $"Total: {_myKillProof.GetToken(singleRandomToken.Id)?.Amount ?? 0} of {chatLink} (killproof.me/{_myKillProof.KpId})");
                }
                else
                {
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
                    }
                    else
                    {
                        timeOutRightSend.Add(chatLink.ItemId, DateTimeOffset.Now);
                    }
                    chatLink.Quantity = Convert.ToByte(1);
                    GameService.GameIntegration.Chat.Send(SmartPingMenuRightclickSendMessage.Replace("{0}", token.Amount.ToString())
                                                                                            .Replace("{1}", chatLink.ToString())
                                                                                            .Replace("{2}", _myKillProof.KpId));
                }
            };
            bgPanel.Disposed += delegate { GameService.Animation.Tweener.Tween(bgPanel, new {Opacity = 0.0f}, 0.2f); };
            bgPanel.PropertyChanged += delegate
            {
                if (!bgPanel.Visible || _myKillProof == null) return;
                quantity.Text = _myKillProof.GetToken(dropdown.SelectedItem)?.Amount.ToString() ?? "0";
            };
            return bgPanel;
        }

        #endregion

        #endregion
    }
}