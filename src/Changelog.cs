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
            new ChangelogEntry("1.5.0", "October 29, 2026",
                "Beta: PlayStation mode \u2014 recreate Discord's console game card with a console header, an inline running time and a Trending state.",
                "Beta: a Rocket League example preset ships with the app, so the console card from the screenshot is one click away.",
                "The live preview switches between the desktop card and the PlayStation card, complete with the PS mark and the green play clock.",
                "This build is marked BETA in the title bar, the About section and on GitHub."),
            new ChangelogEntry("1.4.0", "October 27, 2026",
                "Appearance moved out of the header and into Settings \u2192 Appearance, as a Dark row and a Light row.",
                "Beta: Custom theme \u2014 pick your own surface, accent and text colours from a swatch palette, a hex code or one slider per channel.",
                "The accent colour now reaches everything the theme paints: primary buttons, focus rings, carets, text selections, switch knobs and the ambient glow.",
                "Custom colours are saved in the portable settings file, so the app reopens in exactly the theme you left it in."),
            new ChangelogEntry("1.3.0", "October 27, 2026",
                "Beta: Idle pause \u2014 five minutes without input clears the presence, and it returns within seconds of your next keyboard or mouse input.",
                "Beta: Remember window size \u2014 the app reopens at the size and position it had when you last closed it.",
                "Beta: Start with Windows \u2014 one switch adds the app to your Windows startup apps, so it is waiting in the tray after you sign in.",
                "The About section now reports the runtime, your Windows build, the live Discord and presence state, the folders the app uses, and links to the project.",
                "The settings pane keeps one height, so a long section scrolls inside itself instead of stretching the dialog."),
            new ChangelogEntry("1.2.2", "October 27, 2026",
                "The Changelog board opens on the newest release only, with an Older updates button that reveals the archive in place.",
                "Settings is now a two-pane window like Discord's: a section rail on the left, one short section at a time on the right.",
                "Version and build date moved into the new About section, and both boards got roomier spacing."),
            new ChangelogEntry("1.2.1", "October 27, 2026",
                "The header buttons dropped their emoji: Changelog, Dark/Light and Settings are now flat, minimal pills.",
                "The unseen-release count now sits inside the Changelog pill, right after its label.",
                "Switching appearance flashes the pill with the accent colour instead of glowing a moon or sun.",
                "The Changelog board now fits inside the window and opens on its newest entry."),
            new ChangelogEntry("1.2.0", "October 27, 2026",
                "Beta: Performance mode \u2014 flat surfaces instead of blur effects, preview redraws batched to a single frame, and auto-updates three times sooner.",
                "Beta: Elapsed timer \u2014 Discord counts how long the presence has been up, with a Restart timer action.",
                "Beta: Summon hotkey \u2014 Ctrl+Alt+D hides the window to the tray or brings it back from anywhere in Windows.",
                "The header controls are now Discord-style pills beside the connection status, each with a label.",
                "Beta features live in their own Settings section and carry a BETA badge."),
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
