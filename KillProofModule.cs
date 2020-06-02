using Blish_HUD;
using Blish_HUD.ArcDps.Common;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Flurl;
using Flurl.Http;
using Gw2Sharp.Models;
using Gw2Sharp.WebApi.V2.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using KillProofModule.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Color = Microsoft.Xna.Framework.Color;
using Control = Blish_HUD.Controls.Control;
using HorizontalAlignment = Blish_HUD.Controls.HorizontalAlignment;
using Label = Blish_HUD.Controls.Label;
using MouseEventArgs = Blish_HUD.Input.MouseEventArgs;
using Panel = Blish_HUD.Controls.Panel;
using TextBox = Blish_HUD.Controls.TextBox;

namespace KillProofModule
{

    [Export(typeof(Module))]
    public class KillProofModule : Module
    {

        private static readonly Logger Logger = Logger.GetLogger(typeof(KillProofModule));

        internal static KillProofModule ModuleInstance;

        #region Service Managers
        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;
        #endregion

        private const int TOP_MARGIN = 0;
        private const int RIGHT_MARGIN = 5;
        private const int BOTTOM_MARGIN = 10;
        private const int LEFT_MARGIN = 8;
        private Point LABEL_SMALL = new Point(400, 30);
        private Point LABEL_BIG = new Point(400, 40);

        private const string SORTBY_ALL = "Everything";
        private const string SORTBY_KILLPROOF = "KillProofs";
        private const string SORTBY_TOKEN = "Tokens";
        private const string SORTBY_TITLE = "Titles";
        private const string SORTBY_RAID = "Raid Titles";
        private const string SORTBY_FRACTAL = "Fractal Titles";
        private string CurrentSortMethod = SORTBY_ALL;

        // Caches
        private Dictionary<string, AsyncTexture2D> TokenRenderRepository;
        private Dictionary<uint, AsyncTexture2D> EliteRenderRepository;
        private Dictionary<uint, AsyncTexture2D> ProfessionRenderRepository;
        // Max profile buttons on SquadPanel before dequeuing FiFo behavior.
        private const int MAX_PLAYERS = 15;

        private const string KILLPROOF_API_URL = "https://killproof.me/api/";
        private const string KILLPROOF_RESOURCES_URL = "https://killproof.me/resources.json";

        private WindowTab KillProofTab;

        private List<KillProof> CachedKillProofs;
        private KillProof CurrentProfile;
        private KillProof MyKillProof;

        private Panel SquadPanel;
        private PlayerButton LocalPlayerButton;
        private Checkbox SmartPingCheckBox;
        private Panel KillProofQuickMenu;

        private SettingEntry<bool> KillProofQuickMenuEnabled;

        private Queue<PlayerButton> DisplayedPlayers;
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
        private static readonly Dictionary<string, int> TokenIdRepository = new Dictionary<string, int>()
        {
            {"Legendary Insight", 77302},
            {"Unstable Cosmic Essence", 81743},
            {"Legendary Divination", 88485},
            {"W1 | Vale Guardian Fragment", 77705},
            {"W1 | Gorseval Tentacle Piece", 77751},
            {"W1 | Sabetha Flamethrower Fragment Piece", 77728},
            {"W2 | Slothasor Mushroom", 77722},
            {"W2 | White Mantle Abomination Crystal", 77761},
            {"W3 | Turret Fragment", 78873},
            {"W3 | Keep Construct Rubble", 78905},
            {"W3 | Ribbon Scrap", 78942},
            {"W4 | Cairn Fragment", 80623},
            {"W4 | Recreation Room Floor Fragment",  80269},
            {"W4 | Impaled Prisoner Token", 80087},
            {"W4 | Fragment of Saul's Burden", 80189},
            {"W5 | Desmina's Token", 85993},
            {"W5 | River of Souls Token", 85785},
            {"W5 | Statue Token", 85800},
            {"W5 | Dhuum's Token", 85633},
            {"W6 | Conjured Amalgamate Token", 88543},
            {"W6 | Twin Largos Token", 88860},
            {"W6 | Qadim's Token", 88645},
            {"W7 | Cardinal Adina's Token", 91246},
            {"W7 | Cardinal Sabir's Token", 91270},
            {"W7 | Ether Djinn's Token", 91175}
        };
        private static readonly List<string> RaidWings = new List<string>()
        {
            "W1",
            "W2",
            "W3",
            "W4",
            "W5",
            "W6",
            "W7"
        };
        private Dictionary<string, int> MyTokenQuantityRepository;
        [ImportingConstructor]
        public KillProofModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) { ModuleInstance = this; }

