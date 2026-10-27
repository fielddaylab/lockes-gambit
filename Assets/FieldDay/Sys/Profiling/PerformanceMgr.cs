#if (UNITY_EDITOR && !IGNORE_UNITY_EDITOR) || DEVELOPMENT_BUILD
#define DEVELOPMENT
#endif

using System;
using System.Diagnostics;
using System.Text;
using BeauPools;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay.Debugging;
using FieldDay.HID;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace FieldDay.Perf {
    public sealed class PerformanceMgr {
        private const int TimingBufferSize =
#if DEVELOPMENT
            60;
#else
            1;
#endif // UNITY_EDITOR

        private const int MetricBufferSize =
#if DEVELOPMENT
            16;
#else
            8;
#endif // UNITY_EDITOR

        private const int MeterBufferSize =
#if DEVELOPMENT
            120;
#else
            1;
#endif // UNITY_EDITOR

        private struct ResourceUsageMeter {
            public long HighWatermark;
            public RingBuffer<long> Buffer;

            public void Create() {
                Buffer = new RingBuffer<long>(MeterBufferSize, RingBufferMode.Overwrite);
            }

            public void Tick(long value, bool adjustHighWatermark) {
                Buffer.PushBack(value);
                if (adjustHighWatermark && value > HighWatermark) {
                    HighWatermark = value;
                }
            }

            public long Current() {
                if (Buffer.Count > 0) {
                    return Buffer.PeekBack();
                }
                return 0;
            }
        }

        private struct ActiveMetric {
            public PerfMetric Metric;
            public ProfilerRecorder Recorder;
            public ushort LastAccess;
        }

        private readonly RingBuffer<PhaseTimingData> m_TimingBuffer;
        private readonly RingBuffer<ActiveMetric> m_TemporaryMetrics;
        private readonly RingBuffer<ActiveMetric> m_ResidentMetrics;

        private ResourceUsageMeter m_MemoryMeter;
        private ResourceUsageMeter m_TexMemoryMeter;
        private ResourceUsageMeter m_GPUMemoryMeter;
        private ResourceUsageMeter m_FrameTimeMeter;
        private long m_LastFrameTS;
        private PerfBoostMode m_BoostMode;

        private PerformanceBudget m_PerformanceBudget;

        internal PerformanceMgr() {
            PerfMetric.Initialize();

            m_TimingBuffer = new RingBuffer<PhaseTimingData>(TimingBufferSize, RingBufferMode.Overwrite);
            m_TemporaryMetrics = new RingBuffer<ActiveMetric>(MetricBufferSize, RingBufferMode.Expand);
            m_ResidentMetrics = new RingBuffer<ActiveMetric>(MetricBufferSize, RingBufferMode.Expand);

#if DEVELOPMENT
            m_MemoryMeter.Create();
            m_FrameTimeMeter.Create();
            m_GPUMemoryMeter.Create();
            m_TexMemoryMeter.Create();
            GameLoop.OnDebugUpdate.Register(OnDebugUpdate);
            GameLoop.OnFrameAdvance.Register(OnFrameAdvance);
#endif // DEVELOPMENT

            if (PerfUtility.IsSecureContext()) {
                UnityEngine.Debug.Log("[PerformanceMgr] Running in a secure context!");
            } else {
                UnityEngine.Debug.LogWarning("[PerformanceMgr] Not running in a secure context");
            }

            m_LastFrameTS = Stopwatch.GetTimestamp();
        }

        internal void Initialize(PerformanceBudget budget) {
            m_PerformanceBudget = budget;
        }

        internal void Shutdown() {
            for(int i = m_TemporaryMetrics.Count; i-- > 0;) {
                m_TemporaryMetrics[i].Recorder.Dispose();
            }
            m_TemporaryMetrics.Clear();

            for (int i = m_ResidentMetrics.Count; i-- > 0;) {
                m_ResidentMetrics[i].Recorder.Dispose();
            }
            m_ResidentMetrics.Clear();

            m_TimingBuffer.Clear();

            PerfMetric.Shutdown();
            m_PerformanceBudget = null;
        }

#if DEVELOPMENT
        internal void OnFrameAdvance() {
            long nowTS = Stopwatch.GetTimestamp();
            long ticksPassed = nowTS - m_LastFrameTS;
            m_LastFrameTS = nowTS;
            m_FrameTimeMeter.Tick(ticksPassed, m_BoostMode == PerfBoostMode.Off && Time.frameCount > 5);

            long allocated = PerfUtility.GetTotalAllocatedMemory();
            m_MemoryMeter.Tick(allocated, true);

            ulong texMemory = PerfUtility.GetTotalAllocatedTextureMemory();
            m_TexMemoryMeter.Tick((long) texMemory, true);

            long gpuMemory = PerfUtility.GetTotalAllocatedGPUMemory();
            m_GPUMemoryMeter.Tick(gpuMemory, true);
        }
#endif // DEVELOPMENT

#if DEVELOPMENT
        private unsafe void OnDebugUpdate() {

            if (DebugFlags.IsFlagSet(DebuggingFlags.DisplayLastFrameStats) && m_TimingBuffer.Count > 0) {
                int frameIndex = Math.Max(0, m_TimingBuffer.Count - s_FrameSeek);
                PhaseTimingData timingData = m_TimingBuffer[frameIndex];

                uint totalTicks = timingData.TotalDuration;
                double desiredFrameDuration = PerfUtility.TargetFrameDurationMS();
                double frameDuration = Profiling.TicksToMillisecs(totalTicks);
                double framePerf = frameDuration / desiredFrameDuration;

                if (totalTicks > 0) {
                    Color color = Color.white;
                    if (framePerf <= 0.85) {
                        color = Color.green;
                    } else if (framePerf >= 1.25) {
                        color = Color.red;
                    } else if (framePerf >= 1.01) {
                        color = Color.yellow;
                    }

                    using (var psb = PooledStringBuilder.CreateLarge()) {

#if UNITY_WEBGL
                        if (!PerfUtility.IsSecureContext()) {
                            psb.Builder.Append("<color=#FFF600>!! CONTEXT IS NOT SECURE\n!! TIMINGS MAY BE INACCURATE</color>\n\n");
                        }
#endif // UNITY_WEBGL

                        psb.Builder.Append("Frame -").AppendNoAlloc(m_TimingBuffer.Count - frameIndex).Append(":\t")
                            .AppendNoAlloc(frameDuration, 2).Append("ms\t");
                        psb.Builder.AppendNoAlloc(100.0 * framePerf, 1).Append("%") 
                            .Append("\n");

                        for (int i = 0; i < PhaseBuckets.MaxBuckets; i++) {
                            double microsecs = Profiling.TicksToMicrosecs(timingData.Duration[i]);
                            double percent = 100 * timingData.Duration[i] / (double)totalTicks;
                            psb.Builder.Append("  ").Append(s_PhaseStrings[i]).Append(":\t").AppendNoAlloc(microsecs, 1).Append("us\t")
                                .AppendNoAlloc(percent, 1).Append("%\n");
                        }
                        psb.Builder.Length -= 1;

                        DebugDraw.AddLogText(psb.Builder, color);
                    }
                } else {
                    DebugDraw.AddLogText("Frame INVALID", Color.red);
                }
            }

            if (DebugInput.IsPressed(InputModifierKeys.Shift, KeyCode.L)) {
                DebugFlags.ToggleFlag(DebuggingFlags.DisplayAlerts);
                if (!DebugDraw.IsRenderingEnabled()) {
                    DebugDraw.EnableRendering();
                }
            }

            if (DebugFlags.IsFlagSet(DebuggingFlags.DisplayAlerts)) {
#if !UNITY_EDITOR
                long texMemory = (long)PerfUtility.GetTotalAllocatedTextureMemory();
                long gpuMemory = (long)PerfUtility.GetTotalAllocatedGPUMemory();
                long cpuMemory = (long)PerfUtility.GetTotalAllocatedMemory();
#if !UNITY_EDITOR && UNITY_WEBGL
                long heapSize = SystemInfo.systemMemorySize * (long)Unsafe.MiB;
#endif // !UNITY_EDITOR && UNITY_WEBGL

                if (m_PerformanceBudget != null) {
                    long maxTexMemory = m_PerformanceBudget.MaxTextureMemoryMB * (long)Unsafe.MiB;
                    long maxGPUMemory = m_PerformanceBudget.MaxGPUMemoryMB * (long)Unsafe.MiB;
                    long maxCPUMemory = m_PerformanceBudget.MaxCPUMemoryMB * (long)Unsafe.MiB;

                    using (PooledStringBuilder psb = PooledStringBuilder.CreateLarge()) {
                        double texPercent = (double)texMemory / maxTexMemory;
                        double gpuPercent = (double)gpuMemory / maxGPUMemory;
                        double cpuPercent = (double)cpuMemory / maxCPUMemory;

#if !UNITY_EDITOR && UNITY_WEBGL
                        double heapPercent = (double)heapSize / maxCPUMemory;

                        if (heapPercent > 1) {
                            psb.Builder.Append("WASM Heap size exceeds budget by ");
                            Unsafe.FormatBytes(heapSize - maxCPUMemory, psb);
                            psb.Builder.Append(" !!\n");
                        }
#endif // !UNITY_EDITOR && UNITY_WEBGL

                        if (texPercent > 1) {
                            psb.Builder.Append("Texture memory exceeds budget by ");
                            Unsafe.FormatBytes(texMemory - maxTexMemory, psb);
                            psb.Builder.Append(" !!\n");
                        }

                        if (gpuPercent > 1) {
                            psb.Builder.Append("GPU memory exceeds budget by ");
                            Unsafe.FormatBytes(gpuMemory - maxGPUMemory, psb);
                            psb.Builder.Append(" !!\n");
                        }

                        if (cpuPercent > 1) {
                            psb.Builder.Append("CPU memory exceeds budget by ");
                            Unsafe.FormatBytes(cpuMemory - maxCPUMemory, psb);
                            psb.Builder.Append(" !!\n");
                        }

                        if (psb.Builder.Length > 0) {
                            psb.Builder.TrimEnd(StringUtils.DefaultNewLineChars);
                            DebugDraw.AddViewportText(new Vector2(1, 1), new Vector2(-8, -160), psb, Color.red, 0, TextAnchor.UpperRight, DebugTextStyle.BackgroundDarkOpaque);
                        }
                    }
                }
#else
                
#endif // UNITY_EDITOR
            }
        }
#endif // DEVELOPMENT

        #region Timing

        internal unsafe void RecordTiming(in PhaseTiming timing) {
            PhaseTimingData timingData;
            uint totalAccum = 0;
            for(int i = 0; i < PhaseBuckets.MaxBuckets; i++) {
                totalAccum += (timingData.Duration[i] = (uint) Math.Min(timing.Duration[i], uint.MaxValue));
            }
            timingData.TotalDuration = totalAccum;
            m_TimingBuffer.PushBack(timingData);
        }

        #endregion // Timing

        #region Boost

        public PerfBoostMode GetBoostMode() {
            return m_BoostMode;
        }

        public void SetBoostMode(PerfBoostMode boostMode) {
            if (boostMode != m_BoostMode) {
                m_BoostMode = boostMode;
                Log.Msg("[PerformanceMgr] Boost mode set to {0}", boostMode == PerfBoostMode.Boost ? "ON" : "OFF");
                Application.backgroundLoadingPriority = boostMode == PerfBoostMode.Boost ? ThreadPriority.Normal : ThreadPriority.Low;
                // TODO: adjust settings to speed up loading time
            }
        }

        #endregion // Boost

        #region Debugging

        public enum DebuggingFlags {
            DisplayLastFrameStats,
            DisplayMemoryGraph,
            DisplayFrameGraph,
            DisplayAlerts
        }

#if DEVELOPMENT

        static private int s_FrameSeek = 1;

        static private readonly int[] s_Framerates = new int[] {
            -1,
            20,
            24,
            30,
            40,
            60,
            90,
            120
        };

        static private readonly string[] s_FramerateStrings = new string[] {
            "Automatic",
            "20",
            "24",
            "30",
            "40",
            "60",
            "90",
            "120"
        };

        static private readonly string[] s_PhaseStrings = new string[] {
            "DbgUpd", "PreUpd", "FixedUpd", "LateFixedUpd",
            "Upd", "UnscUpd", "LateUpd", "UnscLateUpd",
            "CanvasPre", "RenderPre", "PreCull", "PreRender", "PostRender", "FrameAdv"
        };

        [EngineMenuFactory]
        static private DMInfo CreateDebugInfo() {
            DMInfo info = new DMInfo("Performance");
            info.AddSelector("Target Framerate",
                () => Application.targetFrameRate,
                (i) => GameLoop.SetTargetFramerate(i),
                s_Framerates, s_FramerateStrings);

            info.AddDivider();

            DMInfo metrics = new DMInfo("Metrics");

            metrics.AddButton("Dump Available Metrics", () => {
                using (Log.DisableMsgStackTrace()) {
                    int count = 0;
                    Log.Msg("[PerformanceMgr] Enumerating metrics...");
                    foreach (var metric in PerfMetric.EnumerateAvailableMetrics()) {
                        Log.Msg("{0} | {1} ({2}, {3}) [{4}]", metric.Category.Name, metric.Name, metric.DataType.ToString(), metric.UnitType.ToString(), metric.Flags.ToString());
                        count++;
                    }
                    Log.Msg("[PerformanceMgr] Found {0} metrics", count);
                }
            });

            info.AddSubmenu(metrics);

            info.AddDivider();

            DebugFlags.Menu.AddFlagToggle(info, "Display Alerts", DebuggingFlags.DisplayAlerts);
            DebugFlags.Menu.AddFlagToggle(info, "Display Memory Usage Graph", DebuggingFlags.DisplayMemoryGraph);
            DebugFlags.Menu.AddFlagToggle(info, "Display Frame Time Graph", DebuggingFlags.DisplayFrameGraph);

            DebugFlags.Menu.AddFlagToggle(info, "Display Frame Profiling Details", DebuggingFlags.DisplayLastFrameStats);
            info.AddSlider("Frame Selection",
                () => s_FrameSeek,
                (f) => s_FrameSeek = (int) f, 1, TimingBufferSize, 1, "{0}", () => DebugFlags.IsFlagSet(DebuggingFlags.DisplayLastFrameStats), 1);

            return info;
        }

#endif // DEVELOPMENT

        #endregion // Debugging

        #region Metrics

        internal ProfilerRecorder GetRecorder(PerfMetric metric) {
#if DEVELOPMENT
            for (int i = 0; i < m_TemporaryMetrics.Count; i++) {
                ref var metricTracker = ref m_TemporaryMetrics[i];
                if (metricTracker.Metric.HashId == metric.HashId) {
                    metricTracker.LastAccess = Frame.Index;
                    return metricTracker.Recorder;
                }
            }
            ProfilerRecorderHandle handle = PerfMetric.GetHandle(metric);
            if (handle.Valid) {
                ActiveMetric newMetric;
                newMetric.Metric = metric;
                newMetric.LastAccess = Frame.Index;
                newMetric.Recorder = PerfMetric.CreateRecorder(handle);
                newMetric.Recorder.Start();
                m_TemporaryMetrics.PushBack(newMetric);
                return newMetric.Recorder;
            }
#endif // DEVELOPMENT
            return default;
        }

        internal void CleanUpUnusedMetrics() {
#if DEVELOPMENT
            for(int i = m_TemporaryMetrics.Count; i-- > 0;) {
                if (Frame.Age(m_TemporaryMetrics[i].LastAccess) >= 3) {
                    m_TemporaryMetrics[i].Recorder.Dispose();
                    m_TemporaryMetrics.FastRemoveAt(i);
                }
            }
#endif // DEVELOPMENT
        }

        #endregion // Metrics
    }

    public unsafe struct PhaseTimingData {
        public fixed uint Duration[PhaseBuckets.MaxBuckets];
        public uint TotalDuration;
    }

    public enum PerfBoostMode : byte {
        Off,
        Boost
    }

    public partial struct PerfMetric {
        /// <summary>
        /// Requests a value be recorded by the Profiler.
        /// </summary>
        static public ProfilerRecorder Read(PerfMetric metric) {
            return Game.Perf.GetRecorder(metric);
        }

        /// <summary>
        /// Writes a metric and its current value to the given StringBuilder.
        /// </summary>
        static public void WriteMetric(StringBuilder output, PerfMetric metric) {
            output.Append(metric.DisplayName).Append(": ");
            FormatValue(output, Read(metric));
        }

        /// <summary>
        /// Writes a metric and its current value to the given StringBuilder.
        /// Also writes a newline character.
        /// </summary>
        static public void WriteMetricLine(StringBuilder output, PerfMetric metric) {
            WriteMetric(output, metric);
            output.Append('\n');
        }

        /// <summary>
        /// Writes a metric's current value to the given StringBuilder.
        /// </summary>
        static public void WriteMetricValue(StringBuilder output, PerfMetric metric) {
            FormatValue(output, Read(metric));
        }
    }
}