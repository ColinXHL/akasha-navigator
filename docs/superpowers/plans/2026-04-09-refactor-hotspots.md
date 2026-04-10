# AkashaNavigator Refactor Hotspots Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace static singleton and service-locator usage with DI-managed runtime bridges and factories, make persistence failures explicit, and shrink the heaviest UI code-behind files without breaking the plugin system.

**Architecture:** Treat this as an engineering migration, not a search-and-replace cleanup. First lock the current baseline with tests that compile today, then change the real plugin host-object construction path (`PluginHost -> PluginContext -> PluginEngine`) to use DI-owned bridge/factory services, then remove static singletons, then refactor the UI entry points with explicit coordinators for window-only behavior.

**Tech Stack:** .NET 8, WPF, Microsoft.Extensions.DependencyInjection, xUnit, CommunityToolkit.Mvvm, WebView2, ClearScript V8

---

## File Structure

### Composition root and plugin runtime wiring
- Modify: `AkashaNavigator/Core/ServiceCollectionExtensions.cs`
  Responsibility: register all new bridge, factory, coordinator, and workflow services.
- Modify: `AkashaNavigator/Core/Bootstrapper.cs`
  Responsibility: publish the current `PlayerWindow` into a DI-owned runtime bridge and keep startup wiring in one place.
- Create: `AkashaNavigator/Core/Interfaces/IPlayerRuntimeBridge.cs`
  Responsibility: expose runtime access to the current player window and script execution queue without static globals.
- Create: `AkashaNavigator/Services/PlayerRuntimeBridge.cs`
  Responsibility: store and return the current runtime player window inside DI.
- Create: `AkashaNavigator/Core/Interfaces/IPluginHostObjectFactory.cs`
  Responsibility: build the actual host objects that `PluginEngine` adds into the V8 engine.
- Create: `AkashaNavigator/Plugins/Core/PluginHostObjectFactory.cs`
  Responsibility: assemble `player`, `window`, `overlay`, `panel`, `webview`, `subtitle`, `hotkey`, and `osd` host objects from DI services plus per-plugin runtime state.
- Modify: `AkashaNavigator/Plugins/Core/PluginEngineOptions.cs`
  Responsibility: carry bridge/factory references instead of static window getters.
- Modify: `AkashaNavigator/Plugins/Core/PluginEngine.cs`
  Responsibility: use `IPluginHostObjectFactory` when exposing host objects instead of directly newing APIs with globals.
- Modify: `AkashaNavigator/Plugins/Core/PluginContext.cs`
  Responsibility: pass the new engine options through the real plugin initialization path.
- Modify: `AkashaNavigator/Services/PluginHost.cs`
  Responsibility: build plugin engine options from DI and stop using `App.Services`, static getters, and post-construction patching.

### Service-layer cleanup
- Modify: `AkashaNavigator/Services/ProfileManager.cs`
  Responsibility: profile lifecycle and persistence with constructor-injected dependencies only.
- Modify: `AkashaNavigator/Services/OverlayManager.cs`
  Responsibility: pure DI singleton manager with a public constructor.
- Modify: `AkashaNavigator/Services/PanelManager.cs`
  Responsibility: pure DI singleton manager with a public constructor.
- Modify: `AkashaNavigator/Plugins/Apis/WindowApi.cs`
  Responsibility: depend on bridge plus `ICursorDetectionService`, not static services.
- Modify: `AkashaNavigator/Plugins/Apis/WebViewApi.cs`
  Responsibility: depend on bridge plus `ScriptExecutionQueue`, not `App.Services`.
- Modify: `AkashaNavigator/Plugins/Apis/OverlayApi.cs`
  Responsibility: depend on `IOverlayManager`, not `OverlayManager.Instance`.
- Modify: `AkashaNavigator/Plugins/Apis/OverlayContext.cs`
  Responsibility: depend on `IOverlayManager`, not `OverlayManager.Instance`.
- Modify: `AkashaNavigator/Plugins/Apis/PanelApi.cs`
  Responsibility: depend on `IPanelManager`, not `PanelManager.Instance`.
- Modify: `AkashaNavigator/Plugins/Apis/PanelContext.cs`
  Responsibility: depend on `IPanelManager`, not `PanelManager.Instance`.
- Modify: `AkashaNavigator/Plugins/Apis/SubtitleApi.cs`
  Responsibility: depend on `ISubtitleService`, not `SubtitleService.Instance`.
- Modify: `AkashaNavigator/Plugins/Apis/HotkeyApi.cs`
  Responsibility: accept injected runtime collaborators during construction instead of being patched after creation.
- Modify: `AkashaNavigator/Plugins/Apis/OsdApi.cs`
  Responsibility: remain runtime-built from DI-owned `OsdManager`.

### Persistence and logging
- Modify: `AkashaNavigator/Helpers/JsonHelper.cs`
  Responsibility: JSON file I/O with explicit `Result` semantics and no raw debug payload output.
- Modify: `AkashaNavigator/Services/DataService.cs`
  Responsibility: handle save failures explicitly.
- Modify: `AkashaNavigator/Services/ProfileManager.cs`
  Responsibility: return and propagate persistence failures explicitly.
- Modify: `AkashaNavigator/Plugins/Apis/Core/ConfigApi.cs`
  Responsibility: emit config-changed events only after a successful save and log through injected services.
- Modify: `AkashaNavigator/App.xaml.cs`
  Responsibility: use `nameof()` in log contexts and keep startup concerns focused.

### Plugin settings flow
- Create: `AkashaNavigator/ViewModels/Windows/PluginSettingsViewModel.cs`
  Responsibility: own config state, dirty tracking, save decisions, and plugin reload decisions.