        protected override void DefineSettings(SettingCollection settings) {
            KillProofQuickMenuEnabled = settings.DefineSetting("KillProofQuickMenuEnabled", false, "Kill Proof Quick Access Menu", "Quick access to ping kill proofs.");
        }

        private void LoadTextures() {
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

        protected override void Initialize() {
            TokenRenderRepository = new Dictionary<string, AsyncTexture2D>(StringComparer.InvariantCultureIgnoreCase);
            EliteRenderRepository = new Dictionary<uint, AsyncTexture2D>();
            ProfessionRenderRepository = new Dictionary<uint, AsyncTexture2D>();
            DisplayedKillProofs = new List<KillProofButton>();
            DisplayedPlayers = new Queue<PlayerButton>();
            CachedKillProofs = new List<KillProof>();

            if (KillProofQuickMenuEnabled.Value) { KillProofQuickMenu = BuildKillProofQuickMenu(); }

            LoadTextures();

            GameService.ArcDps.Common.Activate();
        }

        protected override async Task LoadAsync() {
            await Task.Run(LoadTokenIcons);
            await Task.Run(LoadProfessionIcons);
            await Task.Run(LoadEliteIcons);
        }

        protected override void OnModuleLoaded(EventArgs e) {
            KillProofTab = GameService.Overlay.BlishHudWindow.AddTab("KillProof", _killProofIconTexture, BuildHomePanel(GameService.Overlay.BlishHudWindow), 0);
            GameService.ArcDps.Common.PlayerAdded += PlayerAddedEvent;

            // Base handler must be called
            base.OnModuleLoaded(e);
        }

        protected override void Update(GameTime gameTime) {
            if (LocalPlayerButton != null) LocalPlayerButton.Parent.Visible = GameService.ArcDps.RenderPresent;
            if (SmartPingCheckBox != null) SmartPingCheckBox.Visible = GameService.ArcDps.RenderPresent;
            if (KillProofQuickMenu != null) KillProofQuickMenu.Visible = GameService.GameIntegration.IsInGame && GameService.ArcDps.Common.PlayersInSquad.Count != 0 && MyTokenQuantityRepository != null;
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
            (bool responseSuccess, var remoteManifest) = await GetJsonResponse<Manifest>("https://raw.githubusercontent.com/blish-hud/KillProof-Module/master/manifest.json");
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
        private async void LoadTokenIcons() {
            (bool responseSuccess, Dictionary<string, Url> tokenRenderUrlRepository) = await GetJsonResponse<Dictionary<string, Url>>(KILLPROOF_API_URL + "icons");
            if (responseSuccess) {
                foreach (KeyValuePair<string, Url> token in tokenRenderUrlRepository) {
                    var renderUri = token.Value;
                    if (TokenRenderRepository.Any(x => x.Key == token.Key)) {
                        try {
                            var textureDataResponse = await GameService.Gw2WebApi.AnonymousConnection.Client.Render
                                .DownloadToByteArrayAsync(renderUri);

                            using (var textureStream = new MemoryStream(textureDataResponse)) {
                                var loadedTexture =
                                    Texture2D.FromStream(GameService.Graphics.GraphicsDevice, textureStream);

                                TokenRenderRepository[token.Key].SwapTexture(loadedTexture);
                            }
                        } catch (Exception ex) {
                            Logger.Warn(ex, $"Request to render service for {renderUri} failed.", renderUri);
                        }
                    } else {
                        TokenRenderRepository.Add(token.Key, GameService.Content.GetRenderServiceTexture(token.Value));
                    }
                }
            }
        }
        private async Task<IReadOnlyList<Profession>> LoadProfessions() {
            return await GameService.Gw2WebApi.AnonymousConnection.Client.V2.Professions
                .ManyAsync(Enum.GetValues(typeof(ProfessionType)).Cast<ProfessionType>());
        }
        private async void LoadProfessionIcons() {
            var professions = await LoadProfessions();
            foreach (Profession profession in professions) {
                var renderUri = (string)profession.IconBig;
                var id = (uint)Enum.GetValues(typeof(ProfessionType)).Cast<ProfessionType>().ToList()
                                  .Find(x => x.ToString().Equals(profession.Id, StringComparison.InvariantCultureIgnoreCase));
                if (ProfessionRenderRepository.Any(x => x.Key == id)) {
                    try {
                        var textureDataResponse = await GameService.Gw2WebApi.AnonymousConnection.Client.Render
                            .DownloadToByteArrayAsync(renderUri);

                        using (var textureStream = new MemoryStream(textureDataResponse)) {
                            var loadedTexture =
                                Texture2D.FromStream(GameService.Graphics.GraphicsDevice, textureStream);

                            ProfessionRenderRepository[id].SwapTexture(loadedTexture);
                        }
                    } catch (Exception ex) {
                        Logger.Warn(ex, $"Request to render service for {renderUri} failed.", renderUri);
                    }
                } else {
                    ProfessionRenderRepository.Add(id, GameService.Content.GetRenderServiceTexture(renderUri));
                }
            }
        }
        private async void LoadEliteIcons() {
            await GetJsonResponse<int[]>("https://api.guildwars2.com/v2/specializations").ContinueWith(
                async response => {
                    (bool responseSuccess, int[] specializations) = (response.Result.Item1, response.Result.Item2);
                    if (!responseSuccess) return;
                    foreach (var i in specializations)
                        await GetJsonResponse<JObject>("https://api.guildwars2.com/v2/specializations/" + i)
                            .ContinueWith(
                                async result => {
                                    var jObj = (JObject)result.Result.Item2;
                                    if (!(bool)jObj["elite"]) return;
                                    var id = (uint)jObj["id"];
                                    if (EliteRenderRepository.Any(x =>
                                        x.Key == id)) {
                                        var renderUri = (string)jObj["profession_icon_big"];
                                        try {
                                            var textureDataResponse = await GameService.Gw2WebApi.AnonymousConnection
                                                .Client
                                                .Render.DownloadToByteArrayAsync(renderUri);

                                            using (var textureStream = new MemoryStream(textureDataResponse)) {
                                                var loadedTexture =
                                                    Texture2D.FromStream(GameService.Graphics.GraphicsDevice,
                                                        textureStream);

                                                EliteRenderRepository[id].SwapTexture(loadedTexture);
                                            }
                                        } catch (Exception ex) {
                                            Logger.Warn(ex, $"Request to render service for {renderUri} failed.",
                                                renderUri);
                                        }
                                    } else {
                                        EliteRenderRepository.Add(id, GameService.Content.GetRenderServiceTexture(
                                            (string)jObj["profession_icon_big"]));
                                    }
                                });
                });
        }
        private AsyncTexture2D GetProfessionRender(CommonFields.Player player) {
            if (!ProfessionRenderRepository.Any(x => x.Key.Equals(player.Profession))) {
                var render = new AsyncTexture2D();
                ProfessionRenderRepository.Add(player.Profession, render);
            }
            return ProfessionRenderRepository[player.Profession];
        }
        private AsyncTexture2D GetEliteRender(CommonFields.Player player) {
            if (player.Elite == 0) { return GetProfessionRender(player); }
            if (!EliteRenderRepository.Any(x => x.Key.Equals(player.Elite))) {
                var render = new AsyncTexture2D();
                EliteRenderRepository.Add(player.Elite, render);
            }
            return EliteRenderRepository[player.Elite];
        }
        private AsyncTexture2D GetTokenRender(string key) {
            if (!TokenRenderRepository.Any(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))) {
                var render = new AsyncTexture2D();
                TokenRenderRepository.Add(key, render);
            }
            return TokenRenderRepository[key];
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
            if (player.Self && LocalPlayerButton != null) {
                LocalPlayerButton.Player = player;
                LocalPlayerButton.Icon = GetEliteRender(player);
                LocalPlayerButton.LeftMouseButtonPressed += delegate {
                    GameService.Overlay.BlishHudWindow.Navigate(BuildKillProofPanel(GameService.Overlay.BlishHudWindow, player));
                };
                if (MyKillProof == null) LoadMyKillProof();
                return;
            }

            if (DisplayedPlayers.Any(x => x.Player.AccountName.Equals(player.AccountName, StringComparison.InvariantCultureIgnoreCase))
                || !ProfileAvailable(player.AccountName).Result) { return; };

            PlayerNotification.ShowNotification(player.AccountName, GetEliteRender(player), "profile available", 10);

            var optionalButton = DisplayedPlayers.FirstOrDefault(x => x.Player.AccountName.Equals(player.AccountName, StringComparison.InvariantCultureIgnoreCase));

            if (optionalButton == null) {
                if (DisplayedPlayers.Count() == MAX_PLAYERS) { DisplayedPlayers.Dequeue().Dispose(); }

                var playerButton = new PlayerButton() {
                    Parent = SquadPanel,
                    Player = player,
                    Icon = GetEliteRender(player),
                    Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size16, ContentService.FontStyle.Regular)
                };
                playerButton.LeftMouseButtonPressed += delegate {
                    playerButton.IsNew = false;
                    GameService.Overlay.BlishHudWindow.Navigate(BuildKillProofPanel(GameService.Overlay.BlishHudWindow, playerButton.Player));
                };
                DisplayedPlayers.Enqueue(playerButton);
            } else {
                optionalButton.Player = player;
                optionalButton.Icon = GetEliteRender(player);
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

            if (GameService.ArcDps.Loaded) {
                var selfButtonPanel = new Panel() {
                    Parent = header,
                    Size = new Point(335, 114),
                    ShowBorder = true,
                    ShowTint = true,
                    Location =
                        new Point(header.Right - 335 - KillProofModule.RIGHT_MARGIN, KillProofModule.TOP_MARGIN + 15)
                };
                SmartPingCheckBox = new Checkbox() {
                    Parent = header,
                    Location = new Point(selfButtonPanel.Location.X + LEFT_MARGIN, selfButtonPanel.Bottom),
                    Size = new Point(selfButtonPanel.Width, 30),
                    Text = "Show Smart Ping Menu",
                    BasicTooltipText =
                        "Shows a menu on the top left corner of your screen which allows you to quickly access and ping your killproofs.",
                    Checked = KillProofQuickMenuEnabled.Value
                };
                SmartPingCheckBox.CheckedChanged += delegate (object sender, CheckChangedEvent e) {
                    KillProofQuickMenuEnabled.Value = e.Checked;
                    if (e.Checked)
                        KillProofQuickMenu = BuildKillProofQuickMenu();
                    else
                        KillProofQuickMenu?.Dispose();
                };
                LocalPlayerButton = new PlayerButton() {
                    Parent = selfButtonPanel,
                    Player = new CommonFields.Player("You started Blish HUD while Guild Wars 2 was already running.",
                        "Refresh map to see your profile.", 0, 0, true),
                    Icon = GameService.Content.GetTexture("733268"),
                    IsNew = false,
                    Location = new Point(0, 0),
                    Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia,
                        ContentService.FontSize.Size16,
                        ContentService.FontStyle.Regular)
                };
            }

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
                tb_account_name.Focused = false;
            };
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

