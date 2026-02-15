using System;
using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using System.Text;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace NecessaryAdminTool
{
    /// <summary>
    /// About Window - Displays application information with branded styling
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            LoadVersionInfo();
        }

        private void LoadVersionInfo()
        {
            try
            {
                // Use modular LogoConfig for version info
                // TAG: #MODULAR #VERSION
                TxtVersion.Text = LogoConfig.FULL_VERSION.TrimStart('v'); // Remove 'v' prefix for full version display
                TxtBuildDate.Text = $"{LogoConfig.COMPILED_DATE_SHORT} {new FileInfo(Assembly.GetExecutingAssembly().Location).LastWriteTime:HH:mm}";

                // Get copyright attribute - TAG: #COPYRIGHT #DYNAMIC
                var copyrightAttr = (AssemblyCopyrightAttribute)
                    Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyCopyrightAttribute));
                TxtCopyright.Text = copyrightAttr?.Copyright ?? LogoConfig.COPYRIGHT;
            }
            catch (Exception ex)
            {
                // Fallback to LogoConfig if something fails - TAG: #COPYRIGHT #DYNAMIC
                TxtVersion.Text = LogoConfig.FULL_VERSION.TrimStart('v');
                TxtBuildDate.Text = LogoConfig.COMPILED_DATE_SHORT;
                TxtCopyright.Text = LogoConfig.COPYRIGHT;

                LogManager.LogError("About window version load failed", ex);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Enable window dragging by clicking anywhere on the header
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        // Export button hover effects
        private void ExportButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF252525"));
            }
        }

        private void ExportButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1A1A1A"));
            }
        }

        // Debug button hover effects
        private void DebugButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF252525"));
            }
        }

        private void DebugButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1A1A1A"));
            }
        }

        // Create admin launcher shortcut
        private void CreateShortcut_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // Prompt for admin username
                string adminUsername = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter your domain admin username:\n\n" +
                    "Format: domain\\username\n" +
                    "Example: process\\admin.bnecessary-a",
                    "Admin Username",
                    $"{Environment.UserDomainName}\\admin.{Environment.UserName}",
                    -1, -1);

                if (string.IsNullOrWhiteSpace(adminUsername))
                {
                    return; // User cancelled
                }

                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                string exeDir = Path.GetDirectoryName(exePath);
                string batPath = Path.Combine(exeDir, "Launch_AsAdmin.bat");

                // Create batch file with runas /savecred
                string batContent = $@"@echo off
