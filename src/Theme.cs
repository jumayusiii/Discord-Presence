using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace DiscordPresence
{
    /// <summary>Material-inspired design tokens and shared UI helpers.</summary>
    public static class Theme
    {
        public enum Appearance
        {
            Dark,
            Light
        }

        // The stock Discord blurple and the two surfaces the built-in themes
        // are built from. A custom theme keeps the same structure but anchors
        // it to the three colours the user picked.
        private static readonly Color BuiltinAccent = Color.FromRgb(0x72, 0x89, 0xDA);
        private static readonly Color DarkSurface = Color.FromRgb(0x1E, 0x21, 0x24);
        private static readonly Color LightSurface = Color.FromRgb(0xF2, 0xF3, 0xF5);

        /// <summary>The accent in force right now, custom themes included.</summary>
        public static Color AccentColor { get; private set; }

        /// <summary>The window background in force right now.</summary>
        public static Color SurfaceColor { get; private set; }

        /// <summary>True while the user's own three colours are in charge.</summary>
        public static bool CustomActive { get; private set; }

        /// <summary>True while the palette in force is a dark one.</summary>
        public static bool IsDark { get; private set; }

        static Theme()
        {
            AccentColor = BuiltinAccent;
            SurfaceColor = DarkSurface;
            IsDark = true;
        }

        // Palette brushes stay mutable so switching appearance only has to
        // repaint these instances; every element shares them.
        public static readonly SolidColorBrush Accent = Solid(BuiltinAccent);
        public static readonly SolidColorBrush AccentHover = Solid(Color.FromRgb(0x82, 0x98, 0xE2));
        public static readonly SolidColorBrush AccentSoft = Solid(Color.FromArgb(0x2E, 0x72, 0x89, 0xDA));
        public static readonly SolidColorBrush Surface = Solid(SurfaceColor);
        public static readonly SolidColorBrush SurfaceAlt = Solid(Color.FromRgb(0x1B, 0x1E, 0x21));
        public static readonly SolidColorBrush TextPrimary = Solid(Colors.White);
        public static readonly SolidColorBrush TextSecondary = Solid(Color.FromRgb(0xCA, 0xD0, 0xD8));
        public static readonly SolidColorBrush TextMuted = Solid(Color.FromRgb(0x95, 0x9C, 0xA5));
        public static readonly SolidColorBrush CardBrush = Solid(Color.FromRgb(0x24, 0x27, 0x2C));
        public static readonly SolidColorBrush CardBorder = Solid(Color.FromRgb(0x2E, 0x33, 0x39));
        public static readonly SolidColorBrush FieldBrush = Solid(Color.FromRgb(0x1A, 0x1D, 0x20));
        public static readonly SolidColorBrush InputBorder = Solid(Color.FromRgb(0x3A, 0x40, 0x48));
        public static readonly SolidColorBrush ChipFill = Solid(Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));
        public static readonly SolidColorBrush ChipBorder = Solid(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
        public static readonly SolidColorBrush SelectedFill = Solid(Color.FromRgb(0x2B, 0x33, 0x48));
        public static readonly SolidColorBrush HoverOverlay = Solid(Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF));
        public static readonly SolidColorBrush HoverOverlayStrong = Solid(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
        public static readonly SolidColorBrush HoverBorder = Solid(Color.FromRgb(0x4A, 0x51, 0x5A));
        public static readonly SolidColorBrush Scrim = Solid(Color.FromArgb(0xC0, 0x00, 0x00, 0x00));
        public static readonly SolidColorBrush TooltipBrush = Solid(Color.FromRgb(0x2E, 0x32, 0x38));
        public static readonly SolidColorBrush ThumbBrush = Solid(Color.FromRgb(0x4C, 0x53, 0x5D));
        public static readonly SolidColorBrush ThumbHoverBrush = Solid(Color.FromRgb(0x59, 0x61, 0x6C));
        public static readonly SolidColorBrush CloseHover = Solid(Color.FromRgb(0xE8, 0x11, 0x23));
        public static readonly SolidColorBrush Success = Solid(Color.FromRgb(0x43, 0xB5, 0x81));
        public static readonly SolidColorBrush Danger = Solid(Color.FromRgb(0xED, 0x42, 0x45));
        public static readonly SolidColorBrush Warning = Solid(Color.FromRgb(0xFA, 0xA6, 0x1A));

        /// <summary>Ink that stays readable on top of the accent colour.</summary>
        public static readonly SolidColorBrush OnAccent = Solid(Colors.White);

        public static Appearance Current { get; private set; }

        public static readonly FontFamily UiFont = new FontFamily("Segoe UI");
        public static readonly FontFamily IconFont = new FontFamily("Segoe MDL2 Assets");
        public static readonly FontFamily EmojiFont = new FontFamily("Segoe UI Emoji");

        private const string StylesXaml = @"
<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
  <Style x:Key=""BaseButton"" TargetType=""Button"">
    <Setter Property=""Foreground"" Value=""#FFFFFF""/>
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""BorderBrush"" Value=""Transparent""/>
    <Setter Property=""BorderThickness"" Value=""0""/>
    <Setter Property=""FontFamily"" Value=""Segoe UI""/>
    <Setter Property=""FontSize"" Value=""12.5""/>
    <Setter Property=""FontWeight"" Value=""SemiBold""/>
    <Setter Property=""Padding"" Value=""16,0""/>
    <Setter Property=""Height"" Value=""40""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""FocusVisualStyle"" Value=""{x:Null}""/>
    <Setter Property=""SnapsToDevicePixels"" Value=""True""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Button"">
          <Border x:Name=""Bd"" Background=""{TemplateBinding Background}""
                  BorderBrush=""{TemplateBinding BorderBrush}""
                  BorderThickness=""{TemplateBinding BorderThickness}""
                  CornerRadius=""9"" SnapsToDevicePixels=""True"">
            <Grid>
              <ContentPresenter Margin=""{TemplateBinding Padding}""
                                HorizontalAlignment=""Center"" VerticalAlignment=""Center""
                                RecognizesAccessKey=""True""/>
              <Border x:Name=""Hover"" Background=""{DynamicResource HoverOverlayBrush}"" CornerRadius=""9""
                      Opacity=""0"" IsHitTestVisible=""False""/>
            </Grid>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""Hover"" Property=""Opacity"" Value=""1""/>
            </Trigger>
            <Trigger Property=""IsPressed"" Value=""True"">
              <Setter TargetName=""Hover"" Property=""Background"" Value=""{DynamicResource HoverOverlayStrongBrush}""/>
              <Setter TargetName=""Hover"" Property=""Opacity"" Value=""1""/>
            </Trigger>
            <Trigger Property=""IsEnabled"" Value=""False"">
              <Setter Property=""Opacity"" Value=""0.45""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key=""PrimaryButton"" TargetType=""Button"" BasedOn=""{StaticResource BaseButton}"">
    <Setter Property=""Background"" Value=""{DynamicResource AccentBrush}""/>
    <Setter Property=""Foreground"" Value=""{DynamicResource OnAccentBrush}""/>
  </Style>

  <Style x:Key=""OutlineButton"" TargetType=""Button"" BasedOn=""{StaticResource BaseButton}"">
    <Setter Property=""Background"" Value=""{DynamicResource ChipFillBrush}""/>
    <Setter Property=""BorderBrush"" Value=""{DynamicResource InputBorderBrush}""/>
    <Setter Property=""BorderThickness"" Value=""1""/>
    <Setter Property=""Foreground"" Value=""{DynamicResource TextPrimaryBrush}""/>
  </Style>

  <Style x:Key=""DangerButton"" TargetType=""Button"" BasedOn=""{StaticResource BaseButton}"">
    <Setter Property=""Background"" Value=""#ED4245""/>
  </Style>

  <Style x:Key=""GhostButton"" TargetType=""Button"" BasedOn=""{StaticResource BaseButton}"">
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""Foreground"" Value=""{DynamicResource TextSecondaryBrush}""/>
  </Style>

  <Style x:Key=""PillButton"" TargetType=""Button"" BasedOn=""{StaticResource BaseButton}"">
    <Setter Property=""Background"" Value=""{DynamicResource ChipFillBrush}""/>
    <Setter Property=""BorderBrush"" Value=""Transparent""/>
    <Setter Property=""BorderThickness"" Value=""0""/>
    <Setter Property=""Foreground"" Value=""{DynamicResource TextSecondaryBrush}""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Button"">
          <Border x:Name=""Bd"" Background=""{TemplateBinding Background}""
                  BorderBrush=""{TemplateBinding BorderBrush}""
                  BorderThickness=""{TemplateBinding BorderThickness}""
                  CornerRadius=""14"" SnapsToDevicePixels=""True"">
            <Grid>
              <ContentPresenter Margin=""{TemplateBinding Padding}""
                                HorizontalAlignment=""Center"" VerticalAlignment=""Center""
                                RecognizesAccessKey=""True""/>
              <Border x:Name=""Hover"" Background=""{DynamicResource HoverOverlayBrush}"" CornerRadius=""14""
                      Opacity=""0"" IsHitTestVisible=""False""/>
              <Border x:Name=""Focus"" BorderBrush=""{DynamicResource AccentBrush}"" BorderThickness=""1"" CornerRadius=""14""
                      Opacity=""0"" IsHitTestVisible=""False""/>
            </Grid>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""Hover"" Property=""Opacity"" Value=""1""/>
            </Trigger>
            <Trigger Property=""IsPressed"" Value=""True"">
              <Setter TargetName=""Hover"" Property=""Background"" Value=""{DynamicResource HoverOverlayStrongBrush}""/>
              <Setter TargetName=""Hover"" Property=""Opacity"" Value=""1""/>
            </Trigger>
            <Trigger Property=""IsKeyboardFocused"" Value=""True"">
              <Setter TargetName=""Focus"" Property=""Opacity"" Value=""1""/>
            </Trigger>
            <Trigger Property=""IsEnabled"" Value=""False"">
              <Setter Property=""Opacity"" Value=""0.45""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key=""FieldTextBox"" TargetType=""TextBox"">
    <Setter Property=""Foreground"" Value=""{DynamicResource TextPrimaryBrush}""/>
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""BorderThickness"" Value=""0""/>
    <Setter Property=""FontFamily"" Value=""Segoe UI""/>
    <Setter Property=""FontSize"" Value=""13""/>
    <Setter Property=""CaretBrush"" Value=""{DynamicResource AccentBrush}""/>
    <Setter Property=""SelectionBrush"" Value=""{DynamicResource AccentBrush}""/>
    <Setter Property=""SelectionOpacity"" Value=""0.45""/>
    <Setter Property=""Padding"" Value=""0""/>
    <Setter Property=""VerticalContentAlignment"" Value=""Center""/>
    <Setter Property=""FocusVisualStyle"" Value=""{x:Null}""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""TextBox"">
          <ScrollViewer x:Name=""PART_ContentHost"" Margin=""13,0""
                        Focusable=""False"" HorizontalScrollBarVisibility=""Hidden""
                        VerticalScrollBarVisibility=""Hidden"" VerticalAlignment=""Center""/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""ToolTip"">
    <Setter Property=""Foreground"" Value=""{DynamicResource TextPrimaryBrush}""/>
    <Setter Property=""Background"" Value=""{DynamicResource TooltipBrush}""/>
    <Setter Property=""BorderBrush"" Value=""{DynamicResource InputBorderBrush}""/>
    <Setter Property=""BorderThickness"" Value=""1""/>
    <Setter Property=""Padding"" Value=""9,6""/>
    <Setter Property=""FontFamily"" Value=""Segoe UI""/>
    <Setter Property=""FontSize"" Value=""11.5""/>
    <Setter Property=""HasDropShadow"" Value=""False""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ToolTip"">
          <Border Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}""
                  BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""6""
                  Padding=""{TemplateBinding Padding}"" SnapsToDevicePixels=""True"">
            <ContentPresenter/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""ScrollBar"">
    <Setter Property=""Width"" Value=""12""/>
    <Setter Property=""MinWidth"" Value=""12""/>
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ScrollBar"">
          <Grid Background=""Transparent"">
            <Track x:Name=""PART_Track"" IsDirectionReversed=""True"">
              <Track.DecreaseRepeatButton>
                <RepeatButton Command=""{x:Static ScrollBar.PageUpCommand}"" Focusable=""False"" IsTabStop=""False"">
                  <RepeatButton.Template>
                    <ControlTemplate TargetType=""RepeatButton"">
                      <Border Background=""Transparent""/>
                    </ControlTemplate>
                  </RepeatButton.Template>
                </RepeatButton>
              </Track.DecreaseRepeatButton>
              <Track.Thumb>
                <Thumb MinHeight=""28"">
                  <Thumb.Template>
                    <ControlTemplate TargetType=""Thumb"">
                      <Border x:Name=""ThumbBorder"" Background=""{DynamicResource ThumbBrush}"" CornerRadius=""4"" Margin=""3,2""/>
                      <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                          <Setter TargetName=""ThumbBorder"" Property=""Background"" Value=""{DynamicResource ThumbHoverBrush}""/>
                        </Trigger>
                      </ControlTemplate.Triggers>
                    </ControlTemplate>
                  </Thumb.Template>
                </Thumb>
              </Track.Thumb>
              <Track.IncreaseRepeatButton>
                <RepeatButton Command=""{x:Static ScrollBar.PageDownCommand}"" Focusable=""False"" IsTabStop=""False"">
                  <RepeatButton.Template>
                    <ControlTemplate TargetType=""RepeatButton"">
                      <Border Background=""Transparent""/>
                    </ControlTemplate>
                  </RepeatButton.Template>
                </RepeatButton>
              </Track.IncreaseRepeatButton>
            </Track>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>";

        public static void Initialize(Application app)
        {
            RefreshStyleBrushes();

            ResourceDictionary styles = (ResourceDictionary)XamlReader.Parse(StylesXaml);
            app.Resources.MergedDictionaries.Add(styles);
        }

        /// <summary>
        /// Styles freeze the brushes they are given, so they get their own
        /// copies. Replacing these entries repaints every styled control.
        /// </summary>
        private static void RefreshStyleBrushes()
        {
            Application app = Application.Current;
            if (app == null)
            {
                return;
            }
            app.Resources["AccentBrush"] = StyleBrush(Accent);
            app.Resources["OnAccentBrush"] = StyleBrush(OnAccent);
            app.Resources["TextPrimaryBrush"] = StyleBrush(TextPrimary);
            app.Resources["TextSecondaryBrush"] = StyleBrush(TextSecondary);
            app.Resources["InputBorderBrush"] = StyleBrush(InputBorder);
            app.Resources["ChipFillBrush"] = StyleBrush(ChipFill);
            app.Resources["ChipBorderBrush"] = StyleBrush(ChipBorder);
            app.Resources["HoverOverlayBrush"] = StyleBrush(HoverOverlay);
            app.Resources["HoverBorderBrush"] = StyleBrush(HoverBorder);
            app.Resources["HoverOverlayStrongBrush"] = StyleBrush(HoverOverlayStrong);
            app.Resources["TooltipBrush"] = StyleBrush(TooltipBrush);
            app.Resources["ThumbBrush"] = StyleBrush(ThumbBrush);
            app.Resources["ThumbHoverBrush"] = StyleBrush(ThumbHoverBrush);
        }

        private static SolidColorBrush StyleBrush(SolidColorBrush source)
        {
            return new SolidColorBrush(source.Color);
        }

        /// <summary>Repaints every shared palette brush for the chosen appearance.</summary>
        public static void Apply(Appearance appearance)
        {
            Current = appearance;
            CustomActive = false;
            bool light = appearance == Appearance.Light;
            IsDark = !light;

            AccentColor = BuiltinAccent;
            SurfaceColor = light ? LightSurface : DarkSurface;
            Set(Accent, AccentColor);
            Set(AccentHover, Color.FromRgb(0x82, 0x98, 0xE2));
            Set(AccentSoft, Color.FromArgb(0x2E, 0x72, 0x89, 0xDA));
            Set(OnAccent, Colors.White);

            Set(Surface, SurfaceColor);
            Set(SurfaceAlt, light ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0x1B, 0x1E, 0x21));
            Set(CardBrush, light ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0x24, 0x27, 0x2C));
            Set(CardBorder, light ? Color.FromRgb(0xE2, 0xE5, 0xEA) : Color.FromRgb(0x2E, 0x33, 0x39));
            Set(FieldBrush, light ? Color.FromRgb(0xF7, 0xF8, 0xFA) : Color.FromRgb(0x1A, 0x1D, 0x20));
            Set(InputBorder, light ? Color.FromRgb(0xCB, 0xD1, 0xD9) : Color.FromRgb(0x3A, 0x40, 0x48));
            Set(TextPrimary, light ? Color.FromRgb(0x1F, 0x23, 0x28) : Colors.White);
            Set(TextSecondary, light ? Color.FromRgb(0x4C, 0x53, 0x5C) : Color.FromRgb(0xCA, 0xD0, 0xD8));
            Set(TextMuted, light ? Color.FromRgb(0x73, 0x7B, 0x85) : Color.FromRgb(0x95, 0x9C, 0xA5));
            Set(ChipFill, light ? Color.FromArgb(0x08, 0x00, 0x00, 0x00) : Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));
            Set(ChipBorder, light ? Color.FromArgb(0x1A, 0x00, 0x00, 0x00) : Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
            Set(SelectedFill, light ? Color.FromRgb(0xE4, 0xE9, 0xFA) : Color.FromRgb(0x2B, 0x33, 0x48));
            Set(HoverOverlay, light ? Color.FromArgb(0x10, 0x00, 0x00, 0x00) : Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF));
            Set(HoverOverlayStrong, light ? Color.FromArgb(0x1F, 0x00, 0x00, 0x00) : Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
            Set(HoverBorder, light ? Color.FromRgb(0xB9, 0xC0, 0xCA) : Color.FromRgb(0x4A, 0x51, 0x5A));
            Set(Scrim, light ? Color.FromArgb(0x59, 0x00, 0x00, 0x00) : Color.FromArgb(0xC0, 0x00, 0x00, 0x00));
            Set(TooltipBrush, light ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0x2E, 0x32, 0x38));
            Set(ThumbBrush, light ? Color.FromRgb(0xC2, 0xC8, 0xD1) : Color.FromRgb(0x4C, 0x53, 0x5D));
            Set(ThumbHoverBrush, light ? Color.FromRgb(0xA9, 0xB1, 0xBC) : Color.FromRgb(0x59, 0x61, 0x6C));

            RefreshStyleBrushes();
        }

        /// <summary>
        /// Paints the whole palette from three anchors the user picked. The
        /// derived tones follow the surface's own brightness, so a light
        /// surface gets light cards and dark borders, and a dark one the other
        /// way round. The built-in palettes are these same tones with the stock
        /// Discord colours as the anchors.
        /// </summary>
        public static void ApplyCustom(Color surface, Color accent, Color text)
        {
            CustomActive = true;
            bool dark = Luminance(surface) < 0.5;
            IsDark = dark;
            Current = dark ? Appearance.Dark : Appearance.Light;

            SurfaceColor = surface;
            AccentColor = accent;

            Set(Surface, surface);
            if (dark)
            {
                Set(SurfaceAlt, Tone(surface, -0.09));
                Set(CardBrush, Tone(surface, 0.03));
                Set(CardBorder, Tone(surface, 0.08));
                Set(FieldBrush, Tone(surface, -0.12));
                Set(InputBorder, Tone(surface, 0.15));
                Set(HoverBorder, Tone(surface, 0.22));
                Set(TooltipBrush, Tone(surface, 0.09));
                Set(ThumbBrush, Tone(surface, 0.22));
                Set(ThumbHoverBrush, Tone(surface, 0.30));
            }
            else
            {
                Set(SurfaceAlt, Tone(surface, 1.0));
                Set(CardBrush, Tone(surface, 1.0));
                Set(CardBorder, Tone(surface, -0.06));
                Set(FieldBrush, Tone(surface, 0.42));
                Set(InputBorder, Tone(surface, -0.15));
                Set(HoverBorder, Tone(surface, -0.21));
                Set(TooltipBrush, Tone(surface, 1.0));
                Set(ThumbBrush, Tone(surface, -0.18));
                Set(ThumbHoverBrush, Tone(surface, -0.27));
            }

            Set(TextPrimary, text);
            Set(TextSecondary, dark ? Mix(text, surface, 0.35) : Mix(text, surface, 0.28));
            Set(TextMuted, dark ? Mix(text, surface, 0.55) : Mix(text, surface, 0.50));

            Set(Accent, accent);
            Set(AccentHover, dark ? Tone(accent, 0.13) : Tone(accent, -0.13));
            Set(AccentSoft, Color.FromArgb(0x2E, accent.R, accent.G, accent.B));
            Set(SelectedFill, Mix(surface, accent, dark ? 0.20 : 0.17));
            Set(OnAccent, Luminance(accent) > 0.62
                ? Color.FromRgb(0x1B, 0x1E, 0x21)
                : Colors.White);

            Set(ChipFill, dark ? Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x08, 0x00, 0x00, 0x00));
            Set(ChipBorder, dark ? Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x1A, 0x00, 0x00, 0x00));
            Set(HoverOverlay, dark ? Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x10, 0x00, 0x00, 0x00));
            Set(HoverOverlayStrong, dark ? Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x1F, 0x00, 0x00, 0x00));
            Set(Scrim, dark ? Color.FromArgb(0xC0, 0x00, 0x00, 0x00) : Color.FromArgb(0x59, 0x00, 0x00, 0x00));

            RefreshStyleBrushes();
        }

        /// <summary>
        /// The three anchors the stock palette is built from, so the custom
        /// theme can start from exactly what the user is looking at.
        /// </summary>
        public static void BuiltinAnchors(bool light, out Color surface, out Color accent, out Color text)
        {
            surface = light ? LightSurface : DarkSurface;
            accent = BuiltinAccent;
            text = light ? Color.FromRgb(0x1F, 0x23, 0x28) : Colors.White;
        }

        /// <summary>Perceived brightness of a colour, 0 (black) to 1 (white).</summary>
        public static double Luminance(Color color)
        {
            return (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
        }

        /// <summary>
        /// WCAG relative luminance: the gamma-corrected brightness used for
        /// contrast maths, not the quick perceived one above.
        /// </summary>
        public static double RelativeLuminance(Color color)
        {
            return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        }

        private static double Linear(byte channel)
        {
            double value = channel / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        /// <summary>WCAG contrast ratio between two colours, 1:1 to 21:1.</summary>
        public static double ContrastRatio(Color first, Color second)
        {
            double a = RelativeLuminance(first);
            double b = RelativeLuminance(second);
            double lighter = Math.Max(a, b);
            double darker = Math.Min(a, b);
            return (lighter + 0.05) / (darker + 0.05);
        }

        /// <summary>White or near-black, whichever reads better on the colour.</summary>
        public static Color ReadableInk(Color background)
        {
            Color dark = Color.FromRgb(0x1B, 0x1E, 0x21);
            return ContrastRatio(Colors.White, background) >= ContrastRatio(dark, background)
                ? Colors.White
                : dark;
        }

        /// <summary>Blends two colours; <paramref name="amount"/> 0 keeps the first.</summary>
        public static Color Mix(Color from, Color to, double amount)
        {
            amount = Clamp(amount);
            return Color.FromRgb(
                Channel(from.R, to.R, amount),
                Channel(from.G, to.G, amount),
                Channel(from.B, to.B, amount));
        }

        /// <summary>Lightens (positive) or darkens (negative) a colour.</summary>
        public static Color Tone(Color color, double amount)
        {
            return amount >= 0 ? Mix(color, Colors.White, amount) : Mix(color, Colors.Black, -amount);
        }

        private static byte Channel(byte from, byte to, double amount)
        {
            return (byte)Math.Round(from + (to - from) * amount);
        }

        private static double Clamp(double value)
        {
            if (value < 0)
            {
                return 0;
            }
            return value > 1 ? 1 : value;
        }

        /// <summary>Reads #RRGGBB (with or without the hash) into a colour.</summary>
        public static bool TryParseHex(string text, out Color color)
        {
            color = Colors.Black;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            string value = text.Trim().TrimStart('#');
            if (value.Length != 6)
            {
                return false;
            }
            for (int i = 0; i < value.Length; i++)
            {
                if (!Uri.IsHexDigit(value[i]))
                {
                    return false;
                }
            }
            color = Color.FromRgb(
                Convert.ToByte(value.Substring(0, 2), 16),
                Convert.ToByte(value.Substring(2, 2), 16),
                Convert.ToByte(value.Substring(4, 2), 16));
            return true;
        }

        /// <summary>Formats a colour the way the picker shows it.</summary>
        public static string ToHex(Color color)
        {
            return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }

        private static void Set(SolidColorBrush brush, Color color)
        {
            if (brush.Color != color)
            {
                brush.Color = color;
            }
        }

        public static Style FindStyle(string key)
        {
            return Application.Current.TryFindResource(key) as Style;
        }

        private static SolidColorBrush Solid(Color color)
        {
            return new SolidColorBrush(color);
        }

        public static TextBlock Text(string text, double size, FontWeight weight, Brush brush)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = UiFont,
                FontSize = size,
                FontWeight = weight,
                Foreground = brush ?? TextPrimary,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        public static TextBlock Glyph(string glyph, double size, Brush brush)
        {
            return new TextBlock
            {
                Text = glyph,
                FontFamily = IconFont,
                FontSize = size,
                Foreground = brush ?? TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
        }

        /// <summary>
        /// Emoji use the colour emoji font and ideal formatting so Windows can
        /// render the full glyph rather than a small monochrome fallback.
        /// </summary>
        public static TextBlock Emoji(string emoji, double size)
        {
            TextBlock block = new TextBlock
            {
                Text = emoji,
                FontFamily = EmojiFont,
                FontSize = size,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            TextOptions.SetTextFormattingMode(block, TextFormattingMode.Ideal);
            return block;
        }

        public static StackPanel SectionHeader(string title, string subtitle)
        {
            StackPanel header = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            header.Children.Add(Text(title, 15.5, FontWeights.SemiBold, TextPrimary));
            if (!string.IsNullOrEmpty(subtitle))
            {
                TextBlock sub = Text(subtitle, 11.5, FontWeights.Normal, TextMuted);
                sub.TextWrapping = TextWrapping.Wrap;
                sub.Margin = new Thickness(0, 3, 0, 0);
                header.Children.Add(sub);
            }
            return header;
        }

        public static Border Card(UIElement content)
        {
            Border card = new Border
            {
                Background = CardBrush,
                BorderBrush = CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18, 16, 18, 18),
                Child = content
            };
            Elevate(card, new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 18,
                ShadowDepth = 3,
                Direction = 270,
                Opacity = 0.28,
                RenderingBias = RenderingBias.Performance
            });
            return card;
        }

        private sealed class ElevatedElement
        {
            public FrameworkElement Element;
            public Effect Shadow;
        }

        // Blur effects are the most expensive visuals in this app, so every one
        // of them is registered here. Performance mode drops them at runtime.
        private static readonly List<ElevatedElement> Elevated = new List<ElevatedElement>();
        private static bool _performanceMode;

        public static bool PerformanceMode
        {
            get { return _performanceMode; }
        }

        /// <summary>Attaches a soft shadow that Performance mode can drop later.</summary>
        public static void Elevate(FrameworkElement element, Effect shadow)
        {
            if (element == null)
            {
                return;
            }
            Elevated.Add(new ElevatedElement { Element = element, Shadow = shadow });
            element.Effect = _performanceMode ? null : shadow;
        }

        public static void SetPerformanceMode(bool enabled)
        {
            _performanceMode = enabled;
            foreach (ElevatedElement entry in Elevated)
            {
                entry.Element.Effect = enabled ? null : entry.Shadow;
            }
        }

        /// <summary>The small accent-tinted BETA marker used by preview features.</summary>
        public static Border BetaChip()
        {
            return new Border
            {
                Background = AccentSoft,
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(7, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = Text("BETA", 8.5, FontWeights.Bold, Accent),
                ToolTip = "Preview feature: behaviour can still change in a later release."
            };
        }

        /// <summary>
        /// A header control shaped like Discord's connection status: a flat
        /// rounded surface holding a short label, with room for one leading or
        /// trailing element such as the unseen-release count.
        /// </summary>
        public static Button Pill(string label, string tooltip, out TextBlock labelText,
            UIElement leading = null, UIElement trailing = null)
        {
            StackPanel row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            FrameworkElement first = leading as FrameworkElement;
            if (first != null)
            {
                first.Margin = new Thickness(0, 0, 6, 0);
                row.Children.Add(first);
            }
            TextBlock text = Text(label, 11.5, FontWeights.SemiBold, TextSecondary);
            row.Children.Add(text);
            FrameworkElement last = trailing as FrameworkElement;
            if (last != null)
            {
                last.Margin = new Thickness(7, 0, 0, 0);
                row.Children.Add(last);
            }

            Button button = new Button
            {
                Style = FindStyle("PillButton"),
                Content = row,
                Height = 28,
                Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = tooltip
            };
            AutomationProperties.SetName(button, label);

            // The label keeps a palette brush so it tracks the theme, and the
            // pill brightens it under the pointer.
            button.MouseEnter += delegate { text.Foreground = TextPrimary; };
            button.MouseLeave += delegate { text.Foreground = TextSecondary; };
            labelText = text;
            return button;
        }

        public static Button Pill(string label, string tooltip)
        {
            TextBlock ignored;
            return Pill(label, tooltip, out ignored);
        }

        public static StackPanel VStack(double gap, params UIElement[] children)
        {
            StackPanel panel = new StackPanel { Orientation = Orientation.Vertical };
            for (int i = 0; i < children.Length; i++)
            {
                if (i > 0 && gap > 0)
                {
                    FrameworkElement child = children[i] as FrameworkElement;
                    if (child != null)
                    {
                        child.Margin = new Thickness(child.Margin.Left, child.Margin.Top + gap,
                            child.Margin.Right, child.Margin.Bottom);
                    }
                }
                panel.Children.Add(children[i]);
            }
            return panel;
        }

        public static StackPanel HStack(double gap, params UIElement[] children)
        {
            StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            for (int i = 0; i < children.Length; i++)
            {
                if (i > 0 && gap > 0)
                {
                    FrameworkElement child = children[i] as FrameworkElement;
                    if (child != null)
                    {
                        child.Margin = new Thickness(child.Margin.Left + gap, child.Margin.Top,
                            child.Margin.Right, child.Margin.Bottom);
                    }
                }
                panel.Children.Add(children[i]);
            }
            return panel;
        }

        /// <summary>A row of equal-width columns separated by <paramref name="gap"/>.</summary>
        public static Grid Row(double gap, params UIElement[] children)
        {
            Grid grid = new Grid();
            for (int i = 0; i < children.Length; i++)
            {
                if (i > 0)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gap) });
                }
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                Grid.SetColumn(children[i], i * 2);
                grid.Children.Add(children[i]);
            }
            return grid;
        }

        public static Button MakeButton(string text, string glyph, string styleKey)
        {
            StackPanel content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (!string.IsNullOrEmpty(glyph))
            {
                TextBlock icon = Glyph(glyph, 12.5, TextPrimary);
                icon.Margin = new Thickness(0, 0, 7, 0);
                content.Children.Add(icon);
            }
            TextBlock label = Text(text, 12.5, FontWeights.SemiBold, TextPrimary);
            content.Children.Add(label);

            return new Button
            {
                Style = FindStyle(styleKey),
                Content = content,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        public static TextBlock Link(string text, string url)
        {
            TextBlock link = Text(text, 11.5, FontWeights.SemiBold, Accent);
            link.HorizontalAlignment = HorizontalAlignment.Left;
            link.Cursor = System.Windows.Input.Cursors.Hand;
            link.MouseEnter += delegate { link.TextDecorations = TextDecorations.Underline; };
            link.MouseLeave += delegate { link.TextDecorations = null; };
            link.MouseLeftButtonUp += delegate
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch
                {
                    // Opening a browser is a convenience; never fail the UI over it.
                }
            };
            return link;
        }

        public static FrameworkElement Spacer(double height)
        {
            return new Border { Height = height, Background = Brushes.Transparent };
        }
    }
}
