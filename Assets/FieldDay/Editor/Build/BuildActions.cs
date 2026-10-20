using BeauUtil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditorInternal;

namespace FieldDay.Editor {
    static public class BuildActions {
        static public bool IsAutomated { get; private set; }
        static public bool ManualStepControl { get; private set; }

        static public bool IsBatchMode {
            get {
                return IsAutomated || IsBatchModeRaw;
            }
        }

        static public bool IsBatchModeRaw {
            get {
                return InternalEditorUtility.inBatchMode || !InternalEditorUtility.isHumanControllingUs;
            }
        }

        [MenuItem("Field Day/Testing/Test Automated Build Script")]
        static public void AutomatedBuild() {
            IsAutomated = true;
            ManualStepControl = true;
            try {
                Console.WriteLine("====================\nField Day build process begins here!\n====================");
                BuildActionSettings settings = ExtractSettingsFromCommandLine();
                LocateAndApplyBuildConfiguration();
                ProcessAssets(settings);
                BuildReport report = GeneratePlayerBuild(settings);
                WriteReportToConsole(report);
                ExitWithResult(report.summary.result);
            } finally {
                IsAutomated = false;
                ManualStepControl = false;
            }
        }

        #region Retrieval

        static public BuildActionSettings ExtractSettingsFromCommandLine() {
            BuildActionSettings settings;
            settings.BuildTarget = BuildTarget.WebGL;
            settings.BuildPath = "./build/WebGL/WebGL";

            string[] commandLineArgs = Environment.GetCommandLineArgs();
            for(int i = 0; i < commandLineArgs.Length; i++) {
                if (commandLineArgs[i] == "-customBuildPath") {
                    settings.BuildPath = commandLineArgs[++i];
                } else if (commandLineArgs[i] == "-buildTarget") {
                    settings.BuildTarget = Enum.Parse<BuildTarget>(commandLineArgs[++i]);
                }
            }

            return settings;
        }

        static public BuildPlayerOptions GetPlayerBuildOptions(BuildActionSettings settings) {
            BuildPlayerOptions options = new BuildPlayerOptions();

            options.target = settings.BuildTarget;
            options.scenes = GetBuildScenes();
            options.locationPathName = settings.BuildPath;

            return options;
        }

        static public string[] GetBuildScenes() {
            var editorScenes = EditorBuildSettings.scenes;
            List<string> scenes = new List<string>(editorScenes.Length);
            for (int i = 0; i < editorScenes.Length; i++) {
                var scene = editorScenes[i];
                if (scene.guid != default && scene.enabled) {
                    scenes.Add(scene.path);
                }
            }
            return scenes.ToArray();
        }

        #endregion // Retrieval

        #region Steps

        static public void LocateAndApplyBuildConfiguration() {
            Console.WriteLine("== Locating build configuration ==");
            AdjustSettingsBuildProcessor.Execute();
        }

        static public void ProcessAssets(BuildActionSettings settings) {
            Console.WriteLine("== Processing assets ==");
            BakeAssetsBuildPreprocessor.Execute();
            StripEditorDataBuildPreprocessor.Execute();
            Console.WriteLine("== Building streamed packs ==");
            StreamedPacks.ExecuteCleanRebuildForPlatform(settings.BuildTarget);
        }

        static public BuildReport GeneratePlayerBuild(BuildActionSettings settings) {
            Console.WriteLine("== Building player ==");
            BuildPlayerOptions buildOptions = GetPlayerBuildOptions(settings);
            return BuildPipeline.BuildPlayer(buildOptions);
        }

        static public void WriteReportToConsole(BuildReport report) {
            StringBuilder sb = new StringBuilder(2048);
            BuildSummary summary = report.summary;
            sb.Append("==========\nBuild Results\n==========\n")
                .Append("\nDuration: ").AppendNoAlloc(summary.totalTime.TotalMinutes, 2).Append("min")
                .Append("\nWarnings: ").AppendNoAlloc(summary.totalWarnings)
                .Append("\nErrors: ").AppendNoAlloc(summary.totalErrors)
                .Append("\nSize: ");
            Unsafe.FormatBytes((long)summary.totalSize, sb);
            sb.Append(" (").AppendNoAlloc(summary.totalSize).Append(" bytes)\n\n");
            Console.WriteLine(sb.ToString());
        }

        static public void ExitWithResult(BuildResult result) {
            switch(result) {
                case BuildResult.Succeeded: {
                    Console.WriteLine("Build succeeded!!");
                    TryExit(0);
                    break;
                }
                case BuildResult.Failed: {
                    Console.WriteLine("Build failed :(");
                    TryExit(101);
                    break;
                }
                case BuildResult.Cancelled: {
                    Console.WriteLine("Build was... cancelled?");
                    TryExit(102);
                    break;
                }
                case BuildResult.Unknown:
                default: {
                    Console.WriteLine("Build result is unknown :/");
                    TryExit(103);
                    break;
                }
            }
        }

        static private void TryExit(int exitCode) {
            if (IsBatchModeRaw) {
                EditorApplication.Exit(exitCode);
            }
        }

        #endregion // Steps
    }

    public struct BuildActionSettings {
        public BuildTarget BuildTarget;
        public string BuildPath;
    }
}