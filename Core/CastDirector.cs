using System;
using System.Collections.Generic;
using AdexansCastingMod.Config;
using UnityEngine;

namespace AdexansCastingMod.Core
{
    internal enum SelectSource { Manual, AutoPilot, System }

    // Owns "who is being spectated" and the ordered player list the UI and shortcuts work from
    internal sealed class CastDirector
    {
        readonly CastSettings settings;
        readonly PlayerTracker tracker;
        readonly Func<CastPlayer, int> scoreOf;
        readonly List<CastPlayer> ordered = new List<CastPlayer>();
        int orderedRevision = -1;
        PlayerSort orderedSort;
        bool orderedIncludeLocal;
        int orderedScoreStamp;

        public CastPlayer Target { get; private set; }
        public float TargetSince { get; private set; }
        public SelectSource LastSource { get; private set; }

        // Only fires on real changes; prev/next may be null
        public event Action<CastPlayer, CastPlayer, SelectSource> TargetChanged;

        // Lets the auto pilot supply a smarter replacement when the target leaves
        public Func<CastPlayer, CastPlayer> ReplacementPicker;

        // Bumped by the scoreboard when scores change so "sort by score" re-sorts
        public int ScoreStamp;

        public CastDirector(CastSettings settings, PlayerTracker tracker, Func<CastPlayer, int> scoreOf)
        {
            this.settings = settings;
            this.tracker = tracker;
            this.scoreOf = scoreOf;
            tracker.PlayerLeft += OnPlayerLeft;
            tracker.RoomChanged += OnRoomChanged;
        }

        public bool IsSelectable(CastPlayer p)
        {
            return p != null && tracker.Contains(p) && (settings.includeLocalPlayer || !p.IsLocal);
        }

        public IReadOnlyList<CastPlayer> Ordered
        {
            get
            {
                if (orderedRevision != tracker.Revision || orderedSort != settings.playerSort ||
                    orderedIncludeLocal != settings.includeLocalPlayer ||
                    (settings.playerSort == PlayerSort.Score && orderedScoreStamp != ScoreStamp))
                {
                    RebuildOrdered();
                }
                return ordered;
            }
        }

        void RebuildOrdered()
        {
            orderedRevision = tracker.Revision;
            orderedSort = settings.playerSort;
            orderedIncludeLocal = settings.includeLocalPlayer;
            orderedScoreStamp = ScoreStamp;

            ordered.Clear();
            IReadOnlyList<CastPlayer> all = tracker.All;
            for (int i = 0; i < all.Count; i++)
                if (settings.includeLocalPlayer || !all[i].IsLocal) ordered.Add(all[i]);

            switch (settings.playerSort)
            {
                case PlayerSort.Name:
                    ordered.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                    break;
                case PlayerSort.TaggedLast:
                    ordered.Sort((a, b) => a.IsTagged != b.IsTagged ? (a.IsTagged ? 1 : -1) : a.JoinOrder.CompareTo(b.JoinOrder));
                    break;
                case PlayerSort.Score:
                    ordered.Sort((a, b) =>
                    {
                        int cmp = scoreOf(b).CompareTo(scoreOf(a));
                        return cmp != 0 ? cmp : a.JoinOrder.CompareTo(b.JoinOrder);
                    });
                    break;
                default:
                    ordered.Sort((a, b) => a.JoinOrder.CompareTo(b.JoinOrder));
                    break;
            }
        }

        public void Select(CastPlayer p, SelectSource source)
        {
            if (p != null && !IsSelectable(p)) return;
            if (p == Target) return;

            CastPlayer prev = Target;
            Target = p;
            TargetSince = Time.unscaledTime;
            LastSource = source;
            TargetChanged?.Invoke(prev, p, source);
        }

        public void Step(int direction)
        {
            IReadOnlyList<CastPlayer> list = Ordered;
            if (list.Count == 0) return;

            int index = -1;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == Target) index = i;

            int next = index < 0 ? (direction > 0 ? 0 : list.Count - 1) : (index + direction + list.Count) % list.Count;
            Select(list[next], SelectSource.Manual);
        }

        public void SelectSlot(int slot)
        {
            IReadOnlyList<CastPlayer> list = Ordered;
            if (slot >= 0 && slot < list.Count) Select(list[slot], SelectSource.Manual);
        }

        // Called every frame: covers the case where nothing is selected yet (just joined a room)
        public void Tick()
        {
            if (Target != null && !tracker.Contains(Target))
            {
                // Safety net in case the leave event was missed
                OnPlayerLeft(Target);
            }

            if (Target == null && settings.reselectOnLeave && !settings.autoPilotEnabled)
            {
                IReadOnlyList<CastPlayer> list = Ordered;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].HasRig)
                    {
                        Select(list[i], SelectSource.System);
                        break;
                    }
                }
            }
        }

        void OnPlayerLeft(CastPlayer p)
        {
            if (p != Target) return;

            CastPlayer replacement = null;
            if (settings.reselectOnLeave)
            {
                replacement = ReplacementPicker?.Invoke(p);
                if (replacement == null)
                {
                    IReadOnlyList<CastPlayer> list = Ordered;
                    for (int i = 0; i < list.Count && replacement == null; i++)
                        if (list[i] != p && list[i].HasRig && !list[i].IsTagged) replacement = list[i];
                    for (int i = 0; i < list.Count && replacement == null; i++)
                        if (list[i] != p && list[i].HasRig) replacement = list[i];
                }
            }

            CastPlayer prev = Target;
            Target = null;
            if (replacement != null && IsSelectable(replacement))
            {
                Target = replacement;
            }
            TargetSince = Time.unscaledTime;
            LastSource = SelectSource.System;
            TargetChanged?.Invoke(prev, Target, SelectSource.System);
        }

        void OnRoomChanged()
        {
            if (Target == null) return;
            CastPlayer prev = Target;
            Target = null;
            TargetSince = Time.unscaledTime;
            LastSource = SelectSource.System;
            TargetChanged?.Invoke(prev, null, SelectSource.System);
        }
    }
}
