using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AdexansCastingMod.Config;
using BepInEx;
using UnityEngine;
using UnityEngine.Networking;

namespace AdexansCastingMod.Core.Updater
{
    internal enum UpdateStatus
    {
        Idle,
        Checking,
        UpToDate,
        Available,     // found, waiting for the user to press Install (ask mode)
        Downloading,
        Verifying,
        Installing,
        RestartToFinish, // new DLL is in place; it loads next time the game starts
        InstallOnExit,   // DLL is locked; a helper swaps it right after the game closes
        Failed,
    }

    // Checks GitHub Releases, downloads the new DLL, verifies it (size, SHA-256, assembly name/version)
    // and swaps it in. The working DLL is never deleted before the new one is verified: it is renamed
    // and copied back if anything goes wrong, and a copy is kept in BepInEx/cache for rollbacks.
    internal sealed class AutoUpdater
    {
        static string WorkFolder => Path.Combine(Paths.CachePath, "AdexansCastingMod", "update");
        static string BackupFolder => Path.Combine(Paths.CachePath, "AdexansCastingMod", "backup");
        static string HelperLog => Path.Combine(WorkFolder, "apply_log.txt");
        static string StatePath => Path.Combine(Storage.Root, "updater.json");

        readonly CastingRuntime rt;
        readonly UpdaterState state;
        GitHubRelease latest;
        Version latestVersion;
        string expectedSha;

        public UpdateStatus Status { get; private set; } = UpdateStatus.Idle;
        public string Message { get; private set; } = "";
        public float Progress { get; private set; }
        public bool Busy { get; private set; }
        public UpdaterState State => state;

        public static Version Installed => ParseVersion(PluginInfo.Version);
        public string LatestVersion => latestVersion != null ? latestVersion.ToString(3) : state.latestVersion;
        public string ReleaseNotes => latest != null ? latest.body : state.latestNotes;
        public bool UpdateKnown => latestVersion != null && latestVersion > Installed;

        public AutoUpdater(CastingRuntime rt)
        {
            this.rt = rt;
            state = Storage.Load(StatePath, () => new UpdaterState());
            state.Sanitize();
            FinishPreviousUpdate();
        }

        // ------------------------------------------------------------ Startup bookkeeping

        // Runs once at startup: records the result of an update applied while the game was closed,
        // removes leftovers and clears a pending update that is now installed
        void FinishPreviousUpdate()
        {
            try
            {
                string target = Plugin.PluginPath;
                if (!string.IsNullOrEmpty(target))
                {
                    foreach (string leftover in new[] { target + ".old", target + ".new" })
                        if (File.Exists(leftover)) TryDelete(leftover);
                }

                if (File.Exists(HelperLog))
                {
                    string[] lines = File.ReadAllLines(HelperLog);
                    string last = lines.Length > 0 ? lines[lines.Length - 1] : "";
                    if (last.Contains("FAILED")) AddHistory(state.pendingFrom, state.pendingVersion, "Failed", last.Substring(last.IndexOf("FAILED", StringComparison.Ordinal)));
                    TryDelete(HelperLog);
                }

                if (state.pendingVersion.Length > 0)
                {
                    Version pending = ParseVersion(state.pendingVersion);
                    if (pending != null && Installed >= pending)
                    {
                        AddHistory(state.pendingFrom, state.pendingVersion, "Installed", "Applied after restart");
                        ClearPending();
                    }
                }
                else if (state.history.Count > 0)
                {
                    // A live swap last session: confirm it once the new version actually runs
                    UpdateHistoryEntry lastEntry = state.history[state.history.Count - 1];
                    if (lastEntry.result == "Ready" && lastEntry.toVersion == PluginInfo.Version)
                        lastEntry.result = "Installed";
                }
                Save();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Updater startup: " + e.Message);
            }
        }

        // ------------------------------------------------------------ Check

        public void CheckNow(bool userInitiated)
        {
            if (Busy)
            {
                if (userInitiated) rt.Toasts.Show("The updater is already working");
                return;
            }
            rt.StartCoroutine(CheckRoutine(userInitiated));
        }

