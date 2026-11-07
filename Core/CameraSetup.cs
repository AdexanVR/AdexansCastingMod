using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AdexansCastingMod.Core
{
    // Gorilla Tag renders with URP. In URP, stereoTargetEye alone does NOT keep a camera out of the
    // headset: allowXRRendering on the URP camera data decides that. Without it the camera was drawn
    // into the HMD, its FOV was forced by the headset, and the desktop only got the low-res eye mirror.
    internal static class CameraSetup
    {
        public static void DesktopOnly(Camera cam, Camera source, bool postProcessing, int antiAliasing, bool shadows)
        {
            cam.stereoTargetEye = StereoTargetEyeMask.None;

            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.allowXRRendering = false;
            data.renderType = CameraRenderType.Base;

            if (source != null && source.TryGetComponent(out UniversalAdditionalCameraData src))
            {
                data.volumeLayerMask = src.volumeLayerMask;
                data.stopNaN = src.stopNaN;
                data.dithering = src.dithering;
            }

            data.renderPostProcessing = postProcessing;
            data.renderShadows = shadows;
            data.antialiasing = ToMode(antiAliasing);
            data.antialiasingQuality = AntialiasingQuality.High;
        }

        // 0 = off, 1 = FXAA, 2 = SMAA
        static AntialiasingMode ToMode(int aa)
        {
            switch (aa)
            {
                case 1: return AntialiasingMode.FastApproximateAntialiasing;
                case 2: return AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                default: return AntialiasingMode.None;
            }
        }

        // Keeps a camera position on the near side of any wall between the target and the camera
        public static Vector3 KeepOutOfWalls(Vector3 from, Vector3 to, float radius, float minDistance)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist < 0.01f) return to;
            dir /= dist;

            GorillaLocomotion.GTPlayer player = GorillaLocomotion.GTPlayer.Instance;
            int mask = player != null ? player.locomotionEnabledLayers.value : Physics.DefaultRaycastLayers;
            if (Physics.SphereCast(from, radius, dir, out RaycastHit hit, dist, mask, QueryTriggerInteraction.Ignore))
                return from + dir * Mathf.Max(minDistance, hit.distance - 0.05f);
            return to;
        }

        public static int LocomotionMask
        {
            get
            {
                GorillaLocomotion.GTPlayer player = GorillaLocomotion.GTPlayer.Instance;
                return player != null ? player.locomotionEnabledLayers.value : Physics.DefaultRaycastLayers;
            }
        }
    }
}
