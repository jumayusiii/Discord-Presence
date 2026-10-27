using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace DiscordPresence
{
    /// <summary>A labelled, filled text field with a floating-hint placeholder and focus ring.</summary>
    public sealed class InputField : StackPanel
    {
        private readonly Border _shell;
        private readonly TextBlock _placeholder;

        public TextBox Box { get; private set; }

        public InputField(string label, string placeholder, int maxLength)
        {
            Orientation = Orientation.Vertical;

            // The caption is notched into the field's outline instead of
            // floating above it, so the label and its input read as one unit.
            Grid root = new Grid();
            Grid surface = new Grid();

            Box = new TextBox
            {
                Style = Theme.FindStyle("FieldTextBox"),
                MaxLength = maxLength,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Foreground = Theme.TextPrimary
            };
            AutomationProperties.SetName(Box, label);
            Box.TextChanged += delegate { UpdatePlaceholder(); };
            Box.GotKeyboardFocus += delegate { SetFocusState(true); };
            Box.LostKeyboardFocus += delegate { SetFocusState(false); };
            surface.Children.Add(Box);

            _placeholder = Theme.Text(placeholder, 13, FontWeights.Normal, Theme.TextMuted);
            _placeholder.Margin = new Thickness(13, 0, 12, 0);
            _placeholder.VerticalAlignment = VerticalAlignment.Center;
            _placeholder.IsHitTestVisible = false;
            _placeholder.TextTrimming = TextTrimming.CharacterEllipsis;
            surface.Children.Add(_placeholder);

            _shell = new Border
            {
                Background = Theme.FieldBrush,
                BorderBrush = Theme.InputBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Height = 40,
                Child = surface,
                Margin = new Thickness(0, 15, 0, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            root.Children.Add(_shell);

            TextBlock caption = Theme.Text(label, 11.5, FontWeights.SemiBold, Theme.TextSecondary);
            caption.HorizontalAlignment = HorizontalAlignment.Left;
            caption.VerticalAlignment = VerticalAlignment.Top;
            caption.Margin = new Thickness(9, 0, 0, 0);
            caption.Padding = new Thickness(4, 1, 4, 1);
            caption.Background = Theme.CardBrush;
            root.Children.Add(caption);

            Children.Add(root);
        }

        public string Value
        {
            get { return Box.Text.Trim(); }
        }

        public void FocusField()
        {
            Box.Focus();
            Box.CaretIndex = Box.Text.Length;
        }

        public void SetText(string value)
        {
            Box.Text = value ?? string.Empty;
        }

        private void UpdatePlaceholder()
        {
            _placeholder.Visibility = Box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetFocusState(bool focused)
        {
            _shell.BorderBrush = focused ? Theme.Accent : Theme.InputBorder;
        }
    }

    public enum SnackbarKind
    {
        Info,
        Success,
        Error
    }

    /// <summary>Material snackbar that fades in above the action bar.</summary>
    public sealed class Snackbar : Border
    {
        private readonly TextBlock _icon;
        private readonly TextBlock _message;
        private readonly TranslateTransform _shift;
        private DispatcherTimer _timer;
        private int _version;

        public Snackbar()
        {
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Bottom;
            Margin = new Thickness(24, 0, 24, 18);
            Padding = new Thickness(15, 10, 18, 10);
            Background = Theme.TooltipBrush;
            BorderBrush = Theme.InputBorder;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(10);
            MaxWidth = 620;
            Opacity = 0;
            IsHitTestVisible = false;
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 20,
                ShadowDepth = 4,
                Direction = 270,
                Opacity = 0.4
            };
            _shift = new TranslateTransform(0, 10);
            RenderTransform = _shift;

            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            _icon = Theme.Glyph("\uE946", 13, Theme.Accent);
            _icon.Margin = new Thickness(0, 0, 10, 0);
            row.Children.Add(_icon);

            _message = Theme.Text(string.Empty, 12, FontWeights.Normal, Theme.TextPrimary);
            _message.TextWrapping = TextWrapping.Wrap;
            _message.TextTrimming = TextTrimming.None;
            _message.MaxWidth = 542;
            row.Children.Add(_message);

            Child = row;

            // One long-lived timer: it always dismisses whatever is on screen
            // when it fires, instead of a single epoch's version number.
            _timer = new DispatcherTimer();
            _timer.Tick += delegate
            {
                _timer.Stop();
                Hide(_version);
            };
        }

        public void Show(string message, SnackbarKind kind)
        {
            _version++;

            _icon.Text = kind == SnackbarKind.Success ? "\uE73E" : kind == SnackbarKind.Error ? "\uE783" : "\uE946";
            _icon.Foreground = kind == SnackbarKind.Success ? Theme.Success :
                kind == SnackbarKind.Error ? Theme.Danger : Theme.Accent;
            _message.Text = message;

            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            _shift.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(190))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });

            _timer.Stop();
            _timer.Interval = TimeSpan.FromSeconds(4.2);
            _timer.Start();
        }

        private void Hide(int version)
        {
            if (version != _version)
            {
                return;
            }
            BeginAnimation(OpacityProperty, new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            });
            _shift.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0, 8, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                });
        }
    }

    public enum ActivityKind
    {
        Playing = 0,
        Streaming = 1,
        Listening = 2,
        Watching = 3
    }

    /// <summary>Selectable activity-type pill.</summary>
    public sealed class ActivityChip : Border
    {
        private readonly TextBlock _icon;
        private readonly TextBlock _label;
        private bool _selected;
        private bool _hovered;

        public ActivityKind Kind { get; private set; }
        public event EventHandler Chosen;

        public ActivityChip(ActivityKind kind, string glyph, string label)
        {
            Kind = kind;
            Height = 34;
            CornerRadius = new CornerRadius(17);
            BorderThickness = new Thickness(1);
            Cursor = Cursors.Hand;
            Focusable = true;
            Background = Brushes.Transparent;

            StackPanel row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _icon = Theme.Emoji(glyph, 15);
            _icon.Margin = new Thickness(0, 0, 5, 0);
            row.Children.Add(_icon);

            _label = Theme.Text(label, 12.5, FontWeights.SemiBold, Theme.TextSecondary);
            row.Children.Add(_label);
            Child = row;

            AutomationProperties.SetName(this, label + " activity");
            ToolTip = "Show this presence as " + label;

            MouseEnter += delegate { _hovered = true; Paint(); };
            MouseLeave += delegate { _hovered = false; Paint(); };
            MouseLeftButtonUp += delegate
            {
                if (Chosen != null)
                {
                    Chosen(this, EventArgs.Empty);
                }
            };
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter || e.Key == Key.Space)
                {
                    if (Chosen != null)
                    {
                        Chosen(this, EventArgs.Empty);
                    }
                    e.Handled = true;
                }
            };
            GotKeyboardFocus += delegate { Paint(); };
            LostKeyboardFocus += delegate { Paint(); };

            Paint();
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            Paint();
        }


        private void Paint()
        {
            if (_selected)
            {
                Background = Theme.SelectedFill;
                BorderBrush = Theme.Accent;
                _label.Foreground = Theme.TextPrimary;
                _icon.Foreground = Theme.Accent;
            }
            else
            {
                Background = _hovered ? Theme.HoverOverlay : Brushes.Transparent;
                BorderBrush = IsKeyboardFocused ? Theme.Accent :
                    _hovered ? Theme.HoverBorder : Theme.InputBorder;
                _label.Foreground = Theme.TextSecondary;
                _icon.Foreground = Theme.TextMuted;
            }
        }
    }

    /// <summary>A selectable settings row with a title and a short description.</summary>
    public sealed class OptionTile : Border
    {
        private readonly TextBlock _icon;
        private readonly TextBlock _title;
        private readonly TextBlock _description;
        private readonly TextBlock _check;
        private bool _selected;
        private bool _hovered;

        public event EventHandler Chosen;

        public OptionTile(string glyph, string title, string description)
        {
            CornerRadius = new CornerRadius(10);
            BorderThickness = new Thickness(1);
            Padding = new Thickness(12, 10, 12, 10);
            Cursor = Cursors.Hand;
            Focusable = true;
            Margin = new Thickness(0, 0, 0, 8);

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _icon = Theme.Glyph(glyph, 14, Theme.TextSecondary);
            _icon.Margin = new Thickness(0, 1, 12, 0);
            _icon.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(_icon);

            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _title = Theme.Text(title, 12.5, FontWeights.SemiBold, Theme.TextPrimary);
            text.Children.Add(_title);
            _description = Theme.Text(description, 11, FontWeights.Normal, Theme.TextMuted);
            _description.TextWrapping = TextWrapping.Wrap;
            _description.TextTrimming = TextTrimming.None;
            _description.Margin = new Thickness(0, 3, 8, 0);
            text.Children.Add(_description);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            _check = Theme.Glyph("\uE73E", 13, Theme.Accent);
            _check.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_check, 2);
            grid.Children.Add(_check);

            Child = grid;
            AutomationProperties.SetName(this, title);

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

        public bool IsSelected
        {
            get { return _selected; }
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
            _check.Visibility = _selected ? Visibility.Visible : Visibility.Collapsed;
            _icon.Foreground = _selected ? Theme.Accent : Theme.TextSecondary;
        }
    }

    /// <summary>A settings row with a title, a description and a Material switch.</summary>
    public sealed class ToggleRow : Border
    {
        private readonly TextBlock _icon;
        private readonly Border _track;
        private readonly Border _knob;
        private bool _on;
        private bool _hovered;

        public event EventHandler Toggled;

        public ToggleRow(string glyph, string title, string description)
        {
            CornerRadius = new CornerRadius(10);
            BorderThickness = new Thickness(1);
            Padding = new Thickness(12, 10, 12, 10);
            Cursor = Cursors.Hand;
            Focusable = true;

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _icon = Theme.Glyph(glyph, 14, Theme.TextSecondary);
            _icon.Margin = new Thickness(0, 1, 12, 0);
            _icon.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(_icon);

            StackPanel text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(Theme.Text(title, 12.5, FontWeights.SemiBold, Theme.TextPrimary));
            TextBlock caption = Theme.Text(description, 11, FontWeights.Normal, Theme.TextMuted);
            caption.TextWrapping = TextWrapping.Wrap;
            caption.TextTrimming = TextTrimming.None;
            caption.Margin = new Thickness(0, 3, 10, 0);
            text.Children.Add(caption);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            _track = new Border
            {
                Width = 38,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                Background = Theme.InputBorder,
                VerticalAlignment = VerticalAlignment.Center
            };
            _knob = new Border
            {
                Width = 16,
                Height = 16,
                CornerRadius = new CornerRadius(8),
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0)
            };
            _track.Child = _knob;
            Grid.SetColumn(_track, 2);
            grid.Children.Add(_track);

            Child = grid;
            AutomationProperties.SetName(this, title);
            AutomationProperties.SetHelpText(this, description);

            MouseEnter += delegate { _hovered = true; Paint(); };
            MouseLeave += delegate { _hovered = false; Paint(); };
            MouseLeftButtonUp += delegate { Toggle(); };
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter || e.Key == Key.Space)
                {
                    Toggle();
                    e.Handled = true;
                }
            };
            GotKeyboardFocus += delegate { Paint(); };
            LostKeyboardFocus += delegate { Paint(); };

            Paint();
            PaintKnob(false);
        }

        public bool IsOn
        {
            get { return _on; }
        }

        public void SetOn(bool on)
        {
            if (_on == on)
            {
                return;
            }
            _on = on;
            Paint();
            PaintKnob(false);
        }

        private void Toggle()
        {
            _on = !_on;
            Paint();
            PaintKnob(true);
            if (Toggled != null)
            {
                Toggled(this, EventArgs.Empty);
            }
        }

        private void Paint()
        {
            Background = _on
                ? Theme.SelectedFill
                : _hovered ? Theme.HoverOverlay : Theme.ChipFill;
            BorderBrush = _on ? Theme.Accent :
                IsKeyboardFocused ? Theme.Accent :
                _hovered ? Theme.HoverBorder : Theme.InputBorder;
            _icon.Foreground = _on ? Theme.Accent : Theme.TextSecondary;
            _track.Background = _on ? Theme.Accent :
                _hovered ? Theme.ThumbHoverBrush : Theme.InputBorder;
        }

        private void PaintKnob(bool animate)
        {
            Thickness target = _on
                ? new Thickness(19, 0, 0, 0)
                : new Thickness(3, 0, 0, 0);
            if (!animate)
            {
                _knob.BeginAnimation(FrameworkElement.MarginProperty, null);
                _knob.Margin = target;
                return;
            }

            ThicknessAnimation slide = new ThicknessAnimation(target, TimeSpan.FromMilliseconds(130))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            _knob.BeginAnimation(FrameworkElement.MarginProperty, slide);
        }
    }
}
