using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CatCourier.Editor
{
    /// <summary>
    /// Severity of a single Day 6 release-readiness finding.
    /// <see cref="Blocker"/> means "not shippable yet"; it is a report entry, never an exception,
    /// so intentionally absent work from either team member still produces a readable report.
    /// </summary>
    public enum ReleaseSeverity
    {
        Pass = 0,
        Warn = 1,
        Blocker = 2
    }

    /// <summary>One observation from one check. Never implies content was created or fixed.</summary>
    public sealed class ReleaseFinding
    {
        public ReleaseFinding(string check, ReleaseSeverity severity, string summary, string detail = null)
        {
            Check = string.IsNullOrEmpty(check) ? "General" : check;
            Severity = severity;
            Summary = summary ?? string.Empty;
            Detail = detail;
        }

        public string Check { get; }

        public ReleaseSeverity Severity { get; }

        public string Summary { get; }

        /// <summary>Optional extra context, such as which entries or assets were inspected.</summary>
        public string Detail { get; }

        public string Label => Severity.ToString().ToUpperInvariant();

        public override string ToString() => $"{Check}: [{Label}] {Summary}";
    }

    /// <summary>
    /// Collects every Day 6 finding. The report is a value, not an exception channel: callers decide
    /// what to do with <see cref="BlockerCount"/> (log it, show it, or gate a submission on it).
    /// </summary>
    public sealed class ReleaseReadinessReport
    {
        private readonly List<ReleaseFinding> findings = new();

        public IReadOnlyList<ReleaseFinding> Findings => findings;

        public int PassCount => Count(ReleaseSeverity.Pass);

        public int WarnCount => Count(ReleaseSeverity.Warn);

        public int BlockerCount => Count(ReleaseSeverity.Blocker);

        public bool HasBlockers => BlockerCount > 0;

        /// <summary>PASS when nothing is missing, WARN when only non-shipping issues remain, otherwise BLOCKED.</summary>
        public string Overall => BlockerCount > 0 ? "BLOCKED" : WarnCount > 0 ? "WARN" : "PASS";

        public void Add(ReleaseFinding finding)
        {
            if (finding != null)
            {
                findings.Add(finding);
            }
        }

        public void Add(string check, ReleaseSeverity severity, string summary, string detail = null)
        {
            Add(new ReleaseFinding(check, severity, summary, detail));
        }

        public IReadOnlyList<ReleaseFinding> ForCheck(string check)
        {
            return findings.Where(finding => finding.Check == check).ToList();
        }

        public IReadOnlyList<ReleaseFinding> WithSeverity(ReleaseSeverity severity)
        {
            return findings.Where(finding => finding.Severity == severity).ToList();
        }

        public string ToPlainText()
        {
            var text = new StringBuilder();
            text.AppendLine($"Cat Courier release readiness: {Overall}");
            text.AppendLine($"  pass {PassCount} | warn {WarnCount} | blocker {BlockerCount}");
            text.AppendLine();

            foreach (var group in findings.GroupBy(finding => finding.Check))
            {
                text.AppendLine($"[{group.Key}]");
                foreach (var finding in group)
                {
                    text.AppendLine($"  {finding.Label,-7} {finding.Summary}");
                    if (!string.IsNullOrEmpty(finding.Detail))
                    {
                        text.AppendLine($"          {finding.Detail}");
                    }
                }

                text.AppendLine();
            }

            return text.ToString().TrimEnd();
        }

        public string ToMarkdown()
        {
            var text = new StringBuilder();
            text.AppendLine("# Cat Courier release readiness");
            text.AppendLine();
            text.AppendLine($"**Result: {Overall}** (pass {PassCount}, warn {WarnCount}, blocker {BlockerCount})");
            text.AppendLine();
            text.AppendLine("| Check | Result | Finding |");
            text.AppendLine("| --- | --- | --- |");
            foreach (var finding in findings)
            {
                text.AppendLine($"| {finding.Check} | {finding.Label} | {Escape(finding.Summary)} |");
            }

            return text.ToString().TrimEnd();
        }

        private int Count(ReleaseSeverity severity)
        {
            return findings.Count(finding => finding.Severity == severity);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("|", "\\|");
        }
    }
}
