using System.Collections.Generic;
using AdexansCastingMod.Config;
using UnityEngine;

namespace AdexansCastingMod.Core
{
    internal enum AutoPilotState { Off, Following, Switching, Override, Waiting }

    // Moves the camera off a player once they are tagged. Switches are rate-limited (min hold time),
    // recently watched players are avoided, and a manual pick pauses the auto pilot for a while.
    internal sealed class AutoPilot
    {
        readonly CastSettings settings;
        readonly PlayerTracker tracker;
        readonly CastDirector director;
        readonly System.Func<Vector3?> cameraPosition;
        readonly Dictionary<int, float> lastWatched = new Dictionary<int, float>();
        readonly List<CastPlayer> candidates = new List<CastPlayer>();

        float overrideUntil;
        float pendingAt = -1f;
        float lastAutoSwitch = -999f;
        float nextRetry;

        public string LastReason { get; private set; } = "";

        public AutoPilot(CastSettings settings, PlayerTracker tracker, CastDirector director, System.Func<Vector3?> cameraPosition)
        {
            this.settings = settings;
            this.tracker = tracker;
            this.director = director;
            this.cameraPosition = cameraPosition;

            tracker.PlayerTagged += OnPlayerTagged;
            tracker.RoomChanged += () =>
            {
                lastWatched.Clear();
                pendingAt = -1f;
            };
            director.TargetChanged += OnTargetChanged;
            director.ReplacementPicker = left => settings.autoPilotEnabled ? Pick(left) : null;
        }

        public bool Enabled
        {
            get => settings.autoPilotEnabled;
            set
            {
                if (settings.autoPilotEnabled == value) return;
                settings.autoPilotEnabled = value;
                overrideUntil = 0f;
                pendingAt = -1f;
                nextRetry = 0f;
            }
        }

        public bool InOverride => Enabled && Time.unscaledTime < overrideUntil;
        public float OverrideRemaining => Mathf.Max(0f, overrideUntil - Time.unscaledTime);
        public bool SwitchPending => Enabled && pendingAt >= 0f;
        public float PendingRemaining => Mathf.Max(0f, pendingAt - Time.unscaledTime);

        public AutoPilotState State
        {
            get
            {
                if (!Enabled) return AutoPilotState.Off;
                if (InOverride) return AutoPilotState.Override;
                if (SwitchPending) return AutoPilotState.Switching;
                if (director.Target == null) return AutoPilotState.Waiting;
                return AutoPilotState.Following;
            }
        }

        public void CancelOverride() => overrideUntil = 0f;

        void OnTargetChanged(CastPlayer prev, CastPlayer next, SelectSource source)
        {
            float now = Time.unscaledTime;
            if (prev != null) lastWatched[prev.Actor] = now;

            pendingAt = -1f;
            if (source == SelectSource.AutoPilot) lastAutoSwitch = now;
            if (source == SelectSource.Manual && Enabled && settings.manualOverrideDuration > 0f)
                overrideUntil = now + settings.manualOverrideDuration;
        }

        void OnPlayerTagged(CastPlayer p)
        {
            if (!Enabled || InOverride || p != director.Target) return;
            // Delay a little so viewers actually see the tag happen before the cut
            pendingAt = Time.unscaledTime + settings.switchDelay;
            LastReason = p.Name + " was tagged";
        }

        public void Tick()
        {
            if (!Enabled)
            {
                pendingAt = -1f;
                return;
            }
            if (InOverride || !HasPlayers()) return;

            float now = Time.unscaledTime;
            CastPlayer target = director.Target;

            if (target == null)
            {
                if (now < nextRetry) return;
                CastPlayer pick = Pick(null);
                if (pick != null)
                {
                    LastReason = "Picked first player";
                    director.Select(pick, SelectSource.AutoPilot);
                }
                else
                {
                    nextRetry = now + 0.5f;
                }
                return;
            }

            if (pendingAt >= 0f)
            {
                if (now < pendingAt) return;
                pendingAt = -1f;
                if (target.IsTagged) TrySwitch(target, target.Name + " was tagged");
                return;
            }

            // Catches targets that were already tagged when auto pilot was turned on or an override ended
            if (target.IsTagged && now - target.TagChangedAt >= settings.switchDelay)
            {
                TrySwitch(target, target.Name + " is tagged");
                return;
            }

            if (settings.idleRotation && !target.IsTagged && now - director.TargetSince >= settings.idleRotationInterval)
                TrySwitch(target, "Scheduled rotation");
        }

