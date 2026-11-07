using System;
using System.Collections.Generic;
using System.Text;
using AdexansCastingMod.Config;
using GorillaNetworking;
using UnityEngine;

namespace AdexansCastingMod.Core
{
    // Joins/leaves rooms through the game's own networking (the same calls the in-game computer uses)
    internal sealed class RoomJoiner
    {
        public const int MaxCodeLength = 10;
        const int MaxRecent = 6;

        readonly CastingRuntime rt;

        public string Pending { get; private set; }
        public float PendingSince { get; private set; }

        public RoomJoiner(CastingRuntime rt)
        {
            this.rt = rt;
        }

        // Room codes are A-Z / 0-9, upper case, at most 10 characters (same as the in-game keyboard)
        public static string Sanitize(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            var sb = new StringBuilder(MaxCodeLength);
            foreach (char ch in code.ToUpperInvariant())
            {
                if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) sb.Append(ch);
                if (sb.Length >= MaxCodeLength) break;
            }
            return sb.ToString();
        }

        public bool IsBusy => Pending != null && Time.unscaledTime - PendingSince < 15f;

        public void Join(string rawCode)
        {
            string code = Sanitize(rawCode);
            if (code.Length == 0)
            {
                rt.Toasts.Show("Enter a room code first", Theme_Warning);
                return;
            }

            PhotonNetworkController controller = PhotonNetworkController.Instance;
            if (controller == null || NetworkSystem.Instance == null)
            {
                rt.Toasts.Show("Network isn't ready yet, try again in a moment", Theme_Warning);
                return;
            }
            if (NetworkSystem.Instance.InRoom && NetworkSystem.Instance.RoomName == code)
            {
                rt.Toasts.Show("Already in " + code);
                return;
            }

            Remember(code);
            Pending = code;
            PendingSince = Time.unscaledTime;
            rt.Toasts.Show("Joining " + code + "…");

            try
            {
                controller.AttemptToJoinSpecificRoomWithCallback(code, JoinType.Solo, result => OnJoinResult(code, result));
            }
            catch (Exception e)
            {
                Pending = null;
                Plugin.Log.LogWarning("Join failed: " + e.Message);
                rt.Toasts.Show("Could not join " + code, Theme_Danger);
            }
        }

        void OnJoinResult(string code, NetJoinResult result)
        {
            if (Pending == code) Pending = null;
            switch (result)
            {
                case NetJoinResult.Success:
                case NetJoinResult.AlreadyInRoom:
                    break; // the tracker announces the room itself
                case NetJoinResult.FallbackCreated:
                    rt.Toasts.Show("Room " + code + " was empty, created it");
                    break;
                case NetJoinResult.Failed_Full:
                    rt.Toasts.Show("Room " + code + " is full", Theme_Danger);
                    break;
                default:
                    rt.Toasts.Show("Could not join " + code, Theme_Danger);
                    break;
            }
        }

        public void Leave()
        {
            NetworkSystem ns = NetworkSystem.Instance;
            if (ns == null || !ns.InRoom) return;
            Pending = null;
            rt.Toasts.Show("Leaving " + ns.RoomName);
            try
            {
                _ = ns.ReturnToSinglePlayer();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Leave failed: " + e.Message);
            }
        }

        void Remember(string code)
        {
            List<string> recent = rt.Settings.recentRooms;
            recent.Remove(code);
            recent.Insert(0, code);
            while (recent.Count > MaxRecent) recent.RemoveAt(recent.Count - 1);
            rt.Settings.roomCode = code;
            rt.MarkSettingsDirty();
        }

        static Color Theme_Warning => UI.Theme.Warning;
        static Color Theme_Danger => UI.Theme.Danger;
    }
}
