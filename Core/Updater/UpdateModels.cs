using System;
using System.Collections.Generic;

#pragma warning disable CS0649 // fields are filled by JsonUtility

namespace AdexansCastingMod.Core.Updater
{
    internal static class UpdateConfig
    {
        // The ONLY place updates are downloaded from. Change this to your own GitHub "owner/repo".
        // The repository must be public. Publish releases with release.ps1 (it uploads the DLL and its
        // SHA-256 file); every player's mod then picks the new version up on its own.
        public const string Repository = "AdexanVR/AdexansCastingMod";

        public const string AssemblyName = "AdexansCastingMod";
        public const string DllAsset = "AdexansCastingMod.dll";
        public const string ChecksumAsset = "AdexansCastingMod.dll.sha256";

        public static string LatestReleaseApi => "https://api.github.com/repos/" + Repository + "/releases/latest";
        public static string DownloadPrefix => "https://github.com/" + Repository + "/releases/download/";
        public static string ReleasesPage => "https://github.com/" + Repository + "/releases";
    }

    // Shapes of the GitHub REST API response (only the fields used here)
    [Serializable]
    internal class GitHubRelease
    {
        public string tag_name;
        public string name;
        public string body;
        public string html_url;
        public string published_at;
        public bool draft;
        public bool prerelease;
        public GitHubAsset[] assets;
    }

    [Serializable]
    internal class GitHubAsset
    {
        public string name;
        public string browser_download_url;
        public long size;
        public string digest; // "sha256:<hex>" on newer GitHub releases
    }

    [Serializable]
    internal class UpdateHistoryEntry
    {
        public string date;
        public string fromVersion;
        public string toVersion;
        public string result;
        public string detail;
    }

    // Persisted to BepInEx/config/AdexansCastingMod/updater.json (never touched by an update itself)
    [Serializable]
    internal class UpdaterState
    {
        public string lastCheck = "";
        public string latestVersion = "";
        public string latestName = "";
        public string latestNotes = "";
        public string latestUrl = "";

        // An update downloaded and verified, waiting to be swapped in when the game closes
        public string pendingVersion = "";
        public string pendingFile = "";
        public string pendingSha = "";
        public string pendingFrom = "";

        public List<UpdateHistoryEntry> history = new List<UpdateHistoryEntry>();

        public void Sanitize()
        {
            if (history == null) history = new List<UpdateHistoryEntry>();
            history.RemoveAll(h => h == null);
            while (history.Count > 30) history.RemoveAt(0);
            lastCheck = lastCheck ?? "";
            latestVersion = latestVersion ?? "";
            latestName = latestName ?? "";
            latestNotes = latestNotes ?? "";
            latestUrl = latestUrl ?? "";
            pendingVersion = pendingVersion ?? "";
            pendingFile = pendingFile ?? "";
            pendingSha = pendingSha ?? "";
            pendingFrom = pendingFrom ?? "";
        }
    }
}