        IEnumerator CheckRoutine(bool userInitiated)
        {
            Busy = true;
            SetStatus(UpdateStatus.Checking, "Checking GitHub for a new version…");

            using (UnityWebRequest req = UnityWebRequest.Get(UpdateConfig.LatestReleaseApi))
            {
                req.SetRequestHeader("User-Agent", "AdexansCastingMod-Updater");
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                req.timeout = 20;
                yield return req.SendWebRequest();

                state.lastCheck = Now();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    string why = req.responseCode == 404 ? "no releases published yet" : req.error;
                    Fail("Couldn't reach GitHub: " + why, record: false);
                    Busy = false;
                    Save();
                    yield break;
                }

                try
                {
                    latest = JsonUtility.FromJson<GitHubRelease>(req.downloadHandler.text);
                }
                catch (Exception e)
                {
                    latest = null;
                    Fail("GitHub sent an unreadable answer (" + e.Message + ")", record: false);
                    Busy = false;
                    yield break;
                }
            }

            latestVersion = latest != null && !latest.draft && !latest.prerelease ? ParseVersion(latest.tag_name) : null;
            if (latestVersion == null)
            {
                Fail("The latest release has no version tag like v1.2.0", record: false);
                Busy = false;
                yield break;
            }

            state.latestVersion = latestVersion.ToString(3);
            state.latestName = latest.name ?? "";
            state.latestNotes = latest.body ?? "";
            state.latestUrl = latest.html_url ?? "";
            Save();

            if (latestVersion <= Installed)
            {
                SetStatus(UpdateStatus.UpToDate, "You're on the newest version");
                Busy = false;
                yield break;
            }

            if (state.pendingVersion == state.latestVersion && File.Exists(state.pendingFile))
            {
                SetStatus(UpdateStatus.InstallOnExit, "v" + state.pendingVersion + " installs when you close the game");
                Busy = false;
                yield break;
            }

            Busy = false;
            if (rt.Settings.updateInstallMode == UpdateInstallMode.Automatic)
            {
                InstallLatest();
            }
            else
            {
                SetStatus(UpdateStatus.Available, "Version " + LatestVersion + " is available");
                rt.Toasts.Show("Update " + LatestVersion + " available (Settings > Updates)", UI.Theme.Accent);
            }
        }

        // ------------------------------------------------------------ Download + verify + install

        public void InstallLatest()
        {
            if (Busy || latest == null || !UpdateKnown) return;
            rt.StartCoroutine(DownloadRoutine());
        }

        IEnumerator DownloadRoutine()
        {
            Busy = true;
            GitHubAsset dll = FindAsset(UpdateConfig.DllAsset);
            GitHubAsset sum = FindAsset(UpdateConfig.ChecksumAsset);
            if (dll == null)
            {
                Fail("The release has no " + UpdateConfig.DllAsset + " file");
                Busy = false;
                yield break;
            }
            if (!IsOfficial(dll) || (sum != null && !IsOfficial(sum)))
            {
                Fail("Refused: the download link isn't from " + UpdateConfig.Repository);
                Busy = false;
                yield break;
            }

            // Expected hash: the .sha256 file; GitHub's own digest must agree when present
            expectedSha = null;
            if (sum != null)
            {
                using (UnityWebRequest req = UnityWebRequest.Get(sum.browser_download_url))
                {
                    req.SetRequestHeader("User-Agent", "AdexansCastingMod-Updater");
                    req.timeout = 20;
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success) expectedSha = ParseSha(req.downloadHandler.text);
                }
            }
            string digest = dll.digest != null && dll.digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? dll.digest.Substring(7).ToLowerInvariant() : null;
            if (expectedSha == null) expectedSha = digest;
            if (expectedSha == null)
            {
                Fail("The release has no SHA-256 checksum, so it can't be verified");
                Busy = false;
                yield break;
            }
            if (digest != null && digest != expectedSha)
            {
                Fail("Checksums in the release don't match each other");
                Busy = false;
                yield break;
            }

            Directory.CreateDirectory(WorkFolder);
            string version = latestVersion.ToString(3);
            string part = Path.Combine(WorkFolder, "AdexansCastingMod-" + version + ".download");
            string staged = Path.Combine(WorkFolder, "AdexansCastingMod-" + version + ".dll");
            TryDelete(part);

