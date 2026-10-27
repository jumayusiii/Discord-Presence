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
        public bool AutoUpdate { get; set; }
        public bool ReduceMotion { get; set; }
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
                AutoUpdate = AutoUpdate,
                ReduceMotion = ReduceMotion,
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
