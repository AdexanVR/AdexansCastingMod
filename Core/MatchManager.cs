using System.Collections.Generic;
using AdexansCastingMod.Config;

namespace AdexansCastingMod.Core
{
    // Scoreboard state: team scores, rounds, match status and per-player records
    internal sealed class MatchManager
    {
        readonly CastSettings settings;
        readonly MatchTimer timer;
        readonly Dictionary<string, PlayerRecord> byKey = new Dictionary<string, PlayerRecord>();

        public MatchData Data { get; private set; }
        public bool Dirty { get; set; }

        // Incremented on every score edit so sorted views know to refresh
        public int ScoreStamp { get; private set; }

        public MatchManager(CastSettings settings, MatchData data, MatchTimer timer, PlayerTracker tracker)
        {
            this.settings = settings;
            this.timer = timer;
            Load(data);
            tracker.PlayerTagged += OnPlayerTagged;
            // Records are created lazily; drop the empty ones of players from the previous room
            tracker.RoomChanged += () => PruneRecords(tracker);
        }

        public void Load(MatchData data)
        {
            Data = data;
            byKey.Clear();
            foreach (PlayerRecord r in data.players) byKey[r.key] = r;
            ScoreStamp++;
        }

        void Changed()
        {
            Dirty = true;
            ScoreStamp++;
        }

        public PlayerRecord Record(CastPlayer p)
        {
            if (p == null) return null;
            string key = p.RecordKey;
            if (!byKey.TryGetValue(key, out PlayerRecord r))
            {
                r = new PlayerRecord { key = key, name = p.Name };
                byKey[key] = r;
                Data.players.Add(r);
                Dirty = true;
            }
            else if (r.name != p.Name && !string.IsNullOrEmpty(p.Name))
            {
                r.name = p.Name;
                Dirty = true;
            }
            return r;
        }

        public int ScoreOf(CastPlayer p) => p != null && byKey.TryGetValue(p.RecordKey, out PlayerRecord r) ? r.score : 0;

        public int TeamOf(CastPlayer p) => p != null && byKey.TryGetValue(p.RecordKey, out PlayerRecord r) ? r.team : 0;

        void OnPlayerTagged(CastPlayer p)
        {
            if (!settings.countTagsAutomatically) return;
            PlayerRecord r = Record(p);
            r.timesTagged++;
            Changed();
        }

        public void AddTeamScore(int team, int delta)
        {
            if (team == 1) Data.teamAScore = System.Math.Max(0, Data.teamAScore + delta);
            else if (team == 2) Data.teamBScore = System.Math.Max(0, Data.teamBScore + delta);
            Changed();
        }

        public void AddPlayerScore(PlayerRecord r, int delta)
        {
            if (r == null) return;
            r.score += delta;
            Changed();
        }

        public void SetTeam(PlayerRecord r, int team)
        {
            if (r == null || r.team == team) return;
            r.team = team;
            Changed();
        }

        public void SwapTeams()
        {
            MatchData d = Data;
            (d.teamAName, d.teamBName) = (d.teamBName, d.teamAName);
            (d.teamAColor, d.teamBColor) = (d.teamBColor, d.teamAColor);
            (d.teamAScore, d.teamBScore) = (d.teamBScore, d.teamAScore);
            (d.teamARecord, d.teamBRecord) = (d.teamBRecord, d.teamARecord);
            foreach (PlayerRecord r in d.players)
                if (r.team != 0) r.team = 3 - r.team;
            Changed();
        }

        public void NextRound()
        {
            Data.round++;
            if (Data.round > Data.totalRounds) Data.totalRounds = Data.round;
            Data.status = MatchStatus.PreMatch;
            if (settings.resetTimerOnNextRound) timer.Reset();
            Changed();
        }

        public void PreviousRound()
        {
            if (Data.round <= 1) return;
            Data.round--;
            Changed();
        }

        public void ResetScores()
        {
            Data.teamAScore = 0;
            Data.teamBScore = 0;
            foreach (PlayerRecord r in Data.players) r.score = 0;
            Changed();
        }

        public void ResetTagCounts()
        {
            foreach (PlayerRecord r in Data.players) r.timesTagged = 0;
            Changed();
        }

        public void ResetMatch()
        {
            ResetScores();
            ResetTagCounts();
            Data.round = 1;
            Data.status = MatchStatus.PreMatch;
            timer.Reset();
            Changed();
        }

        // Forget records of players not currently in the room who have no team or score
        public void PruneRecords(PlayerTracker tracker)
        {
            var present = new HashSet<string>();
            foreach (CastPlayer p in tracker.All) present.Add(p.RecordKey);
            Data.players.RemoveAll(r => !present.Contains(r.key) && r.team == 0 && r.score == 0 && r.timesTagged == 0);
            Load(Data);
            Dirty = true;
        }

        public MatchStatus EffectiveStatus
        {
            get
            {
                if (!settings.autoMatchStatus) return Data.status;
                switch (timer.State)
                {
                    case TimerState.Running: return timer.InOvertime ? MatchStatus.Overtime : MatchStatus.Live;
                    case TimerState.Paused: return MatchStatus.Paused;
                    case TimerState.Finished: return Data.round >= Data.totalRounds ? MatchStatus.Final : MatchStatus.RoundOver;
                    default: return Data.status;
                }
            }
        }

        public string StatusLabel(MatchStatus s)
        {
            switch (s)
            {
                case MatchStatus.PreMatch: return "PRE-MATCH";
                case MatchStatus.Live: return "LIVE";
                case MatchStatus.Paused: return "PAUSED";
                case MatchStatus.Overtime: return "OVERTIME";
                case MatchStatus.Intermission: return "INTERMISSION";
                case MatchStatus.RoundOver: return "ROUND OVER";
                case MatchStatus.Final: return "FINAL";
                default: return string.IsNullOrEmpty(Data.customStatus) ? "CUSTOM" : Data.customStatus.ToUpperInvariant();
            }
        }
    }
}