- Create: `AkashaNavigator/Core/Interfaces/IPluginSettingsWindowService.cs`
  Responsibility: open plugin settings windows through DI-owned factories.
- Create: `AkashaNavigator/Core/Interfaces/IPluginSettingsEditSessionCoordinator.cs`
  Responsibility: coordinate overlay edit mode, parent window hide/show, and focus restoration.
- Create: `AkashaNavigator/Services/PluginSettingsWindowService.cs`
  Responsibility: create the view model, create the window, set owner, and show it.
- Create: `AkashaNavigator/Services/PluginSettingsEditSessionCoordinator.cs`
  Responsibility: keep window-only overlay editing behavior out of the view model.
- Modify: `AkashaNavigator/Views/Windows/PluginSettingsWindow.xaml.cs`
  Responsibility: keep dynamic `SettingsUiRenderer` hosting plus window/session behavior only.
- Modify: `AkashaNavigator/Views/Pages/MyProfilesPage.xaml.cs`
  Responsibility: use the launcher service instead of the static settings window entry point.

### Page and control-bar workflow extraction
- Create: `AkashaNavigator/Models/Profile/DeleteProfilePlan.cs`
  Responsibility: carry the UI-ready data needed to confirm profile deletion.
- Create: `AkashaNavigator/Core/Interfaces/IProfileDeletionWorkflow.cs`
  Responsibility: prepare and execute profile deletion with plugin uninstall decisions.
- Create: `AkashaNavigator/Services/ProfileDeletionWorkflow.cs`
  Responsibility: compute the delete plan and execute the confirmed workflow.
- Modify: `AkashaNavigator/ViewModels/Pages/MyProfilesPageViewModel.cs`
  Responsibility: request delete plans and execute confirmed deletion through a workflow service.
- Modify: `AkashaNavigator/Views/Pages/MyProfilesPage.xaml.cs`
  Responsibility: remain dialog/file-picker glue only.
- Create: `AkashaNavigator/Services/ControlBarDisplayController.cs`
  Responsibility: own the control-bar display rules, timers decisions, and debounce checks.
- Modify: `AkashaNavigator/Views/Windows/ControlBarWindow.xaml.cs`
  Responsibility: delegate visibility rule decisions to the controller.

### Tests
- Create: `AkashaNavigator.Tests/Architecture/ServiceRegistrationTests.cs`
  Responsibility: verify current registrations and future DI registrations resolve correctly.
- Create: `AkashaNavigator.Tests/TestDoubles/PluginRefactorTestDoubles.cs`
  Responsibility: provide the fake services and recorders used by the new refactor tests.
- Create: `AkashaNavigator.Tests/Plugins/PluginHostObjectFactoryTests.cs`
  Responsibility: verify host objects are built from injected bridge/services and not static globals.
- Create: `AkashaNavigator.Tests/Helpers/JsonHelperTests.cs`
  Responsibility: verify persistence success/failure semantics.
- Create: `AkashaNavigator.Tests/ViewModels/PluginSettingsViewModelTests.cs`
  Responsibility: verify dirty tracking, save decisions, and plugin reload behavior.
- Create: `AkashaNavigator.Tests/Services/ProfileDeletionWorkflowTests.cs`
  Responsibility: verify delete-plan preparation and confirmed execution.
- Create: `AkashaNavigator.Tests/Windows/ControlBarDisplayControllerTests.cs`
  Responsibility: verify the control-bar decision logic without a real window.

## Task 1: Lock Today’s Baseline With Tests That Compile Now

**Files:**
- Create: `AkashaNavigator.Tests/Architecture/ServiceRegistrationTests.cs`
- Create: `AkashaNavigator.Tests/Helpers/JsonHelperTests.cs`

- [x] **Step 1: Write the current DI baseline test only against types that exist today**

```csharp
using AkashaNavigator.Core;
using AkashaNavigator.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AkashaNavigator.Tests.Architecture;

public class ServiceRegistrationTests
{
    [Fact]
    public void ConfigureAppServices_ResolvesSameProfileManagerInstanceEachTime()
    {
        var services = new ServiceCollection();
        services.ConfigureAppServices();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IProfileManager>();
        var second = provider.GetRequiredService<IProfileManager>();

        Assert.Same(first, second);
    }

    [Fact]
    public void ConfigureAppServices_ResolvesSamePluginHostInstanceEachTime()
    {
        var services = new ServiceCollection();
        services.ConfigureAppServices();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IPluginHost>();
        var second = provider.GetRequiredService<IPluginHost>();

        Assert.Same(first, second);
    }
}
```

- [x] **Step 2: Run the current DI baseline test**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ServiceRegistrationTests"`
Expected: PASS

- [x] **Step 3: Write JSON helper tests against current behavior**

```csharp
using AkashaNavigator.Helpers;

namespace AkashaNavigator.Tests.Helpers;

public class JsonHelperTests
{
    [Fact]
    public void SaveToFile_ReturnsFailure_WhenPathIsEmpty()
    {
        var result = JsonHelper.SaveToFile(string.Empty, new { Name = "Test" });

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void LoadFromFile_ReturnsFailure_WhenFileDoesNotExist()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var result = JsonHelper.LoadFromFile<Dictionary<string, string>>(missingPath);

        Assert.False(result.IsSuccess);
    }
}
```

- [x] **Step 4: Run JSON helper tests**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~JsonHelperTests"`
Expected: PASS

- [ ] **Step 5: Commit the baseline tests**

```bash
git add AkashaNavigator.Tests/Architecture/ServiceRegistrationTests.cs AkashaNavigator.Tests/Helpers/JsonHelperTests.cs
git commit -m "test(refactor): add baseline DI and persistence coverage"
```

