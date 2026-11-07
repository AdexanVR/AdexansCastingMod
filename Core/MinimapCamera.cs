using AdexansCastingMod.Config;
using UnityEngine;

namespace AdexansCastingMod.Core
{
    // Orthographic top-down camera over the spectated player, rendered into a texture for the minimap widget.
    // It sits just under whatever is above the player (cave roofs, tree canopies) so the player stays visible,
    // renders only at the configured rate, and never renders to the headset or the screen directly.
    [DefaultExecutionOrder(32001)]
    internal sealed class MinimapCamera : MonoBehaviour
    {
        static readonly Color Background = new Color(0.07f, 0.08f, 0.1f, 1f);

        CastSettings settings;
        CastDirector director;
        SpectatorCamera spectator;
        System.Func<bool> shouldRender;

        Camera cam;
        RenderTexture texture;
        float nextRender;

        public bool Active { get; private set; }
        public RenderTexture Texture => Active ? texture : null;
        public Camera Cam => Active ? cam : null;
        public float MapYaw { get; private set; }

        public void Init(CastSettings settings, CastDirector director, SpectatorCamera spectator, System.Func<bool> shouldRender)
        {
            this.settings = settings;
            this.director = director;
            this.spectator = spectator;
            this.shouldRender = shouldRender;
        }

        void LateUpdate()
        {
            if (settings == null) return;

            Transform head = director.Target?.Head;
            if (head == null || !shouldRender())
            {
                Deactivate();
                return;
            }

            Ensure();
            Vector3 hp = head.position;

            float height = settings.minimapMaxHeight;
            if (Physics.SphereCast(hp + Vector3.up * 0.3f, 0.25f, Vector3.up, out RaycastHit hit, height, CameraSetup.LocomotionMask, QueryTriggerInteraction.Ignore))
                height = Mathf.Max(0.5f, hit.distance + 0.05f);

            if (settings.minimapRotate)
            {
                if (spectator.IsLive)
                {
                    MapYaw = spectator.Yaw;
                }
                else
                {
                    Vector3 f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                    if (f.sqrMagnitude > 1e-4f) MapYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                }
            }
            else
            {
                MapYaw = 0f;
            }

            cam.transform.SetPositionAndRotation(hp + Vector3.up * height, Quaternion.Euler(90f, MapYaw, 0f));
            cam.orthographicSize = settings.minimapZoom * 0.5f;
            cam.farClipPlane = height + 80f;

            // Render only at the configured rate: enable the camera for the frames that should draw
            float now = Time.unscaledTime;
            bool render = now >= nextRender;
            if (render) nextRender = now + 1f / settings.minimapFps;
            cam.enabled = render;
            Active = true;
        }

        void Ensure()
        {
            int res = settings.minimapResolution;
            if (texture == null || texture.width != res)
            {
                if (texture != null)
                {
                    if (cam != null) cam.targetTexture = null;
                    texture.Release();
                    Destroy(texture);
                }
                texture = new RenderTexture(res, res, 16, RenderTextureFormat.ARGB32)
                {
                    name = "AdexansCastingMod_Minimap",
                    antiAliasing = 2,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                texture.Create();
                if (cam != null) cam.targetTexture = texture;
            }

            if (cam != null) return;

            var go = new GameObject("AdexansCastingMod_MinimapCamera");
            go.transform.SetParent(transform, false);
            cam = go.AddComponent<Camera>();

            Camera source = null;
            GorillaTagger tagger = GorillaTagger.Instance;
            if (tagger != null && tagger.thirdPersonCamera != null)
                source = tagger.thirdPersonCamera.GetComponentInChildren<Camera>(true);
            if (source != null) cam.cullingMask = source.cullingMask;

            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.nearClipPlane = 0.02f;
            cam.depth = -50f;
            cam.targetTexture = texture;
            CameraSetup.DesktopOnly(cam, source, false, 0, false);
            cam.enabled = false;
        }

        void Deactivate()
        {
            if (cam != null && cam.enabled) cam.enabled = false;
            Active = false;
        }

        void OnDestroy()
        {
            if (cam != null) Destroy(cam.gameObject);
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
        }
    }
}
