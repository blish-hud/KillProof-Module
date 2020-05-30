using Blish_HUD;
using Blish_HUD.ArcDps.Common;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;

namespace KillProofModule.Controls
{
    public class PlayerButton : DetailsButton
    {
        private const int DEFAULT_WIDTH = 327;
        private const int DEFAULT_HEIGHT = 100;
        private const int DEFAULT_BOTTOMSECTION_HEIGHT = 35;

        private readonly Texture2D PIXEL;
        private readonly Texture2D BORDER_SPRITE;
        private readonly Texture2D SEPARATOR;

        private BitmapFont _font;
        public BitmapFont Font
        {
            get => _font;
            set
            {
                if (_font == value) return;
                _font = value;
                OnPropertyChanged();
            }
        }

        private CommonFields.Player _player;
        public CommonFields.Player Player
        {
            get => _player;
            set
            {
                _player = value;
                OnPropertyChanged();
            }
        }

        private bool _isNew = true;
        public bool IsNew
        {
            get => _isNew;
            set
            {
                if (_isNew == value) return;
                _isNew = value;
                OnPropertyChanged();
            }
        }

        public PlayerButton() {
            this.BORDER_SPRITE = BORDER_SPRITE ?? Content.GetTexture(@"controls/detailsbutton/605003");
            this.SEPARATOR = SEPARATOR ?? Content.GetTexture("157218");
            this.PIXEL = PIXEL ?? ContentService.Textures.Pixel;

            this.Size = new Point(DEFAULT_WIDTH, DEFAULT_HEIGHT);
        }

        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds) {
            // Draw background
            spriteBatch.DrawOnCtrl(this, PIXEL, bounds, Color.Black * 0.25f);

            // Draw bottom section
            spriteBatch.DrawOnCtrl(this, PIXEL, this.ContentRegion, Color.Black * 0.1f);

            int iconSize = this.IconSize == DetailsIconSize.Large ? DEFAULT_HEIGHT : DEFAULT_HEIGHT - DEFAULT_BOTTOMSECTION_HEIGHT;

            // Draw bottom text
            spriteBatch.DrawStringOnCtrl(this, Player.AccountName, Content.DefaultFont14, new Rectangle(iconSize + 20, iconSize - DEFAULT_BOTTOMSECTION_HEIGHT, DEFAULT_WIDTH - 40, DEFAULT_BOTTOMSECTION_HEIGHT), Color.White, false, true, 2);

            if (this.Icon != null) {
                // Draw icon
                spriteBatch.DrawOnCtrl(this, this.Icon, new Rectangle(iconSize / 2 - 64 / 2 + (this.IconSize == DetailsIconSize.Small ? 10 : 0), iconSize / 2 - 64 / 2, 64, 64), Color.White);

                // Draw icon box
                if (this.IconSize == DetailsIconSize.Large)
                    spriteBatch.DrawOnCtrl(this, BORDER_SPRITE, new Rectangle(0, 0, iconSize, iconSize), Color.White);
            }

            // Draw bottom section seperator
            spriteBatch.DrawOnCtrl(this, SEPARATOR, new Rectangle(this.ContentRegion.X, bounds.Height - 40, bounds.Width, 8), Color.White);

            // Wrap text
            if (Player.CharacterName != null && this.Font != null) {
                string wrappedText = DrawUtil.WrapText(this.Font, Player.CharacterName, DEFAULT_WIDTH - 40 - iconSize - 20);

                // Draw name
                spriteBatch.DrawStringOnCtrl(this, wrappedText, this.Font, new Rectangle(iconSize + 20, 0, DEFAULT_WIDTH - iconSize - 20, this.Height - DEFAULT_BOTTOMSECTION_HEIGHT), Color.White, false, true, 2);
            }

            if (this.IsNew) {
                spriteBatch.DrawStringOnCtrl(this, "New", Content.DefaultFont14, new Rectangle(iconSize + 18, 2, DEFAULT_WIDTH - iconSize - 20, this.Height - DEFAULT_BOTTOMSECTION_HEIGHT), Color.Gold, false, true, 2, HorizontalAlignment.Right, VerticalAlignment.Top);
            }
        }
    }
}
