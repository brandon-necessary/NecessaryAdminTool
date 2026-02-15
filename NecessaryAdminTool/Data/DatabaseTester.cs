using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// TAG: #DATABASE_TESTING #SETUP_WIZARD #VERSION_1_0

namespace NecessaryAdminTool.Data
{
    /// <summary>
    /// Comprehensive database testing utility for validating IDataProvider implementations
    /// Used during setup wizard to ensure database provider is working correctly
    /// TAG: #DATABASE_TESTING #QUALITY_ASSURANCE
    /// </summary>
    public class DatabaseTester
    {
        private readonly IDataProvider _provider;
        private readonly StringBuilder _log;
        private int _passCount;
        private int _failCount;
        private int _totalTests;

        public DatabaseTester(IDataProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _log = new StringBuilder();
            _passCount = 0;
            _failCount = 0;
            _totalTests = 0;
        }

        /// <summary>
        /// Run all database tests and return detailed results
        /// </summary>
        public async Task<DatabaseTestResult> RunAllTestsAsync()
        {
            _log.Clear();
            _passCount = 0;
            _failCount = 0;
            _totalTests = 0;

            var startTime = DateTime.Now;
            LogHeader("DATABASE PROVIDER TEST SUITE");
            LogInfo($"Provider Type: {_provider.GetType().Name}");
            LogInfo($"Start Time: {startTime:yyyy-MM-dd HH:mm:ss}");
            LogSeparator();

            try
            {
                // Test 1: Connection/Initialization
                await TestInitializationAsync();

                // Test 2: Computer Management
                await TestComputerManagementAsync();

                // Test 3: Tag Management
                await TestTagManagementAsync();

                // Test 4: Scan History
                await TestScanHistoryAsync();

                // Test 5: Settings Management
                await TestSettingsManagementAsync();

                // Test 6: Statistics
                await TestStatisticsAsync();

                // Test 7: Cleanup
                await TestCleanupAsync();
            }
            catch (Exception ex)
            {
                LogError($"FATAL ERROR during testing: {ex.Message}");
                _failCount++;
            }

            var endTime = DateTime.Now;
            var duration = endTime - startTime;

            LogSeparator();
            LogHeader("TEST SUMMARY");
            LogInfo($"Total Tests: {_totalTests}");
            LogInfo($"Passed: {_passCount} ({(_totalTests > 0 ? (_passCount * 100.0 / _totalTests).ToString("F1") : "0")}%)");
            LogInfo($"Failed: {_failCount} ({(_totalTests > 0 ? (_failCount * 100.0 / _totalTests).ToString("F1") : "0")}%)");
            LogInfo($"Duration: {duration.TotalSeconds:F2} seconds");
            LogInfo($"End Time: {endTime:yyyy-MM-dd HH:mm:ss}");

            var result = new DatabaseTestResult
            {
                Success = _failCount == 0,
                TotalTests = _totalTests,
                PassedTests = _passCount,
                FailedTests = _failCount,
                Duration = duration,
                Log = _log.ToString()
            };

            return result;
        }

        #region Test Methods

        private async Task TestInitializationAsync()
        {
            LogSection("INITIALIZATION TESTS");

            // Test: Basic connectivity
            await RunTestAsync("Database Connection", async () =>
            {
                var stats = await _provider.GetDatabaseStatsAsync();
                if (stats == null)
                    throw new Exception("GetDatabaseStatsAsync returned null");
            });
        }

