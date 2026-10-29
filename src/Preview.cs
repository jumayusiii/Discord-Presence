using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DiscordPresence
{
    /// <summary>One Rich Presence button in the preview.</summary>
    public sealed class PreviewButton
    {
        public string Label { get; set; }
        public string Url { get; set; }
    }

    /// <summary>
    /// A Discord-style activity card that mirrors the current editor values.
    /// It deliberately uses Discord's dark palette instead of the app theme,
    /// because it previews the card in Discord's profile popout.
    /// </summary>
    public sealed class PresencePreview : Border
    {
        private static readonly SolidColorBrush PanelBrush = Brush(0x23, 0x24, 0x28);
        private static readonly SolidColorBrush PanelEdgeBrush = Brush(0x1A, 0x1B, 0x1E);
        private static readonly SolidColorBrush HeadingBrush = Brush(0xB5, 0xBA, 0xC1);
        private static readonly SolidColorBrush NameBrush = Brush(0xF2, 0xF3, 0xF5);
        private static readonly SolidColorBrush LineBrush = Brush(0xDB, 0xDE, 0xE1);
        private static readonly SolidColorBrush MutedBrush = Brush(0x6D, 0x6F, 0x78);
        private static readonly SolidColorBrush ArtBrush = Brush(0x2B, 0x2D, 0x31);
        private static readonly SolidColorBrush ButtonBrush = Brush(0x4E, 0x50, 0x58);
        private static readonly SolidColorBrush ButtonHoverBrush = Brush(0x5D, 0x60, 0x69);
        private static readonly SolidColorBrush ArrowBrush = Brush(0xB5, 0xBA, 0xC1);
        private static readonly SolidColorBrush LiveBrush = Brush(0x3B, 0xA5, 0x5D);

        private static readonly Dictionary<string, ImageSource> ImageCache =
            new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        private readonly TextBlock _kindText;
        private readonly Border _platformMark;
        private readonly TextBlock _elapsedText;
        private readonly DispatcherTimer _elapsedTimer;
        private long _elapsedStartMs;
        private readonly TextBlock _nameText;
        private readonly TextBlock _detailsText;
        private readonly TextBlock _stateText;
        private readonly StackPanel _liveRow;
        private readonly StackPanel _liveTime;
        private readonly TextBlock _liveTimer;
        private readonly TextBlock _liveFlame;
        private readonly TextBlock _liveState;
        private readonly Border _largeArt;
        private readonly TextBlock _largeGlyph;
        private readonly Border _smallRing;
        private readonly Border _smallArt;
        private readonly TextBlock _smallGlyph;
        private readonly StackPanel _buttons;

        private string _largeArtUrl;
        private string _smallArtUrl;
        private int _largeArtVersion;
        private int _smallArtVersion;
        private bool _playstation;

        public PresencePreview()
        {
            Background = PanelBrush;
            BorderBrush = PanelEdgeBrush;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(10);
            Padding = new Thickness(14, 12, 14, 13);
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            StackPanel content = new StackPanel();

            // Discord prints the elapsed counter at the top right of the card.
            Grid heading = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _kindText = Theme.Text("PLAYING A GAME", 10, FontWeights.Bold, HeadingBrush);

            // Discord prints a small console mark beside the header when the
            // game was detected on a linked console. The preview mirrors that.
            TextBlock ps = Theme.Text("PS", 8.5, FontWeights.Bold, HeadingBrush);
            ps.FontStyle = FontStyles.Italic;
            _platformMark = new Border
            {
                Background = Brush(0x3A, 0x3C, 0x43),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = ps,
                Visibility = Visibility.Collapsed,
                ToolTip = "PlayStation mode"
            };

            StackPanel header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            header.Children.Add(_kindText);
            header.Children.Add(_platformMark);
            heading.Children.Add(header);

            _elapsedText = Theme.Text(string.Empty, 10, FontWeights.Bold, HeadingBrush);
            _elapsedText.HorizontalAlignment = HorizontalAlignment.Right;
            _elapsedText.Visibility = Visibility.Collapsed;
            Grid.SetColumn(_elapsedText, 1);
            heading.Children.Add(_elapsedText);
            content.Children.Add(heading);

            _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _elapsedTimer.Tick += delegate { PaintElapsed(); };

            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(11) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid artGrid = new Grid { Width = 56, Height = 56, VerticalAlignment = VerticalAlignment.Top };
            _largeArt = new Border
            {
                Width = 56,
                Height = 56,
                CornerRadius = new CornerRadius(8),
                Background = ArtBrush,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            _largeGlyph = Theme.Glyph("\uE7FC", 21, MutedBrush);
            _largeGlyph.HorizontalAlignment = HorizontalAlignment.Center;
            _largeGlyph.VerticalAlignment = VerticalAlignment.Center;
            _largeArt.Child = _largeGlyph;
            artGrid.Children.Add(_largeArt);

            _smallArt = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = ArtBrush
            };
            _smallGlyph = Theme.Glyph("\uE91B", 9, MutedBrush);
            _smallGlyph.HorizontalAlignment = HorizontalAlignment.Center;
            _smallGlyph.VerticalAlignment = VerticalAlignment.Center;
            _smallArt.Child = _smallGlyph;

            _smallRing = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = PanelBrush,
                Padding = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Visibility = Visibility.Collapsed,
                Child = _smallArt
            };
            artGrid.Children.Add(_smallRing);

            Grid.SetColumn(artGrid, 0);
            row.Children.Add(artGrid);

            StackPanel lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _nameText = Theme.Text("Your game name", 14, FontWeights.SemiBold, NameBrush);
            _nameText.VerticalAlignment = VerticalAlignment.Top;
            lines.Children.Add(_nameText);

            // PlayStation mode prints the running time and the state on one
            // line under the title, with a green play mark, exactly like the
            // console card in Discord's Games shelf.
            _liveRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 3, 0, 0),
                Visibility = Visibility.Collapsed
            };
            _liveTime = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBlock play = Theme.Text("\u25B6", 8, FontWeights.Normal, LiveBrush);
            play.VerticalAlignment = VerticalAlignment.Center;
            play.Margin = new Thickness(0, 1, 5, 0);
            _liveTime.Children.Add(play);

            _liveTimer = Theme.Text("0:00", 12, FontWeights.SemiBold, LiveBrush);
            _liveTimer.VerticalAlignment = VerticalAlignment.Center;
            _liveTime.Children.Add(_liveTimer);
            _liveRow.Children.Add(_liveTime);

            _liveFlame = Theme.Emoji("\uD83D\uDD25", 11);
            _liveFlame.VerticalAlignment = VerticalAlignment.Center;
            _liveFlame.Margin = new Thickness(12, 0, 5, 0);
            _liveRow.Children.Add(_liveFlame);

            _liveState = Theme.Text("Trending", 12, FontWeights.Normal, LineBrush);
            _liveState.VerticalAlignment = VerticalAlignment.Center;
            _liveRow.Children.Add(_liveState);
            lines.Children.Add(_liveRow);

            _detailsText = Theme.Text(string.Empty, 12, FontWeights.Normal, LineBrush);
            _detailsText.Margin = new Thickness(0, 3, 0, 0);
            _detailsText.Visibility = Visibility.Collapsed;
            lines.Children.Add(_detailsText);

            _stateText = Theme.Text(string.Empty, 12, FontWeights.Normal, LineBrush);
            _stateText.Margin = new Thickness(0, 1, 0, 0);
            _stateText.Visibility = Visibility.Collapsed;
            lines.Children.Add(_stateText);

            Grid.SetColumn(lines, 2);
            row.Children.Add(lines);
            content.Children.Add(row);

            _buttons = new StackPanel { Margin = new Thickness(0, 9, 0, 0) };
            content.Children.Add(_buttons);

            Child = content;
        }

        /// <summary>Replaces the visible card with the current editor values.</summary>
        public void Update(ActivityKind kind, PresencePlatform platform, string applicationId,
            string gameName, string details, string state, string streamUrl,
            string largeImage, string largeText, string smallImage, string smallText,
            List<PreviewButton> buttons, long elapsedStartMs)
        {
            bool playstation = platform == PresencePlatform.PlayStation;
            _playstation = playstation;
            string header = Trim(details);
            if (playstation)
            {
                _kindText.Text = header.Length > 0 ? header : "Playing on PlayStation";
                _platformMark.Visibility = Visibility.Visible;
                SetLine(_detailsText, string.Empty);
                SetLine(_stateText, string.Empty);
            }
            else
            {
                _kindText.Text = KindLabel(kind);
                _platformMark.Visibility = Visibility.Collapsed;
                SetLine(_detailsText, details);
                SetLine(_stateText, state);
            }

            SetElapsed(elapsedStartMs);

            string name = Trim(gameName);
            _nameText.Text = name.Length > 0 ? name : "Your game name";
            _nameText.Foreground = name.Length > 0 ? NameBrush : MutedBrush;
            _nameText.ToolTip = name.Length > 0 ? name : null;

            _liveRow.Visibility = playstation ? Visibility.Visible : Visibility.Collapsed;
            if (playstation)
            {
                string live = Trim(state);
                string liveText = StripFlame(live);
                _liveState.Text = liveText;
                _liveState.Visibility = liveText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
                _liveFlame.Visibility = liveText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
                _liveTime.Visibility = elapsedStartMs > 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            string large = ResolveArtUrl(applicationId, largeImage);
            string small = ResolveArtUrl(applicationId, smallImage);
            UpdateArt(true, large, largeText);
            UpdateArt(false, small, smallText);
            _smallRing.Visibility = large != null && small != null ? Visibility.Visible : Visibility.Collapsed;

            _buttons.Children.Clear();
            List<PreviewButton> shown = new List<PreviewButton>();
            if (buttons != null)
            {
                foreach (PreviewButton button in buttons)
                {
                    if (shown.Count >= 2)
                    {
                        break;
                    }
                    AddButtonIfValid(shown, button);
                }
            }

            // Streaming is published as Playing, and the stream link fills a
            // free button slot just like it does in BuildActivity().
            if (kind == ActivityKind.Streaming && shown.Count < 2)
            {
                string stream = Trim(streamUrl);
                if (IsHttpUrl(stream) && !HasUrl(shown, stream))
                {
                    PreviewButton watch = new PreviewButton();
                    watch.Label = "Watch stream";
                    watch.Url = stream;
                    shown.Add(watch);
                }
            }

            foreach (PreviewButton button in shown)
            {
                _buttons.Children.Add(CreateButton(button));
            }
            _buttons.Visibility = shown.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Shows Discord's elapsed counter. Zero means the timer is off, which
        /// hides the text and stops the one-second repaint.
        /// </summary>
        private void SetElapsed(long startMs)
        {
            _elapsedStartMs = startMs;
            if (startMs <= 0)
            {
                _elapsedText.Visibility = Visibility.Collapsed;
                _liveTime.Visibility = Visibility.Collapsed;
                _elapsedTimer.Stop();
                return;
            }
            PaintElapsed();
            if (!_elapsedTimer.IsEnabled)
            {
                _elapsedTimer.Start();
            }
        }

        private void PaintElapsed()
        {
            if (_elapsedStartMs <= 0 || !IsVisible)
            {
                return;
            }
            long seconds = Math.Max(0, (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _elapsedStartMs) / 1000);
            long hours = seconds / 3600;
            long minutes = (seconds % 3600) / 60;
            string clock = hours > 0
                ? string.Format("{0}:{1:00}:{2:00}", hours, minutes, seconds % 60)
                : string.Format("{0:00}:{1:00}", minutes, seconds % 60);
            _liveTimer.Text = clock;

            if (_playstation)
            {
                // The console card puts the running time under the title
                // instead of in the top-right corner.
                _elapsedText.Visibility = Visibility.Collapsed;
                _liveTime.Visibility = Visibility.Visible;
                return;
            }

            _elapsedText.Text = clock + " elapsed";
            _elapsedText.Visibility = Visibility.Visible;
        }

        private void AddButtonIfValid(List<PreviewButton> shown, PreviewButton button)
        {
            if (button == null)
            {
                return;
            }
            string label = Trim(button.Label);
            string url = Trim(button.Url);
            if (label.Length == 0 || !IsHttpUrl(url) || HasUrl(shown, url))
            {
                return;
            }
            PreviewButton copy = new PreviewButton();
            copy.Label = label;
            copy.Url = url;
            shown.Add(copy);
        }

        private static bool HasUrl(List<PreviewButton> buttons, string url)
        {
            foreach (PreviewButton button in buttons)
            {
                if (string.Equals(button.Url, url, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private Border CreateButton(PreviewButton button)
        {
            Border shell = new Border
            {
                Height = 30,
                Background = ButtonBrush,
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand,
                ToolTip = button.Url
            };
            if (_buttons.Children.Count > 0)
            {
                shell.Margin = new Thickness(0, 6, 0, 0);
            }

            Grid grid = new Grid();
            TextBlock label = Theme.Text(button.Label, 12, FontWeights.SemiBold, Brushes.White);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Margin = new Thickness(28, 0, 28, 0);
            label.TextAlignment = TextAlignment.Center;
            grid.Children.Add(label);

            TextBlock arrow = new TextBlock
            {
                Text = "\u2197",
                FontFamily = Theme.UiFont,
                FontSize = 12,
                Foreground = ArrowBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 1)
            };
            grid.Children.Add(arrow);
            shell.Child = grid;

            shell.MouseEnter += delegate { shell.Background = ButtonHoverBrush; };
            shell.MouseLeave += delegate { shell.Background = ButtonBrush; };
            shell.MouseLeftButtonUp += delegate { OpenUrl(button.Url); };
            return shell;
        }

        private static string KindLabel(ActivityKind kind)
        {
            if (kind == ActivityKind.Listening)
            {
                return "LISTENING TO";
            }
            if (kind == ActivityKind.Watching)
            {
                return "WATCHING";
            }
            // Streaming is rejected by RPC and published as Playing.
            return "PLAYING A GAME";
        }

        private static void SetLine(TextBlock block, string value)
        {
            string text = Trim(value);
            block.Text = text;
            block.ToolTip = text.Length > 0 ? text : null;
            block.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string ResolveArtUrl(string applicationId, string value)
        {
            string raw = Trim(value);
            if (raw.Length == 0)
            {
                return null;
            }
            if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return IsHttpUrl(raw) ? raw : null;
            }
            if (raw.IndexOf("://", StringComparison.Ordinal) >= 0)
            {
                return null;
            }

            string id = Trim(applicationId);
            if (!IsDigits(id) || id.Length < 15 || id.Length > 20)
            {
                return null;
            }
            foreach (char c in raw)
            {
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-'))
                {
                    return null;
                }
            }

            // Uploaded assets are keyed by name in the RPC payload. Discord
            // resolves them to CDN ids server side, so a key may not be
            // available here; the placeholder remains when that happens.
            return "https://cdn.discordapp.com/app-assets/" + id + "/" + raw.ToLowerInvariant() + ".png";
        }

        private void UpdateArt(bool large, string url, string hover)
        {
            Border target = large ? _largeArt : _smallArt;
            TextBlock glyph = large ? _largeGlyph : _smallGlyph;
            string tip = Trim(hover);
            target.ToolTip = tip.Length > 0 ? tip : null;

            // The preview refreshes on every edit; do not restart a download
            // (or flash the placeholder) when the artwork itself did not change.
            string previous = large ? _largeArtUrl : _smallArtUrl;
            if (string.Equals(previous, url, StringComparison.Ordinal))
            {
                return;
            }

            if (large)
            {
                _largeArtUrl = url;
                _largeArtVersion++;
            }
            else
            {
                _smallArtUrl = url;
                _smallArtVersion++;
            }

            target.Background = ArtBrush;
            glyph.Visibility = Visibility.Visible;

            if (url == null)
            {
                return;
            }

            ImageSource cached;
            lock (ImageCache)
            {
                ImageCache.TryGetValue(url, out cached);
            }
            if (cached != null)
            {
                ApplyArt(large, cached);
                return;
            }

            int version = large ? _largeArtVersion : _smallArtVersion;
            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(url, UriKind.Absolute);
                bitmap.DecodePixelWidth = large ? 160 : 80;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.EndInit();

                Action apply = delegate
                {
                    if (!IsCurrent(large, version) || bitmap.IsDownloading || bitmap.PixelWidth == 0)
                    {
                        return;
                    }
                    try
                    {
                        bitmap.Freeze();
                    }
                    catch
                    {
                        // A frozen image is only an optimisation.
                    }
                    lock (ImageCache)
                    {
                        if (ImageCache.Count > 40)
                        {
                            ImageCache.Clear();
                        }
                        ImageCache[url] = bitmap;
                    }
                    ApplyArt(large, bitmap);
                };

                bitmap.DownloadCompleted += delegate { apply(); };
                bitmap.DownloadFailed += delegate
                {
                    if (IsCurrent(large, version))
                    {
                        ShowArtPlaceholder(large);
                    }
                };
                bitmap.DecodeFailed += delegate
                {
                    if (IsCurrent(large, version))
                    {
                        ShowArtPlaceholder(large);
                    }
                };

                if (!bitmap.IsDownloading)
                {
                    apply();
                }
            }
            catch
            {
                ShowArtPlaceholder(large);
            }
        }

        private bool IsCurrent(bool large, int version)
        {
            return version == (large ? _largeArtVersion : _smallArtVersion);
        }

        private void ApplyArt(bool large, ImageSource source)
        {
            Border target = large ? _largeArt : _smallArt;
            TextBlock glyph = large ? _largeGlyph : _smallGlyph;
            ImageBrush brush = new ImageBrush(source)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
            target.Background = brush;
            glyph.Visibility = Visibility.Collapsed;
            if (large)
            {
                _smallRing.Visibility = _smallArtUrl != null ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ShowArtPlaceholder(bool large)
        {
            Border target = large ? _largeArt : _smallArt;
            TextBlock glyph = large ? _largeGlyph : _smallGlyph;
            target.Background = ArtBrush;
            glyph.Visibility = Visibility.Visible;
            if (large)
            {
                _smallRing.Visibility = Visibility.Collapsed;
            }
        }

        private static bool IsHttpUrl(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri))
            {
                return false;
            }
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        private static bool IsDigits(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            foreach (char c in value)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }
            return true;
        }

        private static string Trim(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        /// <summary>
        /// The PlayStation line draws its own flame, so a leading flame emoji in
        /// the State field (kept there for Discord itself) is not shown twice.
        /// </summary>
        private static string StripFlame(string value)
        {
            string text = Trim(value);
            if (text.StartsWith("\uD83D\uDD25", StringComparison.Ordinal))
            {
                text = text.Substring(2);
            }
            return text.TrimStart('\uFE0F', ' ');
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // A broken link should never take the editor down.
            }
        }

        private static SolidColorBrush Brush(byte red, byte green, byte blue)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }
    }
}
