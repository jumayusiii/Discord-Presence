using System;
using System.Reflection;
using Microsoft.Win32;

namespace DiscordPresence
{
    /// <summary>
    /// Machine details the About section reports. Everything here is read-only
    /// and cheap, so the dialog can ask for it every time it opens.
    /// </summary>
    public static class SystemFacts
    {
        private const string WindowsKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        /// <summary>"Windows 11 (24H2, build 26100)" and friends.</summary>
        public static string WindowsVersion()
        {
            Version version = Environment.OSVersion.Version;
            string name = version.Major >= 10 && version.Build >= 22000
                ? "Windows 11"
                : version.Major >= 10 ? "Windows 10" : "Windows";

            string release = ReadWindowsValue("DisplayVersion");
            string build = version.Build.ToString();
            if (version.Revision > 0)
            {
                build += "." + version.Revision;
            }
            return release.Length > 0
                ? name + " (" + release + ", build " + build + ")"
                : name + " (build " + build + ")";
        }

        /// <summary>The CLR this exe is running on, plus its bitness.</summary>
        public static string Runtime()
        {
            return "CLR " + Environment.Version + " (" +
                (IntPtr.Size == 8 ? "64-bit" : "32-bit") + ")";
        }

        /// <summary>Full path of the running exe; the portable folder when nothing moved it.</summary>
        public static string ExecutablePath()
        {
            try
            {
                Assembly assembly = Assembly.GetEntryAssembly();
                if (assembly != null && assembly.Location.Length > 0)
                {
                    return assembly.Location;
                }
            }
            catch
            {
                // Fall through to the app base directory below.
            }
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DiscordPresence.exe");
        }

        private static string ReadWindowsValue(string value)
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(WindowsKey))
                {
                    if (key != null)
                    {
                        object data = key.GetValue(value);
                        if (data != null)
                        {
                            return Convert.ToString(data).Trim();
                        }
                    }
                }
            }
            catch
            {
                // An unreadable key just means fewer details on screen.
            }
            return string.Empty;
        }
    }

    /// <summary>
    /// The "start with Windows" switch, kept in the per-user Run key so the app
    /// needs no installer and no administrator rights.
    /// </summary>
    public static class StartupEntry
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Discord Presence";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    if (key == null)
                    {
                        return false;
                    }
                    string value = key.GetValue(ValueName) as string;
                    if (string.IsNullOrEmpty(value))
                    {
                        return false;
                    }
                    // A stale entry from a moved copy still counts as "on": the
                    // next launch rewrites it with the current path.
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Adds or removes the entry. Returns false with a reason when Windows
        /// refuses the write, so the switch can bounce back honestly.
        /// </summary>
        public static bool TrySet(bool enabled, out string error)
        {
            error = null;
            try
            {
                if (!enabled)
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                    {
                        if (key != null)
                        {
                            key.DeleteValue(ValueName, false);
                        }
                    }
                    return true;
                }

                string path = SystemFacts.ExecutablePath();
                if (path.Length == 0 || path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    error = "The app is running from memory, so Windows has no path to start it again.";
                    return false;
                }

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null)
                    {
                        error = "Windows would not open its startup list. Try again as this user.";
                        return false;
                    }
                    key.SetValue(ValueName, "\"" + path + "\"");
                }
                return true;
            }
            catch (Exception ex)
            {
                error = "Windows refused the startup entry: " + ex.Message;
                return false;
            }
        }
    }
}
