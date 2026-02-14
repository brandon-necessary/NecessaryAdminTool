# Enterprise Remote Control Tools Integration Research
## Comprehensive Technical Analysis for C# WPF Integration

**Document Version:** 1.0
**Date:** 2026-02-13
**Purpose:** Technical analysis of enterprise remote control tool integration methods for C# WPF applications

---

## Executive Summary

This document provides a comprehensive technical analysis of integration methods for popular enterprise remote control and RMM (Remote Monitoring and Management) tools. The analysis focuses on programmatic session launching, API capabilities, authentication requirements, and implementation complexity for integration into a C# WPF application.

### Quick Reference Matrix

| Tool | API Available | URL Scheme | CLI Support | Complexity | Licensing |
|------|--------------|------------|-------------|------------|-----------|
| ScreenConnect/ConnectWise | Yes | Yes | Limited | Medium | $36-66/mo |
| TeamViewer | Yes | No | Yes | Medium | $24-230/mo |
| AnyDesk | Yes | No | Yes | Easy | Contact vendor |
| NinjaOne | Yes | No | Limited | Medium | ~$1.50-3.75/endpoint |
| ManageEngine Endpoint Central | Yes | No | Limited | Medium | $795/yr+ |
| Dameware Remote Everywhere | Yes | No | Limited | Medium | Contact vendor |
| RemotePC | Yes | No | No | Easy | Contact vendor |
| LogMeIn/GoToAssist | Yes (Legacy) | No | No | Medium | Contact vendor |

---

## 1. ConnectWise Control (ScreenConnect)

### Overview
ConnectWise Control (formerly ScreenConnect) is a remote support and access solution with robust API capabilities and one of the best integration stories among RMM tools.

### Integration Methods

