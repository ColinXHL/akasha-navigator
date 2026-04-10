# Refactor Hotspots Follow-up Closure Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the remaining refactor-hotspots gaps so plugin runtime construction is fully DI-compliant, plan artifacts are complete, and remaining technical debt is reduced to known non-blocking items.

**Architecture:** Add a dedicated host-object factory in the real plugin runtime path (`PluginHost -> PluginContext -> PluginEngine`) and remove the remaining service-locator usage in the settings window workflow. Keep behavior unchanged, verify with focused regression tests first, then full-suite validation and manual smoke tests.

**Tech Stack:** .NET 8, WPF, Microsoft.Extensions.DependencyInjection, xUnit, ClearScript V8

---

## File Structure

### Plugin runtime factory closure
- Create: `AkashaNavigator/Core/Interfaces/IPluginHostObjectFactory.cs`
  Responsibility: unified creation contract for host objects injected into V8.
- Create: `AkashaNavigator/Plugins/Core/PluginHostObjectFactory.cs`
  Responsibility: build all permission-gated APIs from DI services + runtime context.
- Modify: `AkashaNavigator/Plugins/Core/PluginEngineOptions.cs`
  Responsibility: carry `HostObjectFactory` in options.
- Modify: `AkashaNavigator/Plugins/Core/PluginEngine.cs`
  Responsibility: replace direct `new XxxApi(...)` paths with factory usage.
- Modify: `AkashaNavigator/Core/ServiceCollectionExtensions.cs`
  Responsibility: register `IPluginHostObjectFactory`.
- Modify: `AkashaNavigator/Services/PluginHost.cs`
  Responsibility: pass `HostObjectFactory` into `PluginEngineOptions`.

### Remaining DI/service-locator cleanup
- Modify: `AkashaNavigator/Views/Windows/PluginSettingsWindow.xaml.cs`
  Responsibility: remove `App.Services` lookup and use constructor-injected `IOverlayManager`.
- Modify: `AkashaNavigator/Core/ServiceCollectionExtensions.cs`
  Responsibility: update plugin settings window factory signature to provide `IOverlayManager`.
- Modify: `AkashaNavigator/Services/ProfileManager.cs`
  Responsibility: check `JsonHelper.SaveToFile` result in `SavePluginConfig` and return failure correctly.

### Test closure
- Create: `AkashaNavigator.Tests/Plugins/PluginHostObjectFactoryTests.cs`
  Responsibility: prove factory can construct required APIs from injected collaborators.
- Modify: `AkashaNavigator.Tests/TestDoubles/PluginRefactorTestDoubles.cs`
  Responsibility: add test doubles needed by factory tests.
- Modify: `AkashaNavigator.Tests/ProfileManagerTests.cs`
  Responsibility: add save-failure behavior assertion for `SavePluginConfig`.

## Task 1: Reintroduce Host-Object Factory on Real Runtime Path

**Files:**
- Create: `AkashaNavigator/Core/Interfaces/IPluginHostObjectFactory.cs`
- Create: `AkashaNavigator/Plugins/Core/PluginHostObjectFactory.cs`
- Modify: `AkashaNavigator/Plugins/Core/PluginEngineOptions.cs`
- Modify: `AkashaNavigator/Plugins/Core/PluginEngine.cs`
- Modify: `AkashaNavigator/Core/ServiceCollectionExtensions.cs`
- Modify: `AkashaNavigator/Services/PluginHost.cs`

- [ ] **Step 1: Add failing factory test first**

```csharp
// AkashaNavigator.Tests/Plugins/PluginHostObjectFactoryTests.cs
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Models.Plugin;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Plugins.Utils;
using AkashaNavigator.Tests.TestDoubles;
using Xunit;

namespace AkashaNavigator.Tests.Plugins;

public class PluginHostObjectFactoryTests
{
    [Fact]
    public void CreateWindowApi_UsesInjectedRuntimeBridgeAndCursorService()
    {
        var factory = PluginRefactorFactoryBuilder.Create();
        var context = new PluginContext("plugin.alpha", @"C:\\plugin", @"C:\\config",
            new PluginManifest { Id = "plugin.alpha", Name = "Alpha", Version = "1.0.0", Main = "main.js" });
        var eventManager = new EventManager();

        var api = factory.CreateWindowApi(context, eventManager);

        Assert.NotNull(api);
        Assert.False(api.IsClickThrough());
    }
}
```