        private async Task TestComputerManagementAsync()
        {
            LogSection("COMPUTER MANAGEMENT TESTS");

            var testHostname = "TEST-COMPUTER-" + Guid.NewGuid().ToString().Substring(0, 8);
            ComputerInfo testComputer = null;

            // Test: Create computer
            await RunTestAsync("Save Computer (Create)", async () =>
            {
                testComputer = new ComputerInfo
                {
                    Hostname = testHostname,
                    IpAddress = "192.168.1.100",
                    OperatingSystem = "Windows 11 Pro",
                    LastSeen = DateTime.Now,
                    IsOnline = true,
                    Manufacturer = "Test Manufacturer",
                    Model = "Test Model",
                    SerialNumber = "TEST-12345"
                };
                await _provider.SaveComputerAsync(testComputer);
            });

            // Test: Retrieve computer
            await RunTestAsync("Get Computer by Hostname", async () =>
            {
                var retrieved = await _provider.GetComputerAsync(testHostname);
                if (retrieved == null)
                    throw new Exception("Computer not found after saving");
                if (retrieved.Hostname != testHostname)
                    throw new Exception($"Hostname mismatch: expected {testHostname}, got {retrieved.Hostname}");
            });

            // Test: Update computer
            await RunTestAsync("Save Computer (Update)", async () =>
            {
                testComputer.OperatingSystem = "Windows 11 Pro Updated";
                testComputer.IpAddress = "192.168.1.101";
                await _provider.SaveComputerAsync(testComputer);

                var updated = await _provider.GetComputerAsync(testHostname);
                if (updated.OperatingSystem != "Windows 11 Pro Updated")
                    throw new Exception("Computer update failed");
            });

            // Test: Get all computers
            await RunTestAsync("Get All Computers", async () =>
            {
                var all = await _provider.GetAllComputersAsync();
                if (all == null)
                    throw new Exception("GetAllComputersAsync returned null");
                if (!all.Any(c => c.Hostname == testHostname))
                    throw new Exception("Test computer not found in GetAllComputersAsync results");
            });

            // Test: Search computers
            await RunTestAsync("Search Computers", async () =>
            {
                var results = await _provider.SearchComputersAsync(testHostname.Substring(0, 10));
                if (results == null || !results.Any())
                    throw new Exception("Search returned no results for test computer");
            });

            // Test: Delete computer
            await RunTestAsync("Delete Computer", async () =>
            {
                await _provider.DeleteComputerAsync(testHostname);
                var deleted = await _provider.GetComputerAsync(testHostname);
                if (deleted != null)
                    throw new Exception("Computer still exists after deletion");
            });
        }

        private async Task TestTagManagementAsync()
        {
            LogSection("TAG MANAGEMENT TESTS");

            var testHostname = "TEST-TAG-COMPUTER-" + Guid.NewGuid().ToString().Substring(0, 8);
            var testTag1 = "TestTag1";
            var testTag2 = "TestTag2";

            // Create test computer
            await _provider.SaveComputerAsync(new ComputerInfo
            {
                Hostname = testHostname,
                IpAddress = "192.168.1.200",
                OperatingSystem = "Windows 11",
                LastSeen = DateTime.Now
            });

            // Test: Add tag
            await RunTestAsync("Add Tag", async () =>
            {
                await _provider.AddTagAsync(testHostname, testTag1);
                var tags = await _provider.GetComputerTagsAsync(testHostname);
                if (!tags.Contains(testTag1))
                    throw new Exception("Tag not found after adding");
            });

            // Test: Add multiple tags
            await RunTestAsync("Add Multiple Tags", async () =>
            {
                await _provider.AddTagAsync(testHostname, testTag2);
                var tags = await _provider.GetComputerTagsAsync(testHostname);
                if (tags.Count < 2)
                    throw new Exception($"Expected at least 2 tags, found {tags.Count}");
            });

            // Test: Get all tags
            await RunTestAsync("Get All Tags", async () =>
            {
                var allTags = await _provider.GetAllTagsAsync();
                if (!allTags.Contains(testTag1) || !allTags.Contains(testTag2))
                    throw new Exception("Not all test tags found in GetAllTagsAsync");
            });

            // Test: Remove tag
            await RunTestAsync("Remove Tag", async () =>
            {
                await _provider.RemoveTagAsync(testHostname, testTag1);
                var tags = await _provider.GetComputerTagsAsync(testHostname);
                if (tags.Contains(testTag1))
                    throw new Exception("Tag still exists after removal");
            });

            // Cleanup
            await _provider.DeleteComputerAsync(testHostname);
        }

        private async Task TestScanHistoryAsync()
        {
            LogSection("SCAN HISTORY TESTS");

            // Test: Save scan history
            await RunTestAsync("Save Scan History", async () =>
            {
                var scan = new ScanHistory
                {
                    StartTime = DateTime.Now.AddMinutes(-10),
                    EndTime = DateTime.Now,
                    ComputersScanned = 100,
                    SuccessCount = 95,
                    FailureCount = 5,
                    DurationSeconds = 600
                };
                await _provider.SaveScanHistoryAsync(scan);
            });

            // Test: Get last scan
            await RunTestAsync("Get Last Scan", async () =>
            {
                var lastScan = await _provider.GetLastScanAsync();
                if (lastScan == null)
                    throw new Exception("GetLastScanAsync returned null after saving scan");
            });

            // Test: Get scan history
            await RunTestAsync("Get Scan History", async () =>
            {
                var history = await _provider.GetScanHistoryAsync(10);
                if (history == null || history.Count == 0)
                    throw new Exception("GetScanHistoryAsync returned empty results");
            });
        }

