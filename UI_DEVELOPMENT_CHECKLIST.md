# UI Development Checklist

**Purpose:** Ensure all UI development follows Modern UI standards (Fluent Design, v2.0)
**Last Updated:** February 15, 2026
**Required For:** All new windows, dialogs, controls, and UI components

---

## 📋 Pre-Development Checklist

Before creating any new UI component, verify:

- [ ] UI design aligns with Fluent Design System
- [ ] Component doesn't already exist (check `UI/Components/`)
- [ ] User interaction pattern is consistent with existing UI
- [ ] Accessibility requirements considered (keyboard navigation, screen readers)

---

## 🎨 Fluent Design Standards (MANDATORY)

### ✅ 1. Theme Integration

**Required:** All new XAML files must reference Fluent theme

```xaml
<!-- TAG: #AUTO_UPDATE_UI_ENGINE #FLUENT_DESIGN -->
<UserControl.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="/UI/Themes/Fluent.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</UserControl.Resources>
```

**Checklist:**
- [ ] Fluent.xaml referenced in XAML
- [ ] Using `MicaBrush` for backgrounds
- [ ] Using `MicaAltBrush` for elevated surfaces
- [ ] Using `AccentBrush` for primary actions
- [ ] Using `FluentCornerRadius` for rounded corners
- [ ] Using semantic colors: `SuccessBrush`, `WarningBrush`, `ErrorBrush`, `InfoBrush`

### ✅ 2. Color Palette (DO NOT hardcode colors)

**Always use semantic brushes from Fluent.xaml:**

```xaml
<!-- CORRECT -->
<Border Background="{StaticResource MicaBrush}"/>
<Button Background="{StaticResource AccentBrush}"/>
<TextBlock Foreground="{StaticResource SuccessBrush}"/>

<!-- WRONG - DO NOT DO THIS -->
<Border Background="#F3F3F3"/>  ❌
<Button Background="Blue"/>     ❌
<TextBlock Foreground="#00FF00"/> ❌
```

