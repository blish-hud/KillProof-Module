using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;

namespace KillProofModule.Controls
{
    public class KillProofButton : DetailsButton
    {
        private const int DEFAULT_WIDTH = 327;
        private const int DEFAULT_HEIGHT = 100;
        private const int DEFAULT_BOTTOMSECTION_HEIGHT = 35;
        private readonly Texture2D BORDER_SPRITE;

        private readonly Texture2D ICON_TITLE;
        private readonly Texture2D PIXEL;
        private readonly Texture2D SEPARATOR;

        private string _bottomText = "z";

        private BitmapFont _font;

        private bool _isTitleDisplay;

        public KillProofButton()
        {
            ICON_TITLE = ICON_TITLE ?? KillProofModule.ModuleInstance._sortByTitleTexture;
            BORDER_SPRITE = BORDER_SPRITE ?? Content.GetTexture(@"controls/detailsbutton/605003");
            SEPARATOR = SEPARATOR ?? Content.GetTexture("157218");
            PIXEL = PIXEL ?? ContentService.Textures.Pixel;

            Size = new Point(DEFAULT_WIDTH, DEFAULT_HEIGHT);
        }

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

        public string BottomText
        {
            get => _bottomText;
            set
            {
                if (_bottomText == value) return;
                _bottomText = value;
            }
        }

        public bool IsTitleDisplay
        {
            get => _isTitleDisplay;
            set
            {
                if (value == _isTitleDisplay) return;
                _isTitleDisplay = value;
            }
        }

        public override void PaintBeforeChildren(SpriteBatch spriteBatch, Rectangle bounds)
        {
            // Draw background
            spriteBatch.DrawOnCtrl(this, PIXEL, bounds, Color.Black * 0.25f);

            // Draw bottom section
            spriteBatch.DrawOnCtrl(this, PIXEL, ContentRegion, Color.Black * 0.1f);

            var iconSize = IconSize == DetailsIconSize.Large
                ? DEFAULT_HEIGHT
                : DEFAULT_HEIGHT - DEFAULT_BOTTOMSECTION_HEIGHT;

            // Draw bottom text
            if (IsTitleDisplay)
                spriteBatch.DrawOnCtrl(this, ICON_TITLE,
                    new Rectangle(DEFAULT_WIDTH - 36, bounds.Height - DEFAULT_BOTTOMSECTION_HEIGHT + 1, 32, 32),
                    Color.White);
            else
                spriteBatch.DrawStringOnCtrl(this, BottomText, Content.DefaultFont14,
                    new Rectangle(iconSize + 20, iconSize - DEFAULT_BOTTOMSECTION_HEIGHT, DEFAULT_WIDTH - 40,
                        DEFAULT_BOTTOMSECTION_HEIGHT), Color.White, false, true, 2);
            if (Icon != null && Icon.HasTexture)
            {
                // Draw icon
                spriteBatch.DrawOnCtrl(this, Icon,
                    new Rectangle(iconSize / 2 - 64 / 2 + (IconSize == DetailsIconSize.Small ? 10 : 0),
                        iconSize / 2 - 64 / 2, 64, 64), Color.White);

                // Draw icon box
                if (IconSize == DetailsIconSize.Large)
                    spriteBatch.DrawOnCtrl(this, BORDER_SPRITE, new Rectangle(0, 0, iconSize, iconSize), Color.White);
            }

            // Draw bottom section seperator
            spriteBatch.DrawOnCtrl(this, SEPARATOR, new Rectangle(ContentRegion.X, bounds.Height - 40, bounds.Width, 8),
                Color.White);

            // Wrap text
            var wrappedText = DrawUtil.WrapText(Font, Text, DEFAULT_WIDTH - 40 - iconSize - 20);

            // Draw name
            spriteBatch.DrawStringOnCtrl(this, wrappedText, Font,
                new Rectangle(iconSize + 20, 0, DEFAULT_WIDTH - iconSize - 20, Height - DEFAULT_BOTTOMSECTION_HEIGHT),
                Color.White, false, true, 2);
        }
    }
}