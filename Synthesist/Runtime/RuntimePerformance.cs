#if SYNTHESIST_ACCEPTANCE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace WOTR_MAKING_FRIENDS.Synthesist.Runtime
{
    // Opt-in timing only. No state, resources, settings or game-time changes.
    internal static class RuntimePerformance
    {
        private sealed class Counter { public long Calls, Ticks, Maximum; }
        private static readonly Dictionary<string, Counter> counters = new();
        private static readonly List<double> frames = new();
        private static DateTime endsAt;
        private static long startedAt;
        private static int sampleThread;
        private static int combatFrames;
        private static string sampleId;
        internal static bool Active { get; private set; }

        internal static void Start(string id)
        {
            if (Active) throw new InvalidOperationException("A performance sample is already running.");
            counters.Clear(); frames.Clear();
            combatFrames = 0;
            sampleId = id;
            startedAt = Stopwatch.GetTimestamp();
            sampleThread = Environment.CurrentManagedThreadId;
            endsAt = DateTime.UtcNow.AddSeconds(8);
            Active = true;
        }

        internal static Scope Measure(string name) => new(name, Active && Environment.CurrentManagedThreadId == sampleThread);
        internal readonly struct Scope : IDisposable
        {
            private readonly string name;
            private readonly long start;
            private readonly bool active;
            internal Scope(string name, bool active)
            { this.name = name; this.active = active; start = active ? Stopwatch.GetTimestamp() : 0; }
            public void Dispose()
            {
                if (!active || !Active) return;
                long ticks = Stopwatch.GetTimestamp() - start;
                if (!counters.TryGetValue(name, out var value)) counters[name] = value = new Counter();
                value.Calls++; value.Ticks += ticks; value.Maximum = Math.Max(value.Maximum, ticks);
            }
        }

        internal static void EndFrame()
        {
            if (!Active) return;
            frames.Add(Time.unscaledDeltaTime * 1000d);
            if (Kingmaker.Game.Instance.Player.MainCharacter.Value.IsInCombat) combatFrames++;
            if (DateTime.UtcNow < endsAt) return;
            Active = false;
            var sorted = frames.OrderBy(v => v).ToArray();
            double seconds = (Stopwatch.GetTimestamp() - startedAt) / (double)Stopwatch.Frequency;
            var report = new
            {
                SampleId = sampleId,
                Seconds = seconds, Frames = frames.Count, FramesPerSecond = frames.Count / seconds,
                CombatFrames = combatFrames,
                MeanFrameMilliseconds = frames.Average(),
                P95FrameMilliseconds = sorted[(int)Math.Min(sorted.Length - 1, Math.Floor(sorted.Length * .95))],
                MaximumFrameMilliseconds = sorted.Last(), Application.isFocused,
                QualitySettings.vSyncCount, Application.targetFrameRate,
                EngineTimeScale = Time.timeScale, Kingmaker.Game.Instance.TimeController.PlayerTimeScale,
                Area = Kingmaker.Game.Instance.CurrentlyLoadedArea?.name,
                Mode = Kingmaker.Game.Instance.CurrentMode.ToString(),
                NestedTimings = true,
                Mod = counters.Select(p => new
                {
                    Name = p.Key, p.Value.Calls,
                    TotalMilliseconds = p.Value.Ticks * 1000d / Stopwatch.Frequency,
                    MillisecondsPerFrame = p.Value.Ticks * 1000d / Stopwatch.Frequency / frames.Count,
                    MaximumCallMilliseconds = p.Value.Maximum * 1000d / Stopwatch.Frequency
                }).OrderByDescending(p => p.TotalMilliseconds).ToArray()
            };
            File.WriteAllText(Path.Combine(Main.modInfo.Path, "synthesist-performance.json"), JsonConvert.SerializeObject(report, Formatting.Indented));
        }
    }
}
#endif