**Semantic Colors:**
- `MicaBrush` - Main background (windows, panels)
- `MicaAltBrush` - Elevated surfaces (cards, dialogs)
- `AccentBrush` - Primary actions, highlights
- `SuccessBrush` - Success states (#10B981)
- `WarningBrush` - Warning states (#F59E0B)
- `ErrorBrush` - Error states (#EF4444)
- `InfoBrush` - Info states (#3B82F6)

### ✅ 3. Corner Radius (Consistent rounded corners)

**Required:** Use FluentCornerRadius resources

```xaml
<Border CornerRadius="{StaticResource FluentCornerRadius}"/>        <!-- 8px -->
<Border CornerRadius="{StaticResource FluentCornerRadiusSmall}"/>   <!-- 4px -->
<Border CornerRadius="{StaticResource FluentCornerRadiusLarge}"/>   <!-- 12px -->
```

**DO NOT hardcode:** `CornerRadius="5"` ❌

### ✅ 4. Spacing & Layout

**Standard spacing values:**
- Small: 4px
- Medium: 8px
- Large: 12px
- XLarge: 16px

```xaml
<!-- Grid spacing -->
<Grid Margin="12" RowSpacing="8" ColumnSpacing="12"/>

<!-- Card padding -->
<Border Padding="16,12"/>

<!-- Button margins -->
<Button Margin="0,0,8,0"/>
```

---

## 🔔 Toast Notifications (MANDATORY)

### ✅ 1. NO MessageBox.Show() ALLOWED

**NEVER use MessageBox.Show() in any new code!**

```csharp
// WRONG - DO NOT USE
MessageBox.Show("Error occurred", "Error"); ❌

// CORRECT - Use ToastManager
Managers.UI.ToastManager.ShowError("Error occurred"); ✅
```

### ✅ 2. Toast Usage Patterns

**Always use appropriate toast type:**

```csharp
// Success - Green checkmark
ToastManager.ShowSuccess("Computer scanned successfully");

// Info - Blue info icon
ToastManager.ShowInfo("Scanning 15 computers...");

// Warning - Yellow warning icon
ToastManager.ShowWarning("Some computers unreachable");

// Error - Red X icon
ToastManager.ShowError("Failed to connect to Active Directory");
```

### ✅ 3. When to Use Each Type

**Success:**
- Operation completed successfully
- Data saved/updated
- Connection established
- Task finished

**Info:**
- Operation started
- Status updates
- Informational messages
- Progress notifications

**Warning:**
- Non-critical issues
- Partial failures
- Deprecated feature usage
- Configuration issues

**Error:**
- Critical failures
- Exceptions caught
- Validation failures
- Connection errors

### ✅ 4. Toast Integration Checklist

- [ ] All `MessageBox.Show()` replaced with `ToastManager`
- [ ] Success toasts on workflow completion
- [ ] Error toasts in all `catch` blocks
- [ ] Info toasts for long-running operations
- [ ] Warning toasts for validation issues
- [ ] Toast messages are concise (< 60 characters)
- [ ] Toast messages are user-friendly (no technical jargon)

---

## ⌨️ Keyboard Shortcuts (MANDATORY)

### ✅ 1. Command Palette Integration

**All new actions MUST be added to Command Palette:**

Location: `UI/Components/CommandPalette.xaml.cs`

```csharp
// TAG: #AUTO_UPDATE_UI_ENGINE #COMMAND_PALETTE
private void InitializeCommands()
{
    _commands.Add(new PaletteCommand
    {
        Name = "Your Feature Name",
        Description = "Brief description",
        Category = "Category",
        Shortcut = "Ctrl+Alt+X",
        Icon = "\uE8F4",
        Action = () => YourFeatureMethod()
    });
}
```

**Checklist:**
- [ ] Command added to `_commands` list
- [ ] Unique keyboard shortcut assigned
- [ ] Shortcut doesn't conflict with existing shortcuts
- [ ] Icon from Segoe MDL2 Assets
- [ ] Category assigned (Navigation/Actions/Tools/Settings/Help)
- [ ] Description is clear and concise

### ✅ 2. Standard Keyboard Shortcuts

**Reserved shortcuts (DO NOT use):**
- `Ctrl+K` - Command Palette
- `Ctrl+F` - Search/Find
- `Ctrl+R` - Refresh
- `Ctrl+S` - Save
- `Ctrl+N` - New
- `Ctrl+O` - Open
- `Ctrl+W` - Close window
- `Ctrl+Q` - Quit
- `F1` - Help
- `F5` - Refresh
- `Esc` - Cancel/Close

**Available patterns:**
- `Ctrl+Alt+[Letter]` - Feature-specific shortcuts
- `Ctrl+Shift+[Letter]` - Advanced features
- `F2-F12` - Function keys (avoid F5)

### ✅ 3. Keyboard Navigation

**All dialogs/windows must support:**
- [ ] Tab navigation between controls
- [ ] Enter to confirm/submit
- [ ] Escape to cancel/close
- [ ] Arrow keys for list/tree navigation
- [ ] Space for selection/toggle
- [ ] Keyboard shortcuts documented in tooltip

```xaml
<!-- Good example -->
<Button Content="Save"
        ToolTip="Save changes (Ctrl+S)"
        TabIndex="1"
        IsDefault="True"/>

<Button Content="Cancel"
        ToolTip="Cancel and close (Esc)"
        TabIndex="2"
        IsCancel="True"/>
```

---

## 🔄 Async Operations & Loading States

### ✅ 1. Skeleton Loaders (Use for all async operations)

**Required:** Show skeleton loaders during data loading

```xaml
<!-- TAG: #AUTO_UPDATE_UI_ENGINE #SKELETON_LOADERS -->
<local:SkeletonLoader x:Name="LoaderSkeleton"
                      Visibility="Visible"
                      Width="Auto" Height="100"/>

<ItemsControl x:Name="DataGrid"
              Visibility="Collapsed"/>
```

```csharp
// Show skeleton during load
LoaderSkeleton.Visibility = Visibility.Visible;
DataGrid.Visibility = Visibility.Collapsed;

// Load data async
await LoadDataAsync();

// Hide skeleton when done
LoaderSkeleton.Visibility = Visibility.Collapsed;
DataGrid.Visibility = Visibility.Visible;
```

**Checklist:**
- [ ] Skeleton loader shown before async operation
- [ ] Skeleton hidden after operation completes
- [ ] Skeleton dimensions match final content
- [ ] Error handling hides skeleton and shows error toast

### ✅ 2. Progress Indicators

**Use appropriate indicator:**

```xaml
<!-- Indeterminate (unknown duration) -->
<ProgressBar IsIndeterminate="True" Margin="12,0"/>

<!-- Determinate (known duration) -->
<ProgressBar Value="{Binding Progress}"
             Maximum="100"
             Margin="12,0"/>

<!-- Status text -->
<TextBlock Text="{Binding StatusText}"
           Foreground="Gray"
           Margin="0,4,0,0"/>
```

---

## 🏷️ Tagging Requirements (MANDATORY)

### ✅ 1. All UI Code Must Be Tagged

**Required tags:**

```csharp
// XAML files
// TAG: #AUTO_UPDATE_UI_ENGINE #FLUENT_DESIGN #YOUR_COMPONENT

// Code-behind files
// TAG: #AUTO_UPDATE_UI_ENGINE #YOUR_COMPONENT

// User controls
// TAG: #AUTO_UPDATE_UI_ENGINE #NATIVE_WPF #USER_CONTROL

// Converters
// TAG: #AUTO_UPDATE_UI_ENGINE #CONVERTERS

// Animations
// TAG: #AUTO_UPDATE_UI_ENGINE #ANIMATIONS
```

**Specific feature tags:**
- `#TOAST_NOTIFICATIONS` - Toast notification code
- `#COMMAND_PALETTE` - Command palette integration
- `#KEYBOARD_SHORTCUTS` - Keyboard shortcut handling
- `#SKELETON_LOADERS` - Loading states
- `#CARD_VIEW` - Card-based layouts
- `#AD_TREE_VIEW` - Active Directory tree
- `#SEARCH_FILTER` - Search/filter functionality

### ✅ 2. Tag Placement

```csharp
/// <summary>
/// Your component description
/// TAG: #AUTO_UPDATE_UI_ENGINE #YOUR_COMPONENT #FLUENT_DESIGN
/// </summary>
public partial class YourControl : UserControl
{
    // TAG: #AUTO_UPDATE_UI_ENGINE #INITIALIZATION
    public YourControl()
    {
        InitializeComponent();
    }

    // TAG: #AUTO_UPDATE_UI_ENGINE #EVENT_HANDLING
    private void Button_Click(object sender, RoutedEventArgs e)
    {
        // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
        ToastManager.ShowSuccess("Action completed");
    }
}
```

---

## 🎭 Icons (Segoe MDL2 Assets)

### ✅ 1. Standard Icons

**Always use Segoe MDL2 Assets font:**

```xaml
<TextBlock Text="&#xE721;"
           FontFamily="Segoe MDL2 Assets"
           FontSize="16"/>
```

**Common icons:**
- Computer: `\uE977` / `&#xE977;`
- Folder: `\uE8B7` / `&#xE8B7;`
- User: `\uE77B` / `&#xE77B;`
- Group: `\uE902` / `&#xE902;`
- Search: `\uE721` / `&#xE721;`
- Refresh: `\uE72C` / `&#xE72C;`
- Settings: `\uE713` / `&#xE713;`
- Save: `\uE74E` / `&#xE74E;`
- Delete: `\uE74D` / `&#xE74D;`
- Edit: `\uE70F` / `&#xE70F;`
- Add: `\uE710` / `&#xE710;`
- Close: `\uE711` / `&#xE711;`
- CheckMark: `\uE73E` / `&#xE73E;`
- Error: `\uE783` / `&#xE783;`
- Warning: `\uE7BA` / `&#xE7BA;`
- Info: `\uE946` / `&#xE946;`

**Icon Reference:** https://docs.microsoft.com/en-us/windows/apps/design/style/segoe-ui-symbol-font

---

## 📱 Responsive Design

### ✅ 1. Minimum Window Sizes

```csharp
// In Window constructor or XAML
MinWidth = 800;
MinHeight = 600;
```

### ✅ 2. Adaptive Layouts

**Use Grid with star sizing:**

```xaml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="250" MinWidth="200"/>  <!-- Sidebar -->
        <ColumnDefinition Width="*"/>                    <!-- Content -->
    </Grid.ColumnDefinitions>
</Grid>
```

### ✅ 3. ScrollViewer for Overflow

```xaml
<ScrollViewer VerticalScrollBarVisibility="Auto"
              HorizontalScrollBarVisibility="Disabled">
    <StackPanel>
        <!-- Content -->
    </StackPanel>
</ScrollViewer>
```

---

## ♿ Accessibility (Required)

### ✅ 1. Automation Properties

```xaml
<Button Content="Save"
        AutomationProperties.Name="Save changes"
        AutomationProperties.HelpText="Saves all pending changes to the database"/>
```

### ✅ 2. Focus Management

```csharp
// Set focus to first input on load
Loaded += (s, e) => TxtInput.Focus();

// Move focus on Enter
TxtInput.KeyDown += (s, e) =>
{
    if (e.Key == Key.Enter)
        BtnSubmit.Focus();
};
```

### ✅ 3. High Contrast Support

**Test UI in High Contrast mode:**
- Windows Settings → Accessibility → Contrast themes

---

## ✅ Final UI Component Checklist

Before committing any UI code:

**Fluent Design:**
- [ ] Fluent.xaml referenced
- [ ] Using semantic color brushes (no hardcoded colors)
- [ ] Using FluentCornerRadius (no hardcoded radii)
- [ ] Mica backgrounds applied
- [ ] Proper spacing/padding (4/8/12/16px)

**Toast Notifications:**
- [ ] No MessageBox.Show() usage
- [ ] Success toasts on completion
- [ ] Error toasts in catch blocks
- [ ] Info toasts for long operations
- [ ] Warning toasts for validation

**Keyboard Support:**
- [ ] Command Palette integration
- [ ] Keyboard shortcuts assigned
- [ ] Tab navigation works
- [ ] Enter/Escape support
- [ ] Shortcuts in tooltips

**Loading States:**
- [ ] Skeleton loaders for async operations
- [ ] Progress indicators where appropriate
- [ ] Error handling with toasts

**Code Quality:**
- [ ] All code tagged (#AUTO_UPDATE_UI_ENGINE)
- [ ] Feature-specific tags added
- [ ] XML documentation on public methods
- [ ] Error handling with logging

**Icons & Visuals:**
- [ ] Segoe MDL2 Assets icons
- [ ] Icons properly sized
- [ ] Consistent icon usage

**Responsive & Accessible:**
- [ ] Minimum window size set
- [ ] Adaptive layouts
- [ ] ScrollViewer for overflow
- [ ] AutomationProperties set
- [ ] Focus management
- [ ] High contrast tested

**Testing:**
- [ ] Visual appearance matches Fluent Design
- [ ] All keyboard shortcuts work
- [ ] Toast notifications display correctly
- [ ] Loading states show/hide properly
- [ ] No XAML errors in Output window
- [ ] Responsive to window resizing

---

## 🚫 Common Mistakes to Avoid

**DON'T:**
- ❌ Use MessageBox.Show()
- ❌ Hardcode colors (#FFFFFF, "Blue", etc.)
- ❌ Hardcode corner radius
- ❌ Forget to tag code
- ❌ Skip keyboard shortcuts
- ❌ Ignore loading states
- ❌ Forget accessibility properties
- ❌ Use PNG icons (use Segoe MDL2)
- ❌ Create UI without Command Palette entry
- ❌ Skip error handling

**DO:**
- ✅ Use ToastManager for all notifications
- ✅ Use semantic color brushes
- ✅ Use FluentCornerRadius resources
- ✅ Tag all UI code
- ✅ Add keyboard shortcuts
- ✅ Show skeleton loaders
- ✅ Set AutomationProperties
- ✅ Use Segoe MDL2 icons
- ✅ Integrate with Command Palette
- ✅ Handle errors with toasts and logging

---

## 📚 Reference Files

- `UI/Themes/Fluent.xaml` - Fluent Design resources
- `Managers/UI/ToastManager.cs` - Toast notification system
- `UI/Components/CommandPalette.xaml` - Command Palette
- `UI/Components/SkeletonLoader.xaml` - Loading skeleton
- `UI/Components/ComputerCard.xaml` - Card view example
- `UI/Components/ADTreeView.xaml` - TreeView example

---

**REMEMBER:** Consistency is key. Every UI component should feel like part of the same modern, cohesive application.

**"If it doesn't use Fluent Design, it doesn't ship."**