## Task 2: Introduce The Real Plugin Runtime Bridge And Host-Object Factory

**Files:**
- Create: `AkashaNavigator/Core/Interfaces/IPlayerRuntimeBridge.cs`
- Create: `AkashaNavigator/Services/PlayerRuntimeBridge.cs`
- Create: `AkashaNavigator/Core/Interfaces/IPluginHostObjectFactory.cs`
- Create: `AkashaNavigator/Plugins/Core/PluginHostObjectFactory.cs`
- Modify: `AkashaNavigator/Core/ServiceCollectionExtensions.cs`
- Modify: `AkashaNavigator/Core/Bootstrapper.cs`
- Modify: `AkashaNavigator/Plugins/Core/PluginEngineOptions.cs`
- Modify: `AkashaNavigator/Plugins/Core/PluginEngine.cs`
- Modify: `AkashaNavigator/Plugins/Core/PluginContext.cs`
- Modify: `AkashaNavigator/Services/PluginHost.cs`
- Create: `AkashaNavigator.Tests/TestDoubles/PluginRefactorTestDoubles.cs`
- Create: `AkashaNavigator.Tests/Plugins/PluginHostObjectFactoryTests.cs`

- [x] **Step 1: Write the failing host-object factory test against the new contract**

```csharp
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Plugins.Apis;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Plugins.Utils;
using AkashaNavigator.Tests.TestDoubles;
using Microsoft.ClearScript.V8;

namespace AkashaNavigator.Tests.Plugins;

public class PluginHostObjectFactoryTests
{
    [Fact]
    public void CreateWindowApi_UsesInjectedBridgeAndCursorService()
    {
        var bridge = new FakePlayerRuntimeBridge();
        var factory = new PluginHostObjectFactory(
            bridge,
            new FakeOverlayManager(),
            new FakePanelManager(),
            new FakeCursorDetectionService(),
            new FakeSubtitleService(),
            new ScriptExecutionQueue(new FakeLogService()),
            new HotkeyService(),
            new AkashaNavigator.Core.OsdManager(),
            new FakeLogService());

        var manifest = new PluginManifest { Id = "plugin.alpha", Name = "Alpha", Version = "1.0.0", Main = "main.js" };
        var context = new PluginContext("plugin.alpha", @"C:\plugin", @"C:\config", manifest);
        var eventManager = new EventManager();

        var windowApi = factory.CreateWindowApi(context, eventManager);

        Assert.NotNull(windowApi);
        Assert.False(windowApi.IsClickThrough());
    }
}
```

- [x] **Step 2: Run the new factory test and confirm it fails because the contract does not exist yet**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~PluginHostObjectFactoryTests"`
Expected: FAIL with missing type/member errors for the new bridge/factory contract.

- [x] **Step 3: Create the runtime bridge contract and implementation**

```csharp
using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Core.Interfaces;

public interface IPlayerRuntimeBridge
{
    PlayerWindow? GetPlayerWindow();
    void SetPlayerWindow(PlayerWindow playerWindow);
}
```

```csharp
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Helpers;
using AkashaNavigator.Views.Windows;

namespace AkashaNavigator.Services;

public class PlayerRuntimeBridge : IPlayerRuntimeBridge
{
    private PlayerWindow? _playerWindow;

    public PlayerRuntimeBridge()
    {
    }

    public PlayerWindow? GetPlayerWindow() => _playerWindow;
    public void SetPlayerWindow(PlayerWindow playerWindow) => _playerWindow = playerWindow;
}
```

- [x] **Step 4: Create the real host-object factory for `PluginEngine`**

```csharp
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Plugins.Apis;
using AkashaNavigator.Plugins.Apis.Core;
using AkashaNavigator.Plugins.Utils;
using Microsoft.ClearScript.V8;

namespace AkashaNavigator.Core.Interfaces;

public interface IPluginHostObjectFactory
{
    WindowApi CreateWindowApi(PluginContext context, EventManager eventManager);
    PlayerApi CreatePlayerApi(PluginContext context, EventManager eventManager);
    WebViewApi CreateWebViewApi(string pluginId);
    OverlayApi CreateOverlayApi(PluginContext context, ConfigApi configApi);
    PanelApi CreatePanelApi(PluginContext context, ConfigApi configApi);
    SubtitleApi CreateSubtitleApi(PluginContext context, V8ScriptEngine engine, EventManager eventManager);
    HotkeyApi CreateHotkeyApi(string pluginId);
    OsdApi CreateOsdApi(string pluginId);
}
```

```csharp
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Plugins.Apis;
using AkashaNavigator.Plugins.Apis.Core;
using AkashaNavigator.Plugins.Utils;
using Microsoft.ClearScript.V8;

namespace AkashaNavigator.Plugins.Core;

public sealed class PluginHostObjectFactory : IPluginHostObjectFactory
{
}
```

The implementation must inject and reuse:

```csharp
private readonly IPlayerRuntimeBridge _playerRuntimeBridge;
private readonly IOverlayManager _overlayManager;
private readonly IPanelManager _panelManager;
private readonly ICursorDetectionService _cursorDetectionService;
private readonly ISubtitleService _subtitleService;
private readonly HotkeyService _hotkeyService;
private readonly OsdManager _osdManager;
private readonly ILogService _logService;
```

- [x] **Step 5: Thread the bridge and factory through the real plugin path**

Update `PluginEngineOptions` from:

```csharp
public Func<Views.Windows.PlayerWindow?>? GetPlayerWindow { get; set; }
public AkashaNavigator.Core.OsdManager? OsdManager { get; set; }
```

to:

```csharp
public IPlayerRuntimeBridge RuntimeBridge { get; set; } = null!;
public IPluginHostObjectFactory HostObjectFactory { get; set; } = null!;
```

Then update `PluginEngine.ExposeApiObjects(...)` so host objects are created like this:

```csharp
var playerApi = options.HostObjectFactory.CreatePlayerApi(context, eventManager);
var windowApi = options.HostObjectFactory.CreateWindowApi(context, eventManager);
var panelApi = options.HostObjectFactory.CreatePanelApi(context, configApi);
var overlayApi = options.HostObjectFactory.CreateOverlayApi(context, configApi);
var webviewApi = options.HostObjectFactory.CreateWebViewApi(pluginId);
var hotkeyApi = options.HostObjectFactory.CreateHotkeyApi(pluginId);
var osdApi = options.HostObjectFactory.CreateOsdApi(pluginId);
```

Delete direct `new WindowApi(..., options.GetPlayerWindow)` / `new WebViewApi(..., options.GetPlayerWindow)` calls from `PluginEngine.cs`.

- [x] **Step 6: Publish the current `PlayerWindow` from the composition root**

Replace bootstrapping shaped like:

```csharp
PluginApi.SetGlobalWindowGetter(() => _playerWindow);
PluginHost.SetGlobalWindowGetter(() => _playerWindow);
```

with:

```csharp
var playerRuntimeBridge = sp.GetRequiredService<IPlayerRuntimeBridge>();
playerRuntimeBridge.SetPlayerWindow(_playerWindow);
```

And in DI registration add:

```csharp
services.AddSingleton<IPlayerRuntimeBridge, PlayerRuntimeBridge>();
services.AddSingleton<IPluginHostObjectFactory, PluginHostObjectFactory>();
```

- [x] **Step 7: Run the host-object factory test and a targeted build**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~PluginHostObjectFactoryTests"`
Expected: PASS

Run: `dotnet build`
Expected: PASS, with compile errors exposing any remaining `SetGlobalWindowGetter`, `options.GetPlayerWindow`, or `App.Services` dependencies in the plugin path.

- [ ] **Step 8: Commit the runtime bridge and host-object factory migration**

```bash
git add AkashaNavigator/Core/Interfaces/IPlayerRuntimeBridge.cs AkashaNavigator/Services/PlayerRuntimeBridge.cs AkashaNavigator/Core/Interfaces/IPluginHostObjectFactory.cs AkashaNavigator/Plugins/Core/PluginHostObjectFactory.cs AkashaNavigator/Core/ServiceCollectionExtensions.cs AkashaNavigator/Core/Bootstrapper.cs AkashaNavigator/Plugins/Core/PluginEngineOptions.cs AkashaNavigator/Plugins/Core/PluginEngine.cs AkashaNavigator/Plugins/Core/PluginContext.cs AkashaNavigator/Services/PluginHost.cs AkashaNavigator.Tests/TestDoubles/PluginRefactorTestDoubles.cs AkashaNavigator.Tests/Plugins/PluginHostObjectFactoryTests.cs
git commit -m "refactor(plugins): route host-object creation through DI runtime bridge"
```

## Task 3: Remove Static Singletons Only After The Plugin Path Is On DI

**Files:**
- Modify: `AkashaNavigator/Services/ProfileManager.cs`
- Modify: `AkashaNavigator/Services/OverlayManager.cs`
- Modify: `AkashaNavigator/Services/PanelManager.cs`
- Modify: `AkashaNavigator/Plugins/Apis/WindowApi.cs`
- Modify: `AkashaNavigator/Plugins/Apis/WebViewApi.cs`
- Modify: `AkashaNavigator/Plugins/Apis/OverlayApi.cs`
- Modify: `AkashaNavigator/Plugins/Apis/OverlayContext.cs`
- Modify: `AkashaNavigator/Plugins/Apis/PanelApi.cs`
- Modify: `AkashaNavigator/Plugins/Apis/PanelContext.cs`
- Modify: `AkashaNavigator/Plugins/Apis/SubtitleApi.cs`
- Modify: `AkashaNavigator/Plugins/Apis/HotkeyApi.cs`
- Modify: `AkashaNavigator/Plugins/Apis/OsdApi.cs`
- Modify: `AkashaNavigator/Plugins/Core/PluginApi.cs`
- Modify: `AkashaNavigator/Core/ServiceCollectionExtensions.cs`
- Modify: `AkashaNavigator.Tests/WindowApiTests.cs`
- Modify: `AkashaNavigator.Tests/OverlayApiTests.cs`
- Modify: `AkashaNavigator.Tests/PanelApiTests.cs`
- Modify: `AkashaNavigator.Tests/SubtitleApiTests.cs`
- Modify: `AkashaNavigator.Tests/HotkeyApiTests.cs`
- Test: `AkashaNavigator.Tests/Architecture/ServiceRegistrationTests.cs`

- [x] **Step 1: Make `OverlayManager` and `PanelManager` constructible by DI before changing registration**

Change the class heads to this shape:

```csharp
public class OverlayManager : IOverlayManager
{
    private readonly Dictionary<string, OverlayWindow> _overlays = new();
    private readonly object _lock = new();

    public OverlayManager()
    {
    }
}
```

```csharp
public class PanelManager : IPanelManager
{
    private readonly Dictionary<string, PluginPanelWindow> _panels = new();
    private readonly object _lock = new();

    public PanelManager()
    {
    }
}
```

Delete `Lazy<T>` and `.Instance` from both classes.

- [x] **Step 2: Update API constructors to accept injected collaborators, not statics**

Use these target signatures:

```csharp
public WindowApi(PluginContext context, IPlayerRuntimeBridge runtimeBridge, ICursorDetectionService cursorDetectionService)
public WebViewApi(string pluginId, IPlayerRuntimeBridge runtimeBridge, ScriptExecutionQueue scriptExecutionQueue, ILogService logService)
public OverlayApi(PluginContext context, ConfigApi configApi, IOverlayManager overlayManager)
public PanelApi(PluginContext context, ConfigApi configApi, IPanelManager panelManager)
public SubtitleApi(PluginContext context, V8ScriptEngine engine, ISubtitleService subtitleService)
public HotkeyApi(string pluginId, HotkeyService hotkeyService, ActionDispatcher dispatcher)
public OsdApi(string pluginId, OsdManager osdManager)
```

Delete these patterns everywhere they appear:

```csharp
CursorDetectionService.Instance
OverlayManager.Instance
PanelManager.Instance
SubtitleService.Instance
App.Services?.GetService<ScriptExecutionQueue>()
```

Also migrate the remaining old constructor call sites in:

```csharp
AkashaNavigator/Plugins/Core/PluginApi.cs
AkashaNavigator.Tests/WindowApiTests.cs
AkashaNavigator.Tests/OverlayApiTests.cs
AkashaNavigator.Tests/PanelApiTests.cs
AkashaNavigator.Tests/SubtitleApiTests.cs
AkashaNavigator.Tests/HotkeyApiTests.cs
```

- [x] **Step 3: Remove static singleton sections from `ProfileManager` and stop using service-locator fallbacks**

Delete code shaped like:

```csharp
private static ProfileManager? _instance;
public static ProfileManager Instance { ... }
```

and delete construction fallback logic shaped like:

```csharp
var services = App.Services;
_instance = new ProfileManager(...);
```

- [x] **Step 4: Change the DI registrations only after constructors are ready**

Use:

```csharp
services.AddSingleton<IOverlayManager, OverlayManager>();
services.AddSingleton<IPanelManager, PanelManager>();
services.AddSingleton<IPluginHost, PluginHost>();
services.AddSingleton<IProfileManager, ProfileManager>();
```

Delete registrations shaped like:

```csharp
services.AddSingleton<IOverlayManager>(sp => OverlayManager.Instance);
services.AddSingleton<IPanelManager>(sp => PanelManager.Instance);
```

- [x] **Step 5: Re-run architecture tests and a full build**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ServiceRegistrationTests"`
Expected: PASS

Run: `dotnet build`
Expected: PASS, with compile errors exposing any remaining `.Instance` or `App.Services` references.

- [ ] **Step 6: Commit the static-singleton removal**

```bash
git add AkashaNavigator/Services/ProfileManager.cs AkashaNavigator/Services/OverlayManager.cs AkashaNavigator/Services/PanelManager.cs AkashaNavigator/Plugins/Apis/WindowApi.cs AkashaNavigator/Plugins/Apis/WebViewApi.cs AkashaNavigator/Plugins/Apis/OverlayApi.cs AkashaNavigator/Plugins/Apis/OverlayContext.cs AkashaNavigator/Plugins/Apis/PanelApi.cs AkashaNavigator/Plugins/Apis/PanelContext.cs AkashaNavigator/Plugins/Apis/SubtitleApi.cs AkashaNavigator/Plugins/Apis/HotkeyApi.cs AkashaNavigator/Plugins/Apis/OsdApi.cs AkashaNavigator/Plugins/Core/PluginApi.cs AkashaNavigator/Core/ServiceCollectionExtensions.cs AkashaNavigator.Tests/WindowApiTests.cs AkashaNavigator.Tests/OverlayApiTests.cs AkashaNavigator.Tests/PanelApiTests.cs AkashaNavigator.Tests/SubtitleApiTests.cs AkashaNavigator.Tests/HotkeyApiTests.cs
git commit -m "refactor(core): remove static singleton and service-locator plugin paths"
```

## Task 4: Make Persistence Failures Explicit And Standards-Compliant

**Files:**
- Modify: `AkashaNavigator/Helpers/JsonHelper.cs`
- Modify: `AkashaNavigator/Services/DataService.cs`
- Modify: `AkashaNavigator/Services/ProfileManager.cs`
- Modify: `AkashaNavigator/Plugins/Apis/Core/ConfigApi.cs`
- Modify: `AkashaNavigator/App.xaml.cs`
- Test: `AkashaNavigator.Tests/Helpers/JsonHelperTests.cs`

- [x] **Step 1: Remove raw JSON debug output from `JsonHelper`**

Delete:

```csharp
System.Diagnostics.Debug.WriteLine($"[JsonHelper] Saving to: {filePath}");
System.Diagnostics.Debug.WriteLine($"[JsonHelper] JSON preview (first 500 chars): {json.Substring(0, Math.Min(500, json.Length))}");
```

- [x] **Step 2: Change `DataService` to check `Result` instead of fake exception handling**

Use this shape:

```csharp
private void SaveHistory()
{
    var filePath = GetHistoryFilePath();
    var result = JsonHelper.SaveToFile(filePath, _historyCache);
    if (!result.IsSuccess)
    {
        _logService.Warn(nameof(DataService), "保存历史记录失败 [{FilePath}]: {ErrorCode}", filePath, result.Error.Code);
    }
}
```

Apply the same pattern to `SaveBookmarks()`.

- [x] **Step 3: Change `ProfileManager` persistence helpers to return `Result` and use the real constant name**

Use:

```csharp
private Result SaveProfileToDisk(GameProfile profile)
{
    var filePath = Path.Combine(GetProfileDirectory(profile.Id), AppConstants.ProfileFileName);
    return JsonHelper.SaveToFile(filePath, profile);
}
```

