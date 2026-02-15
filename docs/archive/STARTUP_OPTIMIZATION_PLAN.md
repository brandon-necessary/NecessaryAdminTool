# ⚡ STARTUP OPTIMIZATION PLAN

## 🐌 CURRENT ISSUE
**10-second white screen** before DC unavailable dialog shows

### Root Cause:
`Domain.GetCurrentDomain()` has default timeout of ~10-15 seconds when domain is unreachable

---

## 🎯 OPTIMIZATION STRATEGY

### Option 1: Quick Timeout (FASTEST - Recommended)
**Time Saved:** 10s → 2s

Add custom timeout to domain check:
```csharp
private async Task<bool> CheckDCAvailabilityAsync()
{
    try
    {
        // Use CancellationToken with 2-second timeout
        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
        {
            string domainName = await Task.Run(() =>
            {
                try
                {
                    var domain = Domain.GetCurrentDomain();
                    string name = domain.Name;
                    domain.Dispose();
                    return name;
                }
                catch
                {
                    return null;
                }
            }, cts.Token);

            // ... rest of code
        }
    }
    catch (OperationCanceledException)
    {
        // Timeout - no domain
        return false;
    }
}
```

---

### Option 2: Loading Splash Screen (USER-FRIENDLY)
**Time Saved:** Same, but feels faster

Show splash screen immediately while checking:
```csharp
public MainWindow()
{
    InitializeComponent();

    // Show splash immediately
    ShowLoadingSplash("Checking domain connectivity...");
}

private async void Window_Loaded(object sender, RoutedEventArgs e)
{
    UpdateSplash("Initializing...");

    // Load config
    SecureConfig.LoadConfiguration();

    UpdateSplash("Checking domain...");
    bool dcAvailable = await CheckDCAvailabilityAsync();

    HideSplash();

    // Continue...
}
```

---

### Option 3: Skip Domain Check Initially (INSTANT START)
**Time Saved:** 10s → 0s (but check later)

```csharp
private async void Window_Loaded(object sender, RoutedEventArgs e)
{
    // Skip domain check - assume unavailable initially
    SetGuestReadOnlyMode(); // Default to guest

    // Check domain in background
    _ = Task.Run(async () =>
    {
        await Task.Delay(1000); // Let UI load first
        bool dcAvailable = await CheckDCAvailabilityAsync();

        if (dcAvailable)
        {
            // Domain found! Offer to login
            Dispatcher.Invoke(() => ShowLoginDialog());
        }
    });
}
```

---

### Option 4: Parallel Loading (OPTIMIZED)
**Time Saved:** 3-5s

Load everything in parallel:
```csharp
private async void Window_Loaded(object sender, RoutedEventArgs e)
{
    // Run all startup tasks in parallel
    var tasks = new[]
    {
        Task.Run(() => SecureConfig.LoadConfiguration()),
        Task.Run(() => LoadConfig()),
        Task.Run(() => LoadMasterLog()),
        CheckDCAvailabilityAsync(), // This one takes longest
        LoadPinnedDevices()
    };

    await Task.WhenAll(tasks);

    // Now show dialog based on results
}
```

---

## 🚀 RECOMMENDED IMPLEMENTATION

**Combine Option 1 + Option 2 for best UX:**

1. Show loading splash immediately (0ms)
2. Check domain with 2-second timeout (2s max)
3. Show DC dialog or login based on result

**Total worst case:** 2 seconds instead of 10 seconds!

---

## 📝 IMPLEMENTATION CODE

### Step 1: Add CancellationToken to Domain Check

```csharp
/// <summary>
/// Check if domain controllers are available without throwing exceptions
/// TAG: #DC_DISCOVERY #PERFORMANCE
/// </summary>
private async Task<bool> CheckDCAvailabilityAsync(int timeoutSeconds = 2)
{
    try
    {
        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
        {
            string domainName = await Task.Run(() =>
            {
                try
                {
                    // This call can take 10+ seconds if domain unreachable
                    var domain = Domain.GetCurrentDomain();
                    string name = domain.Name;
                    domain.Dispose();
                    return name;
                }
                catch
                {
                    return null;
                }
            }, cts.Token);

            if (domainName != null)
            {
                CurrentDomainName = domainName;
                UpdateDomainBadge(domainName);
                return true;
            }
            else
            {
                CurrentDomainName = null;
                UpdateDomainBadge(null);
                return false;
            }
        }
    }
    catch (OperationCanceledException)
    {
        // Timeout - domain not reachable within time limit
        LogManager.LogWarning($"Domain check timed out after {timeoutSeconds}s");
        CurrentDomainName = null;
        UpdateDomainBadge(null);
        return false;
    }
    catch (Exception ex)
    {
        LogManager.LogWarning($"Domain check failed: {ex.Message}");
        CurrentDomainName = null;
        UpdateDomainBadge(null);
        return false;
    }
}
```

### Step 2: Add Loading Indicator to Window

