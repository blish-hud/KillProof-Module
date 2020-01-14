using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
using Flurl;
using Flurl.Http;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nekres.KillProof.Controls;

namespace Nekres.KillProof {

    [Export(typeof(Blish_HUD.Modules.Module))]
    public class KillProofModule : Blish_HUD.Modules.Module {

        private static readonly Logger Logger = Logger.GetLogger(typeof(KillProofModule));

        internal static KillProofModule ModuleInstance;

        #region Service Managers
        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;
        #endregion

        private const int       TOP_MARGIN    = 0;
        private const int       RIGHT_MARGIN  = 5;
        private const int       BOTTOM_MARGIN = 10;
        private const int       LEFT_MARGIN   = 8;
        private       Point     LABEL_SMALL   = new Point(400, 30);
        private       Point     LABEL_BIG     = new Point(400, 40);

        private const string SORTBY_ALL        = "Everything";
        private const string SORTBY_KILLPROOF  = "KillProofs";
        private const string SORTBY_TOKEN      = "Tokens";
        private const string SORTBY_TITLE      = "Titles";
        private const string SORTBY_RAID       = "Raid Titles";
        private const string SORTBY_FRACTAL    = "Fractal Titles";
        private       string CurrentSortMethod = SORTBY_ALL;

        // Caches
        private Dictionary<string, AsyncTexture2D> TokenRenderRepository;
        private Dictionary<uint, AsyncTexture2D>   EliteRenderRepository;
        private Dictionary<uint, AsyncTexture2D>   ProfessionRenderRepository;
        private string[]                           ProfessionRepository;

        // Max profile buttons on SquadPanel before dequeuing FiFo behavior.
        private const int MAX_PLAYERS = 15;

        private const string KILLPROOF_API_URL = "https://killproof.me/api/";

        private WindowTab KillProofTab;

        private List<KillProof> CachedKillProofs;
        private KillProof       CurrentProfile;

        private Panel        SquadPanel;
        private PlayerButton LocalPlayerButton;

        private Queue<PlayerButton>   DisplayedPlayers;
        private List<KillProofButton> DisplayedKillProofs;

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

        [ImportingConstructor]
        public KillProofModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) { ModuleInstance = this; }

        protected override void DefineSettings(SettingCollection settings) { /* NOOP */ }

        private void LoadTextures() {
            _killProofIconTexture   = ContentsManager.GetTexture("killproof_icon.png");
            _killProofMeLogoTexture = ContentsManager.GetTexture("killproof_logo.png");

            _deletedItemTexture = ContentsManager.GetTexture("deleted_item.png");

            _sortByWorldBossesTexture = ContentsManager.GetTexture("world-bosses.png");
            _sortByTokenTexture       = ContentsManager.GetTexture("icon_token.png");
            _sortByTitleTexture       = ContentsManager.GetTexture("icon_title.png");
            _sortByRaidTexture        = ContentsManager.GetTexture("icon_raid.png");
            _sortByFractalTexture     = ContentsManager.GetTexture("icon_fractal.png");

            _notificationBackroundTexture = ContentsManager.GetTexture("ns-button.png");
        }

        protected override void Initialize() {
            TokenRenderRepository      = new Dictionary<string, AsyncTexture2D>(StringComparer.InvariantCultureIgnoreCase);
            EliteRenderRepository      = new Dictionary<uint, AsyncTexture2D>();
            ProfessionRenderRepository = new Dictionary<uint, AsyncTexture2D>();
            DisplayedKillProofs        = new List<KillProofButton>();
            DisplayedPlayers           = new Queue<PlayerButton>();
            CachedKillProofs           = new List<KillProof>();

            LoadTextures();

            GameService.ArcDps.Common.Activate();
        }

        protected override async Task LoadAsync() {
            (bool responseSuccess, Dictionary<string, Url> tokenRenderUrlRepository) = await GetJsonResponse<Dictionary<string, Url>>(KILLPROOF_API_URL + "icons");

            if (responseSuccess) {
                foreach (KeyValuePair<string, Url> token in tokenRenderUrlRepository) {
                    TokenRenderRepository.Add(token.Key, GameService.Content.GetRenderServiceTexture(token.Value));
                }
            }
        }

