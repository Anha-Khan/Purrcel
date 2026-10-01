using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CatCourier.Editor
{
    /// <summary>
    /// Day 6 release/content-honesty validation. Read-only: it inspects assets, scenes, project
    /// settings and repository text, and reports PASS / WARN / BLOCKER per check.
    ///
    /// Design rules, in order of importance:
    /// 1. Never fabricate content. Nothing here authors a chunk prefab, clip, breed or screenshot.
    /// 2. Never throw for missing content. Work that is intentionally not authored yet is reported
    ///    as a BLOCKER finding, so a half-finished project still produces a readable report.
    /// 3. Never write project files. No asset creation, no scene mutation, no .meta churn.
    ///
    /// Public API:
    ///   ReleaseReadinessValidator.Validate()            -> ReleaseReadinessReport (callable, headless-safe)
    ///   ReleaseReadinessValidator.ValidateToConsole()   -> logs the plain-text report
    ///   ReleaseReadinessValidator.ThrowIfBlocked(report)-> explicit opt-in submission gate
    /// Menu: Purrcel > Validate > ...
    /// </summary>
    public static partial class ReleaseReadinessValidator
    {
        public const string ScenesPath = "Assets/_Project/Scenes";
        public const string ChunkCatalogPath = "Assets/_Project/Config/ChunkCatalog.asset";
        public const string RevenueCatConfigPath = "Assets/_Project/Config/RevenueCatConfig.asset";
        public const string AnimatorControllerPath = "Assets/_Project/Animations/CatPlaceholder.controller";
        public const string InputActionsPath = "Assets/_Project/Input/CatCourierControls.inputactions";

        /// <summary>Purrcel unlock distance thresholds that RunLoadoutService applies per district.</summary>
        public const float HarbourReachDistance = 600f;
        public const float SuburbsReachDistance = 1200f;

        [MenuItem("Purrcel/Validate/Release Readiness", priority = 20)]
        private static void MenuValidate()
        {
            ValidateToConsole(true);
        }

        [MenuItem("Purrcel/Validate/Log Release Readiness (no dialog)", priority = 21)]
        private static void MenuValidateToConsole()
        {
            ValidateToConsole(false);
        }

        [MenuItem("Purrcel/Validate/Release Gate (throws on blockers)", priority = 22)]
        private static void MenuReleaseGate()
        {
            var report = Validate();
            Debug.Log(report.ToPlainText());
            ThrowIfBlocked(report);
        }

        /// <summary>
        /// Parameterless entry point for <c>-executeMethod</c>, which cannot pass arguments.
        /// Prints the report to the log and writes it next to the project as release-readiness.md.
        /// Absent content is reported, never thrown, so this always exits cleanly.
        /// </summary>
        public static void ValidateToLog()
        {
            var report = ValidateToConsole(false);
            var outputPath = Path.Combine(ProjectRoot, "Logs", "release-readiness.md");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, report.ToMarkdown());
            Debug.Log($"Release readiness report written to {outputPath}");
        }

        /// <summary>
        /// Runs every Day 6 check and returns the collected findings. Safe to call from CI or
        /// -batchmode; it never opens a dialog and never throws for absent content.
        /// </summary>
        public static ReleaseReadinessReport Validate()
        {
            var report = new ReleaseReadinessReport();

            Run(report, ScenesCheck, () => CheckScenes(report));
            Run(report, BuildSettingsCheck, () => CheckBuildSettings(report));
            var coverage = Run(report, CatalogCheck, () => CheckChunkCatalog(report), new CatalogCoverage());
            Run(report, PaidDistrictsCheck, () => CheckPaidDistricts(report, coverage));
            var breeds = Run(report, PaidBreedsCheck, () => CheckPaidBreeds(report), new List<BreedAsset>());
            Run(report, VisibilityCheck, () => CheckPaidContentVisibility(report, coverage, breeds));
            Run(report, AudioCheck, () => CheckAudio(report, coverage));
            Run(report, InputCheck, () => CheckInput(report));
            Run(report, AnimatorCheck, () => CheckAnimatorContract(report));
            Run(report, TagLayerCheck, () => CheckTagsAndLayers(report));
            Run(report, SecretCheck, () => CheckSecretConfigAndIgnores(report));
            Run(report, ReadmeCheck, () => CheckReadme(report));
            return report;
        }

        /// <summary>
        /// Runs one check and turns an unexpected editor exception into a WARN finding, so a broken
        /// check can never abort the report or hide the other findings.
        /// </summary>
        private static void Run(ReleaseReadinessReport report, string check, System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception exception)
            {
                report.Add(check, ReleaseSeverity.Warn,
                    $"Check '{check}' did not complete: {exception.GetType().Name}.",
                    exception.Message);
            }
        }

        private static T Run<T>(ReleaseReadinessReport report, string check, System.Func<T> action, T fallback)
        {
            try
            {
                return action();
            }
            catch (System.Exception exception)
            {
                report.Add(check, ReleaseSeverity.Warn,
                    $"Check '{check}' did not complete: {exception.GetType().Name}.",
                    exception.Message);
                return fallback;
            }
        }

        /// <summary>Runs <see cref="Validate"/>, logs the plain-text report, and optionally shows a dialog.</summary>
        public static ReleaseReadinessReport ValidateToConsole(bool showDialog = true)
        {
            var report = Validate();
            var plainText = report.ToPlainText();

            if (report.HasBlockers)
            {
                Debug.LogWarning(plainText);
            }
            else
            {
                Debug.Log(plainText);
            }

            if (showDialog && !Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Purrcel release readiness",
                    $"Result: {report.Overall}\n\n" +
                    $"PASS: {report.PassCount}\nWARN: {report.WarnCount}\nBLOCKER: {report.BlockerCount}\n\n" +
                    "Full report is in the Console. BLOCKER entries are missing or unfinished content, " +
                    "not errors in this validator. Nothing was created or modified.",
                    "OK");
            }

            return report;
        }

        /// <summary>
        /// Opt-in submission gate. Only call this when a human explicitly wants a build or submission
        /// to stop on BLOCKER findings; <see cref="Validate"/> itself never throws.
        /// </summary>
        public static void ThrowIfBlocked(ReleaseReadinessReport report)
        {
            if (report == null || !report.HasBlockers)
            {
                return;
            }

            var blocked = report.WithSeverity(ReleaseSeverity.Blocker);
            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"Release readiness is {report.Overall}: {blocked.Count} blocker(s).");
            foreach (var finding in blocked)
            {
                lines.AppendLine($"  [{finding.Check}] {finding.Summary}");
            }

            throw new BuildFailedException(lines.ToString().TrimEnd());
        }

        /// <summary>Absolute project root (the folder that contains Assets/ and ProjectSettings/).</summary>
        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }
}