- [ ] **Step 2: Run the focused test (expect fail before implementation)**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~PluginHostObjectFactoryTests"`
Expected: FAIL (missing type/member for `IPluginHostObjectFactory` / `PluginHostObjectFactory`).

- [ ] **Step 3: Create factory interface**

```csharp
// AkashaNavigator/Core/Interfaces/IPluginHostObjectFactory.cs
using AkashaNavigator.Plugins.Apis;
using AkashaNavigator.Plugins.Apis.Core;
using AkashaNavigator.Plugins.Core;
using AkashaNavigator.Plugins.Utils;
using Microsoft.ClearScript.V8;

namespace AkashaNavigator.Core.Interfaces;

public interface IPluginHostObjectFactory
{
    PlayerApi CreatePlayerApi(PluginContext context, EventManager eventManager);
    WindowApi CreateWindowApi(PluginContext context, EventManager eventManager);
    WebViewApi CreateWebViewApi(string pluginId);
    OverlayApi CreateOverlayApi(PluginContext context, ConfigApi configApi);
    PanelApi CreatePanelApi(PluginContext context, ConfigApi configApi);
    SubtitleApi CreateSubtitleApi(PluginContext context, V8ScriptEngine engine, EventManager eventManager);
    HotkeyApi CreateHotkeyApi(string pluginId);
    OsdApi CreateOsdApi(string pluginId);
}
```

- [ ] **Step 4: Implement concrete factory**

```csharp
// AkashaNavigator/Plugins/Core/PluginHostObjectFactory.cs
using AkashaNavigator.Core;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Plugins.Apis;
using AkashaNavigator.Plugins.Apis.Core;
using AkashaNavigator.Plugins.Utils;
using AkashaNavigator.Services;
using Microsoft.ClearScript.V8;

namespace AkashaNavigator.Plugins.Core;

public sealed class PluginHostObjectFactory : IPluginHostObjectFactory
{
    public PluginHostObjectFactory(
        IPlayerRuntimeBridge runtimeBridge,
        IOverlayManager overlayManager,
        IPanelManager panelManager,
        ICursorDetectionService cursorDetectionService,
        ISubtitleService subtitleService,
        ScriptExecutionQueue scriptExecutionQueue,
        HotkeyService hotkeyService,
        OsdManager osdManager,
        ILogService logService)
    { /* assign fields */ }

    // each CreateXxx returns API built from injected fields
}
```

- [ ] **Step 5: Thread factory through engine options and plugin host**

```csharp
// AkashaNavigator/Plugins/Core/PluginEngineOptions.cs
public IPluginHostObjectFactory? HostObjectFactory { get; set; }
```

```csharp
// AkashaNavigator/Services/PluginHost.cs (engine options init)
HostObjectFactory = _hostObjectFactory,
```

Also inject `IPluginHostObjectFactory` into `PluginHost` constructor and assign `_hostObjectFactory` field.

- [ ] **Step 6: Replace direct constructions inside PluginEngine**

```csharp
// AkashaNavigator/Plugins/Core/PluginEngine.cs (inside ExposeApiObjects)
var playerApi = options.HostObjectFactory!.CreatePlayerApi(context, eventManager);
var windowApi = options.HostObjectFactory.CreateWindowApi(context, eventManager);
var panelApi = options.HostObjectFactory.CreatePanelApi(context, configApi);
var overlayApi = options.HostObjectFactory.CreateOverlayApi(context, configApi);
var webviewApi = options.HostObjectFactory.CreateWebViewApi(pluginId);
var hotkeyApi = options.HostObjectFactory.CreateHotkeyApi(pluginId);
var osdApi = options.HostObjectFactory.CreateOsdApi(pluginId);
```

- [ ] **Step 7: Register factory in DI**

```csharp
// AkashaNavigator/Core/ServiceCollectionExtensions.cs
services.AddSingleton<IPluginHostObjectFactory, PluginHostObjectFactory>();
```

- [ ] **Step 8: Run factory test + full tests**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~PluginHostObjectFactoryTests"`
Expected: PASS

Run: `dotnet test`
Expected: PASS

## Task 2: Close Remaining Service-Locator and Persistence Debt

