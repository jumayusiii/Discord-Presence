using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;

namespace DiscordPresence
{
    public sealed class MainWindow : Window
    {
        private readonly DiscordRpc _rpc = new DiscordRpc();
        private readonly AppSettings _settings;
        private readonly object _rpcGate = new object();
        private readonly List<ActivityChip> _chips = new List<ActivityChip>();
        private readonly List<PresencePreset> _presets;

        private OptionTile _quitTile;
        private OptionTile _trayTile;
        private System.Windows.Forms.NotifyIcon _tray;
        private bool _exiting;
        private bool _trayHintShown;

        private InputField _applicationId;
        private InputField _gameName;
        private InputField _details;
        private InputField _state;
        private InputField _streamUrl;
        private InputField _largeImage;
        private InputField _largeText;
        private InputField _smallImage;
        private InputField _smallText;
        private InputField _button1Label;
        private InputField _button1Url;
        private InputField _button2Label;
        private InputField _button2Url;

        private Border _presetBar;
        private WrapPanel _presetChips;
        private Button _deletePresetButton;
        private Grid _modalLayer;
        private Border _modalCard;
        private IInputElement _previousFocus;
        private string _activePresetName = string.Empty;
        private Grid _bootLayer;
        private FrameworkElement _bootLogo;
        private TextBlock _bootText;
        private FrameworkElement _fadeContent;

        private StackPanel _streamBlock;
        private Button _updateButton;
        private Button _clearButton;
        private Snackbar _snackbar;
        private PresencePreview _preview;
        private DispatcherTimer _previewTimer;
        private TextBlock _statusLabel;
        private System.Windows.Shapes.Ellipse _statusDot;
        private Border _changelogBadge;
        private TextBlock _changelogBadgeText;
        private Button _appearanceButton;
        private TextBlock _appearanceGlyph;
        private Ellipse _appearanceHalo;
        private ScaleTransform _appearanceHaloScale;
        private DropShadowEffect _appearanceGlow;
        private DispatcherTimer _reconnectTimer;
        private DispatcherTimer _minimizeTimer;
        private DispatcherTimer _autoUpdateTimer;
        private ToggleRow _autoUpdateRow;
        private ToggleRow _reduceMotionRow;
        private ToggleRow _performanceRow;
        private ToggleRow _elapsedRow;
        private ToggleRow _hotkeyRow;
        private Button _restartTimerButton;
        private TextBlock _appearanceLabel;
        private bool _previewDirty;
        private bool _hotkeyRegistered;
        private WindowChrome _savedChrome;
        private bool _closeAnimationStarted;
        private bool _suppressRestoreAnimation;
        private bool _restoreFadePending;
        private bool _restoreFadeRunning;
        private bool _quitRequested;
        private bool _trayHideRunning;
        private int _trayHideVersion;
        private int _fadeVersion;
        private WindowState _lastWindowState = WindowState.Normal;

        private const int WmSysCommand = 0x0112;
        private const int WmSize = 0x0005;
        private const int WmDpiChanged = 0x02E0;
        private const int ScMinimize = 0xF020;
        private const int ScRestore = 0xF120;
        private const int CornerRadiusDip = 12;
        private const int PreviewFrameDelay = 16;
        private const int PreviewSettleDelay = 120;
        private const int WmHotkey = 0x0312;
        private const int SummonHotkeyId = 0x4D50;
        private const int HotkeyModifiers = 0x0001 | 0x0002 | 0x4000; // Alt + Ctrl + NoRepeat
        private const int HotkeyVirtualKey = 0x44; // D
        private static readonly bool UsesDwmCorners = Environment.OSVersion.Version.Build >= 22000;

        private ActivityKind _kind = ActivityKind.Playing;
        private volatile string _connectedAppId = string.Empty;
        private Dictionary<string, object> _lastPublished;
        private bool _busy;
        private bool _autoConnecting;
        private bool _autoPublishing;
        private bool _autoUpdatePending;
        private bool _suppressAutoUpdate;
        private bool _restoreSuppressed;
        private bool _closing;

        public MainWindow()
        {
            _settings = SettingsStore.Load();
            _presets = _settings.Presets ?? new List<PresencePreset>();
            Theme.Apply(_settings.LightMode ? Theme.Appearance.Light : Theme.Appearance.Dark);
            // The cards and buttons below ask the theme for their shadows, so
            // the performance profile has to be in place before they exist.
            Theme.SetPerformanceMode(_settings.PerformanceMode);

            Title = "Discord Presence";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            Background = Theme.Surface;
            FontFamily = Theme.UiFont;
            Width = 1040;
            Height = 840;
            MinWidth = 900;
            MinHeight = 620;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            TextOptions.SetTextHintingMode(this, TextHintingMode.Fixed);

            Rect work = SystemParameters.WorkArea;
            Width = Math.Min(Width, Math.Max(MinWidth, work.Width - 40));
            Height = Math.Min(Height, Math.Max(MinHeight, work.Height - 40));

            WindowChrome chrome = new WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(6),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false
            };
            WindowChrome.SetWindowChrome(this, chrome);

            Content = BuildRoot();
            ApplySettings();
            RefreshPresetBar();
            RefreshChangelogBadge();
            HookLiveUpdates();

            _rpc.ConnectionChanged += OnRpcConnectionChanged;
            _rpc.Notice += OnRpcNotice;
            SourceInitialized += OnSourceInitialized;
            Loaded += OnWindowLoaded;
            Closing += OnWindowClosing;
            StateChanged += OnWindowStateChanged;
            if (Application.Current != null)
            {
                Application.Current.SessionEnding += delegate { _exiting = true; };
            }

            _reconnectTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            _reconnectTimer.Tick += OnReconnectTick;
            _reconnectTimer.Start();

            _minimizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _minimizeTimer.Tick += OnMinimizeTimerTick;

            _autoUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _autoUpdateTimer.Tick += OnAutoUpdateTick;

            _previewTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(
                    _settings.PerformanceMode ? PreviewFrameDelay : PreviewSettleDelay)
            };
            _previewTimer.Tick += delegate
            {
                _previewTimer.Stop();
                RefreshPreview();
            };

