using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace Sprocket
{
    internal sealed class UpdateInfo
    {
        public Version Version;
        public string TagName;
        public string HtmlUrl;
    }

    /// <summary>Checks GitHub's public Releases API for a newer tagged version. Never blocks the
    /// UI thread and fails silently on any error (offline jobsite, GitHub down, rate-limited, no
    /// releases yet) — this is a nice-to-have notice, never something that should interrupt the
    /// app. Only notifies; the user has to click through and run the installer themselves, same
    /// as any other Windows update — Sprocket never downloads or installs anything on its own.</summary>
    internal static class UpdateChecker
    {
        private const string ReleasesUrl =
            "https://api.github.com/repos/BeardedControlsGuy/sprocket/releases/latest";

        public static void CheckAsync(Action<UpdateInfo> onNewerFound)
        {
            Thread t = new Thread(delegate()
            {
                UpdateInfo info = TryFetch();
                if (info != null && info.Version > AppVersion.Current)
                    onNewerFound(info);
            });
            t.IsBackground = true;
            t.Start();
        }

        private static UpdateInfo TryFetch()
        {
            try
            {
                // GitHub's API has required TLS 1.2+ since 2022. .NET Framework's default
                // SecurityProtocol depends on machine-level config (registry "SchUseStrongCrypto"
                // etc) that isn't consistent across Windows installs — without this, the TLS
                // handshake can fail silently on some machines and never on others, and the
                // catch-all below (by design, so a flaky connection never interrupts the app)
                // would swallow it with zero indication why the update notice never appears.
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(ReleasesUrl);
                req.UserAgent = "Sprocket/" + AppVersion.Display;
                req.Accept = "application/vnd.github+json";
                req.Timeout = 8000;

                string json;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                    json = reader.ReadToEnd();

                string tag = ExtractField(json, "tag_name");
                string htmlUrl = ExtractField(json, "html_url");
                if (tag == null) return null;

                Version version = ParseVersion(tag);
                if (version == null) return null;

                UpdateInfo info = new UpdateInfo();
                info.Version = version;
                info.TagName = tag;
                info.HtmlUrl = htmlUrl;
                return info;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Pulls one top-level string field out of the response — avoids pulling in a
        /// JSON library for the two fields (tag_name, html_url) this actually needs.</summary>
        private static string ExtractField(string json, string field)
        {
            Match m = Regex.Match(json, "\"" + field + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static Version ParseVersion(string tag)
        {
            string s = tag.Trim();
            if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V')) s = s.Substring(1);

            Match m = Regex.Match(s, @"^(\d+)\.(\d+)\.(\d+)");
            if (!m.Success) return null;
            return new Version(
                int.Parse(m.Groups[1].Value),
                int.Parse(m.Groups[2].Value),
                int.Parse(m.Groups[3].Value));
        }
    }
}
