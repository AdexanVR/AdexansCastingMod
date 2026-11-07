using AdexansCastingMod.Config;
using UnityEngine;

namespace AdexansCastingMod.Core
{
    // A separate desktop-only camera drawn on top of the game's own desktop camera. It is excluded from
    // XR rendering (see CameraSetup), so the headset view is never touched. The game's camera is never
    // moved; at most its Camera component is switched off while we're live and switched back on after.
    [DefaultExecutionOrder(32000)]
    internal sealed class SpectatorCamera : MonoBehaviour
    {
        CastSettings settings;
        CastDirector director;
        System.Func<bool> spectatingAllowed;

        Camera cam;
        Camera gameCam;
        bool gameCamDisabledByUs;

        Vector3 pos;
        Quaternion rot = Quaternion.identity;
        float yaw;
        float orbitAngle;
        bool hasPose;
        CastPlayer lastTarget;
        CameraMode lastMode;

        // Last applied quality settings, so URP data is only touched when something changed
        int appliedAa = -1;
        bool appliedPost;
        bool appliedShadows;

        public bool IsLive { get; private set; }

        // Fired when the camera cuts (rather than glides) to a different player while already live
        public event System.Action Cut;
        public Camera LiveCamera => IsLive ? cam : null;
        public Vector3? LivePosition => IsLive && hasPose ? pos : (Vector3?)null;
        public float Yaw => IsLive ? rot.eulerAngles.y : 0f;

        public void Init(CastSettings settings, CastDirector director, System.Func<bool> spectatingAllowed)
        {
            this.settings = settings;
            this.director = director;
            this.spectatingAllowed = spectatingAllowed;
        }

        void LateUpdate()
        {
            if (settings == null) return;

            CastPlayer target = director.Target;
            Transform head = target?.Head;
            bool want = settings.spectatorCameraEnabled && head != null && spectatingAllowed();

            if (!want || !EnsureCamera())
            {
                SetLive(false);
                return;
            }

            ApplyQuality();
            SetLive(true);
            UpdatePose(target, head);
        }

        bool EnsureCamera()
        {
            if (gameCam == null)
            {
                GorillaTagger tagger = GorillaTagger.Instance;
                if (tagger != null && tagger.thirdPersonCamera != null)
                    gameCam = tagger.thirdPersonCamera.GetComponentInChildren<Camera>(true);
            }

            if (cam != null) return true;

            var go = new GameObject("AdexansCastingMod_SpectatorCamera");
            go.transform.SetParent(transform, false);
            cam = go.AddComponent<Camera>();
            if (gameCam != null)
            {
                cam.CopyFrom(gameCam);
                cam.depth = gameCam.depth + 50f;
            }
            else
            {
                cam.depth = 100f;
                cam.clearFlags = CameraClearFlags.Skybox;
            }
            cam.targetTexture = null;
            cam.nearClipPlane = 0.03f;
            cam.enabled = false;
            appliedAa = -1;
            return true;
        }

        void ApplyQuality()
        {
            if (appliedAa == settings.antiAliasing && appliedPost == settings.postProcessing && appliedShadows == settings.cameraShadows) return;
            CameraSetup.DesktopOnly(cam, gameCam, settings.postProcessing, settings.antiAliasing, settings.cameraShadows);
            appliedAa = settings.antiAliasing;
            appliedPost = settings.postProcessing;
            appliedShadows = settings.cameraShadows;
        }

        void SetLive(bool live)
        {
            if (cam != null && cam.enabled != live) cam.enabled = live;

            bool shouldDisableGameCam = live && settings.disableGameDesktopCamera && gameCam != null;
            if (shouldDisableGameCam && !gameCamDisabledByUs && gameCam.enabled)
            {
                gameCam.enabled = false;
                gameCamDisabledByUs = true;
            }
            else if (!shouldDisableGameCam && gameCamDisabledByUs)
            {
                RestoreGameCamera();
            }

            if (!live)
            {
                hasPose = false;
                lastTarget = null;
            }
            IsLive = live;
        }

        void RestoreGameCamera()
        {
            if (gameCamDisabledByUs && gameCam != null) gameCam.enabled = true;
            gameCamDisabledByUs = false;
        }