            _lastWindowState = WindowState;
            Opacity = 0;
        }

        private FrameworkElement BuildRoot()
        {
            Grid root = new Grid { Background = Theme.Surface };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(72) });

            root.Children.Add(BuildTopBar());

            Grid contentLayer = new Grid { ClipToBounds = true };
            Ellipse glow = new Ellipse
            {
                Width = 640,
                Height = 460,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -240, -140, 0),
                IsHitTestVisible = false
            };
            RadialGradientBrush glowBrush = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5)
            };
            glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x1C, 0x72, 0x89, 0xDA), 0));
            glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0x72, 0x89, 0xDA), 1));
            glow.Fill = glowBrush;
            contentLayer.Children.Add(glow);

            ScrollViewer scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = BuildPage(),
                Focusable = false,
                PanningMode = PanningMode.VerticalOnly
            };
            contentLayer.Children.Add(scroll);

            _snackbar = new Snackbar();
            contentLayer.Children.Add(_snackbar);

            Grid.SetRow(contentLayer, 1);
            root.Children.Add(contentLayer);

            Border actionBar = BuildActionBar();
            Grid.SetRow(actionBar, 2);
            root.Children.Add(actionBar);

            _modalLayer = BuildModalLayer();
            Grid.SetRowSpan(_modalLayer, 3);
            root.Children.Add(_modalLayer);

            _bootLayer = BuildBootLayer();
            Grid.SetRowSpan(_bootLayer, 3);
            root.Children.Add(_bootLayer);

            _fadeContent = root;
            return new Border
            {
                Background = Theme.Surface,
                BorderBrush = Theme.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(CornerRadiusDip),
                Child = root
            };
        }

        private Grid BuildBootLayer()
        {
            Grid layer = new Grid
            {
                Background = Theme.Surface,
                ClipToBounds = true,
                IsHitTestVisible = true
            };

            Ellipse glow = new Ellipse
            {
                Width = 500,
                Height = 340,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            RadialGradientBrush glowBrush = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5)
            };
            glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x24, 0x72, 0x89, 0xDA), 0));
            glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0x72, 0x89, 0xDA), 1));
            glow.Fill = glowBrush;
            layer.Children.Add(glow);

            _bootLogo = Brand.Mark(140, Theme.Accent);
            _bootLogo.HorizontalAlignment = HorizontalAlignment.Center;
            _bootLogo.VerticalAlignment = VerticalAlignment.Center;
            _bootLogo.RenderTransformOrigin = new Point(0.5, 0.5);
            _bootLogo.RenderTransform = new ScaleTransform(0.88, 0.88);
            _bootLogo.Opacity = 0;
            layer.Children.Add(_bootLogo);

            _bootText = Theme.Text("Welcome to Discord Presence", 22.5, FontWeights.SemiBold,
                Theme.TextPrimary);
            _bootText.HorizontalAlignment = HorizontalAlignment.Center;
            _bootText.VerticalAlignment = VerticalAlignment.Center;
            _bootText.TextAlignment = TextAlignment.Center;
            _bootText.TextTrimming = TextTrimming.None;
            _bootText.Opacity = 0;
            _bootText.RenderTransform = new TranslateTransform(0, 8);
            layer.Children.Add(_bootText);

            return layer;
        }

        private Grid BuildTopBar()
        {
            Grid bar = new Grid { Background = Brushes.Transparent, Height = 58 };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.MouseLeftButtonDown += OnTitleBarMouseDown;

            StackPanel brand = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(22, 0, 0, 0)
            };
            brand.Children.Add(Brand.AppTile(30, Theme.Accent, Brushes.White));
            TextBlock title = Theme.Text("Discord Presence", 13.5, FontWeights.SemiBold, Theme.TextPrimary);
            title.Margin = new Thickness(11, 0, 0, 0);
            brand.Children.Add(title);
            Grid.SetColumn(brand, 0);
            bar.Children.Add(brand);

            _statusDot = new System.Windows.Shapes.Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = Theme.TextMuted,
                VerticalAlignment = VerticalAlignment.Center
            };
            _statusLabel = Theme.Text("Not connected", 11.5, FontWeights.SemiBold, Theme.TextSecondary);
            _statusLabel.Margin = new Thickness(7, 0, 0, 0);
            StackPanel statusRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            statusRow.Children.Add(_statusDot);
            statusRow.Children.Add(_statusLabel);

            Border statusChip = new Border
            {
                Background = Theme.ChipFill,
                BorderBrush = Theme.ChipBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Height = 28,
                Padding = new Thickness(11, 0, 11, 0),
                Child = statusRow,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "Discord connection status"
            };
            AutomationProperties.SetName(statusChip, "Discord connection status");

            Grid changelogContent = new Grid { Width = 20, Height = 20 };
            TextBlock changelogGlyph = Theme.Emoji("\uD83D\uDCDD", 14.5);
            changelogContent.Children.Add(changelogGlyph);

            _changelogBadgeText = Theme.Text("0", 9, FontWeights.Bold, Brushes.White);
            _changelogBadgeText.HorizontalAlignment = HorizontalAlignment.Center;
            _changelogBadgeText.VerticalAlignment = VerticalAlignment.Center;
            _changelogBadgeText.TextAlignment = TextAlignment.Center;

            _changelogBadge = new Border
            {
                MinWidth = 15,
                Height = 15,
                CornerRadius = new CornerRadius(8),
                Background = Theme.Danger,
                BorderBrush = Theme.Surface,
                BorderThickness = new Thickness(2),
                Padding = new Thickness(3, 0, 3, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
                Child = _changelogBadgeText
            };
            changelogContent.Children.Add(_changelogBadge);

            Grid appearanceContent = new Grid { Width = 20, Height = 20 };
            _appearanceHaloScale = new ScaleTransform(1, 1);
            _appearanceHalo = new Ellipse
            {
                Width = 24,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = _appearanceHaloScale,
                IsHitTestVisible = false
            };
            appearanceContent.Children.Add(_appearanceHalo);

            _appearanceGlyph = Theme.Emoji("\uD83C\uDF19", 14.5);
            _appearanceGlow = new DropShadowEffect
            {
                ShadowDepth = 0,
                BlurRadius = 8,
                RenderingBias = RenderingBias.Performance
            };
            Theme.Elevate(_appearanceGlyph, _appearanceGlow);
            appearanceContent.Children.Add(_appearanceGlyph);

            // Every header control is a Discord-style status pill: surface,
            // outline, glyph and label, exactly like the connection chip.
            _appearanceButton = Theme.Pill(appearanceContent, "Dark", "Appearance", out _appearanceLabel);
            _appearanceButton.Click += delegate { SetLightMode(!_settings.LightMode); };
            RefreshAppearanceButton();

            Button changelogButton = Theme.Pill(changelogContent, "Changelog",
                "Release notes and recent changes");
            AutomationProperties.SetHelpText(changelogButton, "Release notes and recent changes");
            changelogButton.Click += delegate { ShowChangelogDialog(); };

            Button settingsButton = Theme.Pill(Theme.Emoji("\u2699\uFE0F", 14.5), "Settings",
                "Application ID, publishing, tray options and beta features");
            settingsButton.Click += delegate { ShowSettingsDialog(); };

            StackPanel statusGroup = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            statusGroup.Children.Add(statusChip);
            statusGroup.Children.Add(changelogButton);
            statusGroup.Children.Add(_appearanceButton);
            statusGroup.Children.Add(settingsButton);
            Grid.SetColumn(statusGroup, 1);
            bar.Children.Add(statusGroup);

            StackPanel windowControls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            windowControls.Children.Add(MakeWindowButton("\uE921", "Minimize", false));
            windowControls.Children.Add(MakeWindowButton("\uE8BB", "Close", true));
            Grid.SetColumn(windowControls, 2);
            bar.Children.Add(windowControls);

            return bar;
        }

        private Border MakeWindowButton(string glyph, string name, bool close)
        {
            TextBlock icon = Theme.Glyph(glyph, 9.5, Theme.TextSecondary);
            Border button = new Border
            {
                Width = 42,
                Height = 30,
                CornerRadius = new CornerRadius(7),
                Background = Brushes.Transparent,
                Margin = new Thickness(2, 0, 0, 0),
                Cursor = Cursors.Hand,
                Child = icon
            };
            AutomationProperties.SetName(button, name);
            button.MouseEnter += delegate
            {
                button.Background = close ? Theme.CloseHover : Theme.HoverOverlay;
                icon.Foreground = close ? Brushes.White : Theme.TextPrimary;
            };
            button.MouseLeave += delegate
            {
                button.Background = Brushes.Transparent;
                icon.Foreground = Theme.TextSecondary;
            };
            button.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (close)
                {
                    Close();
                }
                else
                {
                    MinimizeWithAnimation();
                }
            };
            return button;
        }

        /// <summary>
        /// Windows only animates minimization for windows with a standard
        /// caption. Swap in the standard frame for the transition, then put
        /// the custom chrome back once the window is safely minimized.
        /// </summary>
        private void MinimizeWithAnimation()
        {
            if (WindowState == WindowState.Minimized || _savedChrome != null)
            {
                return;
            }

            WindowChrome chrome = WindowChrome.GetWindowChrome(this);
            if (chrome == null)
            {
                WindowState = WindowState.Minimized;
                return;
            }

            _savedChrome = chrome;
            ClearWindowRegion();
            WindowChrome.SetWindowChrome(this, null);
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Minimized;
            _minimizeTimer.Stop();
            _minimizeTimer.Start();
        }

        private void OnMinimizeTimerTick(object sender, EventArgs e)
        {
            _minimizeTimer.Stop();
            RestoreCustomChrome();
            if (!_settings.ReduceMotion)
            {
                SetDwmTransitions(false);
            }
        }

        private void RestoreCustomChrome()
        {
            if (_minimizeTimer != null)
            {
                _minimizeTimer.Stop();
            }
            if (_savedChrome == null)
            {
                return;
            }

            WindowChrome chrome = _savedChrome;
            _savedChrome = null;
            WindowStyle = WindowStyle.None;
            WindowChrome.SetWindowChrome(this, chrome);
            ApplyWindowRegion();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            RestoreCustomChrome();
            if (_restoreFadePending && WindowState == WindowState.Normal)
            {
                _restoreFadePending = false;
                PlayRestoreAnimation();
            }
        }

        /// <summary>
        /// One content fade at a time. The opaque window shell stays put, so
        /// the desktop never shows through the middle of a fade (the standard
        /// frame's white client area caused a bright flash there). Starting a
        /// new fade cancels any older completion callback.
        /// </summary>
        private void StartContentFade(double from, double to, int milliseconds,
            IEasingFunction easing, Action completed)
        {
            if (_fadeContent == null)
            {
                return;
            }
            int version = ++_fadeVersion;
            _fadeContent.BeginAnimation(UIElement.OpacityProperty, null);
            _fadeContent.Opacity = from;

            DoubleAnimation fade = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
            {
                EasingFunction = easing
            };
            fade.Completed += delegate
            {
                if (version != _fadeVersion)
                {
                    return;
                }
                _fadeContent.BeginAnimation(UIElement.OpacityProperty, null);
                _fadeContent.Opacity = to;
                if (completed != null)
                {
                    completed();
                }
            };
            _fadeContent.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        private void ShowContentImmediately()
        {
            ++_fadeVersion;
            if (_fadeContent != null)
            {
                _fadeContent.BeginAnimation(UIElement.OpacityProperty, null);
                _fadeContent.Opacity = 1;
            }
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            IsHitTestVisible = true;
        }

        /// <summary>A slow, smooth fade-in when the window comes back from the taskbar or tray.</summary>
        private void PlayRestoreAnimation()
        {
            if (_closing || !IsVisible || WindowState == WindowState.Minimized)
            {
                return;
            }
            if (_settings.ReduceMotion)
            {
                FinishRestoreInstant();
                return;
            }
            if (_restoreFadeRunning)
            {
                return;
            }

            _restoreFadeRunning = true;
            IsHitTestVisible = true;
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            StartContentFade(0, 1, 260, new CubicEase { EasingMode = EasingMode.EaseOut },
                delegate
                {
                    _restoreFadeRunning = false;
                    SetDwmTransitions(true);
                });
        }

        private void FinishRestoreInstant()
        {
            _restoreFadeRunning = false;
            ShowContentImmediately();
            SetDwmTransitions(true);
        }

        /// <summary>Fades the window out, then really closes it.</summary>
        private void BeginCloseAnimation()
        {
            _closeAnimationStarted = true;
            _restoreFadeRunning = false;
            if (_settings.ReduceMotion || !IsVisible || WindowState == WindowState.Minimized)
            {
                // Closing is already in progress here, so re-entering Close()
                // from inside Closing is not allowed. Hand it to the dispatcher.
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (!_closing)
                    {
                        Close();
                    }
                }));
                return;
            }

            IsHitTestVisible = false;
            double from = _fadeContent != null ? _fadeContent.Opacity : 1;
            StartContentFade(from, 0, 220, new CubicEase { EasingMode = EasingMode.EaseInOut }, delegate
            {
                if (!_closing)
                {
                    Close();
                }
            });
        }

        /// <summary>
        /// The frame swap used for the native minimize animation also makes
        /// Windows animate the restore with a full-opacity snapshot. Suppress
        /// that transition while the window is minimized so our own fade is
        /// the only thing that plays on the way back.
        /// </summary>
        private void SetDwmTransitions(bool enabled)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }
            try
            {
                int disabled = enabled ? 0 : 1;
                DwmSetWindowAttribute(handle, 3, ref disabled, 4);
            }
            catch
            {
                // Cosmetic only; older Windows builds can ignore this.
            }
        }

        private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmHotkey)
            {
                if (wParam.ToInt32() == SummonHotkeyId)
                {
                    ToggleSummon();
                    handled = true;
                }
            }
            else if (msg == WmSysCommand)
            {
                int command = wParam.ToInt32() & 0xFFF0;
                if (command == ScMinimize && _savedChrome == null && WindowState != WindowState.Minimized)
                {
                    MinimizeWithAnimation();
                    handled = true;
                }
                else if (command == ScRestore)
                {
                    if (WindowState == WindowState.Minimized && !_settings.ReduceMotion)
                    {
                        // Go transparent before Windows reveals the window, so
                        // the fade-in never starts with a full-opacity flash.
                        _restoreFadePending = true;
                        _restoreFadeRunning = false;
                        BeginAnimation(OpacityProperty, null);
                        Opacity = 1;
                        if (_fadeContent != null)
                        {
                            _fadeContent.BeginAnimation(UIElement.OpacityProperty, null);
                            _fadeContent.Opacity = 0;
                        }
                        SetDwmTransitions(false);
                    }
                    RestoreCustomChrome();
                }
            }
            else if (msg == WmSize || msg == WmDpiChanged)
            {
                ApplyWindowRegion();
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Windows 10 has no rounded window corners of its own, so the window
        /// is clipped to a rounded rectangle. Maximized windows stay square.
        /// </summary>
        private void ApplyWindowRegion()
        {
            if (_savedChrome != null || UsesDwmCorners)
            {
                return;
            }

            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }
            if (WindowState == WindowState.Maximized || WindowState == WindowState.Minimized)
            {
                ClearWindowRegion();
                return;
            }

            NativeRect bounds;
            if (!GetWindowRect(handle, out bounds))
            {
                return;
            }

            double scale = 1.0;
            try
            {
                scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            }
            catch
            {
                // Keep the unscaled radius when DPI cannot be read.
            }

            int radius = Math.Max(1, (int)Math.Round(CornerRadiusDip * scale));
            IntPtr region = CreateRoundRectRgn(0, 0, bounds.Right - bounds.Left + 1, bounds.Bottom - bounds.Top + 1,
                radius * 2, radius * 2);
            if (region == IntPtr.Zero)
            {
                return;
            }
            // SetWindowRgn owns the region handle after a successful call.
            SetWindowRgn(handle, region, true);
        }

        private void ClearWindowRegion()
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                SetWindowRgn(handle, IntPtr.Zero, true);
            }
        }

        private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }
            try
            {
                DragMove();
            }
            catch
            {
                // Dragging can race with window state changes; harmless.
            }
        }

        private FrameworkElement BuildPage()
        {
            _applicationId = new InputField("Application ID", "Paste your application ID", 20);

            StackPanel page = new StackPanel { Margin = new Thickness(28, 20, 22, 18) };

            StackPanel header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            header.Children.Add(Theme.Text("Rich Presence", 25, FontWeights.SemiBold, Theme.TextPrimary));
            TextBlock subtitle = Theme.Text("Design the activity your friends see on Discord.",
                12.5, FontWeights.Normal, Theme.TextMuted);
            subtitle.Margin = new Thickness(0, 4, 0, 0);
            header.Children.Add(subtitle);
            page.Children.Add(header);
            page.Children.Add(BuildPresetBar());

            Grid columns = new Grid();
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.18, GridUnitType.Star) });
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            columns.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
            columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Activity and the preview share the first row, and the buttons and
            // artwork share the second. That keeps the left and right cards on
            // the same horizontal lines instead of drifting with their content.
            Border activityCard = BuildActivityCard();
            Grid.SetColumn(activityCard, 0);
            Grid.SetRow(activityCard, 0);
            columns.Children.Add(activityCard);

            Border previewCard = BuildPreviewCard();
            Grid.SetColumn(previewCard, 2);
            Grid.SetRow(previewCard, 0);
            columns.Children.Add(previewCard);

            Border buttonsCard = BuildButtonsCard();
            Grid.SetColumn(buttonsCard, 0);
            Grid.SetRow(buttonsCard, 2);
            columns.Children.Add(buttonsCard);

            Border artworkCard = BuildArtworkCard();
            Grid.SetColumn(artworkCard, 2);
            Grid.SetRow(artworkCard, 2);
            columns.Children.Add(artworkCard);

            page.Children.Add(columns);
            return page;
        }

        private Border BuildPresetBar()
        {
            Grid bar = new Grid();
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock caption = Theme.Text("Presets", 11.5, FontWeights.SemiBold, Theme.TextSecondary);
            caption.Margin = new Thickness(0, 0, 14, 0);
            Grid.SetColumn(caption, 0);
            bar.Children.Add(caption);

            _presetChips = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_presetChips, 1);
            bar.Children.Add(_presetChips);

            Button save = Theme.MakeButton("Save current", "\uE710", "OutlineButton");
            save.Height = 30;
            save.MinWidth = 0;
            save.Padding = new Thickness(12, 0, 14, 0);
            save.ToolTip = "Save the current setup as a preset";
            AutomationProperties.SetName(save, "Save current preset");
            save.Click += OnSavePresetClicked;

            _deletePresetButton = new Button
            {
                Style = Theme.FindStyle("OutlineButton"),
                Content = Theme.Glyph("\uE74D", 12, Theme.TextSecondary),
                Width = 32,
                Height = 30,
                Padding = new Thickness(0),
                Margin = new Thickness(6, 0, 0, 0),
                IsEnabled = false,
                ToolTip = "Select a preset to delete it"
            };
            AutomationProperties.SetName(_deletePresetButton, "Delete preset");
            _deletePresetButton.Click += OnDeletePresetClicked;

            StackPanel actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            actions.Children.Add(save);
            actions.Children.Add(_deletePresetButton);
            Grid.SetColumn(actions, 2);
            bar.Children.Add(actions);

            _presetBar = new Border
            {
                Background = Theme.SurfaceAlt,
                BorderBrush = Theme.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 0, 12),
                Child = bar
            };
            return _presetBar;
        }

        private void RefreshPresetBar()
        {
            if (_presetChips == null || _deletePresetButton == null)
            {
                return;
            }

            _presetChips.Children.Clear();
            if (_presets.Count == 0)
            {
                TextBlock empty = Theme.Text("No presets yet. Save this setup to reuse it.", 11.5,
                    FontWeights.Normal, Theme.TextMuted);
                empty.TextWrapping = TextWrapping.Wrap;
                empty.TextTrimming = TextTrimming.None;
                empty.Margin = new Thickness(0, 0, 12, 0);
                _presetChips.Children.Add(empty);
            }
            else
            {
                foreach (PresencePreset preset in _presets)
                {
                    _presetChips.Children.Add(CreatePresetChip(preset));
                }
            }

            bool active = !string.IsNullOrEmpty(_activePresetName);
            _deletePresetButton.IsEnabled = active;
            _deletePresetButton.ToolTip = active
                ? "Delete preset \"" + _activePresetName + "\""
                : "Select a preset to delete it";
        }

        private Button CreatePresetChip(PresencePreset preset)
        {
            bool active = string.Equals(preset.Name, _activePresetName, StringComparison.OrdinalIgnoreCase);
            StackPanel row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (active)
            {
                TextBlock check = Theme.Glyph("\uE73E", 9.5, Theme.Accent);
                check.Margin = new Thickness(0, 0, 5, 0);
                row.Children.Add(check);
            }
            TextBlock label = Theme.Text(preset.Name, 11.5, FontWeights.SemiBold, Theme.TextPrimary);
            label.MaxWidth = 168;
            row.Children.Add(label);

            Button chip = new Button
            {
                Style = Theme.FindStyle("OutlineButton"),
                Content = row,
                Height = 30,
                Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(0, 0, 6, 0),
                Background = active ? Theme.SelectedFill : Theme.ChipFill,
                BorderBrush = active ? Theme.Accent : Theme.InputBorder,
                ToolTip = "Load preset \"" + preset.Name + "\""
            };
            AutomationProperties.SetName(chip, "Preset " + preset.Name);
            chip.Click += delegate { ApplyPreset(preset); };
            return chip;
        }

        private void ApplyPreset(PresencePreset preset)
        {
            if (preset == null)
            {
                return;
            }

            _suppressAutoUpdate = true;
            try
            {
                _gameName.SetText(preset.GameName);
                _details.SetText(preset.Details);
                _state.SetText(preset.State);
                _streamUrl.SetText(preset.StreamUrl);
                _largeImage.SetText(preset.LargeImage);
                _largeText.SetText(preset.LargeText);
                _smallImage.SetText(preset.SmallImage);
                _smallText.SetText(preset.SmallText);
                _button1Label.SetText(preset.Button1Label);
                _button1Url.SetText(preset.Button1Url);
                _button2Label.SetText(preset.Button2Label);
                _button2Url.SetText(preset.Button2Url);
                SelectKind((ActivityKind)preset.ActivityType);
            }
            finally
            {
                _suppressAutoUpdate = false;
            }

            _activePresetName = preset.Name;
            RefreshPresetBar();
            SchedulePreview();
            if (_settings.AutoUpdate)
            {
                ScheduleAutoUpdate();
                ShowSnackbar("Preset \"" + preset.Name + "\" loaded. Auto-update is on.",
                    SnackbarKind.Info);
            }
            else
            {
                ShowSnackbar("Preset \"" + preset.Name + "\" loaded. Click Update presence to publish it.",
                    SnackbarKind.Info);
            }
        }

        private void OnSavePresetClicked(object sender, RoutedEventArgs e)
        {
            string suggested = string.Empty;
            int activeIndex = FindPresetIndex(_activePresetName);
            if (activeIndex >= 0 && MatchesCurrent(_presets[activeIndex]))
            {
                suggested = _presets[activeIndex].Name;
            }
            if (string.IsNullOrEmpty(suggested))
            {
                suggested = _gameName.Value;
            }
            if (string.IsNullOrEmpty(suggested))
            {
                suggested = "My preset";
            }
            if (suggested.Length > 40)
            {
                suggested = suggested.Substring(0, 40);
            }
            ShowSavePresetDialog(suggested);
        }

        private void ShowSavePresetDialog(string suggestedName)
        {
            InputField nameField = new InputField("Preset name", "e.g. Night City courier", 40);
            nameField.SetText(suggestedName);
            nameField.Margin = new Thickness(0, 18, 0, 0);

            TextBlock error = Theme.Text(string.Empty, 11, FontWeights.Normal, Theme.Danger);
            error.TextWrapping = TextWrapping.Wrap;
            error.Margin = new Thickness(1, 6, 0, 0);
            error.Visibility = Visibility.Collapsed;

            Button cancel = Theme.MakeButton("Cancel", null, "OutlineButton");
            cancel.Height = 38;
            cancel.MinWidth = 86;
            AutomationProperties.SetName(cancel, "Cancel");
            cancel.Click += delegate { CloseModal(); };

            Button save = Theme.MakeButton("Save preset", "\uE74E", "PrimaryButton");
            save.Height = 38;
            save.MinWidth = 124;
            save.Margin = new Thickness(10, 0, 0, 0);
            save.IsDefault = true;
            AutomationProperties.SetName(save, "Save preset");
            save.Click += delegate
            {
                string name = nameField.Value;
                if (name.Length == 0)
                {
                    error.Text = "Enter a name for this preset.";
                    error.Visibility = Visibility.Visible;
                    nameField.FocusField();
                    return;
                }

                PresencePreset preset = CollectPreset(name);
                int index = FindPresetIndex(preset.Name);
                bool updated = index >= 0;
                if (updated)
                {
                    _presets[index] = preset;
                }
                else
                {
                    _presets.Add(preset);
                }
                _activePresetName = preset.Name;

                bool saved = SavePresetState();
                RefreshPresetBar();
                CloseModal();
                if (saved)
                {
                    ShowSnackbar(updated
                        ? "Preset \"" + preset.Name + "\" updated."
                        : "Preset \"" + preset.Name + "\" saved.", SnackbarKind.Success);
                }
                else
                {
                    ShowSnackbar("Preset saved for this session, but the settings file couldn't be written.",
                        SnackbarKind.Error);
                }
            };

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };
            buttons.Children.Add(cancel);
            buttons.Children.Add(save);

            StackPanel content = new StackPanel();
            content.Children.Add(Theme.Text("Save preset", 18, FontWeights.SemiBold, Theme.TextPrimary));
            TextBlock description = Theme.Text(
                "Keep this game and its details for one-click switching. Your Application ID stays unchanged; saving an existing name updates that preset.",
                11.5, FontWeights.Normal, Theme.TextMuted);
            description.TextWrapping = TextWrapping.Wrap;
            description.TextTrimming = TextTrimming.None;
            description.Margin = new Thickness(0, 6, 0, 0);
            content.Children.Add(description);
            content.Children.Add(nameField);
            content.Children.Add(error);
            content.Children.Add(buttons);

            OpenModal(content);
            Dispatcher.BeginInvoke(new Action(delegate
            {
                nameField.FocusField();
                nameField.Box.SelectAll();
            }), DispatcherPriority.Input);
        }

        private void OnDeletePresetClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_activePresetName))
            {
                return;
            }
            ShowDeletePresetDialog(_activePresetName);
        }

        private void ShowDeletePresetDialog(string name)
        {
            Button cancel = Theme.MakeButton("Cancel", null, "OutlineButton");
            cancel.Height = 38;
            cancel.MinWidth = 86;
            AutomationProperties.SetName(cancel, "Cancel");
            cancel.Click += delegate { CloseModal(); };

            Button delete = Theme.MakeButton("Delete", "\uE74D", "DangerButton");
            delete.Height = 38;
            delete.MinWidth = 104;
            delete.Margin = new Thickness(10, 0, 0, 0);
            AutomationProperties.SetName(delete, "Confirm delete preset");
            delete.Click += delegate
            {
                int index = FindPresetIndex(name);
                if (index >= 0)
                {
                    _presets.RemoveAt(index);
                }
                _activePresetName = string.Empty;

                bool saved = SavePresetState();
                RefreshPresetBar();
                CloseModal();
                if (saved)
                {
                    ShowSnackbar("Preset \"" + name + "\" deleted.", SnackbarKind.Success);
                }
                else
                {
                    ShowSnackbar("Preset deleted for this session, but the settings file couldn't be written.",
                        SnackbarKind.Error);
                }
            };

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 22, 0, 0)
            };
            buttons.Children.Add(cancel);
            buttons.Children.Add(delete);

            StackPanel content = new StackPanel();
            content.Children.Add(Theme.Text("Delete preset", 18, FontWeights.SemiBold, Theme.TextPrimary));
            TextBlock description = Theme.Text(
                "Delete \"" + name + "\"? Your current fields and published presence stay as they are. This can't be undone.",
                11.5, FontWeights.Normal, Theme.TextMuted);
            description.TextWrapping = TextWrapping.Wrap;
            description.TextTrimming = TextTrimming.None;
            description.Margin = new Thickness(0, 6, 0, 0);
            content.Children.Add(description);
            content.Children.Add(buttons);

            OpenModal(content);
            Dispatcher.BeginInvoke(new Action(delegate
            {
                cancel.Focus();
            }), DispatcherPriority.Input);
        }

        private void RefreshChangelogBadge()
        {
            if (_changelogBadge == null || _changelogBadgeText == null)
            {
                return;
            }
            int unseen = Changelog.UnseenCount(_settings.LastSeenChangelog);
            if (unseen <= 0)
            {
                _changelogBadge.Visibility = Visibility.Collapsed;
                return;
            }
            _changelogBadgeText.Text = unseen > 99 ? "99+" : unseen.ToString();
            _changelogBadge.Visibility = Visibility.Visible;
        }

        private void RefreshAppearanceButton()
        {
            if (_appearanceButton == null || _appearanceGlyph == null || _appearanceGlow == null)
            {
                return;
            }

            bool light = _settings.LightMode;
            _appearanceGlyph.Text = light ? "\u2600\uFE0F" : "\uD83C\uDF19";
            _appearanceGlyph.Foreground = new SolidColorBrush(light
                ? Color.FromRgb(0xE0, 0x92, 0x00)
                : Color.FromRgb(0xDE, 0xE5, 0xFF));

            Color glow = light ? Color.FromRgb(0xFF, 0xB3, 0x00) : Theme.AccentColor;
            _appearanceGlow.Color = glow;
            _appearanceGlow.Opacity = 0.95;
            _appearanceHalo.Fill = BuildGlowHalo(glow, light ? (byte)0x52 : (byte)0x68);

            if (_appearanceLabel != null)
            {
                _appearanceLabel.Text = light ? "Light" : "Dark";
            }

            string action = light ? "Switch to dark mode" : "Switch to light mode";
            _appearanceButton.ToolTip = action;
            AutomationProperties.SetHelpText(_appearanceButton, action);
        }

        /// <summary>
        /// A soft bloom after the moon/sun changes. The halo expands a little
        /// and brightens, then settles back to its resting glow.
        /// </summary>
        private void PlayAppearancePulse()
        {
            if (_settings.ReduceMotion || _appearanceHalo == null || _appearanceHaloScale == null)
            {
                return;
            }

            _appearanceHalo.BeginAnimation(UIElement.OpacityProperty, null);
            _appearanceHalo.Opacity = 1;

            DoubleAnimationUsingKeyFrames glow = new DoubleAnimationUsingKeyFrames();
            glow.KeyFrames.Add(new EasingDoubleKeyFrame(
                0.55, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            glow.KeyFrames.Add(new EasingDoubleKeyFrame(
                1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            glow.KeyFrames.Add(new EasingDoubleKeyFrame(
                1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420))));
            _appearanceHalo.BeginAnimation(UIElement.OpacityProperty, glow);

            _appearanceHaloScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _appearanceHaloScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            _appearanceHaloScale.ScaleX = 1;
            _appearanceHaloScale.ScaleY = 1;
            _appearanceHaloScale.BeginAnimation(ScaleTransform.ScaleXProperty, BuildPulseScale());
            _appearanceHaloScale.BeginAnimation(ScaleTransform.ScaleYProperty, BuildPulseScale());
        }

        private static DoubleAnimationUsingKeyFrames BuildPulseScale()
        {
            DoubleAnimationUsingKeyFrames pulse = new DoubleAnimationUsingKeyFrames();
            pulse.KeyFrames.Add(new EasingDoubleKeyFrame(
                0.88, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            pulse.KeyFrames.Add(new EasingDoubleKeyFrame(
                1.14, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            pulse.KeyFrames.Add(new EasingDoubleKeyFrame(
                1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            });
            return pulse;
        }

        private static RadialGradientBrush BuildGlowHalo(Color color, byte alpha)
        {
            RadialGradientBrush halo = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5)
            };
            halo.GradientStops.Add(new GradientStop(
                Color.FromArgb(alpha, color.R, color.G, color.B), 0.0));
            halo.GradientStops.Add(new GradientStop(
                Color.FromArgb((byte)(alpha / 3), color.R, color.G, color.B), 0.62));
            halo.GradientStops.Add(new GradientStop(
                Color.FromArgb(0, color.R, color.G, color.B), 1.0));
            return halo;
        }

        private void ShowChangelogDialog()
        {
            // Opening the board counts as reading the release notes, so the
            // badge clears and the seen version is remembered portably.
            bool hadUnseen = Changelog.HasUnseen(_settings.LastSeenChangelog);
            _settings.LastSeenChangelog = Changelog.Latest.Version;
            if (hadUnseen)
            {
                SettingsStore.Save(CollectSettings());
            }
            RefreshChangelogBadge();

            StackPanel content = new StackPanel();
            content.Children.Add(Theme.Text("Changelog", 18, FontWeights.SemiBold, Theme.TextPrimary));
            TextBlock description = Theme.Text(
                "What changed in each update, newest first.",
                11.5, FontWeights.Normal, Theme.TextMuted);
            description.TextWrapping = TextWrapping.Wrap;
            description.TextTrimming = TextTrimming.None;
            description.Margin = new Thickness(0, 6, 0, 0);
            content.Children.Add(description);

            foreach (ChangelogEntry entry in Changelog.Entries)
            {
                content.Children.Add(BuildChangelogEntry(entry, entry == Changelog.Latest));
            }

            Button done = Theme.MakeButton("Done", null, "PrimaryButton");
            done.Height = 38;
            done.MinWidth = 96;
            done.IsDefault = true;
            AutomationProperties.SetName(done, "Done");
            done.Click += delegate { CloseModal(); };

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 4, 0, 0)
            };
            buttons.Children.Add(done);
            content.Children.Add(buttons);

            OpenModal(content);
            Dispatcher.BeginInvoke(new Action(delegate
            {
                done.Focus();
            }), DispatcherPriority.Input);
        }

        private Border BuildChangelogEntry(ChangelogEntry entry, bool latest)
        {
            Grid header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel heading = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBlock version = Theme.Text(
                "Version " + entry.Version, 12.5, FontWeights.SemiBold, Theme.TextPrimary);
            heading.Children.Add(version);
            if (latest)
            {
                Border latestChip = new Border
                {
                    Background = Theme.AccentSoft,
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(6, 1, 6, 1),
                    Margin = new Thickness(7, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = Theme.Text("LATEST", 8.5, FontWeights.Bold, Theme.Accent)
                };
                heading.Children.Add(latestChip);
            }
            header.Children.Add(heading);

            TextBlock released = Theme.Text(entry.Released, 10.5, FontWeights.Normal, Theme.TextMuted);
            released.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(released, 1);
            header.Children.Add(released);

            StackPanel body = new StackPanel();
            body.Children.Add(header);
            foreach (string note in entry.Notes)
            {
                Grid line = new Grid { Margin = new Thickness(0, 7, 0, 0) };
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(11) });
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Ellipse dot = new Ellipse
                {
                    Width = 4,
                    Height = 4,
                    Fill = Theme.Accent,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 5, 0, 0)
                };
                line.Children.Add(dot);

                TextBlock text = Theme.Text(note, 11.5, FontWeights.Normal, Theme.TextSecondary);
                text.TextWrapping = TextWrapping.Wrap;
                text.TextTrimming = TextTrimming.None;
                Grid.SetColumn(text, 1);
                line.Children.Add(text);
                body.Children.Add(line);
            }

            return new Border
            {
                Background = Theme.SurfaceAlt,
                BorderBrush = Theme.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(13, 11, 13, 13),
                Margin = new Thickness(0, latest ? 14 : 0, 0, 10),
                Child = body
            };
        }

        private void ShowSettingsDialog()
        {
            _quitTile = new OptionTile("\uE8BB", "Quit the app",
                "The close button stops the presence and exits.");
            _trayTile = new OptionTile("\uE921", "Keep running in the tray",
                "The close button hides the window; reopen it from the tray icon.");

            _quitTile.Chosen += delegate { SetMinimizeToTray(false); };
            _trayTile.Chosen += delegate { SetMinimizeToTray(true); };
            _autoUpdateRow = new ToggleRow("\uE895", "Update automatically",
                "Publish edits, status changes and preset switches as soon as you make them.");
            _autoUpdateRow.Toggled += delegate { SetAutoUpdate(_autoUpdateRow.IsOn); };
            _reduceMotionRow = new ToggleRow("\uE769", "Reduce motion",
                "Turn off the launch animation and editor fades.");
            _reduceMotionRow.Toggled += delegate { SetReduceMotion(_reduceMotionRow.IsOn); };

            _performanceRow = new ToggleRow("\uE945", "Performance mode",
                "Drop the blur effects, batch preview redraws to one frame, and auto-update about three times sooner. Aimed at slower machines and heavy artwork.", true);
            _performanceRow.Toggled += delegate { SetPerformanceMode(_performanceRow.IsOn); };
            _elapsedRow = new ToggleRow("\uE916", "Elapsed timer",
                "Let Discord count how long the presence has been up. The clock starts with the next publish and resets with Clear.", true);
            _elapsedRow.Toggled += delegate { SetElapsed(_elapsedRow.IsOn); };
            _hotkeyRow = new ToggleRow("\uE765", "Summon hotkey",
                "Press Ctrl+Alt+D anywhere in Windows to hide the window to the tray or bring it back.", true);
            _hotkeyRow.Toggled += delegate { SetSummonHotkey(_hotkeyRow.IsOn); };

            _restartTimerButton = Theme.MakeButton("Restart timer", null, "OutlineButton");
            _restartTimerButton.Height = 32;
            _restartTimerButton.MinWidth = 0;
            _restartTimerButton.Padding = new Thickness(13, 0, 13, 0);
            _restartTimerButton.HorizontalAlignment = HorizontalAlignment.Left;
            _restartTimerButton.ToolTip = "Start the elapsed clock again from zero";
            AutomationProperties.SetName(_restartTimerButton, "Restart timer");
            _restartTimerButton.Click += delegate { RestartElapsedTimer(); };

            RefreshSettingsTiles();

            Button done = Theme.MakeButton("Done", null, "PrimaryButton");
            done.Height = 38;
            done.MinWidth = 96;
            done.IsDefault = true;
            AutomationProperties.SetName(done, "Done");
            done.Click += delegate { CloseModal(); };

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            buttons.Children.Add(done);

            TextBlock version = Theme.Text(
                "Version " + AppVersion() + " \u00B7 Built " + BuildInfo.Built,
                11, FontWeights.Normal, Theme.TextMuted);
            version.VerticalAlignment = VerticalAlignment.Center;

            Grid footer = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.Children.Add(version);
            Grid.SetColumn(buttons, 1);
            footer.Children.Add(buttons);

            StackPanel content = new StackPanel();
            content.Children.Add(Theme.Text("Settings", 18, FontWeights.SemiBold, Theme.TextPrimary));
            TextBlock description = Theme.Text(
                "Set the Discord application behind the presence, then choose how updates are published and what the close button does. Preview features sit at the bottom and are marked BETA.",
                11.5, FontWeights.Normal, Theme.TextMuted);
            description.TextWrapping = TextWrapping.Wrap;
            description.TextTrimming = TextTrimming.None;
            description.Margin = new Thickness(0, 6, 0, 0);
            content.Children.Add(description);

            TextBlock connectionCaption = Theme.Text("Connection", 11.5,
                FontWeights.SemiBold, Theme.TextSecondary);
            connectionCaption.Margin = new Thickness(1, 18, 0, 4);
            content.Children.Add(connectionCaption);

            // The field is shared with the settings dialog across openings, so
            // unparent it from the previous dialog before reusing it here.
            Panel previousParent = _applicationId.Parent as Panel;
            if (previousParent != null)
            {
                previousParent.Children.Remove(_applicationId);
            }
            content.Children.Add(_applicationId);

            TextBlock applicationHint = Theme.Text(
                "Create a free Discord application, then copy its Application ID.",
                10.5, FontWeights.Normal, Theme.TextMuted);
            applicationHint.TextWrapping = TextWrapping.Wrap;
            applicationHint.Margin = new Thickness(1, 8, 0, 0);
            content.Children.Add(applicationHint);

            TextBlock developerLink = Theme.Link("Open the Discord Developer Portal \u2197",
                "https://discord.com/developers/applications");
            developerLink.Margin = new Thickness(0, 5, 0, 0);
            developerLink.HorizontalAlignment = HorizontalAlignment.Left;
            content.Children.Add(developerLink);

            TextBlock closeCaption = Theme.Text("When you close the window", 11.5,
                FontWeights.SemiBold, Theme.TextSecondary);
            closeCaption.Margin = new Thickness(1, 20, 0, 8);
            content.Children.Add(closeCaption);
            content.Children.Add(_quitTile);
            content.Children.Add(_trayTile);

            _reduceMotionRow.Margin = new Thickness(0, 0, 0, 2);
            TextBlock motionCaption = Theme.Text("Motion", 11.5,
                FontWeights.SemiBold, Theme.TextSecondary);
            motionCaption.Margin = new Thickness(1, 12, 0, 8);
            content.Children.Add(motionCaption);
            content.Children.Add(_reduceMotionRow);

            _autoUpdateRow.Margin = new Thickness(0, 0, 0, 2);
            TextBlock publishCaption = Theme.Text("Presence updates", 11.5,
                FontWeights.SemiBold, Theme.TextSecondary);
            publishCaption.Margin = new Thickness(1, 12, 0, 8);
            content.Children.Add(publishCaption);
            content.Children.Add(_autoUpdateRow);

            TextBlock betaCaption = Theme.Text("Beta", 11.5,
                FontWeights.SemiBold, Theme.TextSecondary);
            betaCaption.Margin = new Thickness(1, 20, 0, 0);
            content.Children.Add(betaCaption);

            TextBlock betaHint = Theme.Text(
                "Preview features. They are tested and usable, but their behaviour can still change in a later release.",
                10.5, FontWeights.Normal, Theme.TextMuted);
            betaHint.TextWrapping = TextWrapping.Wrap;
            betaHint.TextTrimming = TextTrimming.None;
            betaHint.Margin = new Thickness(1, 4, 0, 8);
            content.Children.Add(betaHint);

            _performanceRow.Margin = new Thickness(0, 0, 0, 8);
            content.Children.Add(_performanceRow);

            _elapsedRow.Margin = new Thickness(0, 0, 0, 8);
            content.Children.Add(_elapsedRow);
            content.Children.Add(_restartTimerButton);

            _hotkeyRow.Margin = new Thickness(0, 6, 0, 2);
            content.Children.Add(_hotkeyRow);

            content.Children.Add(footer);

            OpenModal(content);
            Dispatcher.BeginInvoke(new Action(delegate
            {
                _quitTile.Focus();
            }), DispatcherPriority.Input);
        }

        private static string AppVersion()
        {
            Version version = typeof(MainWindow).Assembly.GetName().Version;
            return version == null ? "1.0.0" : version.ToString(3);
        }

        private void RefreshSettingsTiles()
        {
            if (_quitTile != null)
            {
                _quitTile.SetSelected(!_settings.MinimizeToTray);
            }
            if (_trayTile != null)
            {
                _trayTile.SetSelected(_settings.MinimizeToTray);
            }
            if (_autoUpdateRow != null)
            {
                _autoUpdateRow.SetOn(_settings.AutoUpdate);
            }
            if (_reduceMotionRow != null)
            {
                _reduceMotionRow.SetOn(_settings.ReduceMotion);
            }
            if (_performanceRow != null)
            {
                _performanceRow.SetOn(_settings.PerformanceMode);
            }
            if (_elapsedRow != null)
            {
                _elapsedRow.SetOn(_settings.ShowElapsed);
            }
            if (_hotkeyRow != null)
            {
                _hotkeyRow.SetOn(_settings.SummonHotkey);
            }
            if (_restartTimerButton != null)
            {
                _restartTimerButton.IsEnabled = _settings.ShowElapsed;
            }
        }

        private void SetMinimizeToTray(bool value)
        {
            if (_settings.MinimizeToTray == value)
            {
                return;
            }
            _settings.MinimizeToTray = value;
            if (!value)
            {
                if (!IsVisible)
                {
                    RestoreFromTray();
                }
                DisposeTrayIcon();
            }
            SettingsStore.Save(CollectSettings());
            RefreshSettingsTiles();
        }

        private void SetLightMode(bool value)
        {
            if (_settings.LightMode == value)
            {
                return;
            }
            _settings.LightMode = value;
            Theme.Apply(value ? Theme.Appearance.Light : Theme.Appearance.Dark);
            ApplySystemTheme(IntPtr.Zero);
            RefreshAppearanceButton();
            PlayAppearancePulse();
            SettingsStore.Save(CollectSettings());
            RefreshSettingsTiles();
        }

        private void SetAutoUpdate(bool value)
        {
            if (_settings.AutoUpdate == value)
            {
                return;
            }
            _settings.AutoUpdate = value;
            SettingsStore.Save(CollectSettings());
            RefreshSettingsTiles();
            if (!value)
            {
                _autoUpdateTimer.Stop();
                return;
            }
            ScheduleAutoUpdate();
        }

        private void SetReduceMotion(bool value)
        {
            if (_settings.ReduceMotion == value)
            {
                return;
            }
            _settings.ReduceMotion = value;
            SettingsStore.Save(CollectSettings());
            RefreshSettingsTiles();
            if (value)
            {
                _restoreFadeRunning = false;
                ShowContentImmediately();
                SetDwmTransitions(true);
            }
        }

        /// <summary>
        /// Performance mode drops the blur effects, coalesces preview redraws to
        /// a single frame and shortens the auto-publish delay.
        /// </summary>
        private void SetPerformanceMode(bool value)
        {
            if (_settings.PerformanceMode == value)
            {
                return;
            }
            _settings.PerformanceMode = value;
            ApplyPerformanceProfile();
            SettingsStore.Save(CollectSettings());
            RefreshSettingsTiles();
            ShowSnackbar(value
                ? "Performance mode on: flat surfaces, previews batched to one frame, faster auto-updates."
                : "Performance mode off.", SnackbarKind.Info);
        }

        private void ApplyPerformanceProfile()
        {
            Theme.SetPerformanceMode(_settings.PerformanceMode);
            if (_previewTimer != null)
            {
                _previewTimer.Interval = TimeSpan.FromMilliseconds(
                    _settings.PerformanceMode ? PreviewFrameDelay : PreviewSettleDelay);
            }
        }

        private void SetElapsed(bool value)
        {
            if (_settings.ShowElapsed == value)
            {
                return;
            }
            _settings.ShowElapsed = value;
            _settings.ElapsedStart = value
                ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                : 0;
            SettingsStore.Save(CollectSettings());
            RefreshSettingsTiles();
            RefreshPreview();
            ScheduleAutoUpdate();
            ShowSnackbar(value
                ? (_settings.AutoUpdate
                    ? "Elapsed timer on. Discord starts counting with the next publish."
                    : "Elapsed timer on. Update presence to publish it.")
                : "Elapsed timer off.", SnackbarKind.Info);
        }

        private void RestartElapsedTimer()
        {
            _settings.ElapsedStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            SettingsStore.Save(CollectSettings());
            RefreshPreview();
            RefreshSettingsTiles();
            ScheduleAutoUpdate();
            ShowSnackbar(_settings.AutoUpdate
                ? "Timer restarted. Auto-update republishes it in a moment."
                : "Timer restarted. Update presence to publish it.", SnackbarKind.Info);
        }

        /// <summary>Starts the Discord clock the first time a presence goes out.</summary>
        private long EnsureElapsedStart()
        {
            if (!_settings.ShowElapsed)
            {
                return 0;
            }
            if (_settings.ElapsedStart <= 0)
            {
                _settings.ElapsedStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
            return _settings.ElapsedStart;
        }

        private void SetSummonHotkey(bool value)
        {
            if (_settings.SummonHotkey == value)
            {
                return;
            }
            _settings.SummonHotkey = value;
            SettingsStore.Save(CollectSettings());
            RefreshSettingsTiles();
            ApplyHotkey();
            ShowSnackbar(value
                ? (_hotkeyRegistered
                    ? "Press Ctrl+Alt+D anywhere in Windows to hide or summon the window."
                    : "Ctrl+Alt+D is already used by another app, so the hotkey stays off.")
                : "Summon hotkey off.", SnackbarKind.Info);
        }

        private void ApplyHotkey()
        {
            ReleaseHotkey();
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero || !_settings.SummonHotkey)
            {
                return;
            }
            _hotkeyRegistered = RegisterHotKey(handle, SummonHotkeyId, HotkeyModifiers, HotkeyVirtualKey);
        }

        private void ReleaseHotkey()
        {
            if (!_hotkeyRegistered)
            {
                return;
            }
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                UnregisterHotKey(handle, SummonHotkeyId);
            }
            _hotkeyRegistered = false;
        }

        private void ToggleSummon()
        {
            if (IsVisible && WindowState != WindowState.Minimized)
            {
                HideToTray();
            }
            else
            {
                RestoreFromTray();
            }
        }

        private void EnsureTrayIcon()
        {
            if (_tray != null)
            {
                return;
            }

            System.Drawing.Icon icon = null;
            try
            {
                icon = System.Drawing.Icon.ExtractAssociatedIcon(
                    System.Reflection.Assembly.GetEntryAssembly().Location);
            }
            catch
            {
                // Fall back to the generic application icon.
            }
            if (icon == null)
            {
                icon = System.Drawing.SystemIcons.Application;
            }

            System.Windows.Forms.ContextMenuStrip menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Open Discord Presence", null,
                delegate { Dispatcher.BeginInvoke(new Action(RestoreFromTray)); });
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Quit", null,
                delegate { Dispatcher.BeginInvoke(new Action(ExitFromTray)); });

            _tray = new System.Windows.Forms.NotifyIcon
            {
                Icon = icon,
                Text = "Discord Presence",
                ContextMenuStrip = menu,
                Visible = true
            };
            _tray.DoubleClick += delegate { Dispatcher.BeginInvoke(new Action(RestoreFromTray)); };
        }

        private void DisposeTrayIcon()
        {
            if (_tray == null)
            {
                return;
            }
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }

        private void HideToTray()
        {
            EnsureTrayIcon();
            if (_trayHideRunning)
            {
                return;
            }

            if (!IsVisible || WindowState == WindowState.Minimized || _settings.ReduceMotion)
            {
                // Hide first: restoring opacity before hiding paints one
                // full-opacity frame and reads as a flash.
                Hide();
                ++_fadeVersion;
                if (_fadeContent != null)
                {
                    _fadeContent.BeginAnimation(UIElement.OpacityProperty, null);
                    _fadeContent.Opacity = 1;
                }
                BeginAnimation(OpacityProperty, null);
                Opacity = 1;
                IsHitTestVisible = true;
                ShowTrayHint();
                return;
            }

            _trayHideRunning = true;
            _restoreFadeRunning = false;
            int version = ++_trayHideVersion;
            IsHitTestVisible = false;
            double from = _fadeContent != null ? _fadeContent.Opacity : 1;

            StartContentFade(from, 0, 200, new CubicEase { EasingMode = EasingMode.EaseInOut },
                delegate
                {
                    if (version != _trayHideVersion || _closing)
                    {
                        _trayHideRunning = false;
                        return;
                    }

                    _trayHideRunning = false;
                    Hide();
                    _fadeContent.BeginAnimation(UIElement.OpacityProperty, null);
                    _fadeContent.Opacity = 1;
                    IsHitTestVisible = true;
                    ShowTrayHint();
                });
        }

        private void ShowTrayHint()
        {
            if (_tray != null && !_trayHintShown)
            {
                _trayHintShown = true;
                _tray.ShowBalloonTip(3000, "Discord Presence",
                    "Still running here. Double-click the icon to reopen it.",
                    System.Windows.Forms.ToolTipIcon.Info);
            }
        }

        private void RestoreFromTray()
        {
            DisposeTrayIcon();
            _trayHideVersion++;
            _trayHideRunning = false;
            _restoreFadeRunning = false;
            IsHitTestVisible = true;
            _suppressRestoreAnimation = true;
            // Show the window transparent so its first visible frame is not
            // a full-opacity flash before PlayRestoreAnimation starts.
            ++_fadeVersion;
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            if (_fadeContent != null)
            {
                _fadeContent.BeginAnimation(UIElement.OpacityProperty, null);
                _fadeContent.Opacity = _settings.ReduceMotion ? 1 : 0;
            }
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            _suppressRestoreAnimation = false;
            _restoreFadePending = false;
            Activate();
            Topmost = true;
            Topmost = false;
            ApplyWindowRegion();
            PlayRestoreAnimation();
            FlushPreviewIfDirty();
        }

        private void ExitFromTray()
        {
            _quitRequested = true;
            DisposeTrayIcon();
            Close();
        }

        private PresencePreset CollectPreset(string name)
        {
            PresencePreset preset = new PresencePreset
            {
                Name = name,
                GameName = _gameName.Value,
                ActivityType = (int)_kind,
                Details = _details.Value,
                State = _state.Value,
                StreamUrl = _streamUrl.Value,
                LargeImage = _largeImage.Value,
                LargeText = _largeText.Value,
                SmallImage = _smallImage.Value,
                SmallText = _smallText.Value,
                Button1Label = _button1Label.Value,
                Button1Url = _button1Url.Value,
                Button2Label = _button2Label.Value,
                Button2Url = _button2Url.Value
            };
            preset.Normalize();
            return preset;
        }

        private bool MatchesCurrent(PresencePreset preset)
        {
            PresencePreset current = CollectPreset(preset.Name);
            return current.GameName == preset.GameName &&
                current.ActivityType == preset.ActivityType &&
                current.Details == preset.Details &&
                current.State == preset.State &&
                current.StreamUrl == preset.StreamUrl &&
                current.LargeImage == preset.LargeImage &&
                current.LargeText == preset.LargeText &&
                current.SmallImage == preset.SmallImage &&
                current.SmallText == preset.SmallText &&
                current.Button1Label == preset.Button1Label &&
                current.Button1Url == preset.Button1Url &&
                current.Button2Label == preset.Button2Label &&
                current.Button2Url == preset.Button2Url;
        }

        private int FindPresetIndex(string name)
        {
            for (int i = 0; i < _presets.Count; i++)
            {
                if (string.Equals(_presets[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private bool SavePresetState()
        {
            return SettingsStore.Save(CollectSettings()) != null;
        }

        private Grid BuildModalLayer()
        {
            Grid layer = new Grid
            {
                Background = Theme.Scrim,
                Visibility = Visibility.Collapsed
            };

            _modalCard = new Border
            {
                Width = 460,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Theme.CardBrush,
                BorderBrush = Theme.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(22, 20, 22, 20),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            Theme.Elevate(_modalCard, new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 36,
                ShadowDepth = 10,
                Direction = 270,
                Opacity = 0.55,
                RenderingBias = RenderingBias.Performance
            });
            KeyboardNavigation.SetTabNavigation(_modalCard, KeyboardNavigationMode.Cycle);
            layer.Children.Add(_modalCard);

            layer.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape)
                {
                    CloseModal();
                    e.Handled = true;
                }
            };
            layer.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.OriginalSource == layer)
                {
                    CloseModal();
                }
            };

            return layer;
        }

        private void OpenModal(UIElement content)
        {
            if (_modalLayer == null)
            {
                return;
            }

            _previousFocus = Keyboard.FocusedElement;
            // Taller dialogs stay usable when the window is resized short.
            _modalCard.Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false,
                PanningMode = PanningMode.VerticalOnly,
                MaxHeight = Math.Max(320, ActualHeight - 40),
                Content = content
            };
            if (_updateButton != null)
            {
                _updateButton.IsDefault = false;
            }

            _modalLayer.Opacity = 0;
            _modalLayer.Visibility = Visibility.Visible;
            DoubleAnimation fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140));
            fade.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            _modalLayer.BeginAnimation(UIElement.OpacityProperty, fade);

            TranslateTransform rise = new TranslateTransform(0, 10);
            _modalCard.RenderTransform = rise;
            DoubleAnimation slide = new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(180));
            slide.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            rise.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        private void CloseModal()
        {
            if (_modalLayer == null || _modalLayer.Visibility != Visibility.Visible)
            {
                return;
            }

            _modalLayer.Visibility = Visibility.Collapsed;
            _modalLayer.BeginAnimation(UIElement.OpacityProperty, null);
            _modalLayer.Opacity = 1;
            _modalCard.Child = null;
            if (_updateButton != null)
            {
                _updateButton.IsDefault = true;
            }

            IInputElement previous = _previousFocus;
            _previousFocus = null;
            UIElement element = previous as UIElement;
            if (element != null && element.IsVisible && element.IsEnabled)
            {
                Keyboard.Focus(previous);
            }
        }

        private Border BuildActivityCard()
        {
            _gameName = new InputField("Game name", "e.g. Night City Courier", 128);
            _details = new InputField("Details", "What you're doing - e.g. Running the rooftops", 128);
            _state = new InputField("State", "Current status - e.g. With the crew", 128);

            TextBlock typeCaption = Theme.Text("Status type", 11.5, FontWeights.SemiBold, Theme.TextSecondary);
            typeCaption.Margin = new Thickness(1, 0, 0, 5);

            Grid chipRow = Theme.Row(8,
                BuildChip(ActivityKind.Playing, "\uD83C\uDFAE", "Playing"),
                BuildChip(ActivityKind.Streaming, "\uD83C\uDFA5", "Streaming"),
                BuildChip(ActivityKind.Listening, "\uD83C\uDFB6", "Listening"),
                BuildChip(ActivityKind.Watching, "\uD83C\uDFAC", "Watching"));

            StackPanel typeBlock = new StackPanel();
            typeBlock.Children.Add(typeCaption);
            typeBlock.Children.Add(chipRow);

            _streamUrl = new InputField("Stream URL", "https://twitch.tv/yourchannel", 2048);
            TextBlock streamHint = Theme.Text(
                "Discord doesn't allow custom apps to set Streaming. This publishes as Playing; the link is added as a button when a slot is free.",
                10.5, FontWeights.Normal, Theme.TextMuted);
            streamHint.TextWrapping = TextWrapping.Wrap;
            streamHint.Margin = new Thickness(1, 5, 0, 0);
            _streamBlock = new StackPanel { Visibility = Visibility.Collapsed };
            _streamBlock.Children.Add(_streamUrl);
            _streamBlock.Children.Add(streamHint);

            StackPanel fields = Theme.VStack(12,
                _gameName,
                typeBlock,
                Theme.Row(12, _details, _state),
                _streamBlock);

            StackPanel body = new StackPanel();
            body.Children.Add(Theme.SectionHeader("Activity", "The core details of your presence."));
            body.Children.Add(fields);
            return Theme.Card(body);
        }

        private ActivityChip BuildChip(ActivityKind kind, string glyph, string label)
        {
            ActivityChip chip = new ActivityChip(kind, glyph, label);
            if (kind == ActivityKind.Streaming)
            {
                chip.ToolTip = "Discord doesn't allow custom Streaming activity; this publishes as Playing.";
            }
            chip.Chosen += delegate
            {
                SelectKind(kind);
                ScheduleAutoUpdate();
                SchedulePreview();
            };
            _chips.Add(chip);
            return chip;
        }

        private Border BuildButtonsCard()
        {
            _button1Label = new InputField("Label 1", "e.g. Watch stream", 32);
            _button1Url = new InputField("URL 1", "https://...", 2048);
            _button2Label = new InputField("Label 2", "e.g. Join the server", 32);
            _button2Url = new InputField("URL 2", "https://...", 2048);

            StackPanel fields = Theme.VStack(12,
                ButtonRow(_button1Label, _button1Url),
                ButtonRow(_button2Label, _button2Url));

            StackPanel body = new StackPanel();
            body.Children.Add(Theme.SectionHeader("Buttons",
                "Up to two links. Discord shows them to other people only \u2014 never on your own profile."));
            body.Children.Add(fields);
            return Theme.Card(body);
        }

        private static Grid ButtonRow(InputField label, InputField url)
        {
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            Grid.SetColumn(label, 0);
            Grid.SetColumn(url, 2);
            row.Children.Add(label);
            row.Children.Add(url);
            return row;
        }

        private Border BuildPreviewCard()
        {
            _preview = new PresencePreview();

            StackPanel body = new StackPanel();
            body.Children.Add(Theme.SectionHeader("Live preview",
                "How other people see your activity on Discord."));
            body.Children.Add(_preview);
            return Theme.Card(body);
        }

        private Border BuildArtworkCard()
        {
            _largeImage = new InputField("Large image", "https://... or asset key", 2048);
            _largeText = new InputField("Large image text", "Hover text", 128);
            _smallImage = new InputField("Small image", "https://... or asset key", 2048);
            _smallText = new InputField("Small image text", "Hover text", 128);

            StackPanel fields = Theme.VStack(12,
                Theme.Row(12, _largeImage, _largeText),
                Theme.Row(12, _smallImage, _smallText));

            StackPanel body = new StackPanel();
            body.Children.Add(Theme.SectionHeader("Artwork",
                "Use a direct https image URL or an uploaded asset key."));
            body.Children.Add(fields);
            return Theme.Card(body);
        }

        private Border BuildActionBar()
        {
            Grid bar = new Grid();
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel tip = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };
            TextBlock tipText = Theme.Text("Discord Rich Presence Tool \u00A9 2026 by Jumayusi",
                11, FontWeights.Normal, Theme.TextMuted);
            tipText.TextWrapping = TextWrapping.Wrap;
            tip.Children.Add(tipText);
            Grid.SetColumn(tip, 0);
            bar.Children.Add(tip);

            _clearButton = Theme.MakeButton("Clear", "\uE894", "OutlineButton");
            _clearButton.MinWidth = 96;
            _clearButton.ToolTip = "Remove the published activity";
            AutomationProperties.SetName(_clearButton, "Clear presence");
            _clearButton.Click += OnClearClicked;

            _updateButton = Theme.MakeButton("Update presence", "\uE724", "PrimaryButton");
            _updateButton.MinWidth = 176;
            _updateButton.Height = 42;
            _updateButton.IsDefault = true;
            _updateButton.Margin = new Thickness(10, 0, 0, 0);
            Theme.Elevate(_updateButton, new DropShadowEffect
            {
                Color = Theme.AccentColor,
                BlurRadius = 16,
                ShadowDepth = 0,
                Opacity = 0.32
            });
            _updateButton.ToolTip = "Publish this activity to Discord";
            AutomationProperties.SetName(_updateButton, "Update presence");
            _updateButton.Click += OnUpdateClicked;

            StackPanel actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            actions.Children.Add(_clearButton);
            actions.Children.Add(_updateButton);
            Grid.SetColumn(actions, 1);
            bar.Children.Add(actions);

            return new Border
            {
                Background = Theme.SurfaceAlt,
                BorderBrush = Theme.CardBorder,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(22, 0, 22, 0),
                Child = bar
            };
        }

        private void ApplySettings()
        {
            _applicationId.SetText(_settings.ApplicationId);
            _gameName.SetText(_settings.GameName);
            _details.SetText(_settings.Details);
            _state.SetText(_settings.State);
            _streamUrl.SetText(_settings.StreamUrl);
            _largeImage.SetText(_settings.LargeImage);
            _largeText.SetText(_settings.LargeText);
            _smallImage.SetText(_settings.SmallImage);
            _smallText.SetText(_settings.SmallText);
            _button1Label.SetText(_settings.Button1Label);
            _button1Url.SetText(_settings.Button1Url);
            _button2Label.SetText(_settings.Button2Label);
            _button2Url.SetText(_settings.Button2Url);
            SelectKind((ActivityKind)_settings.ActivityType);
            RefreshPreview();
        }

        /// <summary>Every editable field feeds the debounced auto-update pulse.</summary>
        private void HookLiveUpdates()
        {
            InputField[] fields =
            {
                _applicationId, _gameName, _details, _state, _streamUrl,
                _largeImage, _largeText, _smallImage, _smallText,
                _button1Label, _button1Url, _button2Label, _button2Url
            };
            foreach (InputField field in fields)
            {
                field.Box.TextChanged += delegate
                {
                    ScheduleAutoUpdate();
                    SchedulePreview();
                };
            }
        }

        private void SchedulePreview()
        {
            if (_previewTimer == null)
            {
                RefreshPreview();
                return;
            }
            if (_closing || !IsVisible || WindowState == WindowState.Minimized)
            {
                // Nothing on screen needs repainting. The deferred refresh runs
                // when the window comes back instead of burning frames here.
                _previewDirty = true;
                return;
            }
            _previewTimer.Stop();
            _previewTimer.Start();
        }

        private void FlushPreviewIfDirty()
        {
            if (_previewDirty)
            {
                SchedulePreview();
            }
        }

        /// <summary>Rebuilds the Discord-style card from the current fields.</summary>
        private void RefreshPreview()
        {
            if (_preview == null)
            {
                return;
            }
            _previewDirty = false;

            List<PreviewButton> buttons = new List<PreviewButton>();
            AddPreviewButton(buttons, _button1Label.Value, _button1Url.Value);
            AddPreviewButton(buttons, _button2Label.Value, _button2Url.Value);

            _preview.Update(_kind, _applicationId.Value, _gameName.Value, _details.Value, _state.Value,
                _streamUrl.Value, _largeImage.Value, _largeText.Value, _smallImage.Value, _smallText.Value,
                buttons, _settings.ShowElapsed ? _settings.ElapsedStart : 0);
        }

        private static void AddPreviewButton(List<PreviewButton> buttons, string label, string url)
        {
            if (buttons.Count >= 2 || string.IsNullOrEmpty(label) || string.IsNullOrEmpty(url))
            {
                return;
            }
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return;
            }
            PreviewButton button = new PreviewButton();
            button.Label = label.Trim();
            button.Url = url.Trim();
            buttons.Add(button);
        }

        private void SelectKind(ActivityKind kind)
        {
            _kind = kind;
            foreach (ActivityChip chip in _chips)
            {
                chip.SetSelected(chip.Kind == kind);
            }
            _streamBlock.Visibility = kind == ActivityKind.Streaming ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            // The shell is already painted at this point; reveal it in one
            // step and let the launch overlay own the visible animation. That
            // avoids the layered-window white frame a window fade can show.
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
            if (_settings.ReduceMotion)
            {
                if (_bootLayer != null)
                {
                    _bootLayer.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                StartBootSequence();
            }
            if (IsValidAppId(_applicationId.Value))
            {
                SetStatus("Connecting", Theme.Accent);
                TryConnectSilently();
            }
            else
            {
                SetStatus("Not connected", Theme.TextMuted);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    ShowSnackbar("Add your Discord Application ID in Settings to connect.", SnackbarKind.Info);
                }), DispatcherPriority.ApplicationIdle);
            }
        }

        private void StartBootSequence()
        {
            if (_bootLayer == null || _closing)
            {
                return;
            }
            if (_settings.ReduceMotion)
            {
                _bootLayer.Visibility = Visibility.Collapsed;
                return;
            }

            _bootLayer.Visibility = Visibility.Visible;
            _bootLayer.Opacity = 1;

            DoubleAnimationUsingKeyFrames logoFade = new DoubleAnimationUsingKeyFrames();
            logoFade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            logoFade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
            logoFade.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            logoFade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1300))));
            logoFade.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1700)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            });
            _bootLogo.BeginAnimation(UIElement.OpacityProperty, logoFade);

            DoubleAnimationUsingKeyFrames logoScale = new DoubleAnimationUsingKeyFrames();
            logoScale.KeyFrames.Add(new LinearDoubleKeyFrame(0.88, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            logoScale.KeyFrames.Add(new LinearDoubleKeyFrame(0.88, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
            logoScale.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            logoScale.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1300))));
            logoScale.KeyFrames.Add(new EasingDoubleKeyFrame(1.05, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1700)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            });
            ScaleTransform logoTransform = _bootLogo.RenderTransform as ScaleTransform;
            if (logoTransform != null)
            {
                logoTransform.BeginAnimation(ScaleTransform.ScaleXProperty, logoScale);
                logoTransform.BeginAnimation(ScaleTransform.ScaleYProperty, logoScale);
            }

            DoubleAnimation textFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500))
            {
                BeginTime = TimeSpan.FromMilliseconds(1750),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            _bootText.BeginAnimation(UIElement.OpacityProperty, textFade);

            TranslateTransform textShift = _bootText.RenderTransform as TranslateTransform;
            if (textShift != null)
            {
                DoubleAnimation textRise = new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(550))
                {
                    BeginTime = TimeSpan.FromMilliseconds(1750),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                textShift.BeginAnimation(TranslateTransform.YProperty, textRise);
            }

            DoubleAnimation overlayFade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(380))
            {
                BeginTime = TimeSpan.FromMilliseconds(2700),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            overlayFade.Completed += delegate
            {
                if (_bootLayer != null)
                {
                    _bootLayer.Visibility = Visibility.Collapsed;
                    _bootLayer.Opacity = 1;
                }
            };
            _bootLayer.BeginAnimation(UIElement.OpacityProperty, overlayFade);
        }

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            WindowState previous = _lastWindowState;
            _lastWindowState = WindowState;

            // Restored from the taskbar (not the initial show).
            if (!_suppressRestoreAnimation && previous == WindowState.Minimized &&
                WindowState == WindowState.Normal)
            {
                _restoreFadePending = false;
                RestoreCustomChrome();
                if (_settings.ReduceMotion)
                {
                    FinishRestoreInstant();
                }
                else
                {
                    PlayRestoreAnimation();
                }
                FlushPreviewIfDirty();
            }

            // WM_SIZE can arrive before the new state is visible to WPF.
            Dispatcher.BeginInvoke(new Action(ApplyWindowRegion), DispatcherPriority.Loaded);
        }

        private void OnReconnectTick(object sender, EventArgs e)
        {
            if (_closing || _busy || _autoConnecting)
            {
                return;
            }
            if (_rpc.IsConnected || !IsValidAppId(_applicationId.Value))
            {
                return;
            }
            TryConnectSilently();
        }

        private void TryConnectSilently()
        {
            if (_autoConnecting)
            {
                return;
            }
            string id = _applicationId.Value;
            if (!IsValidAppId(id))
            {
                return;
            }
            _autoConnecting = true;

            // Auto-update restores the saved setup on launch and after a
            // reconnect, unless the presence was explicitly cleared.
            Dictionary<string, object> fallback = null;
            if (_settings.AutoUpdate && !_restoreSuppressed)
            {
                string ignored;
                fallback = BuildActivity(out ignored, false);
            }

            Task.Run(delegate
            {
                Dictionary<string, object> resume = null;
                lock (_rpcGate)
                {
                    if (!_rpc.IsConnected || _connectedAppId != id)
                    {
                        string error = _rpc.Connect(id);
                        if (error == null)
                        {
                            _connectedAppId = id;
                            resume = _lastPublished;
                        }
                    }
                    else
                    {
                        resume = _lastPublished;
                    }

                    if (resume == null && _rpc.IsConnected)
                    {
                        resume = fallback;
                    }
                    if (resume != null)
                    {
                        if (_rpc.SetActivity(resume) == null)
                        {
                            _lastPublished = resume;
                        }
                    }
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _autoConnecting = false;
                    if (_closing)
                    {
                        return;
                    }
                    SetStatus(_rpc.IsConnected ? "Connected" : "Discord offline",
                        _rpc.IsConnected ? Theme.Success : Theme.TextMuted);
                }));
            });
        }

        private void OnUpdateClicked(object sender, RoutedEventArgs e)
        {
            if (_busy)
            {
                return;
            }

            _autoUpdateTimer.Stop();
            _autoUpdatePending = false;
            string error;
            Dictionary<string, object> activity = BuildActivity(out error);
            if (activity == null)
            {
                ShowSnackbar(error, SnackbarKind.Error);
                return;
            }

            _restoreSuppressed = false;
            SettingsStore.Save(CollectSettings());
            SetBusy(true);
            SetStatus("Connecting", Theme.Accent);
            PublishActivity(activity, false);
        }

        /// <summary>
        /// Sends one activity to Discord. Quiet publishes come from auto-update:
        /// they keep the UI free and never raise a toast.
        /// </summary>
        private void PublishActivity(Dictionary<string, object> activity, bool quiet)
        {
            string id = _applicationId.Value;
            Task.Run(delegate
            {
                string result;
                lock (_rpcGate)
                {
                    result = null;
                    if (!_rpc.IsConnected || _connectedAppId != id)
                    {
                        result = _rpc.Connect(id);
                        if (result == null)
                        {
                            _connectedAppId = id;
                        }
                    }
                    if (result == null)
                    {
                        result = _rpc.SetActivity(activity);
                    }
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (quiet)
                    {
                        _autoPublishing = false;
                    }
                    else
                    {
                        SetBusy(false);
                    }
                    if (_closing)
                    {
                        return;
                    }

                    if (result == null)
                    {
                        _lastPublished = activity;
                        SetStatus("Connected", Theme.Success);
                        if (_settings.ShowElapsed)
                        {
                            // The publish may have started the clock, so the
                            // preview counter has to catch up right away.
                            RefreshPreview();
                        }
                        if (!quiet)
                        {
                            string message = _kind == ActivityKind.Streaming
                                ? "Published as Playing - Discord doesn't allow a custom Streaming status."
                                : "Presence published to Discord.";
                            if (HasButtons(activity))
                            {
                                message += " Buttons only appear for other people.";
                            }
                            ShowSnackbar(message, SnackbarKind.Success);
                        }
                    }
                    else
                    {
                        SetStatus(_rpc.IsConnected ? "Connected" : "Discord offline",
                            _rpc.IsConnected ? Theme.Success : Theme.TextMuted);
                        if (!quiet)
                        {
                            ShowSnackbar(result, SnackbarKind.Error);
                        }
                    }
                }));
            });
        }

        /// <summary>Restarts the debounce window; a burst of typing publishes once.</summary>
        private void ScheduleAutoUpdate()
        {
            if (_closing || _suppressAutoUpdate || !_settings.AutoUpdate || _autoUpdateTimer == null)
            {
                return;
            }
            _autoUpdatePending = true;
            _restoreSuppressed = false;
            _autoUpdateTimer.Stop();
            _autoUpdateTimer.Interval = TimeSpan.FromMilliseconds(700);
            _autoUpdateTimer.Start();
        }

        private void OnAutoUpdateTick(object sender, EventArgs e)
        {
            _autoUpdateTimer.Stop();
            if (_closing || !_settings.AutoUpdate || !_autoUpdatePending)
            {
                return;
            }
            if (_busy || _autoPublishing)
            {
                _autoUpdateTimer.Interval = TimeSpan.FromMilliseconds(300);
                _autoUpdateTimer.Start();
                return;
            }

            // One attempt per change: a failed publish must not loop on its own.
            _autoUpdatePending = false;
            string error;
            Dictionary<string, object> activity = BuildActivity(out error, false);
            if (activity == null)
            {
                return;
            }

            SettingsStore.Save(CollectSettings());
            _autoPublishing = true;
            SetStatus("Updating", Theme.Accent);
            PublishActivity(activity, true);
        }

        private void OnClearClicked(object sender, RoutedEventArgs e)
        {
            if (_busy)
            {
                return;
            }
            if (_autoUpdateTimer != null)
            {
                _autoUpdateTimer.Stop();
            }
            _autoUpdatePending = false;
            if (!_rpc.IsConnected)
            {
                ShowSnackbar("Discord isn't connected yet. Publish once, then clear.", SnackbarKind.Info);
                return;
            }

            SetBusy(true);
            Task.Run(delegate
            {
                string result;
                lock (_rpcGate)
                {
                    result = _rpc.ClearActivity();
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    SetBusy(false);
                    if (result == null)
                    {
                        _lastPublished = null;
                        _restoreSuppressed = true;
                        // Clearing ends the session, so the next publish starts
                        // the elapsed clock over.
                        _settings.ElapsedStart = 0;
                        SettingsStore.Save(CollectSettings());
                        RefreshPreview();
                        ShowSnackbar("Presence cleared.", SnackbarKind.Success);
                    }
                    else
                    {
                        ShowSnackbar(result, SnackbarKind.Error);
                    }
                }));
            });
        }

        private Dictionary<string, object> BuildActivity(out string error)
        {
            return BuildActivity(out error, true);
        }

        /// <summary>
        /// Builds the activity payload. Auto-update passes focusInvalid: false so
        /// an unfinished edit never yanks focus out of the field being typed in.
        /// </summary>
        private Dictionary<string, object> BuildActivity(out string error, bool focusInvalid)
        {
            error = null;
            string id = _applicationId.Value;
            if (!IsValidAppId(id))
            {
                if (focusInvalid)
                {
                    _applicationId.FocusField();
                }
                error = "Enter a valid Discord Application ID in Settings (15-20 digits).";
                return null;
            }

            Dictionary<string, object> activity = new Dictionary<string, object>();
            string gameName = _gameName.Value;
            if (gameName.Length > 0)
            {
                activity["name"] = gameName;
            }
            // Discord accepts 0, 2, 3 and 5 over RPC. Type 1 (Streaming) is
            // reserved and rejected, so a streaming entry falls back to Playing.
            activity["type"] = _kind == ActivityKind.Streaming ? 0 : (int)_kind;

            string details = _details.Value;
            if (details.Length > 0)
            {
                activity["details"] = details;
            }
            string state = _state.Value;
            if (state.Length > 0)
            {
                activity["state"] = state;
            }

            string streamUrl = null;
            if (_kind == ActivityKind.Streaming)
            {
                streamUrl = _streamUrl.Value;
                if (streamUrl.Length == 0)
                {
                    if (focusInvalid)
                    {
                        _streamUrl.FocusField();
                    }
                    error = "Add a stream URL to use the Streaming option.";
                    return null;
                }
                if (!IsHttpUrl(streamUrl))
                {
                    if (focusInvalid)
                    {
                        _streamUrl.FocusField();
                    }
                    error = "The stream URL must start with http:// or https://.";
                    return null;
                }
            }

            Dictionary<string, object> assets = new Dictionary<string, object>();

            string large = _largeImage.Value;
            string largeText = _largeText.Value;
            if (largeText.Length > 0 && large.Length == 0)
            {
                if (focusInvalid)
                {
                    _largeImage.FocusField();
                }
                error = "Add a large image before setting its hover text.";
                return null;
            }
            if (large.Length > 0)
            {
                string normalized = NormalizeImage(large);
                if (normalized == null)
                {
                    if (focusInvalid)
                    {
                        _largeImage.FocusField();
                    }
                    error = "The large image must be an https URL or an asset key.";
                    return null;
                }
                assets["large_image"] = normalized;
                if (largeText.Length > 0)
                {
                    assets["large_text"] = largeText;
                }
            }

            string small = _smallImage.Value;
            string smallText = _smallText.Value;
            if (smallText.Length > 0 && small.Length == 0)
            {
                if (focusInvalid)
                {
                    _smallImage.FocusField();
                }
                error = "Add a small image before setting its hover text.";
                return null;
            }
            if (small.Length > 0)
            {
                string normalized = NormalizeImage(small);
                if (normalized == null)
                {
                    if (focusInvalid)
                    {
                        _smallImage.FocusField();
                    }
                    error = "The small image must be an https URL or an asset key.";
                    return null;
                }
                assets["small_image"] = normalized;
                if (smallText.Length > 0)
                {
                    assets["small_text"] = smallText;
                }
            }

            if (assets.Count > 0)
            {
                activity["assets"] = assets;
            }

            List<object> buttons = new List<object>();
            string buttonError = AddButton(buttons, _button1Label, _button1Url, "Button 1", focusInvalid);
            if (buttonError == null)
            {
                buttonError = AddButton(buttons, _button2Label, _button2Url, "Button 2", focusInvalid);
            }
            if (buttonError != null)
            {
                error = buttonError;
                return null;
            }
            if (streamUrl != null && buttons.Count < 2 && !ButtonContainsUrl(buttons, streamUrl))
            {
                Dictionary<string, object> watchButton = new Dictionary<string, object>();
                watchButton["label"] = "Watch stream";
                watchButton["url"] = streamUrl;
                buttons.Add(watchButton);
            }
            if (buttons.Count > 0)
            {
                activity["buttons"] = buttons;
            }

            // Discord reads start/end as epoch milliseconds. The clock starts
            // with the first publish, not while the fields are being edited.
            long elapsed = EnsureElapsedStart();
            if (elapsed > 0)
            {
                Dictionary<string, object> timestamps = new Dictionary<string, object>();
                timestamps["start"] = elapsed;
                activity["timestamps"] = timestamps;
            }

            return activity;
        }

        private static bool HasButtons(Dictionary<string, object> activity)
        {
            if (activity == null)
            {
                return false;
            }
            List<object> buttons = activity.ContainsKey("buttons")
                ? activity["buttons"] as List<object>
                : null;
            return buttons != null && buttons.Count > 0;
        }

        private static bool ButtonContainsUrl(List<object> buttons, string url)
        {
            foreach (object item in buttons)
            {
                Dictionary<string, object> button = item as Dictionary<string, object>;
                if (button != null && button.ContainsKey("url") &&
                    string.Equals(Convert.ToString(button["url"]), url, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private string AddButton(List<object> buttons, InputField labelField, InputField urlField,
            string name, bool focusInvalid)
        {
            string label = labelField.Value;
            string url = urlField.Value;
            if (label.Length == 0 && url.Length == 0)
            {
                return null;
            }
            if (label.Length == 0)
            {
                if (focusInvalid)
                {
                    labelField.FocusField();
                }
                return "Add a label for " + name + ".";
            }
            if (url.Length == 0)
            {
                if (focusInvalid)
                {
                    urlField.FocusField();
                }
                return "Add a URL for " + name + ".";
            }
            if (!IsHttpUrl(url))
            {
                if (focusInvalid)
                {
                    urlField.FocusField();
                }
                return "The " + name + " URL must start with http:// or https://.";
            }

            Dictionary<string, object> button = new Dictionary<string, object>();
            button["label"] = label;
            button["url"] = url;
            buttons.Add(button);
            return null;
        }

        private static string NormalizeImage(string value)
        {
            value = value.Trim();
            if (value.Length == 0)
            {
                return null;
            }
            if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return IsHttpUrl(value) ? value : null;
            }
            if (value.IndexOf("://", StringComparison.Ordinal) >= 0)
            {
                return null;
            }
            foreach (char c in value)
            {
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-'))
                {
                    return null;
                }
            }
            return value.ToLowerInvariant();
        }

        private static bool IsHttpUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri))
            {
                return false;
            }
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        private static bool IsValidAppId(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            value = value.Trim();
            if (value.Length < 15 || value.Length > 20)
            {
                return false;
            }
            ulong parsed;
            return ulong.TryParse(value, out parsed);
        }

        private AppSettings CollectSettings()
        {
            return new AppSettings
            {
                ApplicationId = _applicationId.Value,
                GameName = _gameName.Value,
                ActivityType = (int)_kind,
                Details = _details.Value,
                State = _state.Value,
                StreamUrl = _streamUrl.Value,
                LargeImage = _largeImage.Value,
                LargeText = _largeText.Value,
                SmallImage = _smallImage.Value,
                SmallText = _smallText.Value,
                Button1Label = _button1Label.Value,
                Button1Url = _button1Url.Value,
                Button2Label = _button2Label.Value,
                Button2Url = _button2Url.Value,
                Presets = _presets,
                MinimizeToTray = _settings.MinimizeToTray,
                LightMode = _settings.LightMode,
                AutoUpdate = _settings.AutoUpdate,
                ReduceMotion = _settings.ReduceMotion,
                PerformanceMode = _settings.PerformanceMode,
                ShowElapsed = _settings.ShowElapsed,
                SummonHotkey = _settings.SummonHotkey,
                ElapsedStart = _settings.ElapsedStart,
                LastSeenChangelog = _settings.LastSeenChangelog
            };
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            if (_updateButton != null)
            {
                _updateButton.IsEnabled = !busy;
            }
            if (_clearButton != null)
            {
                _clearButton.IsEnabled = !busy;
            }
            if (_presetBar != null)
            {
                _presetBar.IsEnabled = !busy;
            }
            Mouse.OverrideCursor = busy ? Cursors.Wait : null;
        }

        private void SetStatus(string text, Brush dotColor)
        {
            if (_statusLabel == null)
            {
                return;
            }
            _statusLabel.Text = text;
            _statusDot.Fill = dotColor;
        }

        private void ShowSnackbar(string message, SnackbarKind kind)
        {
            if (_closing || _snackbar == null || string.IsNullOrEmpty(message))
            {
                return;
            }
            _snackbar.Show(message, kind);
        }

        private void OnRpcConnectionChanged(bool connected)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (_closing)
                {
                    return;
                }
                if (connected)
                {
                    SetStatus("Connected", Theme.Success);
                }
                else
                {
                    _connectedAppId = string.Empty;
                    SetStatus("Discord offline", Theme.TextMuted);
                    if (_busy)
                    {
                        SetBusy(false);
                    }
                }
            }));
        }

        private void OnRpcNotice(string message)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                ShowSnackbar(message, SnackbarKind.Error);
            }));
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            if (!_exiting && _settings.MinimizeToTray && !_quitRequested)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }

            if (!_exiting && !_closeAnimationStarted && !_settings.ReduceMotion)
            {
                e.Cancel = true;
                BeginCloseAnimation();
                return;
            }

            _exiting = true;
            _closing = true;
            DisposeTrayIcon();
            ReleaseHotkey();
            _reconnectTimer.Stop();
            if (_minimizeTimer != null)
            {
                _minimizeTimer.Stop();
            }
            if (_autoUpdateTimer != null)
            {
                _autoUpdateTimer.Stop();
            }
            if (_previewTimer != null)
            {
                _previewTimer.Stop();
            }
            SettingsStore.Save(CollectSettings());
            try
            {
                _rpc.Dispose();
            }
            catch
            {
                // Shutting down; nothing useful to do with a broken pipe.
            }
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            ApplySystemTheme(handle);
            ApplyHotkey();

            HwndSource source = HwndSource.FromHwnd(handle);
            if (source != null)
            {
                source.AddHook(OnWindowMessage);
            }

            Dispatcher.BeginInvoke(new Action(ApplyWindowRegion), DispatcherPriority.Loaded);
        }

        /// <summary>Keeps DWM title bars and system menus in step with the app palette.</summary>
        private void ApplySystemTheme(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                handle = new WindowInteropHelper(this).Handle;
            }
            if (handle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                int dark = Theme.Current == Theme.Appearance.Dark ? 1 : 0;
                DwmSetWindowAttribute(handle, 20, ref dark, 4);
                int rounded = 2;
                DwmSetWindowAttribute(handle, 33, ref rounded, 4);
            }
            catch
            {
                // Styling hints only; older Windows versions can ignore them.
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom,
            int width, int height);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, int modifiers, int key);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    }
}
