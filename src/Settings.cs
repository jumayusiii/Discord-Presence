using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace DiscordPresence
{
    /// <summary>A reusable, named snapshot of the presence fields.</summary>
    public sealed class PresencePreset
    {
        public string Name { get; set; }
        public string GameName { get; set; }
        public int ActivityType { get; set; }
        public string Details { get; set; }
        public string State { get; set; }
        public string StreamUrl { get; set; }
        public string LargeImage { get; set; }
        public string LargeText { get; set; }
        public string SmallImage { get; set; }
        public string SmallText { get; set; }
        public string Button1Label { get; set; }
        public string Button1Url { get; set; }
        public string Button2Label { get; set; }
        public string Button2Url { get; set; }

        public PresencePreset()
        {
            Name = string.Empty;
            GameName = string.Empty;
            Details = string.Empty;
            State = string.Empty;
            StreamUrl = string.Empty;
            LargeImage = string.Empty;
            LargeText = string.Empty;
            SmallImage = string.Empty;
            SmallText = string.Empty;
            Button1Label = string.Empty;
            Button1Url = string.Empty;
            Button2Label = string.Empty;
            Button2Url = string.Empty;
        }

        public PresencePreset Copy()
        {
            return new PresencePreset
            {
                Name = Name,
                GameName = GameName,
                ActivityType = ActivityType,
                Details = Details,
                State = State,
                StreamUrl = StreamUrl,
                LargeImage = LargeImage,
                LargeText = LargeText,
                SmallImage = SmallImage,
                SmallText = SmallText,
                Button1Label = Button1Label,
                Button1Url = Button1Url,
                Button2Label = Button2Label,
                Button2Url = Button2Url
            };
        }

        public void Normalize()
        {
            Name = Cut(Name, 40);
            GameName = Cut(GameName, 128);
            Details = Cut(Details, 128);
            State = Cut(State, 128);
            StreamUrl = Cut(StreamUrl, 2048);
            LargeImage = Cut(LargeImage, 2048);
            SmallImage = Cut(SmallImage, 2048);
            LargeText = Cut(LargeText, 128);
            SmallText = Cut(SmallText, 128);
            Button1Label = Cut(Button1Label, 32);
            Button2Label = Cut(Button2Label, 32);
            Button1Url = Cut(Button1Url, 2048);
            Button2Url = Cut(Button2Url, 2048);
            if (ActivityType < 0 || ActivityType > 3)
            {
                ActivityType = 0;
            }
        }

        private static string Cut(string value, int max)
        {
            if (value == null)
            {
                return string.Empty;
            }
            value = value.Trim();
            return value.Length > max ? value.Substring(0, max) : value;
        }
    }

    /// <summary>Everything the editor remembers between launches.</summary>
    public sealed class AppSettings
    {
        public string ApplicationId { get; set; }
        public string GameName { get; set; }
        public int ActivityType { get; set; }
        public string Details { get; set; }
        public string State { get; set; }
        public string StreamUrl { get; set; }
        public string LargeImage { get; set; }
        public string LargeText { get; set; }
        public string SmallImage { get; set; }
        public string SmallText { get; set; }
        public string Button1Label { get; set; }
        public string Button1Url { get; set; }
        public string Button2Label { get; set; }
        public string Button2Url { get; set; }
        public List<PresencePreset> Presets { get; set; }
        public bool MinimizeToTray { get; set; }
        public bool LightMode { get; set; }
        public bool CustomTheme { get; set; }
        public string ThemeSurface { get; set; }
        public string ThemeAccent { get; set; }
        public string ThemeText { get; set; }
        public bool AutoUpdate { get; set; }
        public bool ReduceMotion { get; set; }
        public bool PerformanceMode { get; set; }
        public bool ShowElapsed { get; set; }
        public bool SummonHotkey { get; set; }
        public bool IdlePause { get; set; }
        public bool RememberWindow { get; set; }
        public bool StartWithWindows { get; set; }
        public double WindowLeft { get; set; }
        public double WindowTop { get; set; }
        public double WindowWidth { get; set; }
        public double WindowHeight { get; set; }
        public long ElapsedStart { get; set; }
        public string LastSeenChangelog { get; set; }

        public AppSettings()
        {
            ApplicationId = string.Empty;
            GameName = string.Empty;
            Details = string.Empty;
            State = string.Empty;
            StreamUrl = string.Empty;
            LargeImage = string.Empty;
            LargeText = string.Empty;
            SmallImage = string.Empty;
            SmallText = string.Empty;
            Button1Label = string.Empty;
            Button1Url = string.Empty;
            Button2Label = string.Empty;
            Button2Url = string.Empty;
            LastSeenChangelog = string.Empty;
            ThemeSurface = string.Empty;
            ThemeAccent = string.Empty;
            ThemeText = string.Empty;
            Presets = new List<PresencePreset>();
        }

        public AppSettings Copy()
        {
            AppSettings copy = new AppSettings
            {
                ApplicationId = ApplicationId,
                GameName = GameName,
                ActivityType = ActivityType,
                Details = Details,
                State = State,
                StreamUrl = StreamUrl,
                LargeImage = LargeImage,
                LargeText = LargeText,
                SmallImage = SmallImage,
                SmallText = SmallText,
                Button1Label = Button1Label,
                Button1Url = Button1Url,
                Button2Label = Button2Label,
                Button2Url = Button2Url,
                MinimizeToTray = MinimizeToTray,
                LightMode = LightMode,
                CustomTheme = CustomTheme,
                ThemeSurface = ThemeSurface,
                ThemeAccent = ThemeAccent,
                ThemeText = ThemeText,
                AutoUpdate = AutoUpdate,
                ReduceMotion = ReduceMotion,
                PerformanceMode = PerformanceMode,
                ShowElapsed = ShowElapsed,
                SummonHotkey = SummonHotkey,
                IdlePause = IdlePause,
                RememberWindow = RememberWindow,
                StartWithWindows = StartWithWindows,
                WindowLeft = WindowLeft,
                WindowTop = WindowTop,
                WindowWidth = WindowWidth,
                WindowHeight = WindowHeight,
                ElapsedStart = ElapsedStart,
                LastSeenChangelog = LastSeenChangelog
            };
            foreach (PresencePreset preset in Presets)
            {
                copy.Presets.Add(preset.Copy());
            }
            return copy;
        }

        public void Normalize()
        {
            ApplicationId = Cut(ApplicationId, 32);
            GameName = Cut(GameName, 128);
            Details = Cut(Details, 128);
            State = Cut(State, 128);
            StreamUrl = Cut(StreamUrl, 2048);
            LargeImage = Cut(LargeImage, 2048);
            SmallImage = Cut(SmallImage, 2048);
            LargeText = Cut(LargeText, 128);
            SmallText = Cut(SmallText, 128);
            Button1Label = Cut(Button1Label, 32);
            Button2Label = Cut(Button2Label, 32);
            Button1Url = Cut(Button1Url, 2048);
            Button2Url = Cut(Button2Url, 2048);
            LastSeenChangelog = Cut(LastSeenChangelog, 32);
            if (ElapsedStart < 0)
            {
                ElapsedStart = 0;
            }

            // The three custom colours are either all usable or all dropped, so
            // a half-written set can never mix one custom tone with two stock
            // ones, and an empty set reads as "never customised".
            ThemeSurface = NormalizeColor(ThemeSurface);
            ThemeAccent = NormalizeColor(ThemeAccent);
            ThemeText = NormalizeColor(ThemeText);
            if (string.IsNullOrEmpty(ThemeSurface) || string.IsNullOrEmpty(ThemeAccent) ||
                string.IsNullOrEmpty(ThemeText))
            {
                ThemeSurface = string.Empty;
                ThemeAccent = string.Empty;
                ThemeText = string.Empty;
                CustomTheme = false;
            }

            // Zero means "no saved geometry", so an unset or nonsense box
            // never places the window somewhere the user cannot reach it.
            if (WindowWidth < 320 || WindowWidth > 20000 || double.IsNaN(WindowWidth))
            {
                WindowWidth = 0;
            }
            if (WindowHeight < 240 || WindowHeight > 20000 || double.IsNaN(WindowHeight))
            {
                WindowHeight = 0;
            }
            if (double.IsNaN(WindowLeft) || double.IsNaN(WindowTop) ||
                Math.Abs(WindowLeft) > 100000 || Math.Abs(WindowTop) > 100000)
            {
                WindowLeft = 0;
                WindowTop = 0;
            }
            if (WindowWidth == 0 || WindowHeight == 0)
            {
                WindowLeft = 0;
                WindowTop = 0;
            }
            if (ActivityType < 0 || ActivityType > 3)
            {
                ActivityType = 0;
            }

            if (Presets == null)
            {
                Presets = new List<PresencePreset>();
            }
            else
            {
                List<PresencePreset> cleaned = new List<PresencePreset>();
                HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (PresencePreset preset in Presets)
                {
                    if (preset == null)
                    {
                        continue;
                    }
                    preset.Normalize();
                    if (preset.Name.Length == 0 || !names.Add(preset.Name))
                    {
                        continue;
                    }
                    cleaned.Add(preset);
                }
                Presets = cleaned;
            }
        }

        private static string Cut(string value, int max)
        {
            if (value == null)
            {
                return string.Empty;
            }
            value = value.Trim();
            return value.Length > max ? value.Substring(0, max) : value;
        }

        /// <summary>Keeps a #RRGGBB colour and drops anything the picker cannot use.</summary>
        private static string NormalizeColor(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            string text = value.Trim().TrimStart('#').ToUpperInvariant();
            if (text.Length != 6)
            {
                return string.Empty;
            }
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');
                if (!hex)
                {
                    return string.Empty;
                }
            }
            return "#" + text;
        }
    }

    /// <summary>
    /// Stores settings next to the executable so the app stays portable.
    /// Falls back to LocalAppData only when the executable folder is read-only.
    /// </summary>
    public static class SettingsStore
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        public static string PortablePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "discord-presence.json"); }
        }

        public static string FallbackPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DiscordPresence");
                return Path.Combine(dir, "discord-presence.json");
            }
        }

        /// <summary>
        /// The file the app is really reading: the portable one when it exists,
        /// otherwise the fallback that the next save would write to.
        /// </summary>
        public static string ActivePath()
        {
            if (File.Exists(FallbackPath) && !File.Exists(PortablePath))
            {
                return FallbackPath;
            }
            return PortablePath;
        }

        public static AppSettings Load()
        {
            AppSettings loaded = TryLoad(PortablePath) ?? TryLoad(FallbackPath);
            if (loaded == null)
            {
                return new AppSettings();
            }
            loaded.Normalize();
            return loaded;
        }

        /// <summary>Returns the path written to, or null if no file could be written.</summary>
        public static string Save(AppSettings settings)
        {
            settings.Normalize();
            string json;
            try
            {
                json = Json.Serialize(settings);
            }
            catch
            {
                return null;
            }

            try
            {
                File.WriteAllText(PortablePath, json, Utf8);
                return PortablePath;
            }
            catch
            {
                try
                {
                    string dir = Path.GetDirectoryName(FallbackPath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.WriteAllText(FallbackPath, json, Utf8);
                    return FallbackPath;
                }
                catch
                {
                    return null;
                }
            }
        }

        private static AppSettings TryLoad(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }
                return Json.Deserialize<AppSettings>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