        void UpdatePose(CastPlayer target, Transform head)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            Vector3 headPos = head.position;

            Vector3 flatForward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 1e-4f) flatForward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            if (flatForward.sqrMagnitude < 1e-4f) flatForward = Vector3.forward;
            float targetYaw = Mathf.Atan2(flatForward.x, flatForward.z) * Mathf.Rad2Deg;

            bool targetChanged = target != lastTarget;
            bool modeChanged = settings.cameraMode != lastMode;
            if (targetChanged || !hasPose)
            {
                yaw = targetYaw;
                orbitAngle = targetYaw;
            }

            float rotK = Responsiveness(settings.rotationResponsiveness, dt);
            yaw = Mathf.LerpAngle(yaw, targetYaw, rotK * 0.6f);
            Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);

            Vector3 desiredPos;
            Quaternion desiredRot;
            Vector3 lookAt = headPos + Vector3.up * 0.1f;
            bool firstPerson = settings.cameraMode == CameraMode.FirstPerson;

            switch (settings.cameraMode)
            {
                case CameraMode.FirstPerson:
                    desiredPos = headPos + head.forward * settings.firstPersonOffset;
                    desiredRot = settings.stabilizeHorizon
                        ? Quaternion.LookRotation(head.forward, Vector3.up)
                        : head.rotation;
                    break;

                case CameraMode.Overhead:
                    desiredPos = headPos + Vector3.up * settings.overheadHeight + yawRot * Vector3.back * (settings.overheadHeight * 0.35f);
                    desiredPos = AvoidWalls(headPos, desiredPos);
                    desiredRot = Quaternion.LookRotation(lookAt - desiredPos, Vector3.up);
                    break;

                case CameraMode.Orbit:
                    orbitAngle += settings.orbitSpeed * dt;
                    desiredPos = headPos + Quaternion.Euler(0f, orbitAngle, 0f) * new Vector3(0f, settings.followHeight, -settings.followDistance);
                    desiredPos = AvoidWalls(headPos, desiredPos);
                    desiredRot = Quaternion.LookRotation(lookAt - desiredPos, Vector3.up);
                    break;

                default:
                    desiredPos = headPos + yawRot * new Vector3(0f, settings.followHeight, -settings.followDistance);
                    desiredPos = AvoidWalls(headPos, desiredPos);
                    desiredRot = Quaternion.LookRotation(lookAt - desiredPos, Vector3.up);
                    break;
            }

            bool cut = !hasPose || (targetChanged && (settings.transition == TransitionStyle.Cut ||
                                    (pos - desiredPos).magnitude > settings.glideMaxDistance));
            if (cut && hasPose && targetChanged) Cut?.Invoke();
            if (cut || (modeChanged && settings.transition == TransitionStyle.Cut))
            {
                pos = desiredPos;
                rot = desiredRot;
            }
            else
            {
                pos = Vector3.Lerp(pos, desiredPos, Responsiveness(settings.positionResponsiveness, dt));
                // The smoothed path can cut through geometry the desired point avoided; check it too
                if (!firstPerson) pos = AvoidWalls(headPos, pos);
                rot = Quaternion.Slerp(rot, desiredRot, rotK);
            }

            hasPose = true;
            lastTarget = target;
            lastMode = settings.cameraMode;

            cam.transform.SetPositionAndRotation(pos, rot);
            if (!Mathf.Approximately(cam.fieldOfView, settings.fieldOfView)) cam.fieldOfView = settings.fieldOfView;
        }

        // 0 means "no smoothing"; higher values converge faster. Frame-rate independent.
        static float Responsiveness(float k, float dt) => k <= 0f ? 1f : 1f - Mathf.Exp(-k * dt);

        Vector3 AvoidWalls(Vector3 from, Vector3 to)
        {
            return settings.avoidWalls ? CameraSetup.KeepOutOfWalls(from, to, 0.2f, 0.25f) : to;
        }

        void OnDisable()
        {
            if (cam != null) cam.enabled = false;
            RestoreGameCamera();
            IsLive = false;
        }

        void OnDestroy()
        {
            RestoreGameCamera();
            if (cam != null) Destroy(cam.gameObject);
        }
    }
}