**Files:**
- Modify: `AkashaNavigator/Views/Windows/PluginSettingsWindow.xaml.cs`
- Modify: `AkashaNavigator/Core/ServiceCollectionExtensions.cs`
- Modify: `AkashaNavigator/Services/ProfileManager.cs`
- Modify: `AkashaNavigator.Tests/ProfileManagerTests.cs`

- [ ] **Step 1: Inject overlay manager into PluginSettingsWindow constructor**

```csharp
// PluginSettingsWindow.xaml.cs constructor parameters
public PluginSettingsWindow(
    PluginSettingsViewModel viewModel,
    IPluginSettingsEditSessionCoordinator editSessionCoordinator,
    IOverlayManager overlayManager,
    ILogService logService)
```

Use `_overlayManager` field in `EnterOverlayEditMode()` and delete:

```csharp
var overlayManager = App.Services.GetRequiredService<IOverlayManager>();
```

- [ ] **Step 2: Update DI window factory for new constructor signature**

```csharp
// ServiceCollectionExtensions.cs
var overlayManager = sp.GetRequiredService<IOverlayManager>();
return new PluginSettingsWindow(viewModel, coordinator, overlayManager, logService);
```

- [ ] **Step 3: Make ProfileManager.SavePluginConfig respect save result**

```csharp
// ProfileManager.cs in SavePluginConfig
var saveResult = JsonHelper.SaveToFile(configPath, config);
if (!saveResult.IsSuccess)
{
    _logService.Debug(nameof(ProfileManager), "保存插件配置失败 [{ConfigPath}]: {ErrorCode}", configPath,
        saveResult.Error?.Code ?? "UNKNOWN");
    return false;
}

return true;
```

- [ ] **Step 4: Add regression test for SavePluginConfig failure path**

```csharp
// ProfileManagerTests.cs
[Fact]
public void SavePluginConfig_ReturnsFalse_WhenPathInvalid()
{
    var manager = CreateProfileManagerForTests();
    var ok = manager.SavePluginConfig("default", "bad<>id", new Dictionary<string, object> { ["k"] = 1 });
    Assert.False(ok);
}
```

- [ ] **Step 5: Run focused + full validation**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ProfileManagerTests"`
Expected: PASS

Run: `dotnet build AkashaNavigator/AkashaNavigator.csproj -c Release`
Expected: PASS

## Task 3: Complete Runtime Smoke Verification and Plan Closure

**Files:**
- Modify: `docs/superpowers/plans/2026-04-09-refactor-hotspots.md`

- [ ] **Step 1: Execute manual smoke-test checklist in app runtime**

Run: `dotnet run --project AkashaNavigator`

Verify all:
- Plugin with `player` permission receives `player/window/webview` host objects.
- Plugin with `overlay` permission can open/close overlay.
- Plugin settings loads controls from `settings_ui.json`.
- Overlay edit session restores parent window visibility/focus.
- Profile deletion uninstall flow behaves correctly.
- Control bar expand/hide-delay/hide behavior remains correct.

- [ ] **Step 2: Mark original plan checkboxes as complete**

Update in `docs/superpowers/plans/2026-04-09-refactor-hotspots.md`:
- Mark Step 3 of Task 8 (`manual smoke-test`) as `[x]` after completion.
- Mark Step 4 of Task 8 (`final integration commit`) as `[x]` after commit.

- [ ] **Step 3: Create final integration commit**

```bash
git add AkashaNavigator AkashaNavigator.Tests docs/superpowers/plans/2026-04-09-refactor-hotspots.md docs/superpowers/plans/2026-04-09-refactor-hotspots-followup-closure.md
git commit -m "refactor(core): close remaining runtime DI and verification gaps"
```

- [ ] **Step 4: Final verification gate before claiming complete**

Run: `dotnet test && dotnet build AkashaNavigator/AkashaNavigator.csproj -c Release`
Expected: PASS for both commands.

## Self-Review

- Spec coverage: addresses the 4 observed closure gaps (host-object factory missing, related tests missing, residual service-locator usage, unchecked save result) plus pending manual smoke/closure steps.
- Placeholder scan: no TODO/TBD placeholders remain; each task has concrete files and runnable commands.
- Type consistency: uses one consistent naming set (`IPluginHostObjectFactory`, `PluginHostObjectFactory`, `HostObjectFactory`, `IOverlayManager`).
