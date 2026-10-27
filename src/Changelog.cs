using System;

namespace DiscordPresence
{
    /// <summary>One release's short, user-facing change list.</summary>
    public sealed class ChangelogEntry
    {
        public ChangelogEntry(string version, string released, params string[] notes)
        {
            Version = version;
            Released = released;
            Notes = notes ?? new string[0];
        }

        public string Version { get; private set; }
        public string Released { get; private set; }
        public string[] Notes { get; private set; }
    }

    /// <summary>
    /// The changelog ships inside the exe. Add an entry at the top for every
    /// release, and bump the assembly version in Program.cs to match it.
    /// </summary>
    public static class Changelog
    {
        public static readonly ChangelogEntry[] Entries = new[]
        {
            new ChangelogEntry("1.1.7", "October 26, 2026",
                "Header and activity glyphs now use the requested emoji icons."),
            new ChangelogEntry("1.1.6", "October 26, 2026",
                "The moon/sun toggle now blooms with a soft pulse when appearance changes."),
            new ChangelogEntry("1.1.5", "October 26, 2026",
                "The Live preview card now matches the Activity card height.",
                "Appearance moved to a glowing moon/sun toggle in the header.",
                "The Changelog badge now counts unseen releases."),
            new ChangelogEntry("1.1.4", "October 26, 2026",
                "Added the Changelog board beside Settings, with an unread badge for new releases.",
                "The Artwork card now lines up with the Buttons card."),
            new ChangelogEntry("1.1.3", "October 26, 2026",
                "Moved the Application ID into Settings \u2192 Connection.",
                "Restored the live preview beside the editor and removed the live icon."),
            new ChangelogEntry("1.1.2", "October 25, 2026",
                "The build date is stamped beside the version in Settings."),
            new ChangelogEntry("1.1.1", "October 25, 2026",
                "Live preview moved into a header dialog.",
                "Version number added to Settings.")
        };

        public static ChangelogEntry Latest
        {
            get { return Entries[0]; }
        }

        /// <summary>
        /// How many releases are newer than the saved version. Comparing
        /// versions means rebuilding the same release does not re-raise the
        /// badge.
        /// </summary>
        public static int UnseenCount(string lastSeen)
        {
            if (string.IsNullOrEmpty(lastSeen))
            {
                return Entries.Length;
            }

            string seenText = lastSeen.Trim();
            System.Version seen;
            if (!System.Version.TryParse(seenText, out seen))
            {
                return string.Equals(seenText, Latest.Version, StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : Entries.Length;
            }

            int count = 0;
            foreach (ChangelogEntry entry in Entries)
            {
                System.Version released;
                if (System.Version.TryParse(entry.Version, out released) && released > seen)
                {
                    count++;
                }
            }
            return count;
        }

        public static bool HasUnseen(string lastSeen)
        {
            return UnseenCount(lastSeen) > 0;
        }
    }
}