            SetStatus(UpdateStatus.Downloading, "Downloading " + version + "…");
            using (UnityWebRequest req = new UnityWebRequest(dll.browser_download_url, UnityWebRequest.kHttpVerbGET))
            {
                req.downloadHandler = new DownloadHandlerFile(part) { removeFileOnAbort = true };
                req.SetRequestHeader("User-Agent", "AdexansCastingMod-Updater");
                req.timeout = 180;
                UnityWebRequestAsyncOperation op = req.SendWebRequest();
                while (!op.isDone)
                {
                    Progress = req.downloadProgress;
                    yield return null;
                }
                Progress = 1f;
                if (req.result != UnityWebRequest.Result.Success)
                {
                    TryDelete(part);
                    Fail("Download failed: " + req.error);
                    Busy = false;
                    yield break;
                }
            }

            SetStatus(UpdateStatus.Verifying, "Verifying the download…");
            yield return null;
            string problem = Verify(part, dll.size, expectedSha, latestVersion);
            if (problem != null)
            {
                TryDelete(part);
                Fail(problem);
                Busy = false;
                yield break;
            }
            TryDelete(staged);
            File.Move(part, staged);

            SetStatus(UpdateStatus.Installing, "Installing…");
            yield return null;
            Install(staged, expectedSha, version);
            Busy = false;
        }

        static string Verify(string file, long expectedSize, string sha, Version version)
        {
            try
            {
                var info = new FileInfo(file);
                if (!info.Exists || info.Length == 0) return "The download is empty";
                if (expectedSize > 0 && info.Length != expectedSize) return "The download is incomplete (" + info.Length + " of " + expectedSize + " bytes)";
                if (!string.Equals(Sha256(file), sha, StringComparison.OrdinalIgnoreCase)) return "Checksum mismatch: the file is damaged or was tampered with";

                // Reads metadata only; the assembly is not loaded
                AssemblyName name = AssemblyName.GetAssemblyName(file);
                if (name.Name != UpdateConfig.AssemblyName) return "The downloaded file isn't this mod (" + name.Name + ")";
                if (version != null && (name.Version == null || name.Version.Major != version.Major || name.Version.Minor != version.Minor || name.Version.Build != version.Build))
                    return "The downloaded DLL reports version " + name.Version + " instead of " + version.ToString(3);
                return null;
            }
            catch (BadImageFormatException)
            {
                return "The downloaded file isn't a valid DLL";
            }
            catch (Exception e)
            {
                return "Verification failed: " + e.Message;
            }
        }

        // Swap the verified DLL in. If the running DLL is locked, finish the job after the game closes.
        void Install(string staged, string sha, string version)
        {
            string target = Plugin.PluginPath;
            if (string.IsNullOrEmpty(target) || !File.Exists(target))
            {
                Fail("Can't find the installed mod DLL");
                return;
            }

            try
            {
                Backup(target);
            }
            catch (Exception e)
            {
                Fail("Couldn't back up the current version, update cancelled (" + e.Message + ")");
                return;
            }

            string old = target + ".old";
            try
            {
                TryDelete(old);
                File.Move(target, old); // a loaded DLL can often still be renamed on Windows
            }
            catch (Exception)
            {
                ScheduleOnExit(staged, sha, version);
                return;
            }

            try
            {
                File.Copy(staged, target, false);
                if (!string.Equals(Sha256(target), sha, StringComparison.OrdinalIgnoreCase)) throw new IOException("copied file doesn't match the checksum");
            }
            catch (Exception e)
            {
                // Put the working version back exactly as it was
                TryDelete(target);
                try
                {
                    File.Move(old, target);
                }
                catch (Exception restore)
                {
                    Plugin.Log.LogError("Update rollback failed, restore manually from " + BackupFolder + ": " + restore.Message);
                }
                Fail("Install failed, previous version kept (" + e.Message + ")");
                return;
            }

            TryDelete(staged);
            AddHistory(PluginInfo.Version, version, "Ready", "Restart the game to use it");
            Save();
            SetStatus(UpdateStatus.RestartToFinish, "Version " + version + " is installed. Restart the game to use it.");
            rt.Toasts.Show("Update " + version + " installed, restart the game to use it", UI.Theme.Success);
        }

        void ScheduleOnExit(string staged, string sha, string version)
        {
            state.pendingVersion = version;
            state.pendingFile = staged;
            state.pendingSha = sha;
            state.pendingFrom = PluginInfo.Version;
            Save();
            SetStatus(UpdateStatus.InstallOnExit, "Version " + version + " is ready and installs when you close the game");
            rt.Toasts.Show("Update " + version + " installs when you close the game", UI.Theme.Success);
        }

        static void Backup(string target)
        {
            Directory.CreateDirectory(BackupFolder);
            File.Copy(target, Path.Combine(BackupFolder, "AdexansCastingMod-" + PluginInfo.Version + ".dll"), true);

            // Keep the three newest backups
            string[] files = Directory.GetFiles(BackupFolder, "AdexansCastingMod-*.dll");
            Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            for (int i = 3; i < files.Length; i++) TryDelete(files[i]);
        }

        // ------------------------------------------------------------ Rollback

        public string RollbackCandidate
        {
            get
            {
                try
                {
                    if (!Directory.Exists(BackupFolder)) return null;
                    string best = null;
                    Version bestVersion = null;
                    foreach (string file in Directory.GetFiles(BackupFolder, "AdexansCastingMod-*.dll"))
                    {
                        Version v = ParseVersion(Path.GetFileNameWithoutExtension(file).Substring("AdexansCastingMod-".Length));
                        if (v == null || v == Installed) continue;
                        if (bestVersion == null || v > bestVersion)
                        {
                            bestVersion = v;
                            best = file;
                        }
                    }
                    return best;
                }
                catch
                {
                    return null;
                }
            }
        }

        public void Rollback()
        {
            string backup = RollbackCandidate;
            if (Busy || backup == null) return;
            Busy = true;
            try
            {
                Version v = ParseVersion(Path.GetFileNameWithoutExtension(backup).Substring("AdexansCastingMod-".Length));
                string problem = Verify(backup, 0, Sha256(backup), v);
                if (problem != null)
                {
                    Fail("The backup can't be used: " + problem);
                    return;
                }
                Directory.CreateDirectory(WorkFolder);
                string staged = Path.Combine(WorkFolder, "rollback-" + v.ToString(3) + ".dll");
                File.Copy(backup, staged, true);
                Install(staged, Sha256(staged), v.ToString(3));
            }
            catch (Exception e)
            {
                Fail("Rollback failed: " + e.Message);
            }
            finally
            {
                Busy = false;
            }
        }

        // ------------------------------------------------------------ Apply after exit

        // Called when the game quits: hands a pending update to a small PowerShell helper that waits
        // for the game to exit, re-checks the hash, then replaces the DLL (never leaving a half file)
        public void OnApplicationQuit()
        {
            if (state.pendingVersion.Length == 0 || !File.Exists(state.pendingFile)) return;
            string target = Plugin.PluginPath;
            if (string.IsNullOrEmpty(target)) return;

            try
            {
                Directory.CreateDirectory(WorkFolder);
                string script = Path.Combine(WorkFolder, "apply_update.ps1");
                File.WriteAllText(script, HelperScript(Process.GetCurrentProcess().Id, state.pendingFile, target, state.pendingSha, state.pendingVersion), new UTF8Encoding(true));
                var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                Process.Start(psi);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not start the update helper: " + e.Message);
            }
        }

        static string Ps(string s) => "'" + s.Replace("'", "''") + "'";

        static string HelperScript(int pid, string source, string target, string sha, string version)
        {
            var sb = new StringBuilder();
            sb.AppendLine("$ErrorActionPreference = 'Stop'");
            sb.AppendLine("$src = " + Ps(source));
            sb.AppendLine("$dst = " + Ps(target));
            sb.AppendLine("$tmp = $dst + '.new'");
            sb.AppendLine("$expected = " + Ps(sha.ToUpperInvariant()));
            sb.AppendLine("$log = " + Ps(HelperLog));
            sb.AppendLine("function Log($m) { Add-Content -LiteralPath $log -Value ((Get-Date).ToString('u') + ' ' + $m) }");
            sb.AppendLine("try { Wait-Process -Id " + pid + " -Timeout 300 -ErrorAction SilentlyContinue } catch {}");
            sb.AppendLine("for ($i = 0; $i -lt 30; $i++) {");
            sb.AppendLine("  try {");
            sb.AppendLine("    if ((Get-FileHash -LiteralPath $src -Algorithm SHA256).Hash -ne $expected) { Log 'FAILED downloaded file no longer matches its checksum'; exit 1 }");
            sb.AppendLine("    Copy-Item -LiteralPath $src -Destination $tmp -Force");
            sb.AppendLine("    if ((Get-FileHash -LiteralPath $tmp -Algorithm SHA256).Hash -ne $expected) { Remove-Item -LiteralPath $tmp -Force; Log 'FAILED copy was corrupted'; exit 1 }");
            sb.AppendLine("    Move-Item -LiteralPath $tmp -Destination $dst -Force");
            sb.AppendLine("    Remove-Item -LiteralPath $src -Force -ErrorAction SilentlyContinue");
            sb.AppendLine("    Log 'INSTALLED " + version + "'");
            sb.AppendLine("    exit 0");
            sb.AppendLine("  } catch { Start-Sleep -Seconds 1 }");
            sb.AppendLine("}");
            sb.AppendLine("if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }");
            sb.AppendLine("Log 'FAILED the mod file stayed locked'");
            sb.AppendLine("exit 1");
            return sb.ToString();
        }

        // ------------------------------------------------------------ Helpers

        GitHubAsset FindAsset(string name)
        {
            if (latest?.assets != null)
            {
                foreach (GitHubAsset a in latest.assets)
                    if (a != null && string.Equals(a.name, name, StringComparison.OrdinalIgnoreCase)) return a;
            }

            // Fallback if the asset list couldn't be read: release.ps1 always uploads these files under
            // fixed names, so the official link follows from the tag. Integrity still rests on the
            // SHA-256 file (the size check is skipped because the size is unknown here).
            if (latest == null || string.IsNullOrEmpty(latest.tag_name)) return null;
            Plugin.Log.LogWarning("Updater: release asset list was empty (" + (latest.assets?.Length ?? -1) + "), using the standard download link for " + name);
            return new GitHubAsset
            {
                name = name,
                browser_download_url = UpdateConfig.DownloadPrefix + Uri.EscapeDataString(latest.tag_name) + "/" + name,
                size = 0,
            };
        }

        static bool IsOfficial(GitHubAsset a) =>
            a.browser_download_url != null && a.browser_download_url.StartsWith(UpdateConfig.DownloadPrefix, StringComparison.OrdinalIgnoreCase);

        // "abc123…  AdexansCastingMod.dll" or just the hex
        static string ParseSha(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string hex = text.Trim().Split(new[] { ' ', '\t', '\r', '\n', '*' }, StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
            if (hex.Length != 64) return null;
            foreach (char c in hex)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return null;
            return hex;
        }

        public static string Sha256(string file)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream f = File.OpenRead(file))
            {
                byte[] hash = sha.ComputeHash(f);
                var sb = new StringBuilder(64);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // "v1.2.0", "1.2", "Release 2.0.1" -> 1.2.0
        public static Version ParseVersion(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            int start = 0;
            while (start < tag.Length && !char.IsDigit(tag[start])) start++;
            int end = start;
            while (end < tag.Length && (char.IsDigit(tag[end]) || tag[end] == '.')) end++;
            string core = tag.Substring(start, end - start).Trim(new[] { '.' });
            if (core.Length == 0) return null;
            string[] parts = core.Split(new[] { '.' });
            int major = 0, minor = 0, build = 0;
            if (!int.TryParse(parts[0], out major)) return null;
            if (parts.Length > 1 && !int.TryParse(parts[1], out minor)) return null;
            if (parts.Length > 2 && !int.TryParse(parts[2], out build)) return null;
            return new Version(major, minor, build);
        }

        void SetStatus(UpdateStatus s, string message)
        {
            Status = s;
            Message = message;
            if (s != UpdateStatus.Downloading) Progress = 0f;
        }

        void Fail(string message, bool record = true)
        {
            SetStatus(UpdateStatus.Failed, message);
            Plugin.Log.LogWarning("Updater: " + message);
            if (record && latestVersion != null) AddHistory(PluginInfo.Version, latestVersion.ToString(3), "Failed", message);
            Save();
        }

        void AddHistory(string from, string to, string result, string detail)
        {
            state.history.Add(new UpdateHistoryEntry { date = Now(), fromVersion = from ?? "", toVersion = to ?? "", result = result, detail = detail ?? "" });
            while (state.history.Count > 30) state.history.RemoveAt(0);
        }

        void ClearPending()
        {
            if (state.pendingFile.Length > 0) TryDelete(state.pendingFile);
            state.pendingVersion = state.pendingFile = state.pendingSha = state.pendingFrom = "";
        }

        public void ClearHistory()
        {
            state.history.Clear();
            Save();
        }

        void Save() => Storage.Save(StatePath, state);

        static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Still locked; cleaned up next start
            }
        }
    }
}