- [x] **Step 4: Make `ConfigApi` save before emitting events**

Use:

```csharp
var saveResult = _config.SaveToFile();
if (!saveResult.IsSuccess)
{
    _logService.Error(nameof(ConfigApi), "Failed to save config for key '{Key}': {ErrorCode}", key, saveResult.Error.Code);
    return;
}
```

- [x] **Step 5: Replace magic-string log contexts in `App.xaml.cs`**

Use:

```csharp
logService.Info(nameof(App), "检测到需要数据迁移，开始执行...");
```

- [x] **Step 6: Run persistence tests and build**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~JsonHelperTests"`
Expected: PASS

Run: `dotnet build`
Expected: PASS

- [x] **Step 7: Commit the persistence cleanup**

```bash
git add AkashaNavigator/Helpers/JsonHelper.cs AkashaNavigator/Services/DataService.cs AkashaNavigator/Services/ProfileManager.cs AkashaNavigator/Plugins/Apis/Core/ConfigApi.cs AkashaNavigator/App.xaml.cs
git commit -m "refactor(persistence): surface file write failures and normalize logging"
```

## Task 5: Refactor Plugin Settings Without Breaking Dynamic Rendering Or Overlay Edit Sessions

**Files:**
- Create: `AkashaNavigator/ViewModels/Windows/PluginSettingsViewModel.cs`
- Create: `AkashaNavigator/Core/Interfaces/IPluginSettingsWindowService.cs`
- Create: `AkashaNavigator/Core/Interfaces/IPluginSettingsEditSessionCoordinator.cs`
- Create: `AkashaNavigator/Services/PluginSettingsWindowService.cs`
- Create: `AkashaNavigator/Services/PluginSettingsEditSessionCoordinator.cs`
- Modify: `AkashaNavigator/Views/Windows/PluginSettingsWindow.xaml.cs`
- Modify: `AkashaNavigator/Views/Pages/MyProfilesPage.xaml.cs`
- Modify: `AkashaNavigator/Core/ServiceCollectionExtensions.cs`
- Create: `AkashaNavigator.Tests/TestDoubles/PluginRefactorTestDoubles.cs`
- Create: `AkashaNavigator.Tests/ViewModels/PluginSettingsViewModelTests.cs`

- [x] **Step 1: Write the failing view-model test against a real public save API**

```csharp
using AkashaNavigator.Tests.TestDoubles;

namespace AkashaNavigator.Tests.ViewModels;

public class PluginSettingsViewModelTests
{
    [Fact]
    public void SaveAsync_ReloadsPlugin_WhenConfigWasModified()
    {
        var pluginHost = new FakePluginHost();
        var viewModel = new PluginSettingsViewModel(
            new FakeProfileManager(),
            new FakeLogService(),
            pluginHost,
            new FakeNotificationService(),
            "plugin.alpha",
            "Alpha",
            @"C:\plugin",
            @"C:\config",
            "profile-1");

        viewModel.UpdateValue("enabled", true);
        viewModel.SaveAsync().GetAwaiter().GetResult();

        Assert.Equal("plugin.alpha", pluginHost.ReloadedPluginId);
    }
}
```

- [x] **Step 2: Run the view-model test and confirm it fails because the view model does not exist yet**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~PluginSettingsViewModelTests"`
Expected: FAIL with missing type/member errors for `PluginSettingsViewModel` and `SaveAsync()`.

- [x] **Step 3: Keep dynamic `SettingsUiRenderer` hosting in the window and move state/save logic to the view model**

The view model must own:

```csharp
public bool IsDirty { get; private set; }
public async Task SaveAsync()
public void UpdateValue(string key, object? value)
```

The window must keep:

```csharp
private void RenderSettings()
private void OnSettingValueChanged(object? sender, SettingsValueChangedEventArgs e)
private void OnButtonAction(object? sender, SettingsButtonActionEventArgs e)
```

`OnSettingValueChanged(...)` should call `viewModel.UpdateValue(...)` instead of saving directly.

- [x] **Step 4: Create a dedicated edit-session coordinator for overlay edit mode**

Use this contract:

```csharp
public interface IPluginSettingsEditSessionCoordinator
{
    void EnterOverlayEditSession(PluginSettingsWindow window, OverlayWindow overlayWindow);
    void ExitOverlayEditSession(PluginSettingsWindow window);
}
```

Move parent-window hide/show, focus restore, and overlay exit handling out of the view model and into this coordinator.

- [x] **Step 5: Replace the static settings window entry point with a launcher service**

Use:

```csharp
public interface IPluginSettingsWindowService
{
    void Show(string pluginId, string pluginName, string pluginDirectory, string configDirectory, Window? owner = null, string? profileId = null);
}
```

Register factories explicitly in DI:

```csharp
services.AddTransient<Func<string, string, string, string, string?, PluginSettingsViewModel>>(...);
services.AddTransient<Func<PluginSettingsViewModel, PluginSettingsWindow>>(...);
services.AddSingleton<IPluginSettingsWindowService, PluginSettingsWindowService>();
services.AddSingleton<IPluginSettingsEditSessionCoordinator, PluginSettingsEditSessionCoordinator>();
```

Use this target window constructor so the factory signature is concrete:

```csharp
public PluginSettingsWindow(
    PluginSettingsViewModel viewModel,
    IPluginSettingsEditSessionCoordinator editSessionCoordinator,
    ILogService logService)
```

Delete `PluginSettingsWindow.ShowSettings(...)`.

- [x] **Step 6: Update `MyProfilesPage` to use the launcher service**

Use this call shape:

```csharp
_pluginSettingsWindowService.Show(pluginId, pluginName, pluginDirectory, configDirectory, Window.GetWindow(this), profileId);
```