echo ========================================
echo  NecessaryAdminTool Suite - Admin Launcher
echo ========================================
echo.
echo Launching as: {adminUsername}
echo.
echo NOTE: First time will ask for password.
echo       Password will be saved securely by Windows.
echo.
runas /user:{adminUsername} /savecred ""{exePath}""
";

                File.WriteAllText(batPath, batContent);

                // Create desktop shortcut using IWshRuntimeLibrary
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktopPath, "NecessaryAdminTool Suite (Admin).lnk");

                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                var shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = batPath;
                shortcut.WorkingDirectory = exeDir;
                shortcut.Description = $"Launch NecessaryAdminTool Suite as {adminUsername}";
                shortcut.IconLocation = exePath + ",0";
                shortcut.Save();

                System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);

                LogManager.LogInfo($"Admin launcher created for user: {adminUsername}");

                MessageBox.Show(
                    "✅ Admin launcher created successfully!\n\n" +
                    $"Desktop shortcut: \"NecessaryAdminTool Suite (Admin)\"\n" +
                    $"Batch file: {batPath}\n\n" +
                    "📌 FIRST TIME USE:\n" +
                    "1. Double-click the desktop shortcut\n" +
                    "2. Enter your admin password when prompted\n" +
                    "3. Windows will remember your password\n\n" +
                    "📌 FUTURE USE:\n" +
                    "Just double-click the shortcut - no password needed!",
                    "Shortcut Created",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to create admin launcher", ex);
                MessageBox.Show(
                    $"Failed to create admin launcher:\n\n{ex.Message}",
                    "Creation Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Export legal terms to HTML
        private void ExportLegalTerms_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                string htmlContent = GenerateLegalTermsHTML();
                string tempPath = Path.Combine(Path.GetTempPath(), $"NecessaryAdminTool_Legal_Terms_{DateTime.Now:yyyyMMdd_HHmmss}.html");

                File.WriteAllText(tempPath, htmlContent, Encoding.UTF8);

                // Open in default browser
                Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });

                LogManager.LogInfo($"Legal terms exported to: {tempPath}");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to export legal terms", ex);
                MessageBox.Show($"Failed to export legal terms: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GenerateLegalTermsHTML()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;

            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>NecessaryAdminTool Suite - End-User License Agreement</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #0D0D0D 0%, #1A1A1A 100%);
            color: #CCCCCC;
            line-height: 1.6;
            padding: 20px;
        }}

        .container {{
            max-width: 900px;
            margin: 0 auto;
            background: #1E1E1E;
            border: 2px solid #FF8533;
            border-radius: 12px;
            overflow: hidden;
            box-shadow: 0 10px 40px rgba(255, 133, 51, 0.3);
        }}

        .header {{
            background: linear-gradient(135deg, #1A1A1A 0%, #2A2A2A 100%);
            padding: 40px 30px;
            text-align: center;
            border-bottom: 2px solid #FF8533;
        }}

        .logo {{
            width: 80px;
            height: 80px;
            margin: 0 auto 20px;
            background: linear-gradient(135deg, #FF8533 0%, #CC6B29 100%);
            border-radius: 16px;
            display: flex;
            align-items: center;
            justify-content: center;
            box-shadow: 0 0 30px rgba(255, 133, 51, 0.6);
        }}

        .logo-text {{
            color: white;
            font-size: 48px;
            font-weight: bold;
        }}

        .brand {{
            font-size: 42px;
            font-weight: 900;
            background: linear-gradient(90deg, #FF8533 0%, #A1A1AA 70%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
            margin-bottom: 5px;
        }}

        .tagline {{
            color: #FF8533;
            font-size: 11px;
            font-weight: bold;
            letter-spacing: 3px;
            opacity: 0.8;
        }}

        .version {{
            display: inline-block;
            margin-top: 15px;
            padding: 6px 12px;
            background: #1A1A1A;
            border: 1px solid #FF8533;
            border-radius: 6px;
            color: #FF8533;
            font-size: 12px;
            font-weight: bold;
        }}

        .content {{
            padding: 40px;
        }}

        h1 {{
            color: #FF8533;
            font-size: 28px;
            margin-bottom: 30px;
            text-align: center;
            text-transform: uppercase;
            letter-spacing: 2px;
        }}

        h2 {{
            color: #FFFFFF;
            font-size: 16px;
            margin: 30px 0 15px 0;
            padding-bottom: 8px;
            border-bottom: 2px solid #3C3C3C;
        }}

        p {{
            margin-bottom: 15px;
            text-align: justify;
        }}

        ul {{
            margin-left: 20px;
            margin-bottom: 15px;
        }}

        li {{
            margin-bottom: 8px;
        }}

        .notice {{
            background: #2A1A00;
            border-left: 4px solid #FF8533;
            padding: 20px;
            margin: 30px 0;
            border-radius: 6px;
        }}

        .notice-title {{
            color: #FF8533;
            font-weight: bold;
            font-size: 14px;
            margin-bottom: 10px;
        }}

        .contact {{
            background: #1A1A1A;
            padding: 20px;
            border-radius: 8px;
            margin: 30px 0;
            border: 1px solid #3C3C3C;
        }}

        .contact strong {{
            color: #FF8533;
        }}

        .footer {{
            background: #0D0D0D;
            padding: 20px;
            text-align: center;
            color: #666666;
            font-size: 11px;
            border-top: 1px solid #3C3C3C;
        }}

        .print-hide {{
            text-align: center;
            margin: 20px 0;
        }}

        .print-btn {{
            background: #FF8533;
            color: white;
            border: none;
            padding: 12px 30px;
            font-size: 14px;
            font-weight: bold;
            border-radius: 6px;
            cursor: pointer;
            transition: all 0.3s;
        }}

        .print-btn:hover {{
            background: #FFAA66;
            box-shadow: 0 5px 20px rgba(255, 133, 51, 0.4);
        }}

        @media print {{
            body {{
                background: white !important;
                color: black !important;
                padding: 0;
            }}

            .container {{
                border: 1px solid #999 !important;
                box-shadow: none !important;
                background: white !important;
            }}

            .header {{
                background: white !important;
                border-bottom: 2px solid #FF8533 !important;
                page-break-after: avoid;
            }}

            .logo {{
                background: linear-gradient(135deg, #FF8533 0%, #CC6B29 100%) !important;
                box-shadow: none !important;
            }}

            .brand {{
                color: #000 !important;
            }}

            .tagline {{
                color: #666 !important;
            }}

            .version {{
                background: white !important;
                border-color: #FF8533 !important;
                color: #FF8533 !important;
            }}

            .content {{
                background: white !important;
                color: black !important;
            }}

            h1 {{
                color: #000 !important;
                border-bottom: 2px solid #FF8533;
                page-break-after: avoid;
            }}

            h2 {{
                color: #000 !important;
                border-bottom: 1px solid #999 !important;
                page-break-after: avoid;
                margin-top: 20px !important;
            }}

            p, li {{
                color: #000 !important;
                orphans: 3;
                widows: 3;
            }}

            ul {{
                page-break-inside: avoid;
            }}

            .notice {{
                background: #FFF8F0 !important;
                border-left: 4px solid #FF8533 !important;
                color: #000 !important;
                page-break-inside: avoid;
            }}

            .notice-title {{
                color: #FF8533 !important;
            }}

            .contact {{
                background: #F5F5F5 !important;
                border: 1px solid #999 !important;
                color: #000 !important;
                page-break-inside: avoid;
            }}

            .contact strong {{
                color: #000 !important;
            }}

            .contact a {{
                color: #0066CC !important;
            }}

            .footer {{
                background: #F5F5F5 !important;
                border-top: 1px solid #999 !important;
                color: #666 !important;
            }}

            .print-hide {{
                display: none !important;
            }}

            /* Ensure good page breaks */
            h1, h2 {{
                page-break-after: avoid;
            }}

            p, li {{
                page-break-inside: avoid;
            }}
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <div class=""logo"">
                <div class=""logo-text"">A</div>
            </div>
            <div class=""brand"">Brandon Necessary</div>
            <div class=""tagline"">I T   M A N A G E M E N T   S U I T E</div>
            <div class=""version"">v{version.Major}.{version.Minor}.{version.Build}</div>
        </div>

        <div class=""content"">
            <h1>End-User License Agreement (EULA)</h1>

            <div class=""print-hide"">
                <button class=""print-btn"" onclick=""window.print()"">🖨 Print or Save as PDF</button>
            </div>

            {GetLegalContentHTML()}

            <div class=""contact"">
                <strong>For licensing inquiries, permission requests, legal questions, or to report violations of this EULA, please contact:</strong><br><br>
                <strong>{{COMPANY_NAME}} - Legal Department</strong><br>
                Email: <a href=""mailto:support@{{COMPANY_DOMAIN}}"" style=""color: #FF8533;"">support@{{COMPANY_DOMAIN}}</a><br>
                Phone: Contact your authorized {{COMPANY_NAME}} representative
            </div>
        </div>

        <div class=""footer"">
            END-USER LICENSE AGREEMENT - Document Version: 2.0 | Effective Date: 2026-02-11<br>
            NecessaryAdminTool Suite v{version.Major}.{version.Minor} | Brandon Necessary - All Rights Reserved | Confidential &amp; Proprietary<br>
            This document contains 28 sections and constitutes a legally binding agreement.<br><br>
            <em>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</em>
        </div>
    </div>
</body>
</html>";
        }

        private string GetLegalContentHTML()
        {
            return @"
            <h2>1. GRANT OF LICENSE</h2>
            <p>This software product, NecessaryAdminTool Suite (""Software""), is licensed, not sold, to you by Brandon Necessary (""Licensor"") for use strictly in accordance with the terms of this Agreement. This license grants you the right to install and use the Software solely for internal business operations within your organization's authorized IT infrastructure management activities.</p>

            <h2>2. RESTRICTIONS ON USE</h2>
            <p>You may NOT, under any circumstances:</p>
            <ul>
                <li>Reproduce, copy, or duplicate the Software or any portion thereof for distribution, sale, or commercial purposes outside of your authorized business use case;</li>
                <li>Reverse engineer, decompile, disassemble, or attempt to derive the source code of the Software, except to the extent expressly permitted by applicable law;</li>
                <li>Modify, adapt, translate, rent, lease, loan, resell, distribute, or create derivative works based upon the Software or any part thereof;</li>
                <li>Remove, obscure, or alter any proprietary notices (including copyright and trademark notices) that may be affixed to or contained within the Software;</li>
                <li>Use the Software for any purpose outside of authorized IT management within your organization;</li>
                <li>Transfer, sublicense, or assign your rights under this license to any third party without prior written consent from Brandon Necessary;</li>
                <li>Use the Software in any manner that violates applicable local, state, national, or international law;</li>
                <li>Deploy the Software on systems you do not own or have explicit authorization to manage.</li>
            </ul>

            <h2>3. PROPRIETARY RIGHTS</h2>
            <p>The Software is protected by copyright laws and international copyright treaties, as well as other intellectual property laws and treaties. Brandon Necessary retains all rights, title, and interest in and to the Software, including all copyrights, patents, trade secrets, trademarks, and other intellectual property rights therein. This Agreement does not grant you any rights to trademarks, service marks, or trade names of Brandon Necessary.</p>

            <h2>4. CONFIDENTIALITY</h2>
            <p>The Software contains proprietary and confidential information of Brandon Necessary. You agree to maintain the confidentiality of the Software and to not disclose, provide, or otherwise make available such proprietary and confidential information to any third party without the prior written consent of Brandon Necessary.</p>

            <h2>5. WARRANTY DISCLAIMER</h2>
            <p>THE SOFTWARE IS PROVIDED ""AS IS"" WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, AND NONINFRINGEMENT. {{COMPANY_NAME}} DOES NOT WARRANT THAT THE SOFTWARE WILL MEET YOUR REQUIREMENTS OR THAT THE OPERATION OF THE SOFTWARE WILL BE UNINTERRUPTED OR ERROR-FREE.</p>

            <h2>6. LIMITATION OF LIABILITY</h2>
            <p>IN NO EVENT SHALL {{COMPANY_NAME}} BE LIABLE FOR ANY SPECIAL, INCIDENTAL, INDIRECT, OR CONSEQUENTIAL DAMAGES WHATSOEVER (INCLUDING, WITHOUT LIMITATION, DAMAGES FOR LOSS OF BUSINESS PROFITS, BUSINESS INTERRUPTION, LOSS OF BUSINESS INFORMATION, OR ANY OTHER PECUNIARY LOSS) ARISING OUT OF THE USE OF OR INABILITY TO USE THE SOFTWARE, EVEN IF {{COMPANY_NAME}} HAS BEEN ADVISED OF THE POSSIBILITY OF SUCH DAMAGES.</p>

            <h2>7. TERMINATION</h2>
            <p>This license is effective until terminated. Your rights under this license will terminate automatically without notice from Brandon Necessary if you fail to comply with any term(s) of this Agreement. Upon termination, you shall cease all use of the Software and destroy all copies, full or partial, of the Software.</p>

            <h2>8. COMMERCIAL USE PROHIBITION</h2>
            <p>This Software is licensed exclusively for use within the internal operations of the organization to which it has been provided. Any commercial use, resale, redistribution, or deployment of this Software for third-party benefit or monetary gain is strictly prohibited without a separate commercial licensing agreement with Brandon Necessary.</p>

            <h2>9. GOVERNING LAW</h2>
            <p>This Agreement shall be governed by and construed in accordance with the laws of the jurisdiction in which Brandon Necessary operates, without regard to its conflict of law provisions.</p>

            <h2>10. ENTIRE AGREEMENT</h2>
            <p>This Agreement constitutes the entire agreement between you and Brandon Necessary concerning the Software and supersedes all prior or contemporaneous oral or written communications, proposals, and representations with respect to the Software or any other subject matter covered by this Agreement.</p>

            <h2>11. DATA PRIVACY &amp; SECURITY POLICY</h2>
            <p>The Software may collect, process, and store system information, performance metrics, audit logs, and user authentication data necessary for its operation. By using this Software, you acknowledge and consent to such data collection strictly for the purposes of system management, diagnostic logging, and security auditing. Brandon Necessary commits to maintaining the confidentiality and security of collected data in accordance with applicable data protection regulations. No personally identifiable information (PII) or sensitive business data will be transmitted to external servers without explicit user consent. All authentication credentials are encrypted and stored locally using industry-standard cryptographic methods (AES-256, SecureString, and RtlSecureZeroMemory).</p>

            <h2>12. NETWORK ACCESS &amp; SECURITY RESPONSIBILITIES</h2>
            <p>The Software requires network access, WMI (Windows Management Instrumentation), WinRM (Windows Remote Management), and Active Directory connectivity to function properly. You acknowledge that the Software will initiate connections to domain controllers, remote workstations, and network resources within your infrastructure. You are solely responsible for:</p>
            <ul>
                <li>Ensuring appropriate network security policies, firewalls, and access controls are in place;</li>
                <li>Configuring proper authentication mechanisms and role-based access controls;</li>
                <li>Monitoring and auditing all actions performed through the Software;</li>
                <li>Preventing unauthorized access to systems managed by the Software;</li>
                <li>Compliance with your organization's IT security policies and procedures;</li>
                <li>Ensuring all remote management activities are authorized and documented.</li>
            </ul>
            <p>Brandon Necessary is not liable for any security breaches, data loss, or unauthorized access resulting from improper configuration, weak authentication, or misuse of the Software.</p>

            <h2>13. AUDIT &amp; COMPLIANCE OBLIGATIONS</h2>
            <p>The Software maintains comprehensive audit logs of all administrative actions, system modifications, remote access sessions, and deployment activities. You are responsible for retaining these logs in accordance with your organization's data retention policies and applicable regulatory requirements (including but not limited to SOX, HIPAA, PCI-DSS, GDPR, and other industry-specific compliance frameworks). Brandon Necessary reserves the right to audit your use of the Software to ensure compliance with this Agreement. You agree to provide reasonable cooperation and access to records upon request for compliance verification purposes.</p>

            <h2>14. SOFTWARE UPDATES &amp; MAINTENANCE</h2>
            <p>Brandon Necessary may, at its sole discretion, provide updates, patches, bug fixes, or new versions of the Software. You acknowledge that updates may modify, add, or remove features without prior notice. Installation of updates is recommended but not mandatory unless critical security vulnerabilities are addressed. Brandon Necessary is under no obligation to provide updates, technical support, or maintenance services unless otherwise specified in a separate support agreement. Failure to install critical security updates may result in security vulnerabilities for which Brandon Necessary shall not be held liable.</p>

            <h2>15. USER RESPONSIBILITIES &amp; ACCEPTABLE USE</h2>
            <p>You are solely responsible for:</p>
            <ul>
                <li>Ensuring all users of the Software are properly trained and authorized to perform administrative functions;</li>
                <li>Implementing strong password policies, multi-factor authentication where applicable, and principle of least privilege;</li>
                <li>Verifying the legitimacy and safety of all PowerShell scripts, WMI queries, and remote commands executed through the Software;</li>
                <li>Maintaining current backups of all systems prior to performing mass deployments, updates, or system modifications;</li>
                <li>Testing all deployment scripts and update packages in non-production environments before production deployment;</li>
                <li>Ensuring compliance with Microsoft licensing terms for Windows, Active Directory, and other Microsoft technologies accessed through the Software;</li>
                <li>Immediately revoking access credentials for any users who no longer require access to the Software.</li>
            </ul>
            <p>You agree NOT to use the Software for any unlawful, malicious, or unauthorized purposes, including but not limited to unauthorized network scanning, data exfiltration, denial-of-service attacks, or deployment of malicious code.</p>

            <h2>16. THIRD-PARTY COMPONENTS &amp; DEPENDENCIES</h2>
            <p>The Software may incorporate or depend upon third-party libraries, frameworks, and technologies, including but not limited to: Microsoft .NET Framework, Windows Presentation Foundation (WPF), Windows Management Instrumentation (WMI), PowerShell, Active Directory Services, and other Microsoft Windows components. These components are subject to their respective license agreements and terms of use. You acknowledge that use of the Software constitutes acceptance of all applicable third-party license terms. Brandon Necessary makes no warranties regarding third-party components and disclaims all liability for issues arising from third-party dependencies.</p>

            <h2>17. BACKUP &amp; DISASTER RECOVERY</h2>
            <p>THE SOFTWARE DOES NOT PROVIDE AUTOMATED BACKUP OR DISASTER RECOVERY CAPABILITIES. You are solely responsible for implementing and maintaining appropriate backup procedures, disaster recovery plans, and business continuity strategies for all systems managed by the Software. Brandon Necessary strongly recommends creating full system backups and recovery points before performing any mass deployments, operating system upgrades, or system modifications. Brandon Necessary shall not be liable for any data loss, system failures, or recovery costs resulting from use of the Software.</p>

            <h2>18. INDEMNIFICATION</h2>
            <p>You agree to indemnify, defend, and hold harmless Brandon Necessary, its officers, directors, employees, agents, and affiliates from and against any and all claims, liabilities, damages, losses, costs, expenses, or fees (including reasonable attorneys' fees) arising from:</p>
            <ul>
                <li>Your use or misuse of the Software;</li>
                <li>Your violation of this Agreement or any applicable law or regulation;</li>
                <li>Your violation of any rights of any third party;</li>
                <li>Any unauthorized access to systems or data facilitated through the Software;</li>
                <li>Any deployment, modification, or deletion actions performed through the Software;</li>
                <li>Any breach of security, confidentiality, or data protection obligations.</li>
            </ul>

            <h2>19. EXPORT CONTROL &amp; COMPLIANCE</h2>
            <p>The Software may be subject to export control laws and regulations of the United States and other jurisdictions. You agree to comply with all applicable export and import laws, including but not limited to the U.S. Export Administration Regulations (EAR) and International Traffic in Arms Regulations (ITAR). You represent and warrant that you are not located in, under the control of, or a national or resident of any country to which the United States has embargoed goods, and that you are not on any U.S. government list of prohibited or restricted parties. You shall not export, re-export, or transfer the Software in violation of applicable export control laws.</p>

            <h2>20. SYSTEM REQUIREMENTS &amp; COMPATIBILITY</h2>
            <p>The Software is designed to operate on Windows 10/11 systems with .NET Framework 4.8.1 or later, in Active Directory domain environments. Minimum system requirements include: 4-core processor, 8GB RAM, network connectivity, and administrator privileges. Brandon Necessary makes no warranty that the Software will function correctly on systems not meeting these requirements or in non-domain environments. Performance may vary based on network topology, domain size, hardware specifications, and configuration settings. Brandon Necessary is not responsible for compatibility issues with third-party security software, group policies, or custom system configurations that interfere with Software operation.</p>

            <h2>21. TELEMETRY &amp; DIAGNOSTIC DATA</h2>
            <p>The Software may collect anonymous telemetry and diagnostic data including error reports, performance metrics, feature usage statistics, and system configuration information to improve product quality and user experience. All telemetry data is anonymized and does not include personally identifiable information, credentials, or sensitive business data. You may disable telemetry collection through the Software's configuration settings, though this may impact the quality of support services. Brandon Necessary uses collected data solely for product improvement, bug fixes, and feature development purposes.</p>

            <h2>22. INTELLECTUAL PROPERTY CLAIMS</h2>
            <p>If you believe that the Software infringes upon any patent, copyright, trademark, or other intellectual property right, you must promptly notify Brandon Necessary in writing with detailed information regarding the alleged infringement. Brandon Necessary reserves the right to modify or replace allegedly infringing components, obtain necessary licenses, or terminate this Agreement at its sole discretion. This section constitutes your sole and exclusive remedy for any intellectual property infringement claims related to the Software.</p>

            <h2>23. FORCE MAJEURE</h2>
            <p>Brandon Necessary shall not be liable for any failure or delay in performance of its obligations under this Agreement due to causes beyond its reasonable control, including but not limited to: acts of God, war, terrorism, civil unrest, labor disputes, natural disasters, government actions, pandemics, epidemics, cyber-attacks, power failures, telecommunications failures, or other unforeseeable circumstances. In such events, Brandon Necessary's obligations shall be suspended for the duration of the force majeure event.</p>

            <h2>24. SEVERABILITY &amp; WAIVER</h2>
            <p>If any provision of this Agreement is found to be invalid, illegal, or unenforceable by a court of competent jurisdiction, the remaining provisions shall continue in full force and effect. The invalid provision shall be modified to the minimum extent necessary to make it valid and enforceable while preserving the original intent. No waiver of any breach of this Agreement shall constitute a waiver of any subsequent breach. Failure by Brandon Necessary to enforce any provision of this Agreement shall not be construed as a waiver of that or any other provision.</p>

            <h2>25. AMENDMENT &amp; MODIFICATION RIGHTS</h2>
            <p>Brandon Necessary reserves the right to modify, amend, or update this Agreement at any time without prior notice. Continued use of the Software following any such modifications constitutes your acceptance of the revised terms. Material changes to this Agreement will be communicated through the Software interface, email notification, or posted to the Brandon Necessary website. It is your responsibility to review this Agreement periodically for changes. If you do not agree to modified terms, you must immediately cease using the Software and uninstall it from all systems.</p>

            <h2>26. DISPUTE RESOLUTION &amp; ARBITRATION</h2>
            <p>Any disputes, claims, or controversies arising out of or relating to this Agreement or the Software shall first be subject to good-faith negotiation between the parties. If negotiation fails to resolve the dispute within thirty (30) days, the dispute shall be resolved through binding arbitration in accordance with the rules of the American Arbitration Association. The arbitration shall be conducted in the jurisdiction where Brandon Necessary is headquartered. Each party shall bear its own costs and expenses related to arbitration. The arbitrator's decision shall be final and binding, and judgment may be entered in any court of competent jurisdiction. Notwithstanding the foregoing, either party may seek injunctive or other equitable relief in court to prevent irreparable harm or protect intellectual property rights.</p>

            <h2>27. SURVIVAL OF TERMS</h2>
            <p>The following sections shall survive termination or expiration of this Agreement: Proprietary Rights, Confidentiality, Warranty Disclaimer, Limitation of Liability, Indemnification, Governing Law, Dispute Resolution, and any other provisions which by their nature are intended to survive termination. Upon termination, you must immediately cease all use of the Software, destroy all copies, and certify in writing to Brandon Necessary that you have complied with these obligations.</p>

            <h2>28. ACKNOWLEDGMENT &amp; ACCEPTANCE</h2>
            <p>BY INSTALLING, COPYING, ACCESSING, OR OTHERWISE USING THE SOFTWARE, YOU ACKNOWLEDGE THAT:</p>
            <ul>
                <li>You have read and understood this entire Agreement in its entirety;</li>
                <li>You have the authority to bind your organization to these terms;</li>
                <li>You agree to be legally bound by all terms, conditions, restrictions, and obligations set forth herein;</li>
                <li>You have been given adequate opportunity to review this Agreement with legal counsel;</li>
                <li>You acknowledge that this Agreement constitutes a binding legal contract between you (individually and/or on behalf of your organization) and Brandon Necessary.</li>
            </ul>

            <div class=""notice"">
                <div class=""notice-title"">⚠ IMPORTANT NOTICE</div>
                <p>This Software is a powerful administrative tool capable of making system-wide changes, deploying updates to multiple computers simultaneously, modifying Active Directory objects, and executing remote commands. Improper use can result in system failures, data loss, network disruptions, or security vulnerabilities. Always test in non-production environments first. Maintain current backups. Verify all actions before execution. Use with caution and only if you are a trained IT professional with appropriate authorization.</p>
            </div>";
        }

        // ############################################################################
        // REGION: GLOBAL SERVICES CONFIGURATION
        // TAG: #GLOBAL_SERVICES #CONFIG_EDITOR
        // ############################################################################

        private ObservableCollection<ServiceConfigItem> _serviceConfigItems = new ObservableCollection<ServiceConfigItem>();

        /// <summary>
        /// Load global services configuration into the editor
        /// </summary>
        public void LoadGlobalServicesConfig(string jsonConfig)
        {
            try
            {
                _serviceConfigItems.Clear();

                if (string.IsNullOrWhiteSpace(jsonConfig))
                {
                    // Load defaults
                    LoadDefaultServices();
                }
                else
                {
                    // Parse existing JSON
                    var serializer = new JavaScriptSerializer();
                    var services = serializer.Deserialize<List<ServiceConfigItem>>(jsonConfig);
                    foreach (var svc in services)
                    {
                        _serviceConfigItems.Add(svc);
                    }
                }

                GridServicesConfig.ItemsSource = _serviceConfigItems;
                var jsonSerializer = new JavaScriptSerializer();
                TxtJsonConfig.Text = FormatJson(jsonSerializer.Serialize(_serviceConfigItems));
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to load services config", ex);
                LoadDefaultServices();
            }
        }

        /// <summary>
        /// Load default service definitions (grouped by parent company)
        /// </summary>
        private void LoadDefaultServices()
        {
            _serviceConfigItems.Clear();
            var defaults = new[]
            {
                // === ESSENTIAL SERVICES ===
                // Microsoft ecosystem
                new ServiceConfigItem { ServiceName = "Azure", Endpoint = "https://status.azure.com/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "Microsoft 365", Endpoint = "https://status.office.com/api/v1.0/ServiceStatus/CurrentStatus" },
                new ServiceConfigItem { ServiceName = "Microsoft Teams", Endpoint = "https://status.office.com/api/v1.0/ServiceStatus/CurrentStatus" },
                new ServiceConfigItem { ServiceName = "GitHub", Endpoint = "https://www.githubstatus.com/api/v2/status.json" },
                // Google ecosystem
                new ServiceConfigItem { ServiceName = "Google Cloud", Endpoint = "https://status.cloud.google.com/incidents.json" },
                new ServiceConfigItem { ServiceName = "DNS (8.8.8.8)", Endpoint = "ping:8.8.8.8" },
                // Amazon ecosystem
                new ServiceConfigItem { ServiceName = "AWS", Endpoint = "https://status.aws.amazon.com/data.json" },
                // Cloudflare ecosystem
                new ServiceConfigItem { ServiceName = "Cloudflare", Endpoint = "https://www.cloudflarestatus.com/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "DNS (1.1.1.1)", Endpoint = "ping:1.1.1.1" },

                // === HIGH PRIORITY SERVICES ===
                // Microsoft ecosystem
                new ServiceConfigItem { ServiceName = "NuGet", Endpoint = "https://status.nuget.org/api/v2/status.json" },
                // Atlassian ecosystem
                new ServiceConfigItem { ServiceName = "Atlassian", Endpoint = "https://status.atlassian.com/api/v2/status.json" },
                // Communication platforms
                new ServiceConfigItem { ServiceName = "Slack", Endpoint = "https://status.slack.com/api/v2.0.0/current" },
                new ServiceConfigItem { ServiceName = "Zoom", Endpoint = "https://status.zoom.us/api/v2/status.json" },
                // DevOps & Package Management
                new ServiceConfigItem { ServiceName = "DockerHub", Endpoint = "https://status.docker.com/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "NPM Registry", Endpoint = "https://status.npmjs.org/api/v2/status.json" },
                // CRM & Identity
                new ServiceConfigItem { ServiceName = "Salesforce", Endpoint = "https://api.status.salesforce.com/v1/status" },
                new ServiceConfigItem { ServiceName = "Okta", Endpoint = "https://status.okta.com/api/v2/status.json" },
                // Monitoring
                new ServiceConfigItem { ServiceName = "Datadog", Endpoint = "https://status.datadoghq.com/api/v2/status.json" },

                // === MEDIUM PRIORITY SERVICES ===
                // Twilio ecosystem
                new ServiceConfigItem { ServiceName = "Twilio", Endpoint = "https://status.twilio.com/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "SendGrid", Endpoint = "https://status.sendgrid.com/api/v2/status.json" },
                // Independent services
                new ServiceConfigItem { ServiceName = "Stripe", Endpoint = "https://status.stripe.com/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "MongoDB Atlas", Endpoint = "https://status.cloud.mongodb.com/api/v2/status.json" },
                new ServiceConfigItem { ServiceName = "PagerDuty", Endpoint = "https://status.pagerduty.com/api/v2/status.json" }
            };

            foreach (var svc in defaults)
            {
                _serviceConfigItems.Add(svc);
            }

            GridServicesConfig.ItemsSource = _serviceConfigItems;
            var serializer = new JavaScriptSerializer();
            TxtJsonConfig.Text = FormatJson(serializer.Serialize(_serviceConfigItems));
        }

        /// <summary>
        /// Switch to visual editor view
        /// </summary>
        private void BtnVisualEditor_Click(object sender, RoutedEventArgs e)
        {
            VisualEditorPanel.Visibility = Visibility.Visible;
            JsonEditorPanel.Visibility = Visibility.Collapsed;
            BtnVisualEditor.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF8533"));
            BtnVisualEditor.Foreground = Brushes.White;
            BtnJsonEditor.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2A2A2A"));
            BtnJsonEditor.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF888888"));

            // Sync JSON to visual editor
            try
            {
                var serializer = new JavaScriptSerializer();
                var items = serializer.Deserialize<List<ServiceConfigItem>>(TxtJsonConfig.Text);
                _serviceConfigItems.Clear();
                foreach (var item in items)
                {
                    _serviceConfigItems.Add(item);
                }
            }
            catch { }
        }

        /// <summary>
        /// Switch to JSON editor view
        /// </summary>
        private void BtnJsonEditor_Click(object sender, RoutedEventArgs e)
        {
            JsonEditorPanel.Visibility = Visibility.Visible;
            VisualEditorPanel.Visibility = Visibility.Collapsed;
            BtnJsonEditor.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF8533"));
            BtnJsonEditor.Foreground = Brushes.White;
            BtnVisualEditor.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2A2A2A"));
            BtnVisualEditor.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF888888"));

            // Sync visual editor to JSON
            var serializer = new JavaScriptSerializer();
            TxtJsonConfig.Text = FormatJson(serializer.Serialize(_serviceConfigItems));
        }

        /// <summary>
        /// Save services configuration
        /// </summary>
        private void BtnSaveServicesConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string jsonToSave;
                var serializer = new JavaScriptSerializer();

                // If JSON editor is visible, validate and save from JSON
                if (JsonEditorPanel.Visibility == Visibility.Visible)
                {
                    var items = serializer.Deserialize<List<ServiceConfigItem>>(TxtJsonConfig.Text);
                    jsonToSave = serializer.Serialize(items);
                }
                else
                {
                    // Save from visual editor
                    jsonToSave = serializer.Serialize(_serviceConfigItems);
                }

                // Save to settings
                Properties.Settings.Default.GlobalServicesConfig = jsonToSave;
                Properties.Settings.Default.Save();

                // Update main window if it's open
                if (Owner is MainWindow mainWin)
                {
                    mainWin.ReloadGlobalServicesConfig(jsonToSave);
                }

                // Show success message
                TxtConfigStatus.Text = "✅ Configuration saved successfully!";
                TxtConfigStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0));
                TxtConfigStatus.Visibility = Visibility.Visible;

                // Hide after 3 seconds
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (s, args) => { TxtConfigStatus.Visibility = Visibility.Collapsed; timer.Stop(); };
                timer.Start();

                LogManager.LogInfo("Global services configuration saved");
            }
            catch (Exception ex)
            {
                TxtConfigStatus.Text = "❌ Invalid JSON format!";
                TxtConfigStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
                TxtConfigStatus.Visibility = Visibility.Visible;
                LogManager.LogError("Failed to save services config", ex);
            }
        }

        /// <summary>
        /// Reset to default services configuration
        /// </summary>
        private void BtnResetServicesConfig_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will reset your global services configuration to the default settings.\n\n" +
                "All custom services and API endpoints will be lost.\n\n" +
                "Do you want to continue?",
                "Reset to Defaults",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                LoadDefaultServices();
                TxtConfigStatus.Text = "🔄 Configuration reset to defaults";
                TxtConfigStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 133, 51));
                TxtConfigStatus.Visibility = Visibility.Visible;
                LogManager.LogInfo("Global services configuration reset to defaults");
            }
        }

        /// <summary>
        /// Test all API endpoints
        /// </summary>
        private async void BtnTestAPIs_Click(object sender, RoutedEventArgs e)
        {
            BtnTestAPIs.IsEnabled = false;
            BtnTestAPIs.Content = "TESTING...";
            TxtConfigStatus.Text = "🧪 Testing API endpoints...";
            TxtConfigStatus.Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243));
            TxtConfigStatus.Visibility = Visibility.Visible;

            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    // TAG: #USER_AGENT #DYNAMIC_VERSION
                    client.DefaultRequestHeaders.Add("User-Agent", $"NecessaryAdminTool-Monitor/{LogoConfig.USER_AGENT_VERSION}");

                    int successCount = 0;
                    int failCount = 0;

                    foreach (var svc in _serviceConfigItems)
                    {
                        if (svc.Endpoint.StartsWith("ping:"))
                        {
                            // Ping test
                            string target = svc.Endpoint.Replace("ping:", "");
                            var ping = new System.Net.NetworkInformation.Ping();
                            var result = await ping.SendPingAsync(target, 3000);
                            if (result.Status == System.Net.NetworkInformation.IPStatus.Success)
                                successCount++;
                            else
                                failCount++;
                        }
                        else
                        {
                            // HTTP test
                            try
                            {
                                var response = await client.GetAsync(svc.Endpoint);
                                if (response.IsSuccessStatusCode)
                                    successCount++;
                                else
                                    failCount++;
                            }
                            catch
                            {
                                failCount++;
                            }
                        }
                    }

                    TxtConfigStatus.Text = $"✅ {successCount} OK, ❌ {failCount} Failed";
                    TxtConfigStatus.Foreground = successCount > failCount
                        ? new SolidColorBrush(Color.FromRgb(0, 255, 0))
                        : new SolidColorBrush(Color.FromRgb(255, 100, 100));
                }
            }
            catch (Exception ex)
            {
                TxtConfigStatus.Text = "❌ Test failed: " + ex.Message;
                TxtConfigStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
                LogManager.LogError("API test failed", ex);
            }
            finally
            {
                BtnTestAPIs.IsEnabled = true;
                BtnTestAPIs.Content = "🧪 TEST APIS";
            }
        }

        /// <summary>
        /// Simple JSON formatter for pretty-printing
        /// </summary>
        private string FormatJson(string json)
        {
            try
            {
                int indent = 0;
                var formatted = new StringBuilder();
                bool inString = false;

                for (int i = 0; i < json.Length; i++)
                {
                    char ch = json[i];

                    if (ch == '"' && (i == 0 || json[i - 1] != '\\'))
                        inString = !inString;

                    if (!inString)
                    {
                        if (ch == '{' || ch == '[')
                        {
                            formatted.Append(ch);
                            formatted.AppendLine();
                            formatted.Append(new string(' ', ++indent * 2));
                        }
                        else if (ch == '}' || ch == ']')
                        {
                            formatted.AppendLine();
                            formatted.Append(new string(' ', --indent * 2));
                            formatted.Append(ch);
                        }
                        else if (ch == ',')
                        {
                            formatted.Append(ch);
                            formatted.AppendLine();
                            formatted.Append(new string(' ', indent * 2));
                        }
                        else if (ch == ':')
                        {
                            formatted.Append(ch);
                            formatted.Append(' ');
                        }
                        else if (!char.IsWhiteSpace(ch))
                        {
                            formatted.Append(ch);
                        }
                    }
                    else
                    {
                        formatted.Append(ch);
                    }
                }

                return formatted.ToString();
            }
            catch
            {
                return json; // Return original if formatting fails
            }
        }
    }

}
