using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;

// TAG: #SECURITY_CRITICAL #METRICS #QUALITY_ASSURANCE #VERSION_2_0
namespace NecessaryAdminTool.SecurityTests
{
    /// <summary>
    /// Calculates overall security score based on test results
    /// Provides category-specific scoring and regression detection
    /// TAG: #SECURITY_METRICS #TEST_REPORTING #CI_CD
    /// </summary>
    public class SecurityScoreCalculator
    {
        private const double MINIMUM_PASSING_SCORE = 90.0;

        public class SecurityScoreReport
        {
            public double OverallScore { get; set; }
            public Dictionary<string, CategoryScore> CategoryScores { get; set; }
            public int TotalTests { get; set; }
            public int PassedTests { get; set; }
            public int FailedTests { get; set; }
            public int SkippedTests { get; set; }
            public TimeSpan TotalExecutionTime { get; set; }
            public List<string> FailedTestNames { get; set; }
            public bool MeetsMinimumScore { get; set; }
            public DateTime GeneratedAt { get; set; }
            public List<string> Recommendations { get; set; }

            public SecurityScoreReport()
            {
                CategoryScores = new Dictionary<string, CategoryScore>();
                FailedTestNames = new List<string>();
                Recommendations = new List<string>();
                GeneratedAt = DateTime.UtcNow;
            }
        }

        public class CategoryScore
        {
            public string CategoryName { get; set; }
            public double Score { get; set; }
            public int TotalTests { get; set; }
            public int PassedTests { get; set; }
            public int FailedTests { get; set; }
            public string RiskLevel { get; set; }

            public CategoryScore(string name)
            {
                CategoryName = name;
            }
        }

        /// <summary>
        /// Calculate security score from test results
        /// </summary>
        public static SecurityScoreReport CalculateScore(ITestResult testResult)
        {
            var report = new SecurityScoreReport();

            // Collect all test results
            var allResults = CollectAllResults(testResult);
            report.TotalTests = allResults.Count;
            report.PassedTests = allResults.Count(r => r.ResultState.Status == TestStatus.Passed);
            report.FailedTests = allResults.Count(r => r.ResultState.Status == TestStatus.Failed);
            report.SkippedTests = allResults.Count(r => r.ResultState.Status == TestStatus.Skipped);
            report.TotalExecutionTime = TimeSpan.FromSeconds(testResult.Duration);

            // Calculate overall score
            if (report.TotalTests > 0)
            {
                report.OverallScore = (double)report.PassedTests / report.TotalTests * 100.0;
            }

            // Collect failed test names
            report.FailedTestNames = allResults
                .Where(r => r.ResultState.Status == TestStatus.Failed)
                .Select(r => r.FullName)
                .ToList();

            // Calculate category scores
            report.CategoryScores = CalculateCategoryScores(allResults);

            // Check if meets minimum score
            report.MeetsMinimumScore = report.OverallScore >= MINIMUM_PASSING_SCORE;

            // Generate recommendations
            report.Recommendations = GenerateRecommendations(report);

            return report;
        }