- [x] **Step 7: Run plugin settings tests and build**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~PluginSettingsViewModelTests"`
Expected: PASS

Run: `dotnet build`
Expected: PASS

- [x] **Step 8: Commit the plugin settings refactor**

```bash
git add AkashaNavigator/ViewModels/Windows/PluginSettingsViewModel.cs AkashaNavigator/Core/Interfaces/IPluginSettingsWindowService.cs AkashaNavigator/Core/Interfaces/IPluginSettingsEditSessionCoordinator.cs AkashaNavigator/Services/PluginSettingsWindowService.cs AkashaNavigator/Services/PluginSettingsEditSessionCoordinator.cs AkashaNavigator/Views/Windows/PluginSettingsWindow.xaml.cs AkashaNavigator/Views/Pages/MyProfilesPage.xaml.cs AkashaNavigator/Core/ServiceCollectionExtensions.cs AkashaNavigator.Tests/TestDoubles/PluginRefactorTestDoubles.cs AkashaNavigator.Tests/ViewModels/PluginSettingsViewModelTests.cs
git commit -m "refactor(plugin-settings): separate window session behavior from config state"
```

## Task 6: Move Profile Deletion Workflow Out Of The Page Without Losing UI Context

**Files:**
- Create: `AkashaNavigator/Models/Profile/DeleteProfilePlan.cs`
- Create: `AkashaNavigator/Core/Interfaces/IProfileDeletionWorkflow.cs`
- Create: `AkashaNavigator/Services/ProfileDeletionWorkflow.cs`
- Modify: `AkashaNavigator/ViewModels/Pages/MyProfilesPageViewModel.cs`
- Modify: `AkashaNavigator/Views/Pages/MyProfilesPage.xaml.cs`
- Modify: `AkashaNavigator/Core/ServiceCollectionExtensions.cs`
- Create: `AkashaNavigator.Tests/TestDoubles/PluginRefactorTestDoubles.cs`
- Create: `AkashaNavigator.Tests/Services/ProfileDeletionWorkflowTests.cs`

- [x] **Step 1: Write the failing workflow test around a delete-plan DTO instead of the page**

```csharp
using AkashaNavigator.Tests.TestDoubles;

namespace AkashaNavigator.Tests.Services;

public class ProfileDeletionWorkflowTests
{
    [Fact]
    public void PrepareDeletePlan_ReturnsUniquePluginSelections_WhenProfileOwnsPluginsExclusively()
    {
        var workflow = new ProfileDeletionWorkflow(
            new FakeProfileManager(),
            new FakePluginAssociationManager(uniquePluginIds: ["plugin.alpha"]),
            new FakePluginLibrary(),
            new FakeNotificationService(),
            new RecordingEventBus());

        var plan = workflow.PrepareDeletePlan("profile-1");

        Assert.Equal("profile-1", plan.ProfileId);
        Assert.Single(plan.PluginChoices);
    }
}
```

- [x] **Step 2: Run the workflow test and confirm it fails because the workflow does not exist yet**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ProfileDeletionWorkflowTests"`
Expected: FAIL with missing type/member errors for `ProfileDeletionWorkflow` and `DeleteProfilePlan`.

- [x] **Step 3: Introduce the delete-plan DTO and workflow service**

Use this DTO shape:

```csharp
public class DeleteProfilePlan
{
    public string ProfileId { get; init; } = string.Empty;
    public string ProfileName { get; init; } = string.Empty;
    public bool RequiresPluginUninstallPrompt { get; init; }
    public IReadOnlyList<PluginUninstallItem> PluginChoices { get; init; } = Array.Empty<PluginUninstallItem>();
}
```

Use this workflow shape:

```csharp
public interface IProfileDeletionWorkflow
{
    DeleteProfilePlan PrepareDeletePlan(string profileId);
    Task ExecuteDeleteAsync(DeleteProfilePlan plan, IReadOnlyList<string> selectedPluginIds);
}
```

Register the workflow in DI:

```csharp
services.AddSingleton<IProfileDeletionWorkflow, ProfileDeletionWorkflow>();
```

- [x] **Step 4: Move `MyProfilesPage` to dialog glue only**

Use this page flow:

```csharp
var plan = _viewModel.PrepareDeleteProfile(profileId);
var dialog = _dialogFactory.CreatePluginUninstallDialog(plan.ProfileName, plan.PluginChoices.ToList());
if (dialog.ShowDialog() == true && dialog.Confirmed)
{
    await _viewModel.ConfirmDeleteProfileAsync(plan, dialog.SelectedPluginIds);
}
```

- [x] **Step 5: Run workflow tests and build**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ProfileDeletionWorkflowTests"`
Expected: PASS

Run: `dotnet build`
Expected: PASS

- [x] **Step 6: Commit the profile deletion workflow extraction**

```bash
git add AkashaNavigator/Models/Profile/DeleteProfilePlan.cs AkashaNavigator/Core/Interfaces/IProfileDeletionWorkflow.cs AkashaNavigator/Services/ProfileDeletionWorkflow.cs AkashaNavigator/ViewModels/Pages/MyProfilesPageViewModel.cs AkashaNavigator/Views/Pages/MyProfilesPage.xaml.cs AkashaNavigator/Core/ServiceCollectionExtensions.cs AkashaNavigator.Tests/TestDoubles/PluginRefactorTestDoubles.cs AkashaNavigator.Tests/Services/ProfileDeletionWorkflowTests.cs
git commit -m "refactor(profiles): extract delete planning and execution workflow"
```

## Task 7: Extract A Real Control-Bar Decision Engine, Not Just A State Enum Wrapper