        private async Task TestSettingsManagementAsync()
        {
            LogSection("SETTINGS MANAGEMENT TESTS");

            var testKey = "TestSetting_" + Guid.NewGuid().ToString().Substring(0, 8);
            var testValue = "TestValue123";

            // Test: Save setting
            await RunTestAsync("Save Setting", async () =>
            {
                await _provider.SaveSettingAsync(testKey, testValue);
            });

            // Test: Get setting
            await RunTestAsync("Get Setting", async () =>
            {
                var retrieved = await _provider.GetSettingAsync(testKey);
                if (retrieved != testValue)
                    throw new Exception($"Setting value mismatch: expected '{testValue}', got '{retrieved}'");
            });

            // Test: Get setting with default
            await RunTestAsync("Get Non-Existent Setting with Default", async () =>
            {
                var nonExistent = await _provider.GetSettingAsync("NonExistentKey_12345", "DefaultValue");
                if (nonExistent != "DefaultValue")
                    throw new Exception("Default value not returned for non-existent setting");
            });

            // Test: Update setting
            await RunTestAsync("Update Setting", async () =>
            {
                var newValue = "UpdatedValue456";
                await _provider.SaveSettingAsync(testKey, newValue);
                var updated = await _provider.GetSettingAsync(testKey);
                if (updated != newValue)
                    throw new Exception("Setting update failed");
            });
        }

        private async Task TestStatisticsAsync()
        {
            LogSection("STATISTICS TESTS");

            // Test: Get database stats
            await RunTestAsync("Get Database Statistics", async () =>
            {
                var stats = await _provider.GetDatabaseStatsAsync();
                if (stats == null)
                    throw new Exception("GetDatabaseStatsAsync returned null");
                LogInfo($"  Total Computers: {stats.TotalComputers}");
                LogInfo($"  Online Computers: {stats.OnlineComputers}");
                LogInfo($"  Database Size: {stats.DatabaseSizeMB} MB");
            });
        }

        private async Task TestCleanupAsync()
        {
            LogSection("CLEANUP TESTS");

            // Test: VacuumDatabase (if supported)
            await RunTestAsync("Vacuum Database", async () =>
            {
                await _provider.VacuumDatabaseAsync();
            });
        }

        #endregion

        #region Helper Methods

        private async Task RunTestAsync(string testName, Func<Task> testAction)
        {
            _totalTests++;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await testAction();
                stopwatch.Stop();
                _passCount++;
                LogPass($"✓ {testName} ({stopwatch.ElapsedMilliseconds}ms)");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _failCount++;
                LogFail($"✗ {testName} ({stopwatch.ElapsedMilliseconds}ms): {ex.Message}");
            }
        }

        private void LogHeader(string message)
        {
            _log.AppendLine();
            _log.AppendLine("═══════════════════════════════════════════════════");
            _log.AppendLine($"  {message}");
            _log.AppendLine("═══════════════════════════════════════════════════");
        }

        private void LogSection(string section)
        {
            _log.AppendLine();
            _log.AppendLine($"--- {section} ---");
        }

        private void LogSeparator()
        {
            _log.AppendLine("---------------------------------------------------");
        }

        private void LogInfo(string message)
        {
            _log.AppendLine($"[INFO] {message}");
        }

        private void LogPass(string message)
        {
            _log.AppendLine($"[PASS] {message}");
        }

        private void LogFail(string message)
        {
            _log.AppendLine($"[FAIL] {message}");
        }

        private void LogError(string message)
        {
            _log.AppendLine($"[ERROR] {message}");
        }

        #endregion
    }

    /// <summary>
    /// Result of database testing
    /// TAG: #DATABASE_TESTING
    /// </summary>
    public class DatabaseTestResult
    {
        public bool Success { get; set; }
        public int TotalTests { get; set; }
        public int PassedTests { get; set; }
        public int FailedTests { get; set; }
        public TimeSpan Duration { get; set; }
        public string Log { get; set; }

        public string Summary => Success
            ? $"All tests passed! ({PassedTests}/{TotalTests})"
            : $"Some tests failed ({FailedTests}/{TotalTests} failures)";
    }
}
