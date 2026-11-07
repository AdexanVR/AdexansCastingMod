using System;
using AdexansCastingMod.Config;
using UnityEngine;

namespace AdexansCastingMod.Core
{
    internal enum TimerState { Stopped, Running, Paused, Finished }

    internal sealed class MatchTimer
    {
        readonly CastSettings settings;
        readonly Beeper beeper;

        double elapsed;
        bool warned;
        int lastBeepSecond = -1;

        public TimerState State { get; private set; } = TimerState.Stopped;
        public bool InOvertime { get; private set; }
        public double Elapsed => elapsed;

        public event Action Finished;
        public event Action OvertimeStarted;
        public event Action<TimerState> StateChanged;

        public MatchTimer(CastSettings settings, Beeper beeper)
        {
            this.settings = settings;
            this.beeper = beeper;
        }

        double Duration => settings.matchDuration;
        bool HasEnd => Duration > 0.0;

        // Remaining time in the current phase (regular time or a fixed-length overtime); -1 if open-ended
        public double PhaseRemaining
        {
            get
            {
                if (!HasEnd) return -1.0;
                if (!InOvertime) return Math.Max(0.0, Duration - elapsed);
                if (settings.overtimeDuration <= 0f) return -1.0;
                return Math.Max(0.0, Duration + settings.overtimeDuration - elapsed);
            }
        }

        public double DisplaySeconds
        {
            get
            {
                if (!settings.countdown || !HasEnd) return elapsed;
                double remaining = PhaseRemaining;
                // Open-ended overtime counts up from zero
                return remaining >= 0.0 ? remaining : elapsed - Duration;
            }
        }

        public bool IsWarning
        {
            get
            {
                if (State != TimerState.Running && State != TimerState.Paused) return false;
                double r = PhaseRemaining;
                return r >= 0.0 && r <= settings.warningThreshold;
            }
        }

        public void StartPauseResume()
        {
            switch (State)
            {
                case TimerState.Running: Pause(); break;
                case TimerState.Paused: Resume(); break;
                default: Start(); break;
            }
        }

        public void Start()
        {
            if (State == TimerState.Finished) ResetInternal();
            SetState(TimerState.Running);
        }

        public void Pause()
        {
            if (State == TimerState.Running) SetState(TimerState.Paused);
        }

        public void Resume()
        {
            if (State == TimerState.Paused) SetState(TimerState.Running);
        }

        public void Reset()
        {
            ResetInternal();
            SetState(TimerState.Stopped);
        }

        void ResetInternal()
        {
            elapsed = 0.0;
            InOvertime = false;
            warned = false;
            lastBeepSecond = -1;
        }

        // Positive = more time left on a countdown
        public void Adjust(float seconds)
        {
            if (settings.countdown && HasEnd) elapsed -= seconds;
            else elapsed += seconds;
            elapsed = Math.Max(0.0, elapsed);

            if (InOvertime && elapsed < Duration) InOvertime = false;
            if (PhaseRemaining > settings.warningThreshold) warned = false;
            lastBeepSecond = -1;
            if (State == TimerState.Finished && (PhaseRemaining > 0.0 || !HasEnd)) SetState(TimerState.Paused);
        }

        void SetState(TimerState s)
        {
            if (State == s) return;
            State = s;
            StateChanged?.Invoke(s);
        }

        public void Tick(float dt)
        {
            if (State != TimerState.Running) return;
            elapsed += dt;

            if (HasEnd)
            {
                if (!InOvertime && elapsed >= Duration)
                {
                    if (settings.overtimeEnabled)
                    {
                        InOvertime = true;
                        warned = false;
                        lastBeepSecond = -1;
                        if (settings.endSound) beeper.Play(Cue.Overtime);
                        OvertimeStarted?.Invoke();
                    }
                    else
                    {
                        Finish();
                        return;
                    }
                }
                else if (InOvertime && settings.overtimeDuration > 0f && elapsed >= Duration + settings.overtimeDuration)
                {
                    Finish();
                    return;
                }
            }

            double remaining = PhaseRemaining;
            if (remaining < 0.0) return;

            if (!warned && settings.warningThreshold > 0f && remaining <= settings.warningThreshold)
            {
                warned = true;
                if (settings.warningSound) beeper.Play(Cue.Warning);
            }

            if (settings.finalCountdownBeeps)
            {
                int second = Mathf.CeilToInt((float)remaining);
                if (second > 0 && second <= settings.finalCountdownSeconds && second != lastBeepSecond)
                {
                    lastBeepSecond = second;
                    beeper.Play(Cue.Tick);
                }
            }
        }

        void Finish()
        {
            if (HasEnd)
                elapsed = InOvertime && settings.overtimeDuration > 0f ? Duration + settings.overtimeDuration : Math.Max(elapsed, Duration);
            SetState(TimerState.Finished);
            if (settings.endSound) beeper.Play(Cue.End);
            Finished?.Invoke();
        }

        public string Format(double seconds)
        {
            if (seconds < 0.0) seconds = 0.0;
            bool tenths = settings.timerFormat == TimerFormat.MinutesSecondsTenths ||
                          (settings.tenthsInFinalTen && settings.countdown && seconds < 10.0 && State != TimerState.Stopped);

            // Floor tenths when counting up, ceil whole seconds when counting down so 0 shows only at the end
            int whole = settings.countdown && !tenths ? (int)Math.Ceiling(seconds - 1e-6) : (int)Math.Floor(seconds);
            int tenth = (int)Math.Floor(seconds * 10.0) % 10;

            switch (settings.timerFormat)
            {
                case TimerFormat.TotalSeconds:
                    return tenths ? $"{whole}.{tenth}" : whole.ToString();
                case TimerFormat.HoursMinutesSeconds:
                    return $"{whole / 3600}:{whole / 60 % 60:00}:{whole % 60:00}" + (tenths ? "." + tenth : "");
                default:
                    return $"{whole / 60:00}:{whole % 60:00}" + (tenths ? "." + tenth : "");
            }
        }

        public string DisplayText => Format(DisplaySeconds);
    }
}