        bool HasPlayers() => tracker.All.Count > 0;

        void TrySwitch(CastPlayer current, string reason)
        {
            float now = Time.unscaledTime;
            if (now < nextRetry) return;

            // Rate limit so a burst of tags doesn't make the camera bounce around
            float holdEnds = Mathf.Max(lastAutoSwitch, director.TargetSince) + settings.minHoldTime;
            if (now < holdEnds)
            {
                pendingAt = holdEnds;
                return;
            }

            CastPlayer pick = Pick(current);
            if (pick == null || pick == current)
            {
                // Everyone is tagged (or alone): stay put and check again shortly
                LastReason = "No eligible players";
                nextRetry = now + 0.5f;
                return;
            }

            LastReason = reason;
            director.Select(pick, SelectSource.AutoPilot);
        }

        public CastPlayer Pick(CastPlayer current)
        {
            float now = Time.unscaledTime;
            candidates.Clear();
            IReadOnlyList<CastPlayer> all = tracker.All;

            // First pass skips players we just watched; second pass allows them if nobody else qualifies
            for (int pass = 0; pass < 2 && candidates.Count == 0; pass++)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    CastPlayer p = all[i];
                    if (p == current || !p.HasRig || p.IsTagged || !director.IsSelectable(p)) continue;
                    if (pass == 0 && lastWatched.TryGetValue(p.Actor, out float t) && now - t < settings.recentTargetCooldown) continue;
                    candidates.Add(p);
                }
            }

            if (candidates.Count == 0) return null;
            if (candidates.Count == 1) return candidates[0];

            switch (settings.switchRule)
            {
                case SwitchRule.ClosestToTagger:
                    return ClosestToTagger() ?? NearestTo(ReferencePoint(current));
                case SwitchRule.NearestToCamera:
                    return NearestTo(ReferencePoint(current));
                case SwitchRule.LongestSurvivor:
                    return LongestSurvivor();
                case SwitchRule.NextInList:
                    return NextInList(current);
                default:
                    return candidates[Random.Range(0, candidates.Count)];
            }
        }

        Vector3? ReferencePoint(CastPlayer current)
        {
            Vector3? cam = cameraPosition();
            if (cam.HasValue) return cam;
            if (current != null && current.HasRig) return current.Position;
            return null;
        }

        // The untagged player with a tagger closest to them is usually where the action is
        CastPlayer ClosestToTagger()
        {
            IReadOnlyList<CastPlayer> all = tracker.All;
            CastPlayer best = null;
            float bestDist = float.MaxValue;
            foreach (CastPlayer c in candidates)
            {
                Vector3 cp = c.Position;
                for (int i = 0; i < all.Count; i++)
                {
                    CastPlayer tagger = all[i];
                    if (!tagger.IsTagged || !tagger.HasRig) continue;
                    float d = (tagger.Position - cp).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = c;
                    }
                }
            }
            return best;
        }

        CastPlayer NearestTo(Vector3? point)
        {
            if (!point.HasValue) return candidates[0];
            CastPlayer best = null;
            float bestDist = float.MaxValue;
            foreach (CastPlayer c in candidates)
            {
                float d = (c.Position - point.Value).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }
            return best;
        }

        CastPlayer LongestSurvivor()
        {
            CastPlayer best = candidates[0];
            foreach (CastPlayer c in candidates)
                if (c.TagChangedAt < best.TagChangedAt) best = c;
            return best;
        }

        CastPlayer NextInList(CastPlayer current)
        {
            IReadOnlyList<CastPlayer> list = director.Ordered;
            int start = -1;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == current) start = i;

            for (int step = 1; step <= list.Count; step++)
            {
                CastPlayer p = list[(start + step + list.Count) % list.Count];
                if (candidates.Contains(p)) return p;
            }
            return candidates[0];
        }
    }
}
