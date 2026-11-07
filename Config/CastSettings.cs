using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdexansCastingMod.Config
{
    public enum CameraMode { ThirdPerson, FirstPerson, Overhead, Orbit }
    public enum SwitchRule { ClosestToTagger, NearestToCamera, LongestSurvivor, NextInList, Random }
    public enum PlayerSort { JoinOrder, Name, TaggedLast, Score }
    public enum TransitionStyle { Cut, Glide }
    public enum TimerFormat { MinutesSeconds, MinutesSecondsTenths, TotalSeconds, HoursMinutesSeconds }
    public enum TagDetection { GameMode, GameModeAndMaterial }
    public enum SubTimerMode { Off, SpectatedSurvival, SinceLastTag, Round }
    public enum SwitchEffect { None, Fade, Wipe }
    public enum UpdateInstallMode { Automatic, AskFirst }

    // Everything here is written to settings.json. JsonUtility needs public fields, so no properties.
    [Serializable]
    public class CastSettings
    {
        public int version = 1;

        // General
        public bool overlayVisible = true;
        public bool casterControlsVisible = true;
        public float hudScale = 1f;
        public float menuScale = 1f;
        public string fontName = "Segoe UI"; // pre-1.1 setting, migrated into fontId
        public string fontId = "";
        public bool menuUsesOverlayFont = false;

        // Day & time (local lighting only)
        public bool timeOverride = false;
        public float timeOfDayHour = 12f;

        // Auto-updater
        public bool autoUpdateCheck = true;
        public UpdateInstallMode updateInstallMode = UpdateInstallMode.Automatic;
        public bool textShadows = true;
        public bool animations = true;
        public float animationSpeed = 1f;
        public Color accentColor = new Color(0.27f, 0.6f, 1f, 1f);
        public bool showToasts = true;
        public float tagPollInterval = 0.1f;

        // Player casting
        public bool spectatorCameraEnabled = true;
        public CameraMode cameraMode = CameraMode.ThirdPerson;
        public float fieldOfView = 75f;
        public float followDistance = 2.4f;
        public float followHeight = 0.7f;
        public float positionResponsiveness = 7f;
        public float rotationResponsiveness = 9f;
        public float firstPersonOffset = 0.12f;
        public float trackingSmoothing = 0.5f;   // 0 = raw head position, 1 = heavy smoothing
        public bool followMovement = true;       // chase cam turns toward the direction of travel
        public bool dynamicFov = true;           // widen the view a little at speed
        public float speedFovBoost = 8f;
        public bool stabilizeHorizon = true;
        public float overheadHeight = 5f;
        public float orbitSpeed = 14f;
        public bool avoidWalls = true;
        public TransitionStyle transition = TransitionStyle.Glide;
        public float glideMaxDistance = 12f;
        public bool includeLocalPlayer = false;
        public bool disableGameDesktopCamera = true;
        public PlayerSort playerSort = PlayerSort.TaggedLast;
        public bool reselectOnLeave = true;
        public TagDetection tagDetection = TagDetection.GameMode;

        // Remote player smoothing (VRRig.lerpValueBody override)
        public bool rigLerpOverride = false;
        public float rigLerpValue = 0.5f;

        // Room joining
        public string roomCode = "";
        public List<string> recentRooms = new List<string>();

        // Image quality of the spectator camera
        public int antiAliasing = 2; // 0 off, 1 FXAA, 2 SMAA
        public bool postProcessing = true;
        public bool cameraShadows = true;

        // Name tags drawn above players in the broadcast view
        public bool showNameTags = true;
        public float nameTagSize = 15f;
        public float nameTagMaxDistance = 60f;
        public bool nameTagOnSpectated = true;

        // Minimap (top-down camera over the spectated player)
        public float minimapZoom = 14f;
        public float minimapMaxHeight = 30f;
        public bool minimapRotate = true;
        public int minimapResolution = 384;
        public int minimapFps = 30;
        public bool minimapMarkers = true;

        // Broadcast effects
        public bool showBanners = true;
        public float bannerDuration = 3f;
        public bool bannerRoundEvents = true;
        public bool bannerLastSurvivor = true;
        public SwitchEffect switchEffect = SwitchEffect.Wipe;

        // Scorebug
        public SubTimerMode scorebugSubTimer = SubTimerMode.SpectatedSurvival;

        // Auto Pilot
        public bool autoPilotEnabled = true;
        public SwitchRule switchRule = SwitchRule.ClosestToTagger;
        public float switchDelay = 1.2f;
        public float minHoldTime = 3f;
        public float manualOverrideDuration = 15f;
        public float recentTargetCooldown = 8f;
        public bool idleRotation = false;
        public float idleRotationInterval = 30f;

        // Timer
        public float matchDuration = 300f;
        public bool countdown = true;
        public bool overtimeEnabled = false;
        public float overtimeDuration = 60f;
        public TimerFormat timerFormat = TimerFormat.MinutesSeconds;
        public bool tenthsInFinalTen = true;
        public string timerLabel = "MATCH TIME";
        public float warningThreshold = 30f;
        public bool warningFlash = true;
        public bool warningSound = true;
        public bool finalCountdownBeeps = true;
        public int finalCountdownSeconds = 5;
        public bool endSound = true;
        public float soundVolume = 0.6f;
        public bool resetTimerOnNextRound = true;
        public bool autoMatchStatus = true;

        // Scoreboard
        public bool showIndividualScores = true;
        public bool showTagCounts = true;
        public bool countTagsAutomatically = true;
        public bool showUnassignedPlayers = true;
        public int maxScoreboardRows = 5;

        // Layout editor
        public float snapGrid = 8f;
        public bool snapToGuides = true;
        public string lastLayoutName = "Default";

        public List<KeyBinding> keyBindings = new List<KeyBinding>();

        public void Sanitize()
        {
            hudScale = Mathf.Clamp(hudScale, 0.5f, 2.5f);
            menuScale = Mathf.Clamp(menuScale, 0.6f, 2f);
            animationSpeed = Mathf.Clamp(animationSpeed, 0.25f, 4f);
            tagPollInterval = Mathf.Clamp(tagPollInterval, 0.03f, 1f);
            fieldOfView = Mathf.Clamp(fieldOfView, 30f, 120f);
            followDistance = Mathf.Clamp(followDistance, 0.5f, 10f);
            followHeight = Mathf.Clamp(followHeight, -1f, 5f);
            positionResponsiveness = Mathf.Clamp(positionResponsiveness, 0f, 30f);
            rotationResponsiveness = Mathf.Clamp(rotationResponsiveness, 0f, 30f);
            firstPersonOffset = Mathf.Clamp(firstPersonOffset, 0f, 0.5f);
            trackingSmoothing = Mathf.Clamp01(trackingSmoothing);
            speedFovBoost = Mathf.Clamp(speedFovBoost, 0f, 25f);
            overheadHeight = Mathf.Clamp(overheadHeight, 1f, 20f);
            orbitSpeed = Mathf.Clamp(orbitSpeed, -90f, 90f);
            glideMaxDistance = Mathf.Clamp(glideMaxDistance, 1f, 100f);
            switchDelay = Mathf.Clamp(switchDelay, 0f, 10f);
            minHoldTime = Mathf.Clamp(minHoldTime, 0f, 30f);
            manualOverrideDuration = Mathf.Clamp(manualOverrideDuration, 0f, 120f);
            recentTargetCooldown = Mathf.Clamp(recentTargetCooldown, 0f, 60f);
            idleRotationInterval = Mathf.Clamp(idleRotationInterval, 5f, 300f);
            matchDuration = Mathf.Clamp(matchDuration, 0f, 3600f * 3f);
            overtimeDuration = Mathf.Clamp(overtimeDuration, 0f, 3600f);
            warningThreshold = Mathf.Clamp(warningThreshold, 0f, 600f);
            finalCountdownSeconds = Mathf.Clamp(finalCountdownSeconds, 0, 30);
            soundVolume = Mathf.Clamp01(soundVolume);
            maxScoreboardRows = Mathf.Clamp(maxScoreboardRows, 1, 10);
            snapGrid = Mathf.Clamp(snapGrid, 0f, 64f);
            antiAliasing = Mathf.Clamp(antiAliasing, 0, 2);
            rigLerpValue = Mathf.Clamp(rigLerpValue, 0.02f, 1f);
            bannerDuration = Mathf.Clamp(bannerDuration, 1f, 10f);
            if (roomCode == null) roomCode = "";
            if (recentRooms == null) recentRooms = new List<string>();
            recentRooms.RemoveAll(string.IsNullOrEmpty);
            nameTagSize = Mathf.Clamp(nameTagSize, 8f, 48f);
            nameTagMaxDistance = Mathf.Clamp(nameTagMaxDistance, 5f, 300f);
            minimapZoom = Mathf.Clamp(minimapZoom, 3f, 80f);
            minimapMaxHeight = Mathf.Clamp(minimapMaxHeight, 2f, 100f);
            minimapResolution = Mathf.Clamp(minimapResolution, 128, 1024);
            minimapFps = Mathf.Clamp(minimapFps, 5, 120);
            if (string.IsNullOrEmpty(fontName)) fontName = "Segoe UI";
            if (string.IsNullOrEmpty(fontId)) fontId = "os:" + fontName;
            if (float.IsNaN(timeOfDayHour) || float.IsInfinity(timeOfDayHour)) timeOfDayHour = 12f;
            timeOfDayHour = Mathf.Repeat(timeOfDayHour, 24f);
            if (!System.Enum.IsDefined(typeof(UpdateInstallMode), updateInstallMode)) updateInstallMode = UpdateInstallMode.Automatic;
            if (timerLabel == null) timerLabel = "";
            if (keyBindings == null) keyBindings = new List<KeyBinding>();
            KeyBindings.MergeDefaults(keyBindings);
        }
    }
}