**Files:**
- Create: `AkashaNavigator/Services/ControlBarDisplayController.cs`
- Modify: `AkashaNavigator/Views/Windows/ControlBarWindow.xaml.cs`
- Modify: `AkashaNavigator/Core/ServiceCollectionExtensions.cs`
- Create: `AkashaNavigator.Tests/Windows/ControlBarDisplayControllerTests.cs`

- [x] **Step 1: Write the failing controller test against a real decision API**

```csharp
namespace AkashaNavigator.Tests.Windows;

public class ControlBarDisplayControllerTests
{
    [Fact]
    public void EvaluateMouse_WhenPointerLeavesWindowAndDelayExpires_ReturnsHidden()
    {
        var controller = new ControlBarDisplayController();
        controller.SetState(ControlBarDisplayState.Expanded, DateTime.UtcNow.AddSeconds(-2));

        var result = controller.EvaluateHideDelay(
            isMouseOverWindow: false,
            isMouseInTopTriggerZone: false,
            isContextMenuOpen: false,
            isUrlTextBoxFocused: false,
            nowUtc: DateTime.UtcNow);

        Assert.Equal(ControlBarDisplayState.Hidden, result.NextState);
    }
}
```

- [x] **Step 2: Run the controller test and confirm it fails because the controller does not exist yet**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ControlBarDisplayControllerTests"`
Expected: FAIL with missing type/member errors for `ControlBarDisplayController`.

- [x] **Step 3: Create the controller around decisions, not just state storage**

Use this shape:

```csharp
public sealed class ControlBarDecision
{
    public ControlBarDisplayState NextState { get; init; }
    public bool StartHideDelayTimer { get; init; }
    public bool StopHideDelayTimer { get; init; }
}

public class ControlBarDisplayController
{
    public ControlBarDisplayState State { get; private set; } = ControlBarDisplayState.Hidden;
    public DateTime LastStateChangeUtc { get; private set; } = DateTime.MinValue;

    public void SetState(ControlBarDisplayState state, DateTime nowUtc) { ... }
    public ControlBarDecision EvaluateMouse(bool isMouseOverWindow, bool isMouseInTopTriggerZone, bool isContextMenuOpen, bool isUrlTextBoxFocused, DateTime nowUtc) { ... }
    public ControlBarDecision EvaluateHideDelay(bool isMouseOverWindow, bool isMouseInTopTriggerZone, bool isContextMenuOpen, bool isUrlTextBoxFocused, DateTime nowUtc) { ... }
}
```

- [x] **Step 4: Wire the controller into `ControlBarWindow` and register it in DI**

Add:

```csharp
services.AddSingleton<ControlBarDisplayController>();
```

Update `ControlBarWindow` constructor to accept it and route `MouseCheckTimer_Tick` / `HideDelayTimer_Tick` through the controller.

- [x] **Step 5: Run controller tests and build**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ControlBarDisplayControllerTests"`
Expected: PASS

Run: `dotnet build`
Expected: PASS

- [x] **Step 6: Commit the control-bar extraction**

```bash
git add AkashaNavigator/Services/ControlBarDisplayController.cs AkashaNavigator/Views/Windows/ControlBarWindow.xaml.cs AkashaNavigator/Core/ServiceCollectionExtensions.cs AkashaNavigator.Tests/Windows/ControlBarDisplayControllerTests.cs
git commit -m "refactor(control-bar): extract visibility rules into a decision controller"
```

## Task 8: Final Verification

**Files:**
- Modify: none unless verification reveals breakage
- Test: `AkashaNavigator.Tests`

- [x] **Step 1: Run the full test suite**

Run: `dotnet test`
Result: PASS (`Failed: 0, Passed: 1016, Skipped: 32, Total: 1048`)

- [x] **Step 2: Run a release build**

Run: `dotnet build AkashaNavigator/AkashaNavigator.csproj -c Release`
Result: PASS (`0 warnings, 0 errors`)

- [ ] **Step 3: Manually smoke-test the high-risk runtime flows**

Run: `dotnet run --project AkashaNavigator`

Blocked note: Manual smoke-test remains pending in the current CLI session because elevation/runtime GUI interaction is unavailable.

Pending manual verification checklist:
- [ ] A plugin with `player` permission still gets `player`, `window`, and `webview` host objects.
- [ ] A plugin with `overlay` permission still opens and closes overlay windows.
- [ ] Plugin settings still render dynamic controls from `settings_ui.json`.
- [ ] Entering and exiting overlay edit mode still restores parent windows correctly.
- [ ] Deleting a profile still shows the correct uninstall confirmation flow.
- [ ] The control bar still expands, delays hiding, and fully hides correctly.

- [ ] **Step 4: Create the final integration commit**

```bash
git add AkashaNavigator AkashaNavigator.Tests docs/superpowers/plans/2026-04-09-refactor-hotspots.md
git commit -m "refactor(core): replace plugin globals and trim heavy UI entry points"
```

## Self-Review

- Spec coverage: this plan covers the real plugin construction path, DI/static-singleton removal, persistence/result misuse, plugin settings runtime boundaries, profile deletion workflow extraction, and control-bar decision logic extraction.
- Placeholder scan: no `TODO`, `TBD`, or “similar to above” placeholders remain.
- Type consistency: the plan uses the same names throughout: `IPlayerRuntimeBridge`, `PlayerRuntimeBridge`, `IPluginHostObjectFactory`, `PluginHostObjectFactory`, `IPluginSettingsWindowService`, `IPluginSettingsEditSessionCoordinator`, `DeleteProfilePlan`, `IProfileDeletionWorkflow`, and `ControlBarDisplayController`.
