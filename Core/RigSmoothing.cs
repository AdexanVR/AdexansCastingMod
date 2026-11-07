using System.Collections.Generic;
using AdexansCastingMod.Config;
using UnityEngine;

namespace AdexansCastingMod.Core
{
    // Overrides VRRig.lerpValueBody on remote rigs. Each frame the game moves a remote player's body,
    // head and hands that fraction of the way toward their latest network position:
    // low = smooth but delayed/floaty, high = accurate but choppy. Only this client's view changes.
    internal sealed class RigSmoothing
    {
        readonly CastSettings settings;
        readonly System.Func<bool> allowed;
        readonly Dictionary<VRRig, float> originals = new Dictionary<VRRig, float>();
        readonly List<VRRig> dead = new List<VRRig>();
        float nextTick;

        // The value the game ships with, read from the first remote rig seen (null until then)
        public float? GameDefault { get; private set; }

        public RigSmoothing(CastSettings settings, System.Func<bool> allowed)
        {
            this.settings = settings;
            this.allowed = allowed;
        }

        public void Tick()
        {
            float now = Time.unscaledTime;
            if (now < nextTick) return;
            nextTick = now + 0.25f;
            if (!VRRigCache.isInitialized) return;

            IReadOnlyList<RigContainer> containers = VRRigCache.ActiveRigContainers;
            if (containers == null) return;

            bool apply = settings.rigLerpOverride && allowed();
            for (int i = 0; i < containers.Count; i++)
            {
                RigContainer c = containers[i];
                VRRig rig = c != null ? c.Rig : null;
                if (rig == null || rig.isOfflineVRRig) continue;

                // Remember the untouched value before ever changing it, so it can be put back exactly
                if (!originals.TryGetValue(rig, out float original))
                {
                    original = rig.lerpValueBody;
                    originals[rig] = original;
                    if (!GameDefault.HasValue) GameDefault = original;
                }

                float want = apply ? settings.rigLerpValue : original;
                if (!Mathf.Approximately(rig.lerpValueBody, want)) rig.lerpValueBody = want;
            }

            // Forget rigs that were destroyed
            dead.Clear();
            foreach (VRRig rig in originals.Keys)
                if (rig == null) dead.Add(rig);
            foreach (VRRig rig in dead) originals.Remove(rig);
        }

        public void RestoreAll()
        {
            foreach (KeyValuePair<VRRig, float> kv in originals)
                if (kv.Key != null) kv.Key.lerpValueBody = kv.Value;
        }
    }
}