#### A. REST API
**Documentation:** [ConnectWise Session Manager API Reference](https://docs.connectwise.com/ConnectWise_Control_Documentation/Developers/Session_Manager_API_Reference)

**Authentication:**
- API Token Generation: Navigate to Extras > Generate API Token in admin interface
- Some integrations require Base32 OTP secret for TOTP authentication
- Bearer token authentication for API calls

**Key API Methods:**

```csharp
// Example: Create Session
public Guid CreateSession(string name)
{
    var session = SessionManagerPool.Demux.CreateSession(
        SessionType.Support,
        name,
        null,
        false,
        name
    );
    return session.SafeNav(s => s.SessionID);
}

// Example: Find or Create Session
public Guid FindOrCreateSession(string name, string host)
{
    var session = SessionManagerPool.Demux.GetSessions()
        .FirstOrDefault(s => s.Name == name);

    if (session == null)
        session = SessionManagerPool.Demux.CreateSession(
            SessionType.Support,
            name,
            host,
            false,
            name
        );

    return session.SafeNav(s => s.SessionID);
}
```

**Session Launch URL Pattern:**
```
https://<your_screenconnect_fqdn_with_port>/Host#Access/All Machines//{{agent.ScreenConnectGUID}}/Join
```

**Client Launch Parameters:**
- Supports custom parameters passed via URL encoding
- Parameters are sent back to server when session is created

#### B. URL Scheme Integration
**Scheme:** `connectwise-control://`

**Implementation in C#:**
```csharp
using System.Diagnostics;

public void LaunchScreenConnectSession(string sessionId)
{
    string url = $"https://your-server.screenconnect.com/Host#Access/All Machines//{sessionId}/Join";

    ProcessStartInfo psi = new ProcessStartInfo
    {
        FileName = url,
        UseShellExecute = true
    };
    Process.Start(psi);
}
```

### Pricing & Licensing
- **Remote Support One:** $36/license/month (single license restriction)
- **Remote Support Standard:** $56/concurrent tech/month
- **Remote Support Premium:** $66/concurrent tech/month
- **Remote Unattended Access:** Starting at $39/month for 25 agents
- **On-Premise Licenses:** Starting at ~$2,000 with annual renewal for updates
- **API Access:** Included with license (no additional cost)

### Implementation Complexity
**Rating:** Medium

**Pros:**
- Well-documented Session Manager API
- Supports both REST API and URL-based launching
- Active community and good documentation
- Flexible authentication options

**Cons:**
- Requires ScreenConnect server instance (cloud or on-premise)
- API token management needed
- Some endpoints may require authentication refresh

**C# Integration Recommendations:**
1. Use REST API for session creation and management
2. Launch sessions via direct URLs using Process.Start with UseShellExecute = true
3. Store API tokens securely (Windows Credential Manager or encrypted config)
4. Implement token refresh logic for long-running applications

### Sources
- [ConnectWise Session Manager API Reference](https://docs.connectwise.com/ConnectWise_Control_Documentation/Developers/Session_Manager_API_Reference)
- [ConnectWise Integration Guide](https://docs.connectwise.com/ScreenConnect_Documentation/Developers/Integration_guide)
- [ScreenConnect Pricing](https://www.screenconnect.com/pricing)
- [Automating ScreenConnect Launch URLs | NinjaOne](https://www.ninjaone.com/script-hub/automating-screenconnect-launch-urls-powershell/)

---

## 2. TeamViewer

### Overview
TeamViewer is one of the most widely-used remote control solutions with comprehensive API support for commercial use.

### Integration Methods

#### A. TeamViewer Web API
**Documentation:** [TeamViewer API Documentation](https://webapi.teamviewer.com/api/v1/docs/index)

**Authentication:**
- **OAuth 2.0 Flow:**
  ```
  https://login.teamviewer.com/oauth2/authorize?response_type=code&client_id={YOUR_CLIENT_ID}&redirect_uri={REDIRECT_URI}&display=popup
  ```
- **Script Token:** Available via TeamViewer Management Console for simpler integration
- **Bearer Token:** Used for all API calls

**Session Creation:**
- API supports creating support sessions via POST request
- Returns session links for both QuickSupport and full client
- Session includes ID and password for connection

**OAuth Token Flow:**
1. Obtain authorization code via OAuth URL
2. Exchange code + client secret for access token
3. Use bearer token in API calls: `Authorization: Bearer {token}`

#### B. Command Line Interface
**Documentation:** [TeamViewer Command Line Parameters](https://www.teamviewer.com/en/global/support/knowledge-base/teamviewer-remote/for-developers/command-line-parameters/)

**Syntax:**
```batch
TeamViewer.exe --id {REMOTE_ID} --password {BASE64_ENCODED_PASSWORD}
```

**C# Implementation:**
```csharp
using System;
using System.Diagnostics;
using System.Text;

public void LaunchTeamViewerSession(string remoteId, string password)
{
    // Password must be Base64 encoded
    byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
    string base64Password = Convert.ToBase64String(passwordBytes);

    string tvPath = @"C:\Program Files\TeamViewer\TeamViewer.exe";
    string arguments = $"--id {remoteId} --password {base64Password}";

    ProcessStartInfo psi = new ProcessStartInfo
    {
        FileName = tvPath,
        Arguments = arguments,
        UseShellExecute = false
    };

    Process.Start(psi);
}
```

#### C. QuickSupport Integration
**GitHub Library:** [TeamViewer.QuickSupport.Integration](https://github.com/ihtfw/TeamViewer.QuickSupport.Integration)

**Features:**
- Check TeamViewer installation status
- Download QuickSupport module automatically
- Retrieve session ID and password
- .NET-friendly wrapper for QuickSupport

```csharp
using TeamViewer.QuickSupport.Integration;

public async Task<SessionInfo> GetQuickSupportSession()
{
    var automator = new Automator();

    // Check if installed
    if (!automator.IsTeamViewerInstalled())
    {
        await automator.DownloadQuickSupport();
    }

    // Get session info
    var sessionInfo = automator.GetSessionInfo();
    return sessionInfo; // Contains ID and Password
}
```

### Pricing & Licensing
- **Remote Access:** $24.90/month (individual users)
- **Business:** $50.90/month (single user)
- **Premium:** $112.90/month (up to 15 users, 300 devices)
- **Corporate:** $229.90/month (larger teams, 500 devices)
- **Volume Discounts:** 20-30% off for 50+ users
- **API Access:** Included with commercial licenses
- **Note:** Many features require add-ons that increase total cost

### Implementation Complexity
**Rating:** Medium

**Pros:**
- Mature API with comprehensive documentation
- Multiple integration methods (API, CLI, QuickSupport)
- .NET library available for QuickSupport
- Wide platform support

**Cons:**
- Requires commercial license for business use
- OAuth flow can be complex for first-time implementation
- CLI requires Base64 password encoding
- No custom URL scheme support
- Expensive for larger deployments

**C# Integration Recommendations:**
1. For attended support: Use QuickSupport with .NET wrapper library
2. For unattended access: Use CLI with Process.Start
3. For advanced automation: Implement OAuth 2.0 flow with Web API
4. Store OAuth tokens securely with refresh logic

### Sources
- [TeamViewer API Documentation](https://webapi.teamviewer.com/api/v1/docs/index)
- [Build a TeamViewer Integration](https://www.teamviewer.com/en/global/support/knowledge-base/teamviewer-classic/for-developers/build-a-teamviewer-integration/)
- [TeamViewer Command Line Parameters](https://www.teamviewer.com/en/global/support/knowledge-base/teamviewer-remote/for-developers/command-line-parameters/)
- [GitHub - TeamViewer QuickSupport Integration](https://github.com/ihtfw/TeamViewer.QuickSupport.Integration)
- [TeamViewer Pricing Overview](https://www.teamviewer.com/en-us/pricing/overview/)

---

## 3. AnyDesk

### Overview
AnyDesk provides a lightweight remote desktop solution with robust command-line support and REST API capabilities.

### Integration Methods

#### A. Command Line Interface
**Documentation:** [AnyDesk Command-Line Interface for Windows](https://support.anydesk.com/docs/command-line-interface-for-windows)

**Basic Connection Syntax:**
```batch
anydesk.exe {REMOTE_ID}
anydesk.exe {REMOTE_ID} --plain
```

**Parameters:**
- `--plain` - Connect using unattended access password without prompts
- `--password` - Specify password for unattended access
- `--get-id` - Retrieve local AnyDesk ID
- `--get-alias` - Retrieve local AnyDesk alias
- `--get-status` - Get connection status

**C# Implementation:**
```csharp
using System.Diagnostics;

public void LaunchAnyDeskSession(string remoteId, string password = null)
{
    string anydeskPath = @"C:\Program Files (x86)\AnyDesk\AnyDesk.exe";
    string arguments = remoteId;

    if (!string.IsNullOrEmpty(password))
    {
        arguments += " --plain";
        // Note: Password must be set via --set-password during setup
    }

    ProcessStartInfo psi = new ProcessStartInfo
    {
        FileName = anydeskPath,
        Arguments = arguments,
        UseShellExecute = false,
        CreateNoWindow = false
    };

    Process.Start(psi);
}

// Automated deployment example
public void SetupUnattendedAccess(string password)
{
    string anydeskPath = @"C:\Program Files (x86)\AnyDesk\AnyDesk.exe";

    // Install AnyDesk
    ProcessStartInfo installPsi = new ProcessStartInfo
    {
        FileName = "AnyDesk.exe",
        Arguments = "--install \"C:\\Program Files (x86)\\AnyDesk\" --start-with-win --silent",
        UseShellExecute = false,
        CreateNoWindow = true
    };
    Process.Start(installPsi).WaitForExit();

    // Set password
    ProcessStartInfo passPsi = new ProcessStartInfo
    {
        FileName = anydeskPath,
        Arguments = "--set-password",
        UseShellExecute = false,
        RedirectStandardInput = true,
        CreateNoWindow = true
    };

    using (Process process = Process.Start(passPsi))
    {
        process.StandardInput.WriteLine(password);
        process.WaitForExit();
    }
}
```

**Desktop Shortcut Modification:**
1. Right-click AnyDesk shortcut > Properties
2. Append to Target field: `{Remote_ID} --plain`
3. Example: `"C:\Program Files (x86)\AnyDesk\AnyDesk.exe" 123456789 --plain`

#### B. REST API
**Documentation:** [AnyDesk REST API](https://support.anydesk.com/knowledge/rest-api)

**Authentication:**
- API Key authentication
- Contact AnyDesk support with customer number to request API credentials
- Python module available on GitHub with example scripts

**API Capabilities:**

**Basic Plan:**
- Show license information
- View system information (license, clients count)
- Get client details (online status, alias)
- Get client list
- View session list within timeframe
- Export session data

**Advanced Plan:**
- All Basic features plus:
- Remove client from license
- Show session details
- Change session comments
- Close active sessions
- Manage aliases (change/remove)
- List Address Book entries
- Full command-line access

**Note:** Detailed REST API endpoint documentation requires API credentials and access to developer portal.

### Pricing & Licensing
- **Free:** Personal use only
- **Solo, Standard, Advanced, Ultimate:** Commercial licenses (contact for pricing)
- **REST API Access:** Available with commercial licenses
  - Basic API features: Included
  - Advanced API features: Higher tier license required
- **Commercial Detection:** Free version detects commercial use and requires license upgrade
- **Volume Licensing:** Available for enterprises

### Implementation Complexity
**Rating:** Easy

**Pros:**
- Excellent CLI support with straightforward syntax
- Lightweight client (<5MB)
- No custom URL encoding required
- Simple password authentication for unattended access
- Cross-platform support (Windows, Linux, macOS)
- Python SDK available for REST API

**Cons:**
- REST API requires contacting support for credentials
- No public REST API documentation (requires developer access)
- Limited .NET-specific libraries
- Commercial license required for business use

**C# Integration Recommendations:**
1. **Primary Method:** Use CLI with Process.Start for session launching
2. **Automated Deployment:** Use CLI parameters for silent installation
3. **Advanced Features:** Request REST API access for license management
4. **Best Practice:** Pre-configure unattended passwords during deployment

### Sources
- [AnyDesk Command-Line Interface for Windows](https://support.anydesk.com/docs/command-line-interface-for-windows)
- [AnyDesk REST API](https://support.anydesk.com/knowledge/rest-api)
- [AnyDesk Features - REST API](https://anydesk.com/en/features/rest-api)
- [AnyDesk Pricing](https://anydesk.com/en/pricing)
- [AnyDesk Licenses](https://support.anydesk.com/docs/anydesk-licenses)

---

## 4. NinjaOne (NinjaRMM)

### Overview
NinjaOne is a comprehensive RMM platform with built-in remote control capabilities and a modern REST API.

### Integration Methods

#### A. REST API
**Documentation:**
- [NinjaOne Public API](https://app.ninjarmm.com/apidocs/)
- [NinjaOne Public API 2.0 (Postman)](https://www.postman.com/ninjaone/ninjaone-api-workspace/collection/8gh1ujj/ninjaone-public-api-2-0)

**Authentication:**
- **OAuth 2.0** (Authorization Code and Implicit Grant)
- API settings: Administration > Apps > API
- Bearer token authentication

**Setup Guide:**
[How to Set Up API OAuth Token](https://www.ninjaone.com/docs/integrations/how-to-set-up-api-oauth-token/)

**API Endpoints:**
The NinjaOne API provides comprehensive device management but specific remote control launch endpoints are not publicly documented. The API focuses on:
- Device inventory and management
- Policy configuration
- Software deployment
- Monitoring and alerting

#### B. Web-Based Remote Launch
**Access Method:**
- Technicians launch sessions from NinjaOne device dashboard
- Click remote control icon from device page
- Can also launch directly from tickets
- Sessions start in 2-3 seconds after handshake

**Technical Details:**
- NinjaOne cloud service provides rendezvous and relay
- No direct URL scheme for external launching
- Encryption: x25519+XSalsa20+Poly1305
- Permission-gated access via technician roles
- End-user approval required for attended sessions

**Integration Limitations:**
Based on research, NinjaOne does not currently provide:
- Public API endpoints for launching remote control sessions
- URL schemes for external session launching
- CLI tools for remote access initiation
- Direct programmatic access to remote control from external applications

**C# Integration Approach:**
```csharp
// NinjaOne integration is limited to web-based launching
// You would need to automate browser navigation or use web automation

using System.Diagnostics;

public void OpenNinjaOneDevice(string deviceId, string ninjaOneUrl)
{
    // Opens NinjaOne web interface to device page
    // Technician must manually click remote control button
    string url = $"{ninjaOneUrl}/device/{deviceId}";

    ProcessStartInfo psi = new ProcessStartInfo
    {
        FileName = url,
        UseShellExecute = true
    };
    Process.Start(psi);
}

// Alternative: Use NinjaOne API to get device info, then use integrated remote tool
public async Task<DeviceInfo> GetDeviceInfo(string deviceId, string apiToken)
{
    using (var client = new HttpClient())
    {
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");

        var response = await client.GetAsync(
            $"https://api.ninjarmm.com/v2/devices/{deviceId}"
        );

        // Parse device information
        // Use hostname/IP with alternative remote tool (TeamViewer, AnyDesk, etc.)
        var deviceInfo = await response.Content.ReadAsAsync<DeviceInfo>();
        return deviceInfo;
    }
}
```

### Pricing & Licensing
- **Pricing Model:** Per-endpoint pricing
- **Costs:**
  - ~$1.50/endpoint for 10,000 devices
  - ~$3.75/endpoint for 50 devices or fewer
- **Annual Contracts:** Generally required
- **Free Trial:** 14 days, no credit card required
- **Median Annual Spend:** $8,952 (based on Vendr data from 32 purchases)
- **Range:** $5,769/year (low) to $24,960/year (high)
- **API Access:** Included with platform
- **Support:** Included with purchase

### Implementation Complexity
**Rating:** Medium (API) / Hard (Remote Control Integration)

**Pros:**
- Modern REST API with comprehensive device management
- Fast remote sessions (2-3 seconds)
- Excellent security (modern encryption)
- Permission-based access control
- API support included

**Cons:**
- **No programmatic remote control launch capability**
- Remote control only accessible via web interface
- No URL scheme for external launching
- No CLI for remote access
- Requires NinjaOne platform subscription
- Must use API to get device info, then use alternative remote tool

**C# Integration Recommendations:**
1. **Not Recommended** as primary remote control solution for external integration
2. **Use Case:** If already using NinjaOne RMM, integrate API for device inventory
3. **Workaround:** Use NinjaOne API to get device details (hostname, IP), then launch alternative remote tool (TeamViewer, AnyDesk, ScreenConnect)
4. **Alternative:** Open NinjaOne web interface to device page via Process.Start

### Important Note
NinjaOne Remote is designed as an integrated feature of the NinjaOne platform and does not support external programmatic launching. Organizations requiring API-driven remote control should consider ConnectWise Control, TeamViewer, or AnyDesk instead.

### Sources
- [NinjaOne Public API](https://app.ninjarmm.com/apidocs/)
- [NinjaOne Remote Documentation](https://www.ninjaone.com/docs/endpoint-management/remote-control/ninjaone-remote/)
- [NinjaOne Pricing](https://www.ninjaone.com/pricing/)
- [NinjaOne Pricing Review 2025](https://tekpon.com/software/ninjaone/pricing/)
- [How to Set Up API OAuth Token](https://www.ninjaone.com/docs/integrations/how-to-set-up-api-oauth-token/)

---

## 5. ManageEngine Endpoint Central (Desktop Central)

### Overview
ManageEngine Endpoint Central is a comprehensive unified endpoint management (UEM) solution with remote control capabilities and REST API support.

### Integration Methods

#### A. REST API
**Documentation:**
- [ManageEngine Endpoint Central API](https://www.manageengine.com/products/desktop-central/api/)
- [API Documentation](https://www.manageengine.com/products/desktop-central/api-doc.html)

**API Structure:**
```
<Server URL>/api/{Version}/{Entity}/{Operation|Action}
```

**Authentication:**
- POST request to `/api/1.4/desktop/authentication`
- Credentials submitted via request body
- Returns authentication token
- Token included in `Authorization` HTTP header for subsequent requests

**API Explorer:**
- Built into Endpoint Central console
- Access: Admin > Integrations > API Explorer
- Provides OpenAPI Specification (OAS) in JSON/YAML
- Interactive testing interface

**C# Authentication Example:**
```csharp
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

public class EndpointCentralClient
{
    private readonly string _baseUrl;
    private string _authToken;
    private readonly HttpClient _client;

    public EndpointCentralClient(string serverUrl)
    {
        _baseUrl = serverUrl;
        _client = new HttpClient();
    }

    public async Task<bool> AuthenticateAsync(string username, string password)
    {
        var authUrl = $"{_baseUrl}/api/1.4/desktop/authentication";

        var authData = new
        {
            username = username,
            password = password
        };

        var content = new StringContent(
            JsonConvert.SerializeObject(authData),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _client.PostAsync(authUrl, content);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            var authResult = JsonConvert.DeserializeObject<AuthResponse>(result);
            _authToken = authResult.Token;

            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_authToken}");
            return true;
        }

        return false;
    }
}

public class AuthResponse
{
    public string Token { get; set; }
}
```

**API Capabilities:**
- Device inventory management
- Software deployment
- Patch management
- Configuration management
- License management
- **Note:** Specific remote control launch endpoint not publicly documented

#### B. Remote Control Access
**Web Interface Method:**
- Navigate to Tools > Remote Control
- Click "Connect" button for target device
- Launches remote control session via web interface

**Direct Connection:**
**Documentation:** [Remote Desktop Connection - Direct Connection](https://www.manageengine.com/products/desktop-central/direct-connection-in-remote-control.html)

**Requirements:**
- Enable UDP communication on network
- Gateway port: Default 8443 (UDP)
- Both agent and viewer machines must support UDP
- Only supports Windows agents with HTML5 viewer

**Benefits:**
- Reduced bandwidth consumption
- Lower latency
- Direct peer-to-peer connection when possible

**Configuration:**
```
Gateway Port: 8443 (UDP)
Protocol: UDP
Supported: Windows agents + HTML5 viewer
```

**C# Integration Approach:**
```csharp
// Remote control typically launched via web interface
// For automation, you would need to:

public void OpenRemoteControl(string deviceId, string endpointCentralUrl)
{
    // Option 1: Open web interface to remote control page
    string url = $"{endpointCentralUrl}/Tools/RemoteControl?deviceId={deviceId}";

    ProcessStartInfo psi = new ProcessStartInfo
    {
        FileName = url,
        UseShellExecute = true
    };
    Process.Start(psi);
}

// Option 2: Check API Explorer for undocumented remote control endpoints
public async Task<string> GetRemoteControlUrl(string deviceId)
{
    // This would require access to API Explorer in your instance
    // Endpoint format may vary, check your API documentation
    var url = $"{_baseUrl}/api/1.4/remotecontrol/launch";

    var requestData = new { deviceId = deviceId };
    var content = new StringContent(
        JsonConvert.SerializeObject(requestData),
        Encoding.UTF8,
        "application/json"
    );

    var response = await _client.PostAsync(url, content);
    // Parse response for remote control URL or session token

    return await response.Content.ReadAsStringAsync();
}
```

### Pricing & Licensing
- **Starting Price:** $795/year
- **Licensing Model:** Based on number of endpoints and users
- **Flexible Slabs:** Not required to buy only prescribed slabs
- **License Types:** Both subscription and perpetual licensing available
- **Special Discounts:** Available for:
  - Educational institutions
  - Government organizations
  - Non-profit organizations
- **API Access:** Included with license (no additional cost)
- **Cloud API:** Available for cloud-based deployments

**Contact:** sales@manageengine.com for custom pricing

### Implementation Complexity
**Rating:** Medium

**Pros:**
- Comprehensive REST API with OpenAPI specification
- Built-in API Explorer for testing
- OAuth-style token authentication
- Flexible licensing options
- Reasonable starting price
- API access included

**Cons:**
- Remote control launch endpoint not clearly documented
- May require web interface for remote control access
- UDP configuration needed for direct connections
- Windows-only for direct connection feature
- API documentation requires accessing API Explorer in console
- Limited public code examples

**C# Integration Recommendations:**
1. **Use REST API** for device inventory and management
2. **Check API Explorer** in your Endpoint Central instance for remote control endpoints
3. **Fallback:** Launch web interface to Tools > Remote Control page
4. **Alternative:** Use API to get device hostname/IP, then use alternative remote tool
5. **Best Practice:** Contact ManageEngine support for remote control API documentation

### Important Notes
- The API supports extensive device management operations
- Remote control launch via API is not well-documented publicly
- Your instance's API Explorer may have additional endpoints not in public docs
- Consider reaching out to ManageEngine support for specific remote control API guidance

### Sources
- [ManageEngine Endpoint Central API](https://www.manageengine.com/products/desktop-central/api/)
- [Connecting to Remote Desktop](https://www.manageengine.com/products/desktop-central/help/remote_desktop_sharing/accessing_remote_desktop.html)
- [Remote Desktop Connection - Direct Connection](https://www.manageengine.com/products/desktop-central/direct-connection-in-remote-control.html)
- [ManageEngine Endpoint Central Pricing](https://www.manageengine.com/products/desktop-central/pricing.html)
- [Licensing the Product](https://www.manageengine.com/products/desktop-central/help/getting_started/licensing_desktop_central.html)

---

## 6. Dameware Remote Everywhere (DRE)

### Overview
Dameware Remote Everywhere is a SolarWinds cloud-based remote support solution with comprehensive REST API support for session creation and management.

### Integration Methods

#### A. REST API
**Documentation:** [Configure the New Session API in DRE](https://documentation.solarwinds.com/en/success_center/dre/content/configure-new-session-api.htm)

**Authentication:**
- **Public API Key** required
- Access keys via: Admin Area > Profile > APIs
- Uses unique account identifier (UID)

**API Endpoint Structure:**
```
HTTP GET or POST
Parameters: URL Encoded (UTF-8)
Parameter order: Flexible
```

**New Session API Parameters:**

| Parameter | Type | Required | Max Length | Description |
|-----------|------|----------|------------|-------------|
| `uid` | String | Yes | - | Account identifier from APIs section |
| `department` | Integer | No | - | Department ID from Management > Departments |
| `tech` | String | No | - | Technician username (email address) |
| `customer_name` | String | No | 128 chars | Customer/end-user name |
| `customer_email` | String | No | 64 chars | Customer email address |
| `customer_number` | String | No | 32 chars | Customer ID or ticket number |
| `problem_description` | String | No | 1024 chars | Issue description |
| `ask_customer_info` | Boolean | No | - | Allow customer to modify pre-filled data (1 = yes) |

**Routing Logic:**
- Both department & tech incorrect → General queue
- Invalid department, valid tech → Routes to technician
- Valid department, invalid tech → Routes to department queue
- Both valid with discrepancy → Routes to technician

**C# Implementation Example:**
```csharp
using System;
using System.Net.Http;
using System.Web;
using System.Threading.Tasks;

public class DamewareClient
{
    private readonly string _apiKey;
    private readonly string _uid;
    private readonly HttpClient _client;

    public DamewareClient(string apiKey, string uid)
    {
        _apiKey = apiKey;
        _uid = uid;
        _client = new HttpClient();
    }

    public async Task<SessionResponse> CreateNewSessionAsync(
        string customerName = null,
        string customerEmail = null,
        string problemDescription = null,
        string technicianEmail = null,
        int? departmentId = null,
        string customerNumber = null,
        bool askCustomerInfo = false)
    {
        // Build query parameters
        var queryParams = HttpUtility.ParseQueryString(string.Empty);
        queryParams["uid"] = _uid;

        if (!string.IsNullOrEmpty(customerName))
            queryParams["customer_name"] = customerName;

        if (!string.IsNullOrEmpty(customerEmail))
            queryParams["customer_email"] = customerEmail;

        if (!string.IsNullOrEmpty(problemDescription))
            queryParams["problem_description"] = problemDescription;

        if (!string.IsNullOrEmpty(technicianEmail))
            queryParams["tech"] = technicianEmail;

        if (departmentId.HasValue)
            queryParams["department"] = departmentId.Value.ToString();

        if (!string.IsNullOrEmpty(customerNumber))
            queryParams["customer_number"] = customerNumber;

        if (askCustomerInfo)
            queryParams["ask_customer_info"] = "1";

        // Build API URL
        string apiUrl = $"https://your-dre-server.com/api/newsession?{queryParams}";

        // Make request
        var response = await _client.GetAsync(apiUrl);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            // Parse response - typically contains session URL or ID
            return ParseSessionResponse(content);
        }

        throw new Exception($"Session creation failed: {response.StatusCode}");
    }

    private SessionResponse ParseSessionResponse(string content)
    {
        // Parse JSON or XML response based on DRE configuration
        // Response typically includes session URL for customer
        return new SessionResponse
        {
            SessionUrl = content, // Simplified - actual parsing needed
            Success = true
        };
    }
}

public class SessionResponse
{
    public string SessionUrl { get; set; }
    public string SessionId { get; set; }
    public bool Success { get; set; }
}

// Usage example
public async Task LaunchDamewareSession()
{
    var client = new DamewareClient("your_api_key", "your_uid");

    var session = await client.CreateNewSessionAsync(
        customerName: "John Doe",
        customerEmail: "john.doe@example.com",
        problemDescription: "Cannot connect to printer",
        technicianEmail: "tech@company.com"
    );

    // Launch session URL in browser or provide to customer
    if (session.Success)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = session.SessionUrl,
            UseShellExecute = true
        };
        Process.Start(psi);
    }
}
```

#### B. Legacy API
**Documentation:** [Legacy APIs in DRE](https://documentation.solarwinds.com/en/success_center/dre/content/configure-apis.htm)

**Capabilities:**
- Create new sessions
- Insert custom landing pages
- Add deferred support requests
- Validate requests

**Recommendation:** SolarWinds recommends using REST API for new integrations and updating existing legacy API calls to REST.

#### C. Deferred Support Request API
**Documentation:** [Configure the Deferred Support Request API](https://documentation.solarwinds.com/en/success_center/dre/content/configure-deferred-support-request-api.htm)

**Purpose:** Allow customers to submit support requests for technicians to handle later

**Parameters:** Similar to New Session API with additional scheduling options

### Pricing & Licensing
- **Pricing:** Contact SolarWinds for quotes
- **License Type:** Per-technician or per-endpoint options available
- **API Access:** Requires REST API key from account
- **API Key Management:**
  - Create multiple keys with different permissions
  - Set expiration dates
  - IP address whitelisting
  - Public/Private key pairs for security

**API Key Configuration:**
- Admin Area > Profile > APIs
- Support for multiple keys
- Individual permissions per key
- Expiration control
- IP restrictions for enhanced security

### Implementation Complexity
**Rating:** Medium

**Pros:**
- Well-documented New Session API
- Flexible parameter structure (no required order)
- Pre-configuration of session details
- Supports both GET and POST
- Multiple API keys with granular permissions
- Good routing logic for technician assignment
- UTF-8 encoding support

**Cons:**
- Requires Dameware account and API key setup
- Pricing not transparent (contact sales)
- Legacy API deprecated (must use REST)
- Limited public code examples
- Session response format varies
- Requires SolarWinds/Dameware infrastructure

**C# Integration Recommendations:**
1. **Use New Session API** for creating remote support sessions
2. **Pre-configure session details** to streamline technician workflow
3. **Implement proper URL encoding** for all parameters
4. **Store API keys securely** (Windows Credential Manager or secure config)
5. **Consider Deferred Support API** for asynchronous support workflows
6. **Use multiple API keys** for different departments or applications

### Important Notes
- Session creation returns a URL that customer uses to connect
- Technician receives notification in Dameware console
- Sessions can be pre-assigned to specific technicians or departments
- Customer information can be made editable or locked
- API supports integration with ticketing systems

### Sources
- [Configure the New Session API in DRE](https://documentation.solarwinds.com/en/success_center/dre/content/configure-new-session-api.htm)
- [Legacy APIs in DRE](https://documentation.solarwinds.com/en/success_center/dre/content/configure-apis.htm)
- [Configure and Manage API Keys in DRE](https://documentation.solarwinds.com/en/success_center/dre/content/configure_api_key.htm)
- [Dameware Remote Everywhere Integration](https://documentation.solarwinds.com/en/success_center/swsd/content/completeguidetoswsd/integrations-dameware.htm)

---

## 7. RemotePC

### Overview
RemotePC is a remote access solution offering Enterprise APIs for integration with business applications and workflows.

### Integration Methods

#### A. REST API
**Documentation:** [RemotePC Enterprise APIs](https://www.remotepc.com/enterprise-api)

**Authentication:**
- **API Key Authentication** with bearer token
- **IP Whitelisting** for security
- Header format: `Authorization: Bearer <api_key>`
- Content-type: `application/json`

**API Key Generation:**
1. Log in to RemotePC Enterprise account
2. Navigate to My Account > API Keys
3. Enter account password
4. Click View to display API key
5. Click Copy Key to clipboard

**Primary API Endpoints (13 Total):**

**1. User Management:**
- `POST /rpcnew/api/msp/user/invite` - Invite users
- `POST /rpcnew/api/msp/user/create` - Create users
- `DELETE /rpcnew/api/msp/user/delete` - Delete users
- `GET /rpcnew/api/msp/user/list` - Get users list

**2. Computer Management:**
- `GET /rpcnew/api/msp/computer/list/{username}` - List user's computers
- `POST /rpcnew/api/msp/computer/assign` - Assign computer to user
- `POST /rpcnew/api/msp/computer/unassign` - Unassign computer
- `POST /rpcnew/api/msp/computer/comment` - Add comment to device

**3. Remote Access (KEY ENDPOINT):**
- `POST /rpcnew/api/msp/computer/get/connectUrl` - **Get Web Viewer connection URL**

**4. Group Management:**
- `POST /rpcnew/api/msp/group/create` - Create groups
- `POST /rpcnew/api/msp/group/move` - Move computers to groups
- `POST /rpcnew/api/msp/group/remove` - Remove computers from groups

**Remote Session Launch Implementation:**

**Endpoint:** `https://web1.remotepc.com/rpcnew/api/msp/computer/get/connectUrl`

**Method:** POST

**Required Parameters:**
- `machine_id` (mandatory) - Device identifier
- `username` (mandatory) - RemotePC username

**Response:**
```json
{
    "url": "https://login.remotepc.com/rpcnew/viewer/redirect/msp/connect/process/[user]"
}
```

**C# Implementation Example:**
```csharp
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class RemotePCClient
{
    private readonly string _apiKey;
    private readonly HttpClient _client;
    private const string BaseUrl = "https://web1.remotepc.com/rpcnew/api/msp";

    public RemotePCClient(string apiKey)
    {
        _apiKey = apiKey;
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    // Get list of computers for a user
    public async Task<ComputerListResponse> GetComputersAsync(string username)
    {
        var url = $"{BaseUrl}/computer/list/{username}";
        var response = await _client.GetAsync(url);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ComputerListResponse>(content);
        }

        throw new HttpRequestException($"Failed to get computers: {response.StatusCode}");
    }

    // Get Web Viewer connection URL for remote session
    public async Task<string> GetConnectionUrlAsync(string machineId, string username)
    {
        var url = $"{BaseUrl}/computer/get/connectUrl";

        var requestData = new
        {
            machine_id = machineId,
            username = username
        };

        var content = new StringContent(
            JsonConvert.SerializeObject(requestData),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _client.PostAsync(url, content);

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<ConnectionUrlResponse>(responseContent);
            return result.Url;
        }

        throw new HttpRequestException($"Failed to get connection URL: {response.StatusCode}");
    }

    // Launch remote session in browser
    public async Task LaunchRemoteSessionAsync(string machineId, string username)
    {
        var connectionUrl = await GetConnectionUrlAsync(machineId, username);

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = connectionUrl,
            UseShellExecute = true
        };

        Process.Start(psi);
    }

    // Create new user
    public async Task<UserResponse> CreateUserAsync(
        string firstname,
        string lastname,
        string username,
        string password,
        bool enable2FA = false,
        bool enableSSO = false)
    {
        var url = $"{BaseUrl}/user/create";

        var requestData = new
        {
            firstname = firstname,
            lastname = lastname,
            username = username,
            password = password,
            enable_2fa = enable2FA ? 1 : 0,
            enable_sso = enableSSO ? 1 : 0
        };

        var content = new StringContent(
            JsonConvert.SerializeObject(requestData),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _client.PostAsync(url, content);

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<UserResponse>(responseContent);
        }

        throw new HttpRequestException($"Failed to create user: {response.StatusCode}");
    }
}

// Response Models
public class ComputerListResponse
{
    public List<Computer> Computers { get; set; }
}

public class Computer
{
    public string MachineId { get; set; }
    public string Hostname { get; set; }
    public string OsVersion { get; set; }
    public string IpAddress { get; set; }
    public DateTime? LastSessionTime { get; set; }
    public bool IsOnline { get; set; }
}

public class ConnectionUrlResponse
{
    public string Url { get; set; }
}

public class UserResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string UserId { get; set; }
}

// Usage Example
public async Task Example()
{
    var client = new RemotePCClient("your_api_key_here");

    // Get list of computers
    var computers = await client.GetComputersAsync("user@company.com");

    // Launch remote session to first computer
    if (computers.Computers.Any())
    {
        var firstComputer = computers.Computers.First();
        await client.LaunchRemoteSessionAsync(
            firstComputer.MachineId,
            "user@company.com"
        );
    }
}
```

**API Response Codes:**
- **200** - Success
- **400** - Invalid Parameters
- **401** - Unauthorized
- **403** - Invalid Request
- **404** - Not Found
- **500** - Server Error

**API Limitations:**
- Maximum 500 records per request
- Pagination support for larger datasets
- IP whitelisting required for security
- Rate limiting may apply (not specified in docs)

### Pricing & Licensing
- **Pricing:** Contact RemotePC for enterprise quotes
- **License Type:** Enterprise plan required for API access
- **Free Plan:** No API access
- **API Access:** Included with Enterprise plan
- **Support:** Available to Enterprise customers

### Implementation Complexity
**Rating:** Easy

**Pros:**
- **Excellent API for remote session launching**
- Simple REST API with JSON format
- Clear endpoint for getting connection URLs
- Comprehensive device and user management
- Bearer token authentication (straightforward)
- Good documentation with parameter specifications
- 13 well-defined endpoints
- Standard HTTP response codes
- IP whitelisting for security

**Cons:**
- No CLI support
- No custom URL scheme
- Requires Enterprise plan for API access
- Pricing not transparent (contact sales)
- Connection launches web viewer (not native app)
- Maximum 500 records per request
- Limited public code examples
- No .NET SDK (must use HttpClient)

**C# Integration Recommendations:**
1. **Primary Method:** Use `get/connectUrl` API endpoint for remote sessions
2. **Workflow:**
   - Get device list via API
   - Find target device by hostname or ID
   - Request connection URL
   - Launch URL in browser via Process.Start
3. **Security:**
   - Store API key in Windows Credential Manager or encrypted config
   - Implement IP whitelisting
   - Consider 2FA for user accounts
4. **Best Practice:**
   - Cache device lists to reduce API calls
   - Implement proper error handling for all HTTP status codes
   - Handle pagination for large device inventories

### Important Notes
- RemotePC Enterprise API is specifically designed for MSP and enterprise use cases
- The web viewer connection is browser-based (not a native application)
- Sessions require RemotePC client installed on target machines
- API supports 2FA and SSO configuration for enhanced security
- All API requests must include proper authentication headers

### Sources
- [RemotePC Enterprise APIs](https://www.remotepc.com/enterprise-api)
- [RemotePC Enterprise Plan FAQs](https://www.remotepc.com/faq_dashboard)
- [RemotePC Features](https://www.remotepc.com/remote-desktop-features)

---

## 8. LogMeIn/GoToAssist (GoTo Resolve)

### Overview
LogMeIn GoToAssist Remote Support has evolved into LogMeIn Resolve. The solution provides REST API capabilities for creating and managing remote support sessions.

### Integration Methods

#### A. GoToAssist Remote Support API
**Documentation:** [GoTo Developer Center - GoToAssist Remote Support API Overview](https://goto-developer.logmeininc.com/gotoassist-remote-support-api-overview)

**API Capabilities:**
- Initiate attended remote support sessions from external applications
- Initiate unattended remote support sessions
- Retrieve session information for external applications
- Integration for incident tracking, reporting, billing, and auditing

**Authentication:**
- OAuth 2.0 authentication
- Client ID and Client Secret from GoTo Developer Center
- Bearer token for API calls

**Session Creation:**
- POST request to create session endpoint
- Returns multiple connection links:
  - Link for TeamViewer client connection
  - Link for QuickSupport module connection
  - Web-based connection link
- Session provides ID and access credentials

**Integration Examples:**
- Salesforce integration documented
- Requires linking Client ID and Client Secret
- Pre-built connectors for major platforms

**C# Implementation Concept:**
```csharp
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class GoToAssistClient
{
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly HttpClient _client;
    private string _accessToken;

    public GoToAssistClient(string clientId, string clientSecret)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        _client = new HttpClient();
    }

    // OAuth 2.0 Authentication
    public async Task<bool> AuthenticateAsync()
    {
        // OAuth flow implementation
        // This is a simplified example - actual implementation requires OAuth flow
        var tokenUrl = "https://api.getgo.com/oauth/v2/token";

        var requestData = new
        {
            grant_type = "client_credentials",
            client_id = _clientId,
            client_secret = _clientSecret
        };

        var content = new StringContent(
            JsonConvert.SerializeObject(requestData),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _client.PostAsync(tokenUrl, content);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(result);
            _accessToken = tokenResponse.AccessToken;

            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_accessToken}");
            return true;
        }

        return false;
    }

    // Create remote support session
    public async Task<SessionInfo> CreateSessionAsync(string customerName = null)
    {
        var apiUrl = "https://api.getgo.com/G2A/rest/v1/sessions";

        var sessionData = new
        {
            customerName = customerName
        };

        var content = new StringContent(
            JsonConvert.SerializeObject(sessionData),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _client.PostAsync(apiUrl, content);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<SessionInfo>(result);
        }

        throw new Exception($"Session creation failed: {response.StatusCode}");
    }
}

public class TokenResponse
{
    public string AccessToken { get; set; }
    public string TokenType { get; set; }
    public int ExpiresIn { get; set; }
}

public class SessionInfo
{
    public string SessionId { get; set; }
    public string CustomerJoinUrl { get; set; }
    public string TechnicianJoinUrl { get; set; }
    public string SessionCode { get; set; }
}
```

#### B. Migration to LogMeIn Resolve
**Status:** GoToAssist is being replaced by LogMeIn Resolve

**Migration Notes:**
- LogMeIn Resolve is the next evolution of GoToAssist
- Companies transitioning users to new platform
- Existing GoToAssist deployments still supported
- API availability for Resolve not fully documented in search results

**Documentation:** [Upgrading from GoToAssist: First Steps with LogMeIn Resolve](https://support.logmein.com/resolve/help/migrating-from-gotoassist-first-steps-with-logmein-resolve)

### Pricing & Licensing
- **Pricing:** Contact LogMeIn for current quotes
- **Legacy Product:** GoToAssist pricing being phased out
- **Resolve Pricing:** New pricing structure for LogMeIn Resolve
- **API Access:** Available with enterprise/developer accounts
- **Developer Portal:** Requires registration at GoTo Developer Center

### Implementation Complexity
**Rating:** Medium

**Pros:**
- Established API for session creation
- OAuth 2.0 standard authentication
- Supports both attended and unattended sessions
- Session information retrievable for external systems
- Pre-built integrations available (Salesforce, etc.)
- Developer portal with documentation

**Cons:**
- **Product in transition** (GoToAssist → LogMeIn Resolve)
- API documentation scattered between old and new platforms
- No custom URL scheme
- No CLI support
- OAuth flow adds complexity
- Pricing not transparent
- Unclear API future with product migration
- Limited current documentation for Resolve API

**C# Integration Recommendations:**
1. **Assess Migration Status:** Determine if staying with GoToAssist or moving to Resolve
2. **Developer Account:** Register at GoTo Developer Center for API credentials
3. **OAuth Implementation:** Implement proper OAuth 2.0 flow with token refresh
4. **Session Management:** Use API to create sessions and retrieve join URLs
5. **Consider Alternatives:** Given product transition, evaluate other tools for new implementations
6. **Contact LogMeIn:** Verify API support roadmap for Resolve before committing

### Important Notes
- GoToAssist API remains functional but is legacy technology
- LogMeIn Resolve is the strategic direction
- API availability and capabilities for Resolve should be verified with LogMeIn
- Existing GoToAssist integrations may need migration
- Documentation for Resolve API is still evolving

### Migration Considerations
If currently using GoToAssist API:
1. Review LogMeIn Resolve migration timeline
2. Test Resolve API capabilities early
3. Plan migration of existing integrations
4. Maintain GoToAssist integration during transition
5. Work with LogMeIn support for migration assistance

### Sources
- [GoTo Developer Center - GoToAssist Remote Support API Overview](https://goto-developer.logmeininc.com/gotoassist-remote-support-api-overview)
- [Upgrading from GoToAssist: First Steps with LogMeIn Resolve](https://support.logmein.com/resolve/help/migrating-from-gotoassist-first-steps-with-logmein-resolve)
- [LogMeIn Resolve is the Next Evolution of GoToAssist](https://www.logmein.com/resources/gotoassist-is-now-logmein-resolve)
- [GoToAssist Remote Support for Salesforce](https://appexchange.salesforce.com/partners/servlet/servlet.FileDownload?file=00P4V00000ombJbUAI)

---

## General C# WPF Integration Patterns

### 1. Process.Start with URL Schemes

**Basic Implementation:**
```csharp
using System.Diagnostics;

public void LaunchRemoteToolViaUrl(string url)
{
    ProcessStartInfo psi = new ProcessStartInfo
    {
        FileName = url,
        UseShellExecute = true
    };

    Process.Start(psi);
}
```

**Use Cases:**
- ScreenConnect session URLs
- RemotePC connection URLs
- Dameware session URLs
- Web-based remote viewers

### 2. Process.Start with CLI Tools

**Basic Implementation:**
```csharp
using System.Diagnostics;

public void LaunchRemoteToolViaCLI(string executablePath, string arguments)
{
    ProcessStartInfo psi = new ProcessStartInfo
    {
        FileName = executablePath,
        Arguments = arguments,
        UseShellExecute = false,
        CreateNoWindow = false
    };

    Process.Start(psi);
}

// Example: AnyDesk
public void ConnectAnyDesk(string remoteId)
{
    LaunchRemoteToolViaCLI(
        @"C:\Program Files (x86)\AnyDesk\AnyDesk.exe",
        $"{remoteId} --plain"
    );
}

// Example: TeamViewer
public void ConnectTeamViewer(string remoteId, string password)
{
    string base64Pass = Convert.ToBase64String(
        Encoding.UTF8.GetBytes(password)
    );

    LaunchRemoteToolViaCLI(
        @"C:\Program Files\TeamViewer\TeamViewer.exe",
        $"--id {remoteId} --password {base64Pass}"
    );
}
```

**Use Cases:**
- AnyDesk connections
- TeamViewer connections
- Any tool with CLI support

### 3. REST API Integration

**HttpClient Pattern:**
```csharp
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

public class RemoteControlApiClient
{
    private readonly HttpClient _client;
    private readonly string _baseUrl;

    public RemoteControlApiClient(string baseUrl, string apiKey)
    {
        _baseUrl = baseUrl;
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<T> GetAsync<T>(string endpoint)
    {
        var response = await _client.GetAsync($"{_baseUrl}/{endpoint}");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(content);
    }

    public async Task<T> PostAsync<T>(string endpoint, object data)
    {
        var json = JsonConvert.SerializeObject(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync($"{_baseUrl}/{endpoint}", content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(responseContent);
    }
}
```

**Use Cases:**
- RemotePC session creation
- Dameware session creation
- ScreenConnect session management
- Any tool with REST API

### 4. Custom URL Scheme Registration

**Registry Registration (for receiving URL scheme calls):**
```csharp
using Microsoft.Win32;

public void RegisterCustomUrlScheme(string schemeName, string applicationPath)
{
    // Requires administrator privileges
    using (var key = Registry.ClassesRoot.CreateSubKey(schemeName))
    {
        key.SetValue("", $"URL:{schemeName}");
        key.SetValue("URL Protocol", "");

        using (var defaultIcon = key.CreateSubKey("DefaultIcon"))
        {
            defaultIcon.SetValue("", $"{applicationPath},1");
        }

        using (var commandKey = key.CreateSubKey(@"shell\open\command"))
        {
            commandKey.SetValue("", $"\"{applicationPath}\" \"%1\"");
        }
    }
}
```

**Use Cases:**
- Receiving remote control launch requests from other applications
- Browser-based session launching
- Integration with ticketing systems

### 5. Secure Credential Storage

**Using Windows Credential Manager:**
```csharp
using System.Runtime.InteropServices;
using System.Security;

public class CredentialManager
{
    public static void SaveCredential(string target, string username, string password)
    {
        var credential = new Credential
        {
            TargetName = target,
            UserName = username,
            CredentialBlob = password,
            CredentialBlobSize = (uint)Encoding.Unicode.GetByteCount(password),
            Persist = CRED_PERSIST.ENTERPRISE,
            Type = CRED_TYPE.GENERIC
        };

        CredWrite(ref credential, 0);
    }

    public static NetworkCredential GetCredential(string target)
    {
        IntPtr credPtr;
        if (CredRead(target, CRED_TYPE.GENERIC, 0, out credPtr))
        {
            var credential = Marshal.PtrToStructure<Credential>(credPtr);
            var password = Marshal.PtrToStringUni(
                credential.CredentialBlob,
                (int)credential.CredentialBlobSize / 2
            );

            CredFree(credPtr);

            return new NetworkCredential(credential.UserName, password);
        }

        return null;
    }

    [DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredWriteW", CharSet = CharSet.Unicode)]
    private static extern bool CredWrite([In] ref Credential userCredential, [In] UInt32 flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, CRED_TYPE type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("Advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
    private static extern bool CredFree([In] IntPtr cred);
}
```

**Use Cases:**
- Storing API keys
- Storing OAuth tokens
- Storing remote access credentials

### 6. Async/Await Pattern for API Calls

**WPF Integration:**
```csharp
public partial class MainWindow : Window
{
    private RemoteControlApiClient _apiClient;

    public MainWindow()
    {
        InitializeComponent();
        _apiClient = new RemoteControlApiClient("https://api.example.com", "api_key");
    }

    private async void LaunchRemoteSessionButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Show loading indicator
            LoadingSpinner.Visibility = Visibility.Visible;
            LaunchButton.IsEnabled = false;

            // Get selected device
            var deviceId = SelectedDeviceId;

            // Create session via API
            var session = await _apiClient.PostAsync<SessionResponse>(
                "sessions/create",
                new { deviceId = deviceId }
            );

            // Launch session
            if (!string.IsNullOrEmpty(session.ConnectionUrl))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = session.ConnectionUrl,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to launch session: {ex.Message}",
                          "Error",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
        }
        finally
        {
            // Hide loading indicator
            LoadingSpinner.Visibility = Visibility.Collapsed;
            LaunchButton.IsEnabled = true;
        }
    }
}
```

---

## Integration Complexity Summary

### Easy (Recommended for Quick Implementation)
1. **AnyDesk** - Excellent CLI, simple syntax
2. **RemotePC** - Clean REST API with connection URL endpoint

### Medium (Moderate Development Effort)
1. **ScreenConnect/ConnectWise** - Good API, URL schemes work well
2. **TeamViewer** - Multiple methods, Base64 encoding required
3. **Dameware** - Good API documentation, straightforward
4. **ManageEngine** - API available but remote control endpoint unclear
5. **NinjaOne** - API limited for remote control launching

### Hard (Significant Development Effort)
1. **LogMeIn/GoToAssist** - Product in transition, unclear API future
2. **Custom Integration** - Building from scratch with proprietary protocols

---

## Recommendations by Use Case

### For Small-Medium Business (SMB)
**Recommended:**
1. **AnyDesk** - Cost-effective, easy CLI integration
2. **ScreenConnect** - Self-hosted option, good API
3. **RemotePC** - If already using for remote access

### For Enterprise with Existing RMM
**Recommended:**
1. **NinjaOne** - If already deployed, use API for device info + alternative remote tool
2. **ManageEngine Endpoint Central** - If already deployed for UEM
3. **ScreenConnect** - Enterprise-grade with excellent API

### For MSP/Multi-Tenant
**Recommended:**
1. **ScreenConnect** - Multi-tenant support, concurrent licensing
2. **Dameware Remote Everywhere** - Department routing, good API
3. **RemotePC Enterprise** - Good API, MSP features

### For Maximum API Control
**Recommended:**
1. **ScreenConnect/ConnectWise** - Best documented API
2. **RemotePC** - Direct connection URL endpoint
3. **Dameware** - Flexible session creation

### For Simplest Integration
**Recommended:**
1. **AnyDesk** - Just shell out to CLI
2. **RemotePC** - Simple REST API
3. **ScreenConnect** - Direct URL launching

---

## Security Considerations

### API Key Management
1. **Never hardcode API keys** in source code
2. **Use Windows Credential Manager** or secure configuration
3. **Implement key rotation** policies
4. **Use separate keys** for different environments (dev, prod)
5. **Monitor API usage** for anomalies

### Authentication Best Practices
1. **Use OAuth 2.0** when available
2. **Implement token refresh** logic
3. **Store tokens securely** (encrypted)
4. **Use HTTPS** for all API calls
5. **Validate SSL certificates**

### Session Security
1. **Log all remote session launches** for audit trail
2. **Require user authentication** before launching sessions
3. **Implement session timeouts**
4. **Use role-based access control** (RBAC)
5. **Enable two-factor authentication** where supported

### Network Security
1. **Implement IP whitelisting** for API calls
2. **Use VPN or private networks** for sensitive operations
3. **Enable firewall rules** for remote control tools
4. **Monitor network traffic** for unauthorized access
5. **Use encrypted connections** for all remote sessions

---

## Testing Strategy

### Unit Testing
```csharp
[TestFixture]
public class RemoteControlClientTests
{
    [Test]
    public async Task CreateSession_ValidParameters_ReturnsSessionUrl()
    {
        // Arrange
        var client = new RemoteControlApiClient("https://test.api.com", "test_key");

        // Act
        var session = await client.CreateSessionAsync("device123");

        // Assert
        Assert.IsNotNull(session);
        Assert.IsNotEmpty(session.ConnectionUrl);
    }
}
```

### Integration Testing
1. **Test with actual APIs** in staging environment
2. **Verify session creation** and launching
3. **Test error handling** for failed API calls
4. **Validate authentication** flows
5. **Test timeout scenarios**

### User Acceptance Testing
1. **Verify UI responsiveness** during API calls
2. **Test loading indicators** and error messages
3. **Validate session launching** from application
4. **Test on multiple devices** and network conditions
5. **Gather user feedback** on workflow

---

## Conclusion

This comprehensive analysis provides technical details for integrating eight popular enterprise remote control tools into a C# WPF application. The best choice depends on:

1. **Current Infrastructure** - What tools are already deployed
2. **Budget** - Licensing costs and API access fees
3. **Integration Complexity** - Development time and resources
4. **Features Required** - API capabilities and session management
5. **Support Requirements** - Documentation and vendor support

**Top Recommendations:**
- **Best Overall API:** ScreenConnect/ConnectWise Control
- **Easiest Integration:** AnyDesk (CLI) or RemotePC (API)
- **Best for Enterprise:** ManageEngine Endpoint Central or ScreenConnect
- **Best for MSP:** ScreenConnect or Dameware Remote Everywhere

**Implementation Priority:**
1. Start with easiest integration (AnyDesk CLI or RemotePC API)
2. Implement proper authentication and credential management
3. Add error handling and logging
4. Expand to additional tools as needed
5. Monitor usage and optimize based on user feedback

---

## Additional Resources

### Official Documentation Links
- [ConnectWise Control Developer Docs](https://docs.connectwise.com/ConnectWise_Control_Documentation/Developers)
- [TeamViewer API Docs](https://webapi.teamviewer.com/api/v1/docs/index)
- [AnyDesk Support Docs](https://support.anydesk.com/)
- [NinjaOne API Docs](https://app.ninjarmm.com/apidocs/)
- [ManageEngine API Docs](https://www.manageengine.com/products/desktop-central/api/)
- [Dameware API Docs](https://documentation.solarwinds.com/en/success_center/dre/)
- [RemotePC Enterprise APIs](https://www.remotepc.com/enterprise-api)
- [GoTo Developer Center](https://goto-developer.logmeininc.com/)

### Community Resources
- Stack Overflow tags: `teamviewer-api`, `screenconnect`, `anydesk`
- GitHub repositories with integration examples
- Vendor community forums and support portals
- Reddit: r/sysadmin, r/msp for real-world experiences

### .NET Libraries
- [TeamViewer QuickSupport Integration](https://github.com/ihtfw/TeamViewer.QuickSupport.Integration)
- Newtonsoft.Json for API integration
- RestSharp as alternative to HttpClient
- CredentialManagement for secure storage

---

**Document End**