        protected override void OnModuleLoaded(EventArgs e) {
            KillProofTab                          =  GameService.Overlay.BlishHudWindow.AddTab("KillProof", _killProofIconTexture, BuildHomePanel(GameService.Overlay.BlishHudWindow), 0);
            GameService.ArcDps.Common.PlayerAdded += PlayerAddedEvent;

            // Base handler must be called
            base.OnModuleLoaded(e);
        }

        protected override void Update(GameTime gameTime) {
            if (GameService.ArcDps.RenderPresent && LocalPlayerButton != null && !LocalPlayerButton.Visible) {
                LocalPlayerButton.Parent.Visible = true;
                LocalPlayerButton.Visible        = true;
            }
        }

        private async Task<(bool, T)> GetJsonResponse<T>(string request) {
            try {
                string rawJson = await request.AllowHttpStatus(HttpStatusCode.NotFound).GetStringAsync();

                return (true, JsonConvert.DeserializeObject<T>(rawJson));
            } catch (FlurlHttpTimeoutException ex) {
                Logger.Warn(ex, $"Request '{request}' timed out.");
            } catch (FlurlHttpException ex) {
                Logger.Warn(ex, $"Request '{request}' was not successful.");
            } catch (JsonReaderException ex) {
                Logger.Warn(ex, $"Failed to read JSON response returned by request '{request}' which returned ''");
            } catch (Exception ex) {
                Logger.Error(ex, $"Unexpected error while requesting '{request}'.");
            }

            return (false, default);
        }

        #region Module Logic

        private async Task<bool> IsLatestVersion() {
            (bool responseSuccess, var remoteManifest) = await GetJsonResponse<Manifest>("https://raw.githubusercontent.com/TybaIt/Community-Module-Pack/module-killproof/Kill%20Proof%20Module/manifest.json");

            if (responseSuccess) {
                if (ModuleInstance.Version >= remoteManifest.Version) {
                    return true;
                } else {
                    Logger.Warn($"A new version of the KillProof module was found: '{remoteManifest.Version.Clean()}'.");
                }
            } else {
                Logger.Info("Failed to check for new version.");
            }

            return false;
        }

        #region Render Getters

        private class ProfessionRender {

            [JsonProperty("icon_big")]
            public string ProfessionIconUrl;

        }

        private async Task<AsyncTexture2D> GetProfessionRender(CommonFields.Player player) {
            if (ProfessionRenderRepository.Any(x => x.Key.Equals(player.Profession))) {
                return ProfessionRenderRepository[player.Profession];
            } else {
                (bool profResponseSuccess, string[] professions) = await GetJsonResponse<string[]>("https://api.guildwars2.com/v2/professions");

                if (profResponseSuccess) {
                    ProfessionRepository = professions;

                    (bool responseSuccess, ProfessionRender pRender) = await GetJsonResponse<ProfessionRender>("https://api.guildwars2.com/v2/professions/" + ProfessionRepository[(int) player.Profession - 1]);

                    if (responseSuccess) {
                        var render = GameService.Content.GetRenderServiceTexture(pRender.ProfessionIconUrl);
                        ProfessionRenderRepository.Add(player.Profession, render);

                        return render;
                    }
                }

                return GameService.Content.GetTexture("733268");
            }
        }

        // TODO: Switch to Gw2Sharp to avoid deserialization workaround
        private class EliteSpecializationRender {

            [JsonProperty("profession_icon_big")]
            public string ProfessionIconUrl;

        }

        private async Task<AsyncTexture2D> GetEliteRender(CommonFields.Player player) {
            if (player.Elite == 0) { return GetProfessionRender(player).Result; }

            if (EliteRenderRepository.Any(x => x.Key.Equals(player.Elite))) {
                return EliteRenderRepository[player.Elite];
            }

            (bool responseSuccess, var esRender) = await GetJsonResponse<EliteSpecializationRender>("https://api.guildwars2.com/v2/specializations/" + player.Elite);

            if (responseSuccess) {
                var render = GameService.Content.GetRenderServiceTexture(esRender.ProfessionIconUrl);
                EliteRenderRepository.Add(player.Elite, render);
                return render;
            }

            return GameService.Content.GetTexture("733268");
        }

