using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Splice.Validation
{
    public enum ContentValidationSeverity { Warning, Error }

    public sealed class ContentValidationIssue
    {
        public ContentValidationSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public Object Context { get; }

        public ContentValidationIssue(ContentValidationSeverity severity, string code, string message, Object context)
        {
            Severity = severity;
            Code = code;
            Message = message;
            Context = context;
        }

        public override string ToString() => $"[{Severity}] {Code}: {Message}";
    }

    public sealed class ContentValidationReport
    {
        private readonly List<ContentValidationIssue> issues = new();
        public IReadOnlyList<ContentValidationIssue> Issues => issues;
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public bool IsValid => ErrorCount == 0;
        public int FactionRegistryCount { get; internal set; }
        public int FactionCount { get; internal set; }
        public int CardCount { get; internal set; }
        public int TowerCount { get; internal set; }
        public int MonsterCount { get; internal set; }
        public int MinerCount { get; internal set; }
        public int HeroRegistryCount { get; internal set; }
        public int HeroCount { get; internal set; }

        public void Error(string code, string message, Object context = null)
        {
            issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, code, message, context));
            ErrorCount++;
        }

        public void Warning(string code, string message, Object context = null)
        {
            issues.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, code, message, context));
            WarningCount++;
        }

        public string Summary() =>
            $"Content Validator: {(IsValid ? "PASS" : "FAIL")} | Errors {ErrorCount}, Warnings {WarningCount} | " +
            $"Registries {FactionRegistryCount}, Factions {FactionCount}, Cards {CardCount}, Towers {TowerCount}, " +
            $"Monsters {MonsterCount}, Miners {MinerCount}, Hero registries {HeroRegistryCount}, Heroes {HeroCount}";

        public string DetailedSummary()
        {
            var builder = new StringBuilder(Summary());
            foreach (var issue in issues) builder.AppendLine().Append(issue);
            return builder.ToString();
        }
    }
}
