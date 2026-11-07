using System;
using System.Collections.Generic;
using AdexansCastingMod.Config;
using UnityEngine;

namespace AdexansCastingMod.Core
{
    // Keeps the roster of players in the room and their tag state. It reads only what the game
    // already exposes to every client (NetworkSystem's player cache, VRRigCache and the active game
    // mode's LocalIsTagged), so nothing here sends anything or changes game state.
    internal sealed class PlayerTracker : IDisposable
    {
        readonly CastSettings settings;
        readonly List<CastPlayer> players = new List<CastPlayer>();
        readonly Dictionary<int, CastPlayer> byActor = new Dictionary<int, CastPlayer>();
        readonly HashSet<int> seen = new HashSet<int>();
        readonly List<CastPlayer> pendingRemoval = new List<CastPlayer>();

        readonly Action<NetPlayer> onNetPlayer;
        readonly Action onRoomEvent;
        readonly Action<RigContainer> onRigEvent;

        NetworkSystem hookedNetwork;
        bool rigEventsHooked;
        bool dirty = true;
        float nextPoll;
        int joinCounter;

        public IReadOnlyList<CastPlayer> All => players;
        public bool InRoom { get; private set; }
        public bool IsPrivateRoom { get; private set; }
        public string RoomName { get; private set; } = "";
        public string GameModeName { get; private set; } = "";
        public int TaggedCount { get; private set; }

        // Unscaled time of the most recent tag in this room (-1 = none yet)
        public float LastTagTime { get; private set; } = -1f;

        // Bumped whenever the roster or any tag state changes, so UI can cache sorted lists
        public int Revision { get; private set; }

        public event Action<CastPlayer> PlayerJoined;
        public event Action<CastPlayer> PlayerLeft;
        public event Action<CastPlayer> PlayerTagged;
        public event Action<CastPlayer> PlayerUntagged;
        public event Action RoomChanged;

        public PlayerTracker(CastSettings settings)
        {
            this.settings = settings;
            onNetPlayer = _ => dirty = true;
            onRoomEvent = () => dirty = true;
            onRigEvent = _ => dirty = true;
        }

        public CastPlayer Get(int actor) => byActor.TryGetValue(actor, out CastPlayer p) ? p : null;

        public bool Contains(CastPlayer p) => p != null && byActor.TryGetValue(p.Actor, out CastPlayer found) && found == p;

        public void Tick()
        {
            HookEvents();
            float now = Time.unscaledTime;
            if (!dirty && now < nextPoll) return;
            nextPoll = now + settings.tagPollInterval;
            dirty = false;

            try
            {
                Sync();
            }
            catch (Exception e)
            {
                // A network hiccup mid-sync must never take the whole overlay down; try again next poll
                Plugin.Log.LogWarning("Player sync failed: " + e.Message);
            }
        }

        void HookEvents()
        {
            NetworkSystem ns = NetworkSystem.Instance;
            if (ns != hookedNetwork)
            {
                UnhookNetwork();
                if (ns != null)
                {
                    ns.OnPlayerJoined += onNetPlayer;
                    ns.OnPlayerLeft += onNetPlayer;
                    ns.OnJoinedRoomEvent += onRoomEvent;
                    ns.OnReturnedToSinglePlayer += onRoomEvent;
                    hookedNetwork = ns;
                }
            }

            if (!rigEventsHooked)
            {
                VRRigCache.OnRigActivated += onRigEvent;
                VRRigCache.OnRigDeactivated += onRigEvent;
                rigEventsHooked = true;
            }
        }

        void UnhookNetwork()
        {
            if (hookedNetwork != null)
            {
                hookedNetwork.OnPlayerJoined -= onNetPlayer;
                hookedNetwork.OnPlayerLeft -= onNetPlayer;
                hookedNetwork.OnJoinedRoomEvent -= onRoomEvent;
                hookedNetwork.OnReturnedToSinglePlayer -= onRoomEvent;
            }
            hookedNetwork = null;
        }

        void Sync()
        {
            NetworkSystem ns = NetworkSystem.Instance;
            bool inRoom = ns != null && ns.InRoom;
            string room = inRoom ? (ns.RoomName ?? "") : "";

            if (inRoom != InRoom || room != RoomName)
            {
                ClearAll();
                LastTagTime = -1f;
                InRoom = inRoom;
                RoomName = room;
                Revision++;
                RoomChanged?.Invoke();
            }

            IsPrivateRoom = inRoom && ns.SessionIsPrivate;

            GorillaGameManager gm = GorillaGameManager.instance;
            GameModeName = gm != null ? SafeGameModeName(gm) : "";

            seen.Clear();
            if (inRoom)
            {
                IReadOnlyList<NetPlayer> cache = ns.NetPlayerCache;
                for (int i = 0; i < cache.Count; i++)
                {
                    NetPlayer net = cache[i];
                    if (net == null || net.IsNull) continue;
                    Track(net, gm);
                }
            }
            else
            {
                TrackOfflineLocal();
            }

            pendingRemoval.Clear();
            foreach (CastPlayer p in players)
                if (!seen.Contains(p.Actor)) pendingRemoval.Add(p);

            foreach (CastPlayer p in pendingRemoval)
            {
                players.Remove(p);
                byActor.Remove(p.Actor);
                Revision++;
                PlayerLeft?.Invoke(p);
            }

            int tagged = 0;
            foreach (CastPlayer p in players)
                if (p.IsTagged) tagged++;
            TaggedCount = tagged;
        }