        private async Task<AsyncTexture2D> GetTokenRender(string key) {
            if (TokenRenderRepository.Any(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))) {
                return TokenRenderRepository[key];
            }

            return _deletedItemTexture;
        }

        #endregion

        private async Task<KillProof> GetKillProofContent(string account) {
            if (CachedKillProofs.Any(x => x.account_name.Equals(account, StringComparison.InvariantCultureIgnoreCase))) {
                return CachedKillProofs.FirstOrDefault(x => x.account_name.Equals(account, StringComparison.InvariantCultureIgnoreCase));
            } else {
                    (bool responseSuccess, var killProof) = await GetJsonResponse<KillProof>(KILLPROOF_API_URL + $"kp/{account}").ConfigureAwait(false);

                    if (responseSuccess && killProof?.error == null) {
                        CachedKillProofs.Add(killProof);
                        return killProof;
                    } else {
                        return null;
                    }
            }
        }

        #region Panel Related Stuff

        private async Task<bool> ProfileAvailable(string account) {
            (bool responseSuccess, var optionalKillProof) = await GetJsonResponse<KillProof>(KILLPROOF_API_URL + $"kp/{account}");

            return responseSuccess && optionalKillProof?.error == null;
        }

        private void PlayerAddedEvent(CommonFields.Player player) {
            if (player.Self) {
                LocalPlayerButton.Player = player;
                LocalPlayerButton.Icon = GetEliteRender(player).Result;
                LocalPlayerButton.LeftMouseButtonPressed += delegate {
                    GameService.Overlay.BlishHudWindow.Navigate(BuildKillProofPanel(GameService.Overlay.BlishHudWindow, player));
                };
                return;
            }

            if (DisplayedPlayers.Any(x => x.Player.AccountName.Equals(player.AccountName, StringComparison.InvariantCultureIgnoreCase))
                || !ProfileAvailable(player.AccountName).Result) { return; };

            PlayerNotification.ShowNotification(player.AccountName, GetEliteRender(player).Result, "profile available", 10);

            var optionalButton = DisplayedPlayers.FirstOrDefault(x => x.Player.AccountName.Equals(player.AccountName, StringComparison.InvariantCultureIgnoreCase));

            if (optionalButton == null) {
                if (DisplayedPlayers.Count() == MAX_PLAYERS) { DisplayedPlayers.Dequeue().Dispose(); }

                var playerButton = new PlayerButton() {
                    Parent = SquadPanel,
                    Player = player,
                    Icon = GetEliteRender(player).Result,
                    Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size16, ContentService.FontStyle.Regular)
                };
                playerButton.LeftMouseButtonPressed += delegate {
                    playerButton.IsNew = false;
                    GameService.Overlay.BlishHudWindow.Navigate(BuildKillProofPanel(GameService.Overlay.BlishHudWindow, playerButton.Player));
                };
                DisplayedPlayers.Enqueue(playerButton);
            } else {
                optionalButton.Player = player;
                optionalButton.Icon = GetEliteRender(player).Result;
            }
            RepositionPlayers();
        }

        private Panel BuildHomePanel(WindowBase wndw) {
            var hPanel = new Panel() {
                CanScroll = false,
                Size = wndw.ContentRegion.Size
            };
            /// ###################
            ///      <HEADER>
            /// ###################
            var header = new Panel() {
                Parent = hPanel,
                Size = new Point(hPanel.Width, 200),
                Location = new Point(0, 0),
                CanScroll = false
            };
            var selfButtonPanel = new Panel() {
                Parent = header,
                Size = new Point(335, 114),
                ShowBorder = true,
                ShowTint = true,
                Location = new Point(header.Right - 335 - KillProofModule.RIGHT_MARGIN, KillProofModule.TOP_MARGIN + 15),
                Visible = GameService.ArcDps.RenderPresent
            };
            LocalPlayerButton = new PlayerButton() {
                Parent = selfButtonPanel,
                Player = new CommonFields.Player("You started Blish HUD while Guild Wars 2 was already running.", "Refresh map to see your profile.", 0, 0, true),
                Icon = GameService.Content.GetTexture("733268"),
                IsNew = false,
                Location = new Point(0, 0),
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size16, ContentService.FontStyle.Regular),
                Visible = GameService.ArcDps.RenderPresent
            };
            var img_killproof = new Image(_killProofMeLogoTexture) {
                Parent = header,
                Size = new Point(128, 128),
                Location = new Point(KillProofModule.LEFT_MARGIN + 10, KillProofModule.TOP_MARGIN + 5)
            };
            var lab_account_name = new Label() {
                Parent = header,
                Size = new Point(200, 30),
                Location = new Point(header.Width / 2 - 100, header.Height / 2 + 30 + KillProofModule.TOP_MARGIN),
                StrokeText = true,
                ShowShadow = true,
                Text = "Account Name or KillProof.me-ID:"
            };
            // Encapsule TextBox because not thread safe.
            GameService.Overlay.QueueMainThreadUpdate((gameTime) => {
                var tb_account_name = new TextBox() {
                    Parent = header,
                    Size = new Point(200, 30),
                    Location = new Point(header.Width / 2 - 100, lab_account_name.Bottom + KillProofModule.TOP_MARGIN),
                    PlaceholderText = "Player.0000",

                };
                tb_account_name.EnterPressed += delegate {
                    if (!string.Equals(tb_account_name.Text, "") && !Regex.IsMatch(tb_account_name.Text, @"[^a-zA-Z0-9.\s]|^\.*$")) {
                        wndw.Navigate(BuildKillProofPanel(wndw, new CommonFields.Player(null, tb_account_name.Text, 0, 0, false)));
                    }
                };
            });
            var lab_squadPanel = new Label() {
                Parent = header,
                Size = new Point(300, 40),
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size24, ContentService.FontStyle.Regular),
                StrokeText = true,
                Location = new Point(KillProofModule.LEFT_MARGIN, header.Bottom - 40),
                Text = "Recent profiles:"
            };
            /// ###################
            ///      </HEADER>
            /// ###################
            /// ###################
            ///      <FOOTER>
            /// ###################
            var footer = new Panel() {
                Parent = hPanel,
                Size = new Point(hPanel.Width, 50),
                Location = new Point(0, hPanel.Height - 50),
                CanScroll = false
            };
            var creditLabel = new Label() {
                Parent = footer,
                Size = LABEL_SMALL,
                HorizontalAlignment = HorizontalAlignment.Center,
                Location = new Point((footer.Width / 2) - (LABEL_SMALL.X / 2), (footer.Height / 2) - (LABEL_SMALL.Y / 2)),
                StrokeText = true,
                ShowShadow = true,
                Text = @"Powered by www.killproof.me"
            };
            Task<bool> checkUpdate = Task.Run(() => IsLatestVersion());
            checkUpdate.Wait();
            var versionLabel = new Label() {
                Parent = footer,
                Size = footer.Size,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                StrokeText = true,
                ShowShadow = true,
                Text = checkUpdate.Result ? ModuleInstance.Version.Clean() : "Update available! Visit killproof.me/addons",
                TextColor = checkUpdate.Result ? Color.White : Color.Red
            };
            /// ###################
            ///      </FOOTER>
            /// ###################
            SquadPanel = new Panel() {
                Parent = hPanel,
                Size = new Point(header.Size.X, hPanel.Height - header.Height - footer.Height),
                Location = new Point(0, header.Bottom),
                ShowBorder = true,
                CanScroll = true,
                ShowTint = true
            };

            return hPanel;
        }

        private void MouseEnterSortButton(object sender, MouseEventArgs e) {
            Control bSortMethod = ((Control)sender);
            bSortMethod.Size = new Point(bSortMethod.Size.X - 4, bSortMethod.Size.Y - 4);
        }
        private void MouseLeftSortButton(object sender, MouseEventArgs e) {
            Control bSortMethod = ((Control)sender);
            bSortMethod.Size = new Point(bSortMethod.Size.X + 4, bSortMethod.Size.Y + 4);
        }
        public Panel BuildKillProofPanel(WindowBase wndw, CommonFields.Player player) {
            var hPanel = new Panel() {
                CanScroll = false,
                Size = wndw.ContentRegion.Size
            };

            //var pageLoading = new LoadingSpinner() {
            //    Parent = hPanel
            //};
            //pageLoading.Location = new Point(hPanel.Size.X / 2 - pageLoading.Size.X / 2, hPanel.Size.Y / 2 - pageLoading.Size.Y / 2);

            var loader = Task.Run(() => GetKillProofContent(player.AccountName));
            loader.Wait();

            //pageLoading.Dispose();

            KillProof currentAccount = loader.Result;

            foreach (KillProofButton e1 in DisplayedKillProofs) { e1.Dispose(); }
            DisplayedKillProofs.Clear();

            if (currentAccount != null) {
                /// ###################
                ///      <HEADER>
                /// ###################
                var header = new Panel() {
                    Parent = hPanel,
                    Size = new Point(hPanel.Width, 200),
                    Location = new Point(0, 0),
                    CanScroll = false
                };
                var currentAccountName = new Label() {
                    Parent = header,
                    Size = LABEL_BIG,
                    Location = new Point(KillProofModule.LEFT_MARGIN, 100 - KillProofModule.BOTTOM_MARGIN),
                    ShowShadow = true,
                    Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size36, ContentService.FontStyle.Regular),
                    Text = ""
                };
                var currentAccountLastRefresh = new Label() {
                    Parent = header,
                    Size = LABEL_SMALL,
                    Location = new Point(KillProofModule.LEFT_MARGIN, currentAccountName.Bottom + KillProofModule.BOTTOM_MARGIN),
                    Text = ""
                };
                var sortingsMenu = new Panel() {
                    Parent = header,
                    Size = new Point(260, 32),
                    Location = new Point(header.Right - 310 - KillProofModule.RIGHT_MARGIN, currentAccountLastRefresh.Location.Y),
                    ShowTint = true,
                };
                var bSortByAll = new Image() {
                    Parent = sortingsMenu,
                    Size = new Point(32, 32),
                    Location = new Point(KillProofModule.RIGHT_MARGIN, 0),
                    Texture = GameService.Content.GetTexture("255369"),
                    BackgroundColor = Color.Transparent,
                    BasicTooltipText = SORTBY_ALL,
                };
                bSortByAll.LeftMouseButtonPressed += UpdateSort;
                bSortByAll.MouseEntered += MouseEnterSortButton;
                bSortByAll.MouseLeft += MouseLeftSortButton;
                var bSortByKillProof = new Image() {
                    Parent = sortingsMenu,
                    Size = new Point(32, 32),
                    Location = new Point(bSortByAll.Right + 20 + KillProofModule.RIGHT_MARGIN, 0),
                    Texture = _sortByWorldBossesTexture,
                    BasicTooltipText = SORTBY_KILLPROOF,
                };
                bSortByKillProof.LeftMouseButtonPressed += UpdateSort;
                bSortByKillProof.MouseEntered += MouseEnterSortButton;
                bSortByKillProof.MouseLeft += MouseLeftSortButton;
                var bSortByToken = new Image() {
                    Parent = sortingsMenu,
                    Size = new Point(23, 23),
                    Location = new Point(bSortByKillProof.Right + KillProofModule.RIGHT_MARGIN, 5),
                    Texture = _sortByTokenTexture,
                    BasicTooltipText = SORTBY_TOKEN,
                };
                bSortByToken.LeftMouseButtonPressed += UpdateSort;
                bSortByToken.MouseEntered += MouseEnterSortButton;
                bSortByToken.MouseLeft += MouseLeftSortButton;
                var bSortByTitle = new Image() {
                    Parent = sortingsMenu,
                    Size = new Point(32, 32),
                    Location = new Point(bSortByToken.Right + 20 + KillProofModule.RIGHT_MARGIN, 0),
                    Texture = _sortByTitleTexture,
                    BasicTooltipText = SORTBY_TITLE,
                };
                bSortByTitle.LeftMouseButtonPressed += UpdateSort;
                bSortByTitle.MouseEntered += MouseEnterSortButton;
                bSortByTitle.MouseLeft += MouseLeftSortButton;
                var bSortByRaid = new Image() {
                    Parent = sortingsMenu,
                    Size = new Point(32, 32),
                    Location = new Point(bSortByTitle.Right + KillProofModule.RIGHT_MARGIN, 0),
                    Texture = _sortByRaidTexture,
                    BasicTooltipText = SORTBY_RAID,
                };
                bSortByRaid.LeftMouseButtonPressed += UpdateSort;
                bSortByRaid.MouseEntered += MouseEnterSortButton;
                bSortByRaid.MouseLeft += MouseLeftSortButton;
                var bSortByFractal = new Image() {
                    Parent = sortingsMenu,
                    Size = new Point(32, 32),
                    Location = new Point(bSortByRaid.Right + KillProofModule.RIGHT_MARGIN, 0),
                    Texture = _sortByFractalTexture,
                    BasicTooltipText = SORTBY_FRACTAL,
                };
                bSortByFractal.LeftMouseButtonPressed += UpdateSort;
                bSortByFractal.MouseEntered += MouseEnterSortButton;
                bSortByFractal.MouseLeft += MouseLeftSortButton;
                /// ###################
                ///      </HEADER>
                /// ###################
                /// ###################
                ///      <FOOTER>
                /// ###################
                var footer = new Panel() {
                    Parent = hPanel,
                    Size = new Point(hPanel.Width, 50),
                    Location = new Point(0, hPanel.Height - 50),
                    CanScroll = false
                };
                var currentAccountKpId = new Label() {
                    Parent = footer,
                    Size = LABEL_SMALL,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Location = new Point(KillProofModule.LEFT_MARGIN, (footer.Height / 2) - (LABEL_SMALL.Y / 2)),
                    Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size8, ContentService.FontStyle.Regular),
                    Text = ""
                };
                var currentAccountProofUrl = new Label() {
                    Parent = footer,
                    Size = LABEL_SMALL,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Location = new Point(KillProofModule.LEFT_MARGIN, (footer.Height / 2) - (LABEL_SMALL.Y / 2) + KillProofModule.BOTTOM_MARGIN),
                    Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size8, ContentService.FontStyle.Regular),
                    Text = ""
                };
                var creditLabel = new Label() {
                    Parent = footer,
                    Size = LABEL_SMALL,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Location = new Point((footer.Width / 2) - (LABEL_SMALL.X / 2), (footer.Height / 2) - (LABEL_SMALL.Y / 2)),
                    StrokeText = true,
                    ShowShadow = true,
                    Text = @"Powered by www.killproof.me"
                };
                /// ###################
                ///      </FOOTER>
                /// ###################
                var contentPanel = new Panel() {
                    Parent = hPanel,
                    Size = new Point(header.Size.X, hPanel.Height - header.Height - footer.Height),
                    Location = new Point(0, header.Bottom),
                    ShowBorder = true,
                    CanScroll = true,
                    ShowTint = true
                };
                currentAccountName.Text = currentAccount.account_name;
                currentAccountLastRefresh.Text = "Last Refresh: " + $"{currentAccount.last_refresh:dddd, d. MMMM yyyy - HH:mm:ss}";
                currentAccountKpId.Text = "ID: " + currentAccount.kpid;
                currentAccountProofUrl.Text = currentAccount.proof_url;

                if (currentAccount.killproofs != null) {
                    foreach (KeyValuePair<string, int> killproof in currentAccount.killproofs) {
                        if (killproof.Value > 0) {
                            var killProofButton = new KillProofButton() {
                                Parent     = contentPanel,
                                Icon       = GetTokenRender(killproof.Key).Result,
                                Font       = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size24, ContentService.FontStyle.Regular),
                                Text       = killproof.Value.ToString(),
                                BottomText = killproof.Key
                            };

                            DisplayedKillProofs.Add(killProofButton);
                        }
                    }
                } else {
                    // TODO: Show button indicating that killproofs were explicitly hidden
                    Logger.Info($"Player '{currentAccount.account_name}' has LI details explicitly hidden.");
                }

                if (currentAccount.tokens != null) {
                    foreach (KeyValuePair<string, int> token in currentAccount.tokens) {
                        if (token.Value > 0) {
                            var killProofButton = new KillProofButton() {
                                Parent     = contentPanel,
                                Icon       = GetTokenRender(token.Key).Result,
                                Font       = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size24, ContentService.FontStyle.Regular),
                                Text       = token.Value.ToString(),
                                BottomText = token.Key
                            };

                            DisplayedKillProofs.Add(killProofButton);
                        }
                    }
                } else {
                    // TODO: Show button indicating that tokens were explicitly hidden
                    Logger.Info($"Player '{currentAccount.account_name}' has tokens explicitly hidden.");
                }

                if (currentAccount.titles != null) {
                    foreach (KeyValuePair<string, string> token in currentAccount.titles) {
                        var titleButton = new KillProofButton() {
                            Parent         = contentPanel,
                            Font           = GameService.Content.DefaultFont16,
                            Text           = token.Key,
                            BottomText     = token.Value,
                            IsTitleDisplay = true
                        };

                        switch (token.Value) {
                            case "token":
                                titleButton.Icon = _sortByTokenTexture;
                                break;
                            case "title":
                                titleButton.Icon = _sortByTitleTexture;
                                break;
                            case "raid":
                                titleButton.Icon = _sortByRaidTexture;
                                break;
                            case "fractal":
                                titleButton.Icon = _sortByFractalTexture;
                                break;
                        }

                        DisplayedKillProofs.Add(titleButton);
                    }
                } else {
                    // TODO: Show button indicating that titles were explicitly hidden
                    Logger.Info($"Player '{currentAccount.account_name}' has titles and achievements explicitly hidden.");
                }

                RepositionKillProofs();

                if (!player.Self && !player.AccountName.Equals(LocalPlayerButton.Player.AccountName, StringComparison.InvariantCultureIgnoreCase)
                    && !DisplayedPlayers.Any(x => x.Player.AccountName.Equals(player.AccountName, StringComparison.InvariantCultureIgnoreCase))) {

                    if (DisplayedPlayers.Count == MAX_PLAYERS) { DisplayedPlayers.Dequeue().Dispose(); }

                    var new_player = new CommonFields.Player(null, player.AccountName, 0, 0, false);
                    var playerButton = new PlayerButton() {
                        Parent = SquadPanel,
                        Player = new_player,
                        Icon = GameService.Content.GetTexture("733268"),
                        Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size16, ContentService.FontStyle.Regular),
                        IsNew = false
                    };
                    playerButton.LeftMouseButtonPressed += delegate {
                        GameService.Overlay.BlishHudWindow.Navigate(BuildKillProofPanel(GameService.Overlay.BlishHudWindow, player));
                    };
                    DisplayedPlayers.Enqueue(playerButton);
                }

            } else {
                var tintPanel = new Panel() {
                    Parent = hPanel,
                    Size = new Point(hPanel.Size.X - 150, hPanel.Size.Y - 150),
                    Location = new Point(75, 75),
                    ShowBorder = true,
                    ShowTint = true
                };
                var lab_nothingHere = new Label() {
                    Parent = hPanel,
                    Size = hPanel.Size,
                    Location = new Point(0, -20),
                    ShowShadow = true,
                    StrokeText = true,
                    Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size36, ContentService.FontStyle.Regular),
                    Text = "Not yet registered :(",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Middle
                };
                var lab_visitUs = new Label() {
                    Parent = hPanel,
                    Size = hPanel.Size,
                    Location = new Point(0, -20),
                    ShowShadow = true,
                    StrokeText = true,
                    Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size24, ContentService.FontStyle.Regular),
                    Text = "\n\nVisit www.killproof.me and allow us to record your KillProofs for you.",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Middle
                };
                if (!player.Self && !player.AccountName.Equals(LocalPlayerButton.Player.AccountName, StringComparison.InvariantCultureIgnoreCase)) {
                    lab_nothingHere.Text = "No profile for \"" + player.AccountName + "\" found :(";
                    lab_visitUs.Text = "\n\nPlease, share www.killproof.me with this player and help expand our database.";
                }
            }

            var backButton = new BackButton(wndw) {
                Text = "KillProof",
                NavTitle = "Profile",
                Parent = hPanel,
                Location = new Point(20, 20),
            };

            backButton.LeftMouseButtonReleased += delegate {
                wndw.NavigateHome();
                wndw.ActivePanel = GameService.Overlay.BlishHudWindow.Panels[KillProofTab];
                RepositionPlayers();
                hPanel.Dispose();
            };

            CurrentProfile = currentAccount;

            return hPanel;
        }
        private void UpdateSort(object sender, EventArgs e) {
            if (sender != null) {
                CurrentSortMethod = ((Control)sender).BasicTooltipText;
            }
            switch (CurrentSortMethod) {
                case SORTBY_ALL:
                    DisplayedKillProofs.Sort((e1, e2) => {
                        int result = e1.IsTitleDisplay.CompareTo(e2.IsTitleDisplay);
                        if (result != 0) return result;
                        else return e1.BottomText.CompareTo(e2.BottomText);
                    });
                    foreach (KillProofButton e1 in DisplayedKillProofs) { e1.Visible = true; }
                    break;
                case SORTBY_KILLPROOF:
                    DisplayedKillProofs.Sort((e1, e2) => e1.BottomText.CompareTo(e2.BottomText));
                    foreach (KillProofButton e1 in DisplayedKillProofs) { e1.Visible = CurrentProfile.killproofs.Any(x => x.Key.Equals(e1.BottomText)); }
                    break;
                case SORTBY_TOKEN:
                    DisplayedKillProofs.Sort((e1, e2) => e1.BottomText.CompareTo(e2.BottomText));
                    foreach (KillProofButton e1 in DisplayedKillProofs) { e1.Visible = CurrentProfile.tokens.Any(x => x.Key.Equals(e1.BottomText)); }
                    break;
                case SORTBY_TITLE:
                    DisplayedKillProofs.Sort((e1, e2) => e1.BottomText.CompareTo(e2.BottomText));
                    foreach (KillProofButton e1 in DisplayedKillProofs) { e1.Visible = e1.IsTitleDisplay; }
                    break;
                case SORTBY_FRACTAL:
                    DisplayedKillProofs.Sort((e1, e2) => e1.Text.CompareTo(e2.Text));
                    foreach (KillProofButton e1 in DisplayedKillProofs) { e1.Visible = e1.BottomText.ToLower().Contains("fractal"); }
                    break;
                case SORTBY_RAID:
                    DisplayedKillProofs.Sort((e1, e2) => e1.Text.CompareTo(e2.Text));
                    foreach (KillProofButton e1 in DisplayedKillProofs) { e1.Visible = e1.BottomText.ToLower().Contains("raid"); }
                    break;
                default:
                    throw new NotSupportedException();
            }
            RepositionKillProofs();
        }
        private void RepositionKillProofs() {
            int pos = 0;
            foreach (KillProofButton e in DisplayedKillProofs) {
                int x = pos % 3;
                int y = pos / 3;
                e.Location = new Point(x * (e.Width + 8), y * (e.Height + 8));

                ((Panel)e.Parent).VerticalScrollOffset = 0;
                e.Parent.Invalidate();
                if (e.Visible) pos++;
            }
        }
        private void RepositionPlayers() {
            var sorted = from player in DisplayedPlayers
                         orderby player.IsNew descending
                         select player;

            int pos = 0;
            foreach (PlayerButton e in sorted) {
                int x = pos % 3;
                int y = pos / 3;
                e.Location = new Point(x * (e.Width + 8), y * (e.Height + 8));

                ((Panel)e.Parent).VerticalScrollOffset = 0;
                e.Parent.Invalidate();
                if (e.Visible) pos++;
            }
        }

    #endregion

    #endregion

    /// <inheritdoc />
    protected override void Unload() {
            GameService.Overlay.BlishHudWindow.RemoveTab(KillProofTab);

            // All static members must be manually unset
            ModuleInstance = null;
        }

    }

}