        /// <summary>
        /// Collect all test results recursively
        /// </summary>
        private static List<ITestResult> CollectAllResults(ITestResult result)
        {
            var results = new List<ITestResult>();

            if (result.HasChildren)
            {
                foreach (var child in result.Children)
                {
                    results.AddRange(CollectAllResults(child));
                }
            }
            else
            {
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Calculate scores for each security category
        /// </summary>
        private static Dictionary<string, CategoryScore> CalculateCategoryScores(List<ITestResult> allResults)
        {
            var categories = new Dictionary<string, CategoryScore>
            {
                { "PowerShell Security", new CategoryScore("PowerShell Security") },
                { "LDAP Security", new CategoryScore("LDAP Security") },
                { "Path Security", new CategoryScore("Path Security") },
                { "Command Injection", new CategoryScore("Command Injection") },
                { "Authentication", new CategoryScore("Authentication") },
                { "Input Validation", new CategoryScore("Input Validation") },
                { "Attack Vectors", new CategoryScore("Attack Vectors") },
                { "Integration", new CategoryScore("Integration") }
            };

            foreach (var result in allResults)
            {
                var category = DetermineCategory(result.FullName);
                if (category != null && categories.ContainsKey(category))
                {
                    categories[category].TotalTests++;
                    if (result.ResultState.Status == TestStatus.Passed)
                    {
                        categories[category].PassedTests++;
                    }
                    else if (result.ResultState.Status == TestStatus.Failed)
                    {
                        categories[category].FailedTests++;
                    }
                }
            }

            // Calculate scores and risk levels
            foreach (var category in categories.Values)
            {
                if (category.TotalTests > 0)
                {
                    category.Score = (double)category.PassedTests / category.TotalTests * 100.0;
                    category.RiskLevel = DetermineRiskLevel(category.Score);
                }
            }

            return categories;
        }

        /// <summary>
        /// Determine which category a test belongs to based on its name
        /// </summary>
        private static string DetermineCategory(string testFullName)
        {
            if (testFullName.Contains("PowerShell"))
                return "PowerShell Security";
            if (testFullName.Contains("LDAP"))
                return "LDAP Security";
            if (testFullName.Contains("Path") || testFullName.Contains("File"))
                return "Path Security";
            if (testFullName.Contains("Command") || testFullName.Contains("Injection"))
                return "Command Injection";
            if (testFullName.Contains("Authentication") || testFullName.Contains("RateLimit"))
                return "Authentication";
            if (testFullName.Contains("Validate") || testFullName.Contains("Sanitize"))
                return "Input Validation";
            if (testFullName.Contains("Attack"))
                return "Attack Vectors";
            if (testFullName.Contains("Integration"))
                return "Integration";

            return "General";
        }

        /// <summary>
        /// Determine risk level based on category score
        /// </summary>
        private static string DetermineRiskLevel(double score)
        {
            if (score >= 95) return "LOW";
            if (score >= 85) return "MEDIUM";
            if (score >= 70) return "HIGH";
            return "CRITICAL";
        }

        /// <summary>
        /// Generate security recommendations based on test results
        /// </summary>
        private static List<string> GenerateRecommendations(SecurityScoreReport report)
        {
            var recommendations = new List<string>();

            // Overall score recommendations
            if (report.OverallScore < MINIMUM_PASSING_SCORE)
            {
                recommendations.Add($"CRITICAL: Overall security score ({report.OverallScore:F1}%) is below minimum threshold ({MINIMUM_PASSING_SCORE}%)");
                recommendations.Add("Action Required: Review and fix all failed security tests before deployment");
            }

            // Category-specific recommendations
            foreach (var category in report.CategoryScores.Values)
            {
                if (category.RiskLevel == "CRITICAL")
                {
                    recommendations.Add($"CRITICAL: {category.CategoryName} has critical vulnerabilities (Score: {category.Score:F1}%)");
                    recommendations.Add($"  - {category.FailedTests} of {category.TotalTests} tests failed in this category");
                }
                else if (category.RiskLevel == "HIGH")
                {
                    recommendations.Add($"WARNING: {category.CategoryName} has high risk (Score: {category.Score:F1}%)");
                }
            }

            // Specific failed test recommendations
            if (report.FailedTests > 0)
            {
                recommendations.Add($"Failed Tests: {report.FailedTests} security tests failed");
                if (report.FailedTestNames.Count <= 10)
                {
                    foreach (var failedTest in report.FailedTestNames)
                    {
                        recommendations.Add($"  - {failedTest}");
                    }
                }
                else
                {
                    recommendations.Add($"  - (showing first 10 of {report.FailedTestNames.Count} failures)");
                    foreach (var failedTest in report.FailedTestNames.Take(10))
                    {
                        recommendations.Add($"  - {failedTest}");
                    }
                }
            }

            // Performance recommendations
            if (report.TotalExecutionTime.TotalSeconds > 5.0)
            {
                recommendations.Add($"PERFORMANCE: Test execution time ({report.TotalExecutionTime.TotalSeconds:F2}s) exceeds target (5s)");
                recommendations.Add("Consider optimizing slow tests for CI/CD pipeline");
            }

            // Positive feedback
            if (report.OverallScore >= 95)
            {
                recommendations.Add("EXCELLENT: Security posture is strong (95%+ pass rate)");
            }
            else if (report.OverallScore >= MINIMUM_PASSING_SCORE)
            {
                recommendations.Add($"GOOD: Security score meets minimum requirements ({report.OverallScore:F1}% >= {MINIMUM_PASSING_SCORE}%)");
            }

            return recommendations;
        }

        /// <summary>
        /// Generate detailed text report
        /// </summary>
        public static string GenerateTextReport(SecurityScoreReport report)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("=" + new string('=', 78));
            sb.AppendLine("  SECURITY TEST REPORT");
            sb.AppendLine("=" + new string('=', 78));
            sb.AppendLine();
            sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();

            // Overall Score
            sb.AppendLine("OVERALL SECURITY SCORE");
            sb.AppendLine("-" + new string('-', 78));
            sb.AppendLine($"Score: {report.OverallScore:F2}%");
            sb.AppendLine($"Status: {(report.MeetsMinimumScore ? "PASS" : "FAIL")} (Minimum: {MINIMUM_PASSING_SCORE}%)");
            sb.AppendLine();

            // Test Summary
            sb.AppendLine("TEST SUMMARY");
            sb.AppendLine("-" + new string('-', 78));
            sb.AppendLine($"Total Tests:   {report.TotalTests}");
            sb.AppendLine($"Passed:        {report.PassedTests} ({(report.TotalTests > 0 ? (double)report.PassedTests / report.TotalTests * 100 : 0):F1}%)");
            sb.AppendLine($"Failed:        {report.FailedTests} ({(report.TotalTests > 0 ? (double)report.FailedTests / report.TotalTests * 100 : 0):F1}%)");
            sb.AppendLine($"Skipped:       {report.SkippedTests}");
            sb.AppendLine($"Execution Time: {report.TotalExecutionTime.TotalSeconds:F2}s");
            sb.AppendLine();

            // Category Scores
            sb.AppendLine("CATEGORY SCORES");
            sb.AppendLine("-" + new string('-', 78));
            sb.AppendLine($"{"Category",-30} {"Score",-10} {"Pass/Total",-15} {"Risk Level",-15}");
            sb.AppendLine(new string('-', 78));

            foreach (var category in report.CategoryScores.Values.OrderBy(c => c.Score))
            {
                sb.AppendLine($"{category.CategoryName,-30} {category.Score:F1}%{"",-6} {category.PassedTests}/{category.TotalTests}{"",-10} {category.RiskLevel,-15}");
            }
            sb.AppendLine();

            // Recommendations
            if (report.Recommendations.Any())
            {
                sb.AppendLine("RECOMMENDATIONS");
                sb.AppendLine("-" + new string('-', 78));
                foreach (var recommendation in report.Recommendations)
                {
                    sb.AppendLine(recommendation);
                }
                sb.AppendLine();
            }

            sb.AppendLine("=" + new string('=', 78));

            return sb.ToString();
        }

        /// <summary>
        /// Check for regression (score decrease from baseline)
        /// </summary>
        public static bool CheckRegression(double currentScore, double baselineScore, double tolerance = 1.0)
        {
            return currentScore < (baselineScore - tolerance);
        }

        /// <summary>
        /// Export report to JSON format
        /// </summary>
        public static string ExportToJson(SecurityScoreReport report)
        {
            var json = new System.Text.StringBuilder();
            json.AppendLine("{");
            json.AppendLine($"  \"generatedAt\": \"{report.GeneratedAt:yyyy-MM-ddTHH:mm:ssZ}\",");
            json.AppendLine($"  \"overallScore\": {report.OverallScore:F2},");
            json.AppendLine($"  \"meetsMinimumScore\": {report.MeetsMinimumScore.ToString().ToLower()},");
            json.AppendLine($"  \"minimumRequiredScore\": {MINIMUM_PASSING_SCORE},");
            json.AppendLine($"  \"totalTests\": {report.TotalTests},");
            json.AppendLine($"  \"passedTests\": {report.PassedTests},");
            json.AppendLine($"  \"failedTests\": {report.FailedTests},");
            json.AppendLine($"  \"skippedTests\": {report.SkippedTests},");
            json.AppendLine($"  \"executionTimeSeconds\": {report.TotalExecutionTime.TotalSeconds:F2},");
            json.AppendLine("  \"categoryScores\": [");

            var categories = report.CategoryScores.Values.ToList();
            for (int i = 0; i < categories.Count; i++)
            {
                var cat = categories[i];
                json.AppendLine("    {");
                json.AppendLine($"      \"name\": \"{cat.CategoryName}\",");
                json.AppendLine($"      \"score\": {cat.Score:F2},");
                json.AppendLine($"      \"totalTests\": {cat.TotalTests},");
                json.AppendLine($"      \"passedTests\": {cat.PassedTests},");
                json.AppendLine($"      \"failedTests\": {cat.FailedTests},");
                json.AppendLine($"      \"riskLevel\": \"{cat.RiskLevel}\"");
                json.Append("    }");
                if (i < categories.Count - 1) json.AppendLine(",");
                else json.AppendLine();
            }

            json.AppendLine("  ],");
            json.AppendLine("  \"recommendations\": [");

            for (int i = 0; i < report.Recommendations.Count; i++)
            {
                json.Append($"    \"{report.Recommendations[i].Replace("\"", "\\\"")}\"");
                if (i < report.Recommendations.Count - 1) json.AppendLine(",");
                else json.AppendLine();
            }

            json.AppendLine("  ]");
            json.AppendLine("}");

            return json.ToString();
        }

        /// <summary>
        /// Get security coverage percentage
        /// </summary>
        public static double GetCoveragePercentage(SecurityScoreReport report)
        {
            // Security coverage based on categories tested
            var targetCategories = 8; // PowerShell, LDAP, Path, Command, Auth, Input, Attack, Integration
            var coveredCategories = report.CategoryScores.Count(c => c.Value.TotalTests > 0);

            return (double)coveredCategories / targetCategories * 100.0;
        }

        /// <summary>
        /// Validate that all critical security areas are tested
        /// </summary>
        public static List<string> ValidateCoverage(SecurityScoreReport report)
        {
            var missingAreas = new List<string>();

            var criticalAreas = new[]
            {
                "PowerShell Security",
                "LDAP Security",
                "Path Security",
                "Command Injection",
                "Authentication"
            };

            foreach (var area in criticalAreas)
            {
                if (!report.CategoryScores.ContainsKey(area) ||
                    report.CategoryScores[area].TotalTests == 0)
                {
                    missingAreas.Add(area);
                }
            }

            return missingAreas;
        }
    }
}
