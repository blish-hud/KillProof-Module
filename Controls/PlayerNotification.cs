using Blish_HUD;
using Blish_HUD.ArcDps.Common;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Nekres.KillProof.Controls {
    public class PlayerNotification : Container {

        private const int NOTIFICATION_WIDTH = 264;
        private const int NOTIFICATION_HEIGHT = 64;

        private const int ICON_SIZE = 64;

        private readonly AsyncTexture2D _icon;

        private static int _visibleNotifications = 0;

        private PlayerNotification(string title, AsyncTexture2D icon, string message) {
            _icon = icon;

            this.Opacity = 0f;
            this.Size = new Point(NOTIFICATION_WIDTH, NOTIFICATION_HEIGHT);
            this.Location = new Point(60, 60 + (NOTIFICATION_HEIGHT + 15) * _visibleNotifications);
            this.BasicTooltipText = "Right click to view profile";

            string wrappedTitle = DrawUtil.WrapText(Content.DefaultFont14, title, this.Width - NOTIFICATION_HEIGHT - 20 - 32);
            var titleLbl = new Label() {
                Parent = this,
                Location = new Point(NOTIFICATION_HEIGHT + 10, 0),
                Size = new Point(this.Width - NOTIFICATION_HEIGHT - 10 - 32, this.Height / 2),
                Font = Content.DefaultFont14,
                Text = wrappedTitle,
            };

            string wrapped = DrawUtil.WrapText(Content.DefaultFont14, message, this.Width - NOTIFICATION_HEIGHT - 20 - 32);
            var messageLbl = new Label() {
                Parent = this,
                Location = new Point(NOTIFICATION_HEIGHT + 10, this.Height / 2),
                Size = new Point(this.Width - NOTIFICATION_HEIGHT - 10 - 32, this.Height / 2),
                Text = wrapped,
            };

            _visibleNotifications++;

            this.RightMouseButtonReleased += delegate {
                GameService.Overlay.BlishHudWindow.Show();
                GameService.Overlay.BlishHudWindow.Navigate(KillProofModule.ModuleInstance.BuildKillProofPanel(GameService.Overlay.BlishHudWindow, new CommonFields.Player(null, title, 0, 0, false)));
                this.Dispose();
            };
        }

        protected override CaptureType CapturesInput() {
            return CaptureType.Mouse;
        }

        private Rectangle _layoutIconBounds;

        /// <inheritdoc />
        public override void RecalculateLayout() {
            int icoSize = 52;

            _layoutIconBounds = new Rectangle(NOTIFICATION_HEIGHT / 2 - icoSize / 2,
                                              NOTIFICATION_HEIGHT / 2 - icoSize / 2,
                                              icoSize,
                                              icoSize);
        }

        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds) {
            spriteBatch.DrawOnCtrl(this,
                                   KillProofModule.ModuleInstance._notificationBackroundTexture,
                                   bounds,
                                   Color.White * 0.85f);

            spriteBatch.DrawOnCtrl(this,
                                   _icon,
                                   _layoutIconBounds);
        }

        private void Show(float duration) {
            Content.PlaySoundEffectByName(@"audio\color-change");

            Animation.Tweener
                     .Tween(this, new { Opacity = 1f }, 0.2f)
                     .Repeat(1)
                     .RepeatDelay(duration)
                     .Reflect()
                     .OnComplete(Dispose);
        }

        public static void ShowNotification(string title, AsyncTexture2D icon, string message, float duration) {
            var notif = new PlayerNotification(title, icon, message) {
                Parent = Graphics.SpriteScreen
            };

            notif.Show(duration);
        }

        protected override void DisposeControl() {
            _visibleNotifications--;

            base.DisposeControl();
        }

    }
}