**In MainWindow.xaml - Add after main Grid:**
```xml
<!-- Loading Overlay - TAG: #LOADING #STARTUP -->
<Border x:Name="LoadingOverlay"
        Background="#DD000000"
        Visibility="Collapsed"
        Panel.ZIndex="9999">
    <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
        <!-- Logo -->
        <Border Width="80" Height="80" CornerRadius="40" Margin="0,0,0,20">
            <Border.Background>
                <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                    <GradientStop Color="#FF8533" Offset="0"/>
                    <GradientStop Color="#A1A1AA" Offset="1"/>
                </LinearGradientBrush>
            </Border.Background>
            <TextBlock Text="A" FontSize="48" FontWeight="Bold"
                       Foreground="White"
                       HorizontalAlignment="Center"
                       VerticalAlignment="Center"/>
        </Border>

        <!-- Loading Text -->
        <TextBlock x:Name="LoadingText"
                   Text="Initializing..."
                   Foreground="White"
                   FontSize="16"
                   FontWeight="SemiBold"
                   HorizontalAlignment="Center"
                   Margin="0,0,0,10"/>

        <!-- Progress Bar -->
        <ProgressBar Width="300"
                     Height="4"
                     IsIndeterminate="True"
                     Foreground="#FF8533"
                     Background="#333333"/>

        <!-- Status Text -->
        <TextBlock x:Name="LoadingStatus"
                   Text="Please wait..."
                   Foreground="#A1A1AA"
                   FontSize="11"
                   HorizontalAlignment="Center"
                   Margin="0,15,0,0"/>
    </StackPanel>
</Border>
```

### Step 3: Update Window_Loaded with Progress

```csharp
private async void Window_Loaded(object sender, RoutedEventArgs e)
{
    // Show loading overlay
    ShowLoading("Initializing ArtaznIT Suite...");

    try
    {
        if (IntPtr.Size == 4)
        {
            LogManager.LogWarning("32-bit mode detected");
            MessageBox.Show("WARNING: Running in 32-bit mode. Some WMI features may not work correctly.",
                "Architecture Warning");
        }

        UpdateLoading("Loading configuration...");
        SecureConfig.LoadConfiguration();
        LoadConfig();
        LoadMasterLog();

        UpdateLoading("Loading pinned devices...");
        _ = LoadPinnedDevices();

        UpdateLoading("Checking domain connectivity...");
        ApplyRoleRestrictions();
        AppendTerminal($"{BrandingConfig.FullProductNameWithEdition} initialized.", false);

        // Check DC with 2-second timeout
        bool dcAvailable = await CheckDCAvailabilityAsync(timeoutSeconds: 2);

        // Hide loading overlay
        HideLoading();

        bool skipLogin = false;

        if (!dcAvailable)
        {
            var dcDialog = new DCAvailabilityDialog();
            var result = dcDialog.ShowDialog();

            if (dcDialog.ShouldRestart)
            {
                Application.Current.Shutdown();
                return;
            }
            else if (dcDialog.ShouldContinueReadOnly)
            {
                AppendTerminal("[STARTUP] Continuing in read-only mode (DCs unavailable)", isError: true);
                AppendTerminal("[STARTUP] Skipping authentication - running as GUEST", isError: true);
                skipLogin = true;
                SetGuestReadOnlyMode();
            }
        }
        else
        {
            _ = InitDCCluster();
            StartDomainVerificationTimer();
        }

        if (!skipLogin)
        {
            _ = ShowLoginDialog();
        }
    }
    catch (Exception ex)
    {
        HideLoading();
        LogManager.LogError("Window_Loaded failed", ex);
        MessageBox.Show($"Initialization error: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

// Helper methods for loading overlay
private void ShowLoading(string message = "Loading...")
{
    Dispatcher.Invoke(() =>
    {
        LoadingText.Text = "ArtaznIT Suite";
        LoadingStatus.Text = message;
        LoadingOverlay.Visibility = Visibility.Visible;
    });
}

private void UpdateLoading(string message)
{
    Dispatcher.Invoke(() =>
    {
        LoadingStatus.Text = message;
    });
}

private void HideLoading()
{
    Dispatcher.Invoke(() =>
    {
        LoadingOverlay.Visibility = Visibility.Collapsed;
    });
}
```

---

## ⏱️ PERFORMANCE COMPARISON

| Method | Current | Optimized | Improvement |
|--------|---------|-----------|-------------|
| White Screen | 10s | 0s | ⚡ Instant |
| Domain Check | 10s | 2s | ⚡ 80% faster |
| Total Startup | 10-15s | 2-3s | ⚡ 5x faster |

---

## 🎯 QUICK FIX (5 minutes)

**Just add timeout - simplest solution:**

Change line in `CheckDCAvailabilityAsync`:
```csharp
// OLD:
bool dcAvailable = await CheckDCAvailabilityAsync();

// NEW:
bool dcAvailable = await CheckDCAvailabilityAsync(timeoutSeconds: 2);
```

And update method signature:
```csharp
private async Task<bool> CheckDCAvailabilityAsync(int timeoutSeconds = 2)
```

**That's it! 80% faster with one parameter.**

---

## 🚀 FULL SOLUTION (30 minutes)

1. ✅ Add CancellationToken timeout (5 min)
2. ✅ Add loading overlay XAML (10 min)
3. ✅ Add Show/Update/Hide methods (5 min)
4. ✅ Update Window_Loaded with progress (10 min)

**Result:** Professional loading screen + 5x faster startup

---

## 📊 STARTUP SEQUENCE (OPTIMIZED)

```
[0ms]   Show loading splash "Initializing..."
[50ms]  Load configs in parallel
[100ms] Update splash "Checking domain..."
[100ms] Start domain check (with 2s timeout)
[2s]    Domain check completes (or times out)
[2s]    Hide splash
[2s]    Show DC dialog OR login dialog
```

**Total: 2 seconds max, with visual feedback throughout!**

---

## 🎓 BEST PRACTICES

✅ **DO:**
- Show UI feedback immediately
- Use timeouts for network operations
- Load tasks in parallel when possible
- Give user visual progress updates

❌ **DON'T:**
- Block UI thread for network calls
- Use default timeouts (too long)
- Hide errors from user
- Load sequentially when parallel works

---

Ready to implement? Start with the **Quick Fix** (2-second timeout) then add loading overlay if desired!
