using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DiscordPresence
{
    /// <summary>
    /// Makes a colour rail behave like a real slider to anything outside the
    /// app: screen readers announce it as a slider and automation can read or
    /// set the channel value.
    /// </summary>
    public sealed class ColorSliderPeer : FrameworkElementAutomationPeer, IRangeValueProvider
    {
        private readonly ColorSlider _owner;

        public ColorSliderPeer(ColorSlider owner)
            : base(owner)
        {
            _owner = owner;
        }

        public override object GetPattern(PatternInterface patternInterface)
        {
            return patternInterface == PatternInterface.RangeValue
                ? this
                : base.GetPattern(patternInterface);
        }

        public double Value
        {
            get { return _owner.Value; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public double Minimum
        {
            get { return 0; }
        }

        public double Maximum
        {
            get { return 255; }
        }

        public double LargeChange
        {
            get { return 16; }
        }

        public double SmallChange
        {
            get { return 1; }
        }

        public void SetValue(double value)
        {
            _owner.SetChannel((int)Math.Round(value));
        }

        protected override string GetClassNameCore()
        {
            return "ColorSlider";
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Slider;
        }
    }

    /// <summary>
    /// One colour channel drawn as a rail with a draggable knob. The rail is
    /// painted black-to-pure-colour so the knob's position is readable even for
    /// a surface that is nearly black.
    /// </summary>
    public sealed class ColorSlider : Grid
    {
        private const double KnobSize = 16;
        private const double RailHeight = 6;

        private readonly Border _rail;
        private readonly Border _knob;
        private readonly TextBlock _valueText;
        private readonly Border _focusRing;
        private int _value;
        private bool _dragging;
        private bool _hovered;

        public event EventHandler Changed;

        public ColorSlider(string label, Color full)
        {
            Height = 26;
            Focusable = true;
            Margin = new Thickness(0, 0, 0, 4);
            AutomationProperties.SetName(this, label);

            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });

            TextBlock letter = Theme.Text(label.Substring(0, 1), 11.5, FontWeights.Bold, Theme.TextMuted);
            letter.HorizontalAlignment = HorizontalAlignment.Left;
            Children.Add(letter);

            Grid track = new Grid { Height = Height };
            track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(track, 1);

            _rail = new Border
            {
                Height = RailHeight,
                CornerRadius = new CornerRadius(3),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new LinearGradientBrush(Colors.Black, full, new Point(0, 0.5), new Point(1, 0.5))
            };
            track.Children.Add(_rail);

            _knob = new Border
            {
                Width = KnobSize,
                Height = KnobSize,
                CornerRadius = new CornerRadius(KnobSize / 2),
                Background = Brushes.White,
                BorderBrush = Theme.InputBorder,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            Theme.Elevate(_knob, new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 5,
                ShadowDepth = 1,
                Direction = 270,
                Opacity = 0.35
            });
            track.Children.Add(_knob);

            _focusRing = new Border
            {
                Height = 20,
                CornerRadius = new CornerRadius(10),
                BorderBrush = Theme.Accent,
                BorderThickness = new Thickness(1),
                Opacity = 0,
                IsHitTestVisible = false
            };
            track.Children.Add(_focusRing);
            Children.Add(track);

            _valueText = Theme.Text("0", 11.5, FontWeights.SemiBold, Theme.TextSecondary);
            _valueText.TextAlignment = TextAlignment.Right;
            _valueText.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(_valueText, 2);
            Children.Add(_valueText);

            track.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                Focus();
                _dragging = true;
                track.CaptureMouse();
                SetFromPoint(e.GetPosition(track).X, track.ActualWidth, true);
                e.Handled = true;
            };
            track.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (_dragging)
                {
                    SetFromPoint(e.GetPosition(track).X, track.ActualWidth, true);
                }
            };
            track.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                _dragging = false;
                track.ReleaseMouseCapture();
                e.Handled = true;
            };
            track.LostMouseCapture += delegate { _dragging = false; };

            MouseEnter += delegate { _hovered = true; Paint(); };
            MouseLeave += delegate { _hovered = false; Paint(); };
            GotKeyboardFocus += delegate { Paint(); };
            LostKeyboardFocus += delegate { Paint(); };
            KeyDown += OnKeyDown;
            SizeChanged += delegate { PaintKnob(); };

            Paint();
        }

        public int Value
        {
            get { return _value; }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ColorSliderPeer(this);
        }

        /// <summary>Sets the channel the way a drag would, and tells the picker.</summary>
        public void SetChannel(int value)
        {
            SetValue(value, true);
        }

        /// <summary>Sets the channel without asking listeners to repaint the app.</summary>
        public void SetValue(int value, bool raise)
        {
            int clamped = value < 0 ? 0 : value > 255 ? 255 : value;
            if (clamped == _value)
            {
                return;
            }
            _value = clamped;
            _valueText.Text = _value.ToString();
            PaintKnob();
            if (raise && Changed != null)
            {
                Changed(this, EventArgs.Empty);
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            int step = e.Key == Key.PageUp || e.Key == Key.PageDown ? 16 : 1;
            if (e.Key == Key.Left || e.Key == Key.Down)
            {
                SetValue(_value - step, true);
            }
            else if (e.Key == Key.Right || e.Key == Key.Up)
            {
                SetValue(_value + step, true);
            }
            else if (e.Key == Key.Home)
            {
                SetValue(0, true);
            }
            else if (e.Key == Key.End)
            {
                SetValue(255, true);
            }
            else
            {
                return;
            }
            e.Handled = true;
        }

        private void SetFromPoint(double x, double width, bool raise)
        {
            double travel = width - KnobSize;
            if (travel <= 0)
            {
                return;
            }
            double fraction = (x - KnobSize / 2) / travel;
            int value = (int)Math.Round(Math.Min(1, Math.Max(0, fraction)) * 255);
            SetValue(value, raise);
        }

        private void Paint()
        {
            _knob.BorderBrush = _hovered || IsKeyboardFocused ? Theme.Accent : Theme.InputBorder;
            _focusRing.Opacity = IsKeyboardFocused ? 1 : 0;
        }

        private void PaintKnob()
        {
            double travel = ActualWidth - 18 - 34 - KnobSize;
            if (travel <= 0)
            {
                return;
            }
            double offset = _value / 255.0 * travel;
            _knob.Margin = new Thickness(offset, 0, 0, 0);
        }
    }

    /// <summary>
    /// One of the three colours a custom theme is built from: a selectable row
    /// with a live swatch, the colour's hex code and a short description.
    /// </summary>
    public sealed class ColorAnchorRow : Border
    {
        private readonly Border _swatch;
        private readonly TextBlock _hex;
        private bool _selected;
        private bool _hovered;

        public event EventHandler Chosen;

        /// <summary>0 surface, 1 accent, 2 text.</summary>
        public int Kind { get; private set; }

        public ColorAnchorRow(int kind, string title, string description)
        {
            Kind = kind;
            CornerRadius = new CornerRadius(10);
            BorderThickness = new Thickness(1);
            Padding = new Thickness(12, 9, 12, 9);
            Cursor = Cursors.Hand;
            Focusable = true;
            AutomationProperties.SetName(this, title);
            AutomationProperties.SetHelpText(this, description);

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _swatch = new Border
            {
                Width = 38,
                Height = 24,
                CornerRadius = new CornerRadius(7),
                BorderBrush = Theme.InputBorder,
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            grid.Children.Add(_swatch);

            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(Theme.Text(title, 12.5, FontWeights.SemiBold, Theme.TextPrimary));
            TextBlock caption = Theme.Text(description, 11, FontWeights.Normal, Theme.TextMuted);
            caption.TextWrapping = TextWrapping.Wrap;
            caption.TextTrimming = TextTrimming.None;
            caption.Margin = new Thickness(0, 3, 10, 0);
            text.Children.Add(caption);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            StackPanel value = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            _hex = Theme.Text("#000000", 11.5, FontWeights.SemiBold, Theme.TextSecondary);
            AutomationProperties.SetName(_hex, title + " hex");
            _hex.TextAlignment = TextAlignment.Right;
            _hex.HorizontalAlignment = HorizontalAlignment.Right;
            value.Children.Add(_hex);
            TextBlock hint = Theme.Text("Edit", 10.5, FontWeights.Normal, Theme.TextMuted);
            hint.TextAlignment = TextAlignment.Right;
            hint.HorizontalAlignment = HorizontalAlignment.Right;
            hint.Margin = new Thickness(0, 2, 0, 0);
            value.Children.Add(hint);
            Grid.SetColumn(value, 2);
            grid.Children.Add(value);

            Child = grid;

            MouseEnter += delegate { _hovered = true; Paint(); };
            MouseLeave += delegate { _hovered = false; Paint(); };
            MouseLeftButtonUp += delegate { Choose(); };
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter || e.Key == Key.Space)
                {
                    Choose();
                    e.Handled = true;
                }
            };
            GotKeyboardFocus += delegate { Paint(); };
            LostKeyboardFocus += delegate { Paint(); };

            Paint();
        }

        public Color Color
        {
            get { return _swatch.Background is SolidColorBrush ? ((SolidColorBrush)_swatch.Background).Color : Colors.Black; }
        }

        public void SetColor(Color color)
        {
            _swatch.Background = new SolidColorBrush(color);
            _hex.Text = Theme.ToHex(color);
            AutomationProperties.SetHelpText(_hex, _hex.Text);
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            Paint();
        }

        private void Choose()
        {
            if (Chosen != null)
            {
                Chosen(this, EventArgs.Empty);
            }
        }

        private void Paint()
        {
            Background = _selected
                ? Theme.SelectedFill
                : _hovered ? Theme.HoverOverlay : Theme.ChipFill;
            BorderBrush = _selected ? Theme.Accent :
                IsKeyboardFocused ? Theme.Accent :
                _hovered ? Theme.HoverBorder : Theme.InputBorder;
        }
    }

    /// <summary>
    /// The custom-theme editor: three anchor rows, and beneath them a live
    /// preview, a swatch palette, a hex field and one rail per RGB channel.
    /// Changes are reported through <see cref="Changed"/> as they happen, so
    /// the app can repaint immediately.
    /// </summary>
    public sealed class ThemeColorPicker : StackPanel
    {
        private const string HexPrompt = "Click a swatch or type a hex code";

        // Neutrals make the best surfaces, accents the best highlights, so the
        // palette leads with the greys and follows with a spread of hues.
        private static readonly string[] Presets = new string[]
        {
            "#FFFFFF", "#F2F3F5", "#E2E5EA", "#C7CCD4", "#9AA1AB", "#6B7280",
            "#4A5058", "#2E3238", "#24272C", "#1E2124", "#15181B", "#000000",
            "#7289DA", "#5865F2", "#3B82F6", "#00A8FC", "#2DD4BF", "#43B581",
            "#57F287", "#FEE75C", "#FAA61A", "#FF7A45", "#ED4245", "#EB459E",
            "#4E5D94", "#3D4A7A", "#2563EB", "#007EA7", "#0E7490", "#2F7A5B",
            "#3BA55D", "#B59B2E", "#B87A12", "#C05621", "#A32B2E", "#A0346F"
        };

        private readonly ColorAnchorRow[] _rows = new ColorAnchorRow[3];
        private readonly StackPanel _editor;
        private static readonly string[] Titles = new string[] { "Surface", "Accent", "Text colour" };
        private Grid _palette;
        private Border _contrastWarning;
        private TextBlock _contrastHeading;
        private TextBlock _contrastText;
        private TextBlock _contrastLabel;
        private readonly TextBox _hexField;
        private readonly ColorSlider _red;
        private readonly ColorSlider _green;
        private readonly ColorSlider _blue;
        private int _active;
        private bool _loading;

        /// <summary>Raised whenever one of the three anchors changes.</summary>
        public event EventHandler Changed;

        /// <summary>Raised by the Built-in colours button: the app reloads the
        /// stock anchors, which depend on the chosen light or dark base.</summary>
        public event EventHandler ResetRequested;

        public ThemeColorPicker()
        {
            Orientation = Orientation.Vertical;
            AutomationProperties.SetName(this, "Custom theme colours");

            StackPanel rows = new StackPanel { Margin = new Thickness(0, 0, 0, 2) };
            string[] titles = Titles;
            string[] captions = new string[]
            {
                "Window background, panels and cards are toned from this colour.",
                "Buttons, switches, links and the highlight on selected rows.",
                "Headings, labels, field values and their quieter shades."
            };
            for (int i = 0; i < 3; i++)
            {
                ColorAnchorRow row = new ColorAnchorRow(i, titles[i], captions[i]);
                row.Margin = new Thickness(0, 0, 0, 8);
                row.Chosen += delegate(object sender, EventArgs e)
                {
                    Select(((ColorAnchorRow)sender).Kind);
                };
                _rows[i] = row;
                rows.Children.Add(row);
            }

            _editor = new StackPanel();
            _editor.Children.Add(BuildContrastWarning());
            _editor.Children.Add(BuildPreview());

            _palette = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            Grid palette = _palette;
            for (int i = 0; i < Presets.Length; i++)
            {
                int column = i % 12;
                int rowIndex = i / 12;
                if (column == 0)
                {
                    palette.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }
                if (rowIndex == 0)
                {
                    palette.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }
                Button swatch = MakeSwatch(Presets[i]);
                Grid.SetColumn(swatch, column);
                Grid.SetRow(swatch, rowIndex);
                palette.Children.Add(swatch);
            }
            _editor.Children.Add(palette);

            _hexField = new TextBox
            {
                Style = Theme.FindStyle("FieldTextBox"),
                Width = 108,
                Height = 34,
                MaxLength = 7,
                VerticalContentAlignment = VerticalAlignment.Center,
                Foreground = Theme.TextPrimary
            };
            AutomationProperties.SetName(_hexField, "Hex code");
            AutomationProperties.SetHelpText(_hexField, HexPrompt);
            _hexField.TextChanged += delegate { CommitHex(); };
            _hexField.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter)
                {
                    CommitHex();
                    e.Handled = true;
                }
            };
            Border hexShell = new Border
            {
                Background = Theme.FieldBrush,
                BorderBrush = Theme.InputBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Width = 108,
                Height = 34,
                Child = _hexField,
                VerticalAlignment = VerticalAlignment.Center
            };
            _hexField.GotKeyboardFocus += delegate { hexShell.BorderBrush = Theme.Accent; };
            _hexField.LostKeyboardFocus += delegate
            {
                CommitHex();
                hexShell.BorderBrush = Theme.InputBorder;
            };

            Button resetButton = MakeResetButton();
            Grid hexRow = new Grid { Margin = new Thickness(0, 12, 0, 10) };
            hexRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hexRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hexRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hexRow.Children.Add(hexShell);
            Grid.SetColumn(resetButton, 2);
            hexRow.Children.Add(resetButton);
            _editor.Children.Add(hexRow);

            _red = new ColorSlider("Red", Colors.Red);
            _green = new ColorSlider("Green", Colors.Lime);
            _blue = new ColorSlider("Blue", Colors.Blue);
            _red.Changed += delegate { SyncFromSliders(); };
            _green.Changed += delegate { SyncFromSliders(); };
            _blue.Changed += delegate { SyncFromSliders(); };
            _editor.Children.Add(_red);
            _editor.Children.Add(_green);
            _editor.Children.Add(_blue);

            TextBlock hint = Theme.Text(
                "Tip: pick the surface first. A dark surface gets lighter cards and a light surface darker ones, and the app offers a one-click fix whenever the text colour ends up hard to read.",
                10.5, FontWeights.Normal, Theme.TextMuted);
            hint.TextWrapping = TextWrapping.Wrap;
            hint.TextTrimming = TextTrimming.None;
            hint.Margin = new Thickness(0, 6, 0, 0);
            _editor.Children.Add(hint);

            Children.Add(rows);
            Children.Add(_editor);
        }

        public Color SurfaceColor
        {
            get { return _rows[0].Color; }
        }

        public Color AccentColor
        {
            get { return _rows[1].Color; }
        }

        public Color TextColor
        {
            get { return _rows[2].Color; }
        }

        /// <summary>Which anchor the picker is editing right now.</summary>
        public int ActiveKind
        {
            get { return _active; }
        }

        /// <summary>Shows the three anchors and points the editor at one of them.</summary>
        public void Load(Color surface, Color accent, Color text, int active)
        {
            _loading = true;
            _rows[0].SetColor(surface);
            _rows[1].SetColor(accent);
            _rows[2].SetColor(text);
            _loading = false;
            UpdateContrast();
            Select(active, false);
        }

        public void Select(int kind)
        {
            Select(kind, true);
        }

        private void Select(int kind, bool reveal)
        {
            _active = kind < 0 || kind > 2 ? 0 : kind;
            AutomationProperties.SetHelpText(this, "Editing the " + Titles[_active] + " colour");
            for (int i = 0; i < _rows.Length; i++)
            {
                _rows[i].SetSelected(i == _active);
            }

            _loading = true;
            Color current = _rows[_active].Color;
            _red.SetValue(current.R, false);
            _green.SetValue(current.G, false);
            _blue.SetValue(current.B, false);
            _hexField.Text = Theme.ToHex(current);
            _hexField.CaretIndex = _hexField.Text.Length;
            _loading = false;

            // Choosing a row scrolls its tools into view: the palette, the hex
            // field and the channel rails together are shorter than the pane,
            // so the row click lands on the part that can actually be used.
            if (reveal && IsVisible && _palette != null)
            {
                _palette.BringIntoView();
            }
        }

        /// <summary>
        /// A warning strip for the one combination the maths cannot save: a
        /// text colour that all but disappears on the chosen surface.
        /// </summary>
        private Border BuildContrastWarning()
        {
            // The strip sits on top of a possibly unreadable palette, so its own
            // text is painted with the readable ink rather than the anchors.
            _contrastText = Theme.Text(string.Empty, 11, FontWeights.Normal, Theme.TextPrimary);
            _contrastText.TextWrapping = TextWrapping.Wrap;
            _contrastText.TextTrimming = TextTrimming.None;
            _contrastText.MaxWidth = 320;

            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _contrastHeading = Theme.Text("Hard to read", 11.5, FontWeights.SemiBold, Theme.Warning);
            text.Children.Add(_contrastHeading);
            text.Children.Add(_contrastText);

            _contrastLabel = Theme.Text("Fix contrast", 11.5, FontWeights.SemiBold, Theme.TextPrimary);
            Button fix = new Button
            {
                Style = Theme.FindStyle("OutlineButton"),
                Content = _contrastLabel,
                Height = 30,
                MinWidth = 0,
                Padding = new Thickness(11, 0, 11, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Use a text colour that reads on this surface"
            };
            AutomationProperties.SetName(fix, "Fix contrast");
            AutomationProperties.SetHelpText(fix, "Swap the text colour for a readable one");
            fix.Click += delegate { FixContrast(); };

            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(text);
            Grid.SetColumn(fix, 1);
            row.Children.Add(fix);

            _contrastWarning = new Border
            {
                Background = Theme.ChipFill,
                BorderBrush = Theme.Warning,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 0, 0, 12),
                Visibility = Visibility.Collapsed,
                Child = row
            };
            return _contrastWarning;
        }

        /// <summary>Shows the warning whenever the text is hard to read on the surface.</summary>
        private void UpdateContrast()
        {
            if (_contrastWarning == null)
            {
                return;
            }

            double ratio = Theme.ContrastRatio(_rows[2].Color, _rows[0].Color);
            bool poor = ratio < 4.5;
            _contrastWarning.Visibility = poor ? Visibility.Visible : Visibility.Collapsed;
            if (poor)
            {
                // Amber on a light surface is almost as hard to read as the text
                // it warns about, so the light case gets a deeper shade of it.
                Color ink = Theme.ReadableInk(_rows[0].Color);
                _contrastHeading.Foreground = Theme.IsDark
                    ? (Brush)Theme.Warning
                    : new SolidColorBrush(Theme.Tone(Theme.Warning.Color, -0.35));
                _contrastText.Foreground = new SolidColorBrush(ink);
                _contrastLabel.Foreground = new SolidColorBrush(ink);
                _contrastText.Text = "This text colour reaches only " + ratio.ToString("0.0") +
                    ":1 on the surface. The readable choice is " + Theme.ToHex(ink) + ".";
            }
        }

        /// <summary>Swaps in whichever of white or near-black reads better on the surface.</summary>
        private void FixContrast()
        {
            Color text = Theme.ReadableInk(_rows[0].Color);
            _loading = true;
            _rows[2].SetColor(text);
            _loading = false;
            UpdateContrast();
            Select(2, false);
            Raise();
        }

        private Border BuildPreview()
        {
            Border card = new Border
            {
                Background = Theme.CardBrush,
                BorderBrush = Theme.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12, 9, 12, 9)
            };

            Grid cardGrid = new Grid();
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel lines = new StackPanel();
            lines.Children.Add(Theme.Text("Presence preview", 12, FontWeights.SemiBold, Theme.TextPrimary));
            TextBlock quiet = Theme.Text("Secondary text on the card colour", 10.5, FontWeights.Normal, Theme.TextMuted);
            quiet.Margin = new Thickness(0, 3, 0, 0);
            lines.Children.Add(quiet);
            cardGrid.Children.Add(lines);

            Border chip = new Border
            {
                Background = Theme.Accent,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(9, 4, 9, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Child = Theme.Text("Accent chip", 10.5, FontWeights.SemiBold, Theme.OnAccent)
            };
            Grid.SetColumn(chip, 1);
            cardGrid.Children.Add(chip);

            card.Child = cardGrid;

            Border frame = new Border
            {
                Background = Theme.Surface,
                BorderBrush = Theme.InputBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 10, 10, 10),
                Child = card
            };
            return frame;
        }

        /// <summary>
        /// A palette entry. It is a real button rather than a plain rectangle,
        /// so it can be reached with the keyboard and read by screen readers.
        /// </summary>
        private Button MakeSwatch(string hex)
        {
            Color color;
            Theme.TryParseHex(hex, out color);

            Button swatch = new Button
            {
                Style = Theme.FindStyle("BaseButton"),
                Background = new SolidColorBrush(color),
                BorderBrush = Theme.ChipBorder,
                BorderThickness = new Thickness(1),
                Height = 26,
                MinWidth = 0,
                Padding = new Thickness(0),
                Margin = new Thickness(3, 0, 3, 6),
                ToolTip = hex
            };
            AutomationProperties.SetName(swatch, "swatch " + hex);
            AutomationProperties.SetHelpText(swatch, "Use " + hex);
            swatch.Click += delegate { Pick(color); };
            return swatch;
        }

        private Button MakeResetButton()
        {
            Button button = new Button
            {
                Style = Theme.FindStyle("OutlineButton"),
                Content = Theme.Text("Built-in colours", 12, FontWeights.SemiBold, Theme.TextPrimary),
                Height = 34,
                MinWidth = 0,
                Padding = new Thickness(13, 0, 13, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                ToolTip = "Start again from the stock Discord colours"
            };
            AutomationProperties.SetName(button, "Built-in colours");
            AutomationProperties.SetHelpText(button,
                "Copy the stock Discord theme colours into the three anchors");
            button.Click += delegate
            {
                if (ResetRequested != null)
                {
                    ResetRequested(this, EventArgs.Empty);
                }
            };
            return button;
        }

        private void Pick(Color color)
        {
            _loading = true;
            _rows[_active].SetColor(color);
            _loading = false;
            UpdateContrast();
            Select(_active, false);
            Raise();
        }

        private void SyncFromSliders()
        {
            if (_loading)
            {
                return;
            }
            Color color = Color.FromRgb((byte)_red.Value, (byte)_green.Value, (byte)_blue.Value);
            _loading = true;
            _rows[_active].SetColor(color);
            _hexField.Text = Theme.ToHex(color);
            _hexField.CaretIndex = _hexField.Text.Length;
            _loading = false;
            UpdateContrast();
            Raise();
        }

        private void CommitHex()
        {
            if (_loading)
            {
                return;
            }
            Color color;
            if (!Theme.TryParseHex(_hexField.Text, out color))
            {
                return;
            }
            if (color == _rows[_active].Color)
            {
                return;
            }
            _loading = true;
            _rows[_active].SetColor(color);
            _red.SetValue(color.R, false);
            _green.SetValue(color.G, false);
            _blue.SetValue(color.B, false);
            _loading = false;
            UpdateContrast();
            Raise();
        }

        private void Raise()
        {
            if (Changed != null)
            {
                Changed(this, EventArgs.Empty);
            }
        }
    }
}
