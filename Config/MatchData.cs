using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdexansCastingMod.Config
{
    public enum MatchStatus { PreMatch, Live, Paused, Overtime, Intermission, RoundOver, Final, Custom }

    // Persisted to match.json so a game crash mid-broadcast doesn't lose the score
    [Serializable]
    public class MatchData
    {
        public string matchTitle = "COMPETITIVE LEAGUE";
        public string teamAName = "TEAM A";
        public string teamBName = "TEAM B";
        public Color teamAColor = new Color(0.27f, 0.6f, 1f, 1f);
        public Color teamBColor = new Color(1f, 0.36f, 0.33f, 1f);
        public int teamAScore;
        public int teamBScore;
        public int round = 1;
        public int totalRounds = 3;
        public MatchStatus status = MatchStatus.PreMatch;
        public string customStatus = "STARTING SOON";
        // Series record shown under each team name in the center-bar scoreboard, e.g. "2-1"
        public string teamARecord = "0-0";
        public string teamBRecord = "0-0";
        public List<PlayerRecord> players = new List<PlayerRecord>();

        public void Sanitize()
        {
            if (players == null) players = new List<PlayerRecord>();
            players.RemoveAll(p => p == null || string.IsNullOrEmpty(p.key));
            round = Mathf.Max(1, round);
            totalRounds = Mathf.Max(1, totalRounds);
            if (matchTitle == null) matchTitle = "";
            if (teamAName == null) teamAName = "TEAM A";
            if (teamBName == null) teamBName = "TEAM B";
            if (customStatus == null) customStatus = "";
            if (teamARecord == null) teamARecord = "";
            if (teamBRecord == null) teamBRecord = "";
        }
    }

    [Serializable]
    public class PlayerRecord
    {
        // UserId when known (stable across rejoins), otherwise "name:<nick>"
        public string key;
        public string name;
        public int team; // 0 = unassigned, 1 = team A, 2 = team B
        public int score;
        public int timesTagged;
    }
}