        void Track(NetPlayer net, GorillaGameManager gm)
        {
            int actor = net.ActorNumber;
            seen.Add(actor);

            if (!byActor.TryGetValue(actor, out CastPlayer p))
            {
                p = new CastPlayer
                {
                    Net = net,
                    Actor = actor,
                    IsLocal = net.IsLocal,
                    JoinOrder = joinCounter++,
                    TagChangedAt = Time.unscaledTime,
                };
                players.Add(p);
                byActor[actor] = p;
                Revision++;
                Refresh(p, net, gm, isNew: true);
                PlayerJoined?.Invoke(p);
                return;
            }

            p.Net = net;
            Refresh(p, net, gm, isNew: false);
        }

        void Refresh(CastPlayer p, NetPlayer net, GorillaGameManager gm, bool isNew)
        {
            p.Rig = FindRig(net);
            if (string.IsNullOrEmpty(p.UserId)) p.UserId = net.UserId;

            string name = p.Rig != null ? p.Rig.playerNameVisible : null;
            if (string.IsNullOrEmpty(name)) name = net.NickName;
            if (string.IsNullOrEmpty(name)) name = "PLAYER " + p.Actor;
            if (name != p.Name)
            {
                p.Name = name;
                Revision++;
            }

            bool tagged = IsTagged(net, p.Rig, gm);
            if (tagged == p.IsTagged) return;

            p.IsTagged = tagged;
            p.TagChangedAt = Time.unscaledTime;
            Revision++;
            if (isNew) return; // joining already-tagged is not a tag event

            if (tagged)
            {
                p.TimesTaggedSession++;
                LastTagTime = Time.unscaledTime;
                PlayerTagged?.Invoke(p);
            }
            else
            {
                PlayerUntagged?.Invoke(p);
            }
        }

        bool IsTagged(NetPlayer net, VRRig rig, GorillaGameManager gm)
        {
            bool tagged = false;
            if (gm != null)
            {
                try
                {
                    tagged = gm.LocalIsTagged(net);
                }
                catch
                {
                    // The game mode can be mid-teardown during room switches
                }
            }

            // Optional fallback for modes that don't report tag state: the tagged/infected material indices
            if (!tagged && settings.tagDetection == TagDetection.GameModeAndMaterial && rig != null)
                tagged = rig.setMatIndex == 1 || rig.setMatIndex == 2;

            return tagged;
        }

        static VRRig FindRig(NetPlayer net)
        {
            if (net.IsLocal)
            {
                GorillaTagger tagger = GorillaTagger.Instance;
                return tagger != null ? tagger.offlineVRRig : null;
            }

            if (!VRRigCache.isInitialized) return null;
            IReadOnlyList<RigContainer> containers = VRRigCache.ActiveRigContainers;
            if (containers == null) return null;

            int actor = net.ActorNumber;
            for (int i = 0; i < containers.Count; i++)
            {
                RigContainer c = containers[i];
                if (c == null) continue;
                VRRig rig = c.Rig;
                if (rig == null) continue;
                NetPlayer creator = rig.Creator;
                if (creator != null && !creator.IsNull && creator.ActorNumber == actor)
                    return rig;
            }
            return null;
        }

        // Offline there is no NetPlayer list; expose the local rig so the camera/layout can be tested solo
        void TrackOfflineLocal()
        {
            if (!settings.includeLocalPlayer) return;
            GorillaTagger tagger = GorillaTagger.Instance;
            if (tagger == null || tagger.offlineVRRig == null) return;

            const int offlineActor = -1;
            seen.Add(offlineActor);
            if (!byActor.TryGetValue(offlineActor, out CastPlayer p))
            {
                p = new CastPlayer { Actor = offlineActor, IsLocal = true, JoinOrder = joinCounter++, TagChangedAt = Time.unscaledTime };
                players.Add(p);
                byActor[offlineActor] = p;
                Revision++;
                PlayerJoined?.Invoke(p);
            }
            p.Rig = tagger.offlineVRRig;
            string name = p.Rig.playerNameVisible;
            p.Name = string.IsNullOrEmpty(name) ? "YOU" : name;
        }

        static string SafeGameModeName(GorillaGameManager gm)
        {
            try
            {
                return gm.GameModeName() ?? "";
            }
            catch
            {
                return "";
            }
        }

        // Leaving/switching rooms is reported once through RoomChanged rather than as N individual leaves
        void ClearAll()
        {
            players.Clear();
            byActor.Clear();
            TaggedCount = 0;
        }

        public void ForceRefresh() => dirty = true;

        public void Dispose()
        {
            UnhookNetwork();
            if (rigEventsHooked)
            {
                VRRigCache.OnRigActivated -= onRigEvent;
                VRRigCache.OnRigDeactivated -= onRigEvent;
                rigEventsHooked = false;
            }
            players.Clear();
            byActor.Clear();
        }
    }
}