        private void MousePressedSortButton(object sender, MouseEventArgs e) {
            Control bSortMethod = ((Control)sender);
            bSortMethod.Size = new Point(bSortMethod.Size.X - 4, bSortMethod.Size.Y - 4);
        }
        private void MouseLeftSortButton(object sender, MouseEventArgs e) {
            Control bSortMethod = ((Control)sender);
            bSortMethod.Size = new Point(32, 32);
        }

        private void FinishLoadingKillProofPanel(WindowBase wndw, Panel hPanel, CommonFields.Player player, KillProof currentAccount) {
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
                bSortByAll.LeftMouseButtonPressed += MousePressedSortButton;
                bSortByAll.LeftMouseButtonReleased += MouseLeftSortButton;
                bSortByAll.MouseLeft += MouseLeftSortButton;
                var bSortByKillProof = new Image() {
                    Parent = sortingsMenu,
                    Size = new Point(32, 32),
                    Location = new Point(bSortByAll.Right + 20 + KillProofModule.RIGHT_MARGIN, 0),
                    Texture = _sortByWorldBossesTexture,
                    BasicTooltipText = SORTBY_KILLPROOF,
                };
                bSortByKillProof.LeftMouseButtonPressed += UpdateSort;
                bSortByKillProof.LeftMouseButtonPressed += MousePressedSortButton;
                bSortByKillProof.LeftMouseButtonReleased += MouseLeftSortButton;
                bSortByKillProof.MouseLeft += MouseLeftSortButton;
                var bSortByToken = new Image() {
                    Parent = sortingsMenu,
                    Size = new Point(32, 32),
                    Location = new Point(bSortByKillProof.Right + KillProofModule.RIGHT_MARGIN, 0),
                    Texture = _sortByTokenTexture,
                    BasicTooltipText = SORTBY_TOKEN,
                };
                bSortByToken.LeftMouseButtonPressed += UpdateSort;
                bSortByToken.LeftMouseButtonPressed += MousePressedSortButton;
                bSortByToken.LeftMouseButtonReleased += MouseLeftSortButton;
                bSortByToken.MouseLeft += MouseLeftSortButton;
                var bSortByTitle = new Image() {
                    Parent = sortingsMenu,
                    Size = new Point(32, 32),
                    Location = new Point(bSortByToken.Right + 20 + KillProofModule.RIGHT_MARGIN, 0),
                    Texture = _sortByTitleTexture,
                    BasicTooltipText = SORTBY_TITLE,
                };
                bSortByTitle.LeftMouseButtonPressed += UpdateSort;
                bSortByTitle.LeftMouseButtonPressed += MousePressedSortButton;
                bSortByTitle.LeftMouseButtonReleased += MouseLeftSortButton;
                bSortByTitle.MouseLeft += MouseLeftSortButton;
                var bSortByRaid = new Image() {
                    Parent = sortingsMenu,
                    Size = new Point(32, 32),
                    Location = new Point(bSortByTitle.Right + KillProofModule.RIGHT_MARGIN, 0),
                    Texture = _sortByRaidTexture,
                    BasicTooltipText = SORTBY_RAID,
                };
                bSortByRaid.LeftMouseButtonPressed += UpdateSort;
                bSortByRaid.LeftMouseButtonPressed += MousePressedSortButton;
                bSortByRaid.LeftMouseButtonReleased += MouseLeftSortButton;
                bSortByRaid.MouseLeft += MouseLeftSortButton;
                var bSortByFractal = new Image() {
                    Parent = sortingsMenu,
                    Size = new Point(32, 32),
                    Location = new Point(bSortByRaid.Right + KillProofModule.RIGHT_MARGIN, 0),
                    Texture = _sortByFractalTexture,
                    BasicTooltipText = SORTBY_FRACTAL,
                };
                bSortByFractal.LeftMouseButtonPressed += UpdateSort;
                bSortByFractal.LeftMouseButtonPressed += MousePressedSortButton;
                bSortByFractal.LeftMouseButtonReleased += MouseLeftSortButton;
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
                                Parent = contentPanel,
                                Icon = GetTokenRender(killproof.Key),
                                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size24, ContentService.FontStyle.Regular),
                                Text = killproof.Value.ToString(),
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
                                Parent = contentPanel,
                                Icon = GetTokenRender(token.Key),
                                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size24, ContentService.FontStyle.Regular),
                                Text = token.Value.ToString(),
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
                            Parent = contentPanel,
                            Font = GameService.Content.DefaultFont16,
                            Text = token.Key,
                            BottomText = token.Value,
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
        }

        public Panel BuildKillProofPanel(WindowBase wndw, CommonFields.Player player) {
            var hPanel = new Panel() {
                CanScroll = false,
                Size = wndw.ContentRegion.Size
            };

            var pageLoading = new LoadingSpinner() {
                Parent = hPanel
            };

            pageLoading.Location = new Point(hPanel.Size.X / 2 - pageLoading.Size.X / 2, hPanel.Size.Y / 2 - pageLoading.Size.Y / 2);

            foreach (KillProofButton e1 in DisplayedKillProofs) { e1.Dispose(); }
            DisplayedKillProofs.Clear();

            GetKillProofContent(player.AccountName).ContinueWith((kpResult) => {
                FinishLoadingKillProofPanel(wndw, hPanel, player, kpResult.Result);
                pageLoading.Dispose();
            });

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

        private async void LoadMyKillProof()
        {
            var player = GameService.ArcDps.Common.PlayersInSquad.First(x => x.Value.Self).Value;
            MyKillProof = await GetKillProofContent(player.AccountName);
            var killproofs = MyKillProof.tokens.MergeLeft(MyKillProof.killproofs);
            MyTokenQuantityRepository = new Dictionary<string, int>();
            foreach (KeyValuePair<string, int> pair in killproofs)
            {
                var kpName = TokenIdRepository.FirstOrDefault(x => x.Key.Contains(pair.Key)).Key ?? pair.Key;
                MyTokenQuantityRepository.Add(kpName, pair.Value);
            }
        }
        private int GetMyQuantity(string token) {
            if (!GameService.ArcDps.Loaded || GameService.ArcDps.Common.PlayersInSquad.Count == 0 || MyTokenQuantityRepository == null) return 0;
            try {
                return MyTokenQuantityRepository.Any(x => x.Key.Contains(token)) ? MyTokenQuantityRepository[token] : 0;
            } catch (KeyNotFoundException ex) {
                Logger.Warn(ex.Message);
                return 0;
            }
        }
        private Panel BuildKillProofQuickMenu() {
            var bgPanel = new Panel() {
                Parent = GameService.Graphics.SpriteScreen,
                Location = new Point(10, 38),
                Size = new Point(400, 40),
                Opacity = 0.4f,
                Visible = false,
                ShowBorder = true
            };
            bgPanel.Resized += delegate (object sender, ResizedEventArgs args) {
                bgPanel.Location = new Point(10, 38);
            };
            bgPanel.MouseEntered += delegate (object sender, MouseEventArgs e) {
                var fadeIn = GameService.Animation.Tweener.Tween(bgPanel, new { Opacity = 1.0f }, 0.45f);
            };
            var leftBracket = new Label() {
                Parent = bgPanel,
                Size = bgPanel.Size,
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size20, ContentService.FontStyle.Regular),
                Text = "[",
                Location = new Point(0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            var quantity = new Label() {
                Parent = bgPanel,
                Size = new Point(30, 30),
                Location = new Point(10, -2)
            };
            var dropdown = new Dropdown() {
                Parent = bgPanel,
                Size = new Point(260, 20),
                Location = new Point(quantity.Right + 2, 3),
                SelectedItem = "Loading .."
            };
            bgPanel.MouseLeft += delegate (object sender, MouseEventArgs e) {
                //TODO: Check for when dropdown IsExpanded
                var fadeOut = GameService.Animation.Tweener.Tween(bgPanel, new { Opacity = 0.4f }, 0.45f);
            };
            var rightBracket = new Label() {
                Parent = bgPanel,
                Size = new Point(10, bgPanel.Height),
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size20, ContentService.FontStyle.Regular),
                Text = "]",
                Location = new Point(dropdown.Right, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            foreach (KeyValuePair<string, int> pair in TokenIdRepository) {
                dropdown.Items.Add(pair.Key);
            }
            dropdown.ValueChanged += async delegate {
                var value = GetMyQuantity(dropdown.SelectedItem);
                quantity.Text = value + "";
            };
            dropdown.SelectedItem = "Legendary Insight";
            var sendButton = new Image() {
                Parent = bgPanel,
                Size = new Point(24, 24),
                Location = new Point(rightBracket.Right + 1, 0),
                Texture = GameService.Content.GetTexture("784268"),
                SpriteEffects = SpriteEffects.FlipHorizontally,
                BasicTooltipText = "Send To Chat\nLeft-Click: Only send code up to a stack's worth (250x). \nRight-Click: Send killproof.me total amount."
            };
            var randomizeButton = new StandardButton() {
                Parent = bgPanel,
                Size = new Point(29, 24),
                Location = new Point(sendButton.Right + 7, 0),
                Text = "W1",
                BackgroundColor = Color.Gray,
                BasicTooltipText = "Random token from selected wing when pressing Send To Chat.\nLeft-Click: Toggle\nRight-Click: Iterate wings"
            };
            randomizeButton.LeftMouseButtonPressed += delegate {
                randomizeButton.Size = new Point(27, 22);
                randomizeButton.Location = new Point(sendButton.Right + 5, 2);
            };

            randomizeButton.LeftMouseButtonReleased += delegate {
                randomizeButton.Size = new Point(29, 24);
                randomizeButton.Location = new Point(sendButton.Right + 7, 0);
                randomizeButton.BackgroundColor = randomizeButton.BackgroundColor == Color.Gray ? Color.LightGreen : Color.Gray;
            };
            randomizeButton.RightMouseButtonPressed += delegate {
                randomizeButton.Size = new Point(27, 22);
                randomizeButton.Location = new Point(sendButton.Right + 5, 2);
            };
            randomizeButton.RightMouseButtonReleased += delegate {
                randomizeButton.Size = new Point(29, 24);
                randomizeButton.Location = new Point(sendButton.Right + 7, 0);
                var current = RaidWings.FindIndex(x => x.Equals(randomizeButton.Text));
                var next = current + 1 <= RaidWings.Count - 1 ? current + 1 : 0;
                randomizeButton.Text = RaidWings[next];
            };
            sendButton.LeftMouseButtonPressed += delegate {
                sendButton.Size = new Point(22, 22);
                sendButton.Location = new Point(rightBracket.Right + 3, 2);
            };
            sendButton.LeftMouseButtonReleased += async delegate {
                sendButton.Size = new Point(24, 24);
                sendButton.Location = new Point(rightBracket.Right + 1, 0);

                if (MyKillProof == null) return;

                var chatLink = new Gw2Sharp.ChatLinks.ItemChatLink();

                if (randomizeButton.BackgroundColor == Color.LightGreen) {
                    var tokenSelection = TokenIdRepository.Where(x => x.Key.StartsWith(randomizeButton.Text)).ToDictionary(x => x.Key, x => x.Value);
                    var singleRandomToken = tokenSelection.ElementAt(RandomUtil.GetRandom(0, tokenSelection.Count - 1));
                    chatLink.ItemId = singleRandomToken.Value;
                    var amount = GetMyQuantity(singleRandomToken.Key);
                    var rest = amount % 250;
                    chatLink.Quantity = Convert.ToByte(amount > 250 && rest != 0 ? (RandomUtil.GetRandom(0, 10) > 7 ? rest : 250) : amount);
                    GameService.GameIntegration.Chat.Send(chatLink.ToString());
                } else {
                    chatLink.ItemId = TokenIdRepository[dropdown.SelectedItem];
                    var amount = GetMyQuantity(dropdown.SelectedItem);
                    var rest = amount % 250;
                    chatLink.Quantity = Convert.ToByte(amount > 250 && rest != 0 ? (RandomUtil.GetRandom(0, 10) > 7 ? rest : 250) : amount);
                    GameService.GameIntegration.Chat.Send(chatLink.ToString());
                }
            };
            sendButton.RightMouseButtonPressed += delegate {
                sendButton.Size = new Point(22, 22);
                sendButton.Location = new Point(rightBracket.Right + 3, 2);
            };
            var timeOutRightSend = new Dictionary<int, DateTimeOffset>();
            sendButton.RightMouseButtonReleased += async delegate {
                sendButton.Size = new Point(24, 24);
                sendButton.Location = new Point(rightBracket.Right + 1, 0);

                if (MyKillProof == null) return;

                var chatLink = new Gw2Sharp.ChatLinks.ItemChatLink();

                if (randomizeButton.BackgroundColor == Color.LightGreen) {
                    var tokenSelection = TokenIdRepository.Where(x => x.Key.StartsWith(randomizeButton.Text)).ToDictionary(x => x.Key, x => x.Value);
                    var singleRandomToken = tokenSelection.ElementAt(RandomUtil.GetRandom(0, tokenSelection.Count - 1));
                    chatLink.ItemId = singleRandomToken.Value;

                    if (timeOutRightSend.Any(x => x.Key == chatLink.ItemId)) {
                        var cooldown = DateTimeOffset.Now.Subtract(timeOutRightSend[chatLink.ItemId]);
                        var kpName = TokenIdRepository.First(x => x.Value.Equals(chatLink.ItemId)).Key.Split('|').Reverse().ToList()[0].Trim();
                        if (cooldown.TotalMinutes < 2) {
                            var timeLeft = TimeSpan.FromMinutes(2 - cooldown.TotalMinutes);
                            var minuteWord = timeLeft.TotalSeconds > 119 ? $" {timeLeft:%m} minutes and" : timeLeft.TotalSeconds > 59 ? $" {timeLeft:%m} minute and" : "";
                            var secondWord = timeLeft.Seconds > 9 ? $"{timeLeft:ss} seconds" : timeLeft.Seconds > 1 ? $"{timeLeft:%s} seconds" : $"{timeLeft:%s} second";
                            ScreenNotification.ShowNotification($"You can't send your {kpName} total\nwithin the next{minuteWord} {secondWord} again.", ScreenNotification.NotificationType.Error);
                            return;
                        }
                        timeOutRightSend[chatLink.ItemId] = DateTimeOffset.Now;
                    } else {
                        timeOutRightSend.Add(chatLink.ItemId, DateTimeOffset.Now);
                    }
                    chatLink.Quantity = Convert.ToByte(1);
                    GameService.GameIntegration.Chat.Send($"Total: {GetMyQuantity(singleRandomToken.Key)} of {chatLink} (killproof.me/{MyKillProof.kpid})");
                } else {
                    chatLink.ItemId = TokenIdRepository[dropdown.SelectedItem];

                    if (timeOutRightSend.Any(x => x.Key == chatLink.ItemId)) {
                        var cooldown = DateTimeOffset.Now.Subtract(timeOutRightSend[chatLink.ItemId]);
                        var kpName = TokenIdRepository.First(x => x.Value.Equals(chatLink.ItemId)).Key.Split('|').Reverse().ToList()[0].Trim();
                        if (cooldown.TotalMinutes < 2) {
                            var timeLeft = TimeSpan.FromMinutes(2 - cooldown.TotalMinutes);
                            var minuteWord = timeLeft.TotalSeconds > 119 ? $" {timeLeft:%m} minutes and" : timeLeft.TotalSeconds > 59 ? $" {timeLeft:%m} minute and" : "";
                            var secondWord = timeLeft.Seconds > 9 ? $"{timeLeft:ss} seconds" : timeLeft.Seconds > 1 ? $"{timeLeft:%s} seconds" : $"{timeLeft:%s} second";
                            ScreenNotification.ShowNotification($"You can't send your {kpName} total\nwithin the next{minuteWord} {secondWord} again.", ScreenNotification.NotificationType.Error);
                            return;
                        }
                        timeOutRightSend[chatLink.ItemId] = DateTimeOffset.Now;
                    } else {
                        timeOutRightSend.Add(chatLink.ItemId, DateTimeOffset.Now);
                    }
                    chatLink.Quantity = Convert.ToByte(1);
                    GameService.GameIntegration.Chat.Send($"Total: {GetMyQuantity(dropdown.SelectedItem)} of {chatLink} (killproof.me/{MyKillProof.kpid})");
                }
            };
            bgPanel.Disposed += delegate {
                var fadeOut = GameService.Animation.Tweener.Tween(bgPanel, new { Opacity = 0.0f }, 0.2f);
            };
            bgPanel.PropertyChanged += delegate(object sender, PropertyChangedEventArgs e)
            {
                if (!bgPanel.Visible) return;
                quantity.Text = GetMyQuantity(dropdown.SelectedItem).ToString();
            };
            return bgPanel;
        }
        #endregion

        #endregion

        /// <inheritdoc />
        protected override void Unload()
        {
            KillProofQuickMenu?.Dispose();
            SquadPanel?.Dispose();
            LocalPlayerButton?.Dispose();
            foreach (KillProofButton c in DisplayedKillProofs) c?.Dispose();
            GameService.Overlay.BlishHudWindow.RemoveTab(KillProofTab);
            // All static members must be manually unset
            ModuleInstance = null;
        }

    }

}