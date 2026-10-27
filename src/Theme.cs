using System;
using System.Diagnostics;
using System.Windows;
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

        public static readonly Color AccentColor = Color.FromRgb(0x72, 0x89, 0xDA);
        public static readonly Color SurfaceColor = Color.FromRgb(0x1E, 0x21, 0x24);

        // Palette brushes stay mutable so switching appearance only has to
        // repaint these instances; every element shares them.
        public static readonly SolidColorBrush Accent = Solid(AccentColor);
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

        public static Appearance Current { get; private set; }

        private static readonly Color DarkSurface = Color.FromRgb(0x1E, 0x21, 0x24);
        private static readonly Color LightSurface = Color.FromRgb(0xF2, 0xF3, 0xF5);

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
    <Setter Property=""Background"" Value=""#7289DA""/>
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

  <Style x:Key=""FieldTextBox"" TargetType=""TextBox"">
    <Setter Property=""Foreground"" Value=""{DynamicResource TextPrimaryBrush}""/>
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""BorderThickness"" Value=""0""/>
    <Setter Property=""FontFamily"" Value=""Segoe UI""/>
    <Setter Property=""FontSize"" Value=""13""/>
    <Setter Property=""CaretBrush"" Value=""#7289DA""/>
    <Setter Property=""SelectionBrush"" Value=""#7289DA""/>
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
            app.Resources["TextPrimaryBrush"] = StyleBrush(TextPrimary);
            app.Resources["TextSecondaryBrush"] = StyleBrush(TextSecondary);
            app.Resources["InputBorderBrush"] = StyleBrush(InputBorder);
            app.Resources["ChipFillBrush"] = StyleBrush(ChipFill);
            app.Resources["HoverOverlayBrush"] = StyleBrush(HoverOverlay);
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
            bool light = appearance == Appearance.Light;

            Set(Surface, light ? LightSurface : DarkSurface);
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
            return new Border
            {
                Background = CardBrush,
                BorderBrush = CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18, 16, 18, 18),
                Child = content,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 18,
                    ShadowDepth = 3,
                    Direction = 270,
                    Opacity = 0.28,
                    RenderingBias = RenderingBias.Performance
                }
            };
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
