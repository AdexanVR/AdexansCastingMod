using UnityEngine;

namespace AdexansCastingMod.Core
{
    // Snapshot of one player in the room. The VRRig reference is re-resolved on every tracker poll because
    // the game pools rigs and may hand the same VRRig to a different player after someone leaves.
    internal sealed class CastPlayer
    {
        public NetPlayer Net;
        public int Actor;
        public string UserId;
        public string Name = "";
        public VRRig Rig;
        public bool IsLocal;
        public int JoinOrder;

        public bool IsTagged;
        public float TagChangedAt;
        public int TimesTaggedSession;

        public bool HasRig => Rig != null;

        public Color Color
        {
            get
            {
                if (Rig == null) return new Color(0.5f, 0.5f, 0.5f, 1f);
                Color c = Rig.playerColor;
                c.a = 1f;
                return c;
            }
        }

        public Transform Head
        {
            get
            {
                if (Rig == null) return null;
                VRMap head = Rig.head;
                if (head != null && head.rigTarget != null) return head.rigTarget;
                return Rig.transform;
            }
        }

        public Vector3 Position
        {
            get
            {
                Transform t = Head;
                return t != null ? t.position : Vector3.zero;
            }
        }

        // Seconds since this player was last untagged (or joined)
        public float SurvivalTime => IsTagged ? 0f : Time.unscaledTime - TagChangedAt;

        // Used to flash the player list row right after a tag
        public float SinceTagChange => Time.unscaledTime - TagChangedAt;

        public string RecordKey => !string.IsNullOrEmpty(UserId) ? UserId : "name:" + Name;
    }
}
