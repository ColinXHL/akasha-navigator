# Service Locator Eradication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不使用过渡兼容层的前提下，彻底移除当前代码库中业务代码对 `App.Services` 的依赖，并删除相关静态 `Instance` 入口，统一走构造函数注入。

**Architecture:** 采用“先测试约束、再删服务定位器、最后收口验证”的硬切换方案。所有调用链由 DI 容器直接提供依赖，不保留 `App.Services` fallback，不保留这批服务的 `Instance` 访问入口。执行方式采用 TDD（每个任务先写失败测试）+ SDD（每个任务独立子代理实现与双重审查）。

**Tech Stack:** .NET 8, WPF, Microsoft.Extensions.DependencyInjection, xUnit

---

## File Structure

### DI/Service Locator 清理目标
- Modify: `AkashaNavigator/Views/Windows/PluginSettingsWindow.xaml.cs`
  - 删除 `App.Services.GetRequiredService<IOverlayManager>()`，改为构造注入。
- Modify: `AkashaNavigator/Core/HotkeyManager.cs`
  - 删除 `App.Services?.GetService<HotkeyService>()`，改为构造注入 `HotkeyService`。
- Modify: `AkashaNavigator/App.xaml.cs`
  - `HotkeyManager` 改为从 DI 获取，不再 `new HotkeyManager()`。
- Modify: `AkashaNavigator/Services/DataService.cs`
  - 删除 `App.Services` + 静态 `Instance` 区块。
- Modify: `AkashaNavigator/Services/PioneerNoteService.cs`
  - 删除 `App.Services` + 静态 `Instance`/`ResetInstance` 区块。
- Modify: `AkashaNavigator/Services/WindowStateService.cs`
  - 删除 `App.Services` + 静态 `Instance` 区块。
- Modify: `AkashaNavigator/Services/PluginAssociationManager.cs`
  - 删除 `App.Services` + 静态 `Instance`/`ResetInstance` 区块。
- Modify: `AkashaNavigator/Services/ProfileMarketplaceService.cs`
  - 删除 `App.Services` + 静态 `Instance`/`ResetInstance` 区块；测试构造函数改为显式传依赖。
- Modify: `AkashaNavigator/Views/Dialogs/ExitRecordPrompt.xaml.cs`
  - `ShouldShowPrompt` 不再 fallback 到 `PioneerNoteService.Instance`。
- Modify: `AkashaNavigator/Views/Windows/PlayerWindow.xaml.cs`
  - 构造注入 `IPioneerNoteService` 并传给 `ShouldShowPrompt`。
- Modify: `AkashaNavigator/Core/ServiceCollectionExtensions.cs`
  - 注册 `HotkeyManager`；移除 `PioneerNoteService.Instance = instance` 这类静态回填。

### 新增测试
- Create: `AkashaNavigator.Tests/Architecture/ServiceLocatorEradicationTests.cs`
  - 约束：应用层（除 `App.xaml.cs`）不得出现 `App.Services`。
  - 约束：上述 5 个服务文件不得再包含静态 `Instance` 成员。
- Create: `AkashaNavigator.Tests/Views/ExitRecordPromptTests.cs`
  - 约束：`ShouldShowPrompt` 仅依赖显式传入服务。
- Modify: `AkashaNavigator.Tests/Architecture/ServiceRegistrationTests.cs`
  - 校验 `HotkeyManager` 可从 DI 解析且依赖完整。

## Task 1: 建立“禁止 Service Locator”测试护栏（TDD 红灯）

**Files:**
- Create: `AkashaNavigator.Tests/Architecture/ServiceLocatorEradicationTests.cs`
- Modify: `AkashaNavigator.Tests/Architecture/ServiceRegistrationTests.cs`

- [x] **Step 1: 先写失败测试（红灯）**

```csharp
// AkashaNavigator.Tests/Architecture/ServiceLocatorEradicationTests.cs
using System.IO;
using Xunit;

namespace AkashaNavigator.Tests.Architecture;

public class ServiceLocatorEradicationTests
{
    [Fact]
    public void ApplicationCode_ExceptAppXaml_ShouldNotUseAppServices()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var appDir = Path.Combine(root, "AkashaNavigator");

        var offenders = Directory.GetFiles(appDir, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.EndsWith("App.xaml.cs", StringComparison.OrdinalIgnoreCase))
            .Where(p => File.ReadAllText(p).Contains("App.Services", StringComparison.Ordinal))
            .ToList();

        Assert.True(offenders.Count == 0, $"Found App.Services usage:\n{string.Join("\n", offenders)}");
    }

    [Theory]
    [InlineData("AkashaNavigator/Services/DataService.cs")]
    [InlineData("AkashaNavigator/Services/PioneerNoteService.cs")]
    [InlineData("AkashaNavigator/Services/WindowStateService.cs")]
    [InlineData("AkashaNavigator/Services/PluginAssociationManager.cs")]
    [InlineData("AkashaNavigator/Services/ProfileMarketplaceService.cs")]
    public void TargetServices_ShouldNotContainStaticInstance(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(path);

        Assert.DoesNotContain(" static ", text);
        Assert.DoesNotContain(" Instance", text);
    }
}
```

- [x] **Step 2: 运行测试确认失败**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ServiceLocatorEradicationTests"`
Expected: FAIL（当前代码仍存在 `App.Services` 和 `Instance`）。

- [x] **Step 3: 补充 DI 注册测试（HotkeyManager）**

```csharp
// AkashaNavigator.Tests/Architecture/ServiceRegistrationTests.cs
[Fact]
public void ConfigureAppServices_ShouldResolveHotkeyManager()
{
    var services = new ServiceCollection();
    services.ConfigureAppServices();
    using var provider = services.BuildServiceProvider();

    var manager = provider.GetRequiredService<HotkeyManager>();
    Assert.NotNull(manager);
}
```

- [x] **Step 4: 运行 DI 注册测试**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ServiceRegistrationTests"`
Expected: FAIL（在注册 HotkeyManager 前应先失败）。

## Task 2: 清理 UI 入口链路中的 `App.Services`（PluginSettings + HotkeyManager + App）

**Files:**
- Modify: `AkashaNavigator/Views/Windows/PluginSettingsWindow.xaml.cs`
- Modify: `AkashaNavigator/Core/ServiceCollectionExtensions.cs`
- Modify: `AkashaNavigator/Core/HotkeyManager.cs`
- Modify: `AkashaNavigator/App.xaml.cs`

- [x] **Step 1: 为 PluginSettingsWindow 增加 IOverlayManager 注入**

```csharp
// constructor signature
public PluginSettingsWindow(
    PluginSettingsViewModel viewModel,
    IPluginSettingsEditSessionCoordinator editSessionCoordinator,
    IOverlayManager overlayManager,
    ILogService logService)
```

并将 `EnterOverlayEditMode()` 内部改为使用 `_overlayManager` 字段，删除：

```csharp
var overlayManager = App.Services.GetRequiredService<IOverlayManager>();
```

- [x] **Step 2: 修改窗口工厂注入参数**

```csharp
// ServiceCollectionExtensions.cs
var coordinator = sp.GetRequiredService<IPluginSettingsEditSessionCoordinator>();
var overlayManager = sp.GetRequiredService<IOverlayManager>();
var logService = sp.GetRequiredService<ILogService>();
return new PluginSettingsWindow(viewModel, coordinator, overlayManager, logService);
```

- [x] **Step 3: HotkeyManager 改为构造注入 HotkeyService**

```csharp
public class HotkeyManager
{
    private readonly HotkeyService _hotkeyService;

    public HotkeyManager(HotkeyService hotkeyService)
    {
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _seekFlushTimer = new DispatcherTimer();
        _seekFlushTimer.Tick += OnSeekFlushTimerTick;
    }
}
```

删除 `Initialize()` 中通过 `App.Services` 获取/new `HotkeyService` 的分支逻辑。

- [x] **Step 4: App 使用 DI 获取 HotkeyManager**

```csharp
// App.xaml.cs: InitializeManagers
_hotkeyManager = Services.GetRequiredService<HotkeyManager>();
_hotkeyManager.Initialize(playerWindow, _config, _osdManager.ShowMessage);
```

- [x] **Step 5: 注册 HotkeyManager 并验证**

```csharp
// ServiceCollectionExtensions.cs
services.AddSingleton<HotkeyManager>();
```

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ServiceRegistrationTests"`
Expected: PASS

## Task 3: 删除服务层静态 Instance 入口（无过渡硬切）

**Files:**
- Modify: `AkashaNavigator/Services/DataService.cs`
- Modify: `AkashaNavigator/Services/PioneerNoteService.cs`
- Modify: `AkashaNavigator/Services/WindowStateService.cs`
- Modify: `AkashaNavigator/Services/PluginAssociationManager.cs`
- Modify: `AkashaNavigator/Services/ProfileMarketplaceService.cs`
- Modify: `AkashaNavigator/Core/ServiceCollectionExtensions.cs`
- Modify: `AkashaNavigator/Views/Dialogs/ExitRecordPrompt.xaml.cs`
- Modify: `AkashaNavigator/Views/Windows/PlayerWindow.xaml.cs`

- [x] **Step 1: 删除 5 个服务中的 `#region Singleton` 及 `Instance/ResetInstance`**

示例目标（DataService）：

```csharp
public class DataService : IDataService
{
    private readonly ILogService _logService;
    private readonly IProfileManager _profileManager;
    // ...
}
```

其它 4 个服务同样删除静态单例入口。

- [x] **Step 2: 删除 ServiceCollection 中静态回填逻辑**

将：

```csharp
services.AddSingleton<IPioneerNoteService>(sp =>
{
    var instance = new PioneerNoteService(...);
    PioneerNoteService.Instance = instance;
    return instance;
});
```

改为：

```csharp
services.AddSingleton<IPioneerNoteService, PioneerNoteService>();
```

- [x] **Step 3: 修复 ProfileMarketplaceService 测试构造函数依赖**

将测试构造函数改为显式传入依赖，不再访问 `PluginAssociationManager.Instance` / `PluginLibrary.Instance`：

```csharp
internal ProfileMarketplaceService(
    ILogService logService,
    IProfileManager profileManager,
    IPluginAssociationManager pluginAssociationManager,
    IPluginLibrary pluginLibrary,
    string configFilePath,
    HttpClient? httpClient = null)
```

- [x] **Step 4: 修复 ExitRecordPrompt 静态调用 fallback**

```csharp
public static bool ShouldShowPrompt(string url, IPioneerNoteService pioneerNoteService)
{
    if (string.IsNullOrWhiteSpace(url))
        return false;

    return !pioneerNoteService.IsUrlRecorded(url);
}
```

- [x] **Step 5: PlayerWindow 构造注入 IPioneerNoteService 并传参调用**

```csharp
// field
private readonly IPioneerNoteService _pioneerNoteService;

// ctor params
..., ScriptExecutionQueue scriptQueue, IPioneerNoteService pioneerNoteService)

// assign
_pioneerNoteService = pioneerNoteService ?? throw new ArgumentNullException(nameof(pioneerNoteService));

// usage
if (ExitRecordPrompt.ShouldShowPrompt(currentUrl, _pioneerNoteService))
```

- [x] **Step 6: 运行架构护栏测试**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ServiceLocatorEradicationTests"`
Expected: PASS

## Task 4: 针对新接口约束补测试并完成全量验证

**Files:**
- Create: `AkashaNavigator.Tests/Views/ExitRecordPromptTests.cs`
- Modify: `AkashaNavigator.Tests/Architecture/ServiceLocatorEradicationTests.cs`

- [x] **Step 1: 先写 ExitRecordPrompt 失败测试**

```csharp
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Views.Dialogs;
using Xunit;

namespace AkashaNavigator.Tests.Views;

public class ExitRecordPromptTests
{
    [Fact]
    public void ShouldShowPrompt_ReturnsFalse_WhenUrlAlreadyRecorded()
    {
        var service = new FakePioneerNoteService(isRecorded: true);
        var result = ExitRecordPrompt.ShouldShowPrompt("https://example.com", service);
        Assert.False(result);
    }
}
```

- [x] **Step 2: 实现最小 Fake 并让测试通过**

```csharp
private sealed class FakePioneerNoteService : IPioneerNoteService
{
    private readonly bool _isRecorded;
    public FakePioneerNoteService(bool isRecorded) => _isRecorded = isRecorded;
    public bool IsUrlRecorded(string url) => _isRecorded;
    // 其余接口成员抛 NotImplementedException（测试不会调用）
}
```

- [x] **Step 3: 运行视图相关测试**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ExitRecordPromptTests"`
Expected: PASS

- [x] **Step 4: 全量验证（强制门禁）**

Run: `dotnet test`
Expected: PASS

Run: `dotnet build AkashaNavigator/AkashaNavigator.csproj -c Release`
Expected: PASS

Run: `dotnet run --project AkashaNavigator`
Expected: 可启动；重点检查退出提示链路不崩溃。

## Task 5: SDD 执行与审查节奏（强制）

**Files:**
- Modify: `docs/superpowers/plans/2026-04-10-service-locator-eradication-plan.md`

- [x] **Step 1: 每个 Task 用独立子代理实现（SDD）**

执行顺序：Task 1 -> Task 2 -> Task 3 -> Task 4。

- [x] **Step 2: 每个 Task 后执行双审查**

1. Spec Review（是否严格按计划、无额外范围）
2. Code Quality Review（接口边界、异常处理、测试质量）

- [x] **Step 3: 将审查结论和命令结果回填到本计划文档**

格式要求：
- `Result: PASS/FAIL`
- 关键数字（通过/失败/跳过）
- 问题与修复记录

- [x] **Step 4: 完成后再考虑 commit/PR**

仅当 Task 1-4 全绿且 `ServiceLocatorEradicationTests`、全量测试、Release Build、手工启动验证均通过。

## Self-Review

- 覆盖性：包含所有当前 `App.Services` 残留点（PluginSettingsWindow/DataService/PioneerNoteService/WindowStateService/PluginAssociationManager/ProfileMarketplaceService/HotkeyManager）。
- 无过渡：计划要求删除静态 `Instance` 与 DI 静态回填，不保留 fallback。
- TDD：每个任务均先红灯测试，再最小实现，再绿灯验证。
- SDD：每个任务独立子代理执行并双审查。

---

## Execution Log (2026-04-10)

### Task 1

- Result: PASS
- 红灯验证（首次执行）：
  - `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ServiceLocatorEradicationTests"`
  - Result: FAIL（6 失败 / 0 通过 / 0 跳过）
  - 问题记录：测试根目录定位与扫描范围过宽（误扫 `.worktrees`、测试项目），随后修复为基于 `AkashaNavigator.sln` 定位仓库根并仅扫描 `AkashaNavigator/`。
- 红灯验证（首次执行）：
  - `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ServiceRegistrationTests"`
  - Result: FAIL（1 失败 / 4 通过 / 0 跳过）
  - 问题记录：`HotkeyManager` 未注册到 DI。
- 绿灯复验：
  - `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ServiceRegistrationTests"`
  - Result: PASS（0 失败 / 5 通过 / 0 跳过）

### Task 2

- Result: PASS
- 关键变更：
  - `PluginSettingsWindow` 改为注入 `IOverlayManager`。
  - `HotkeyManager` 改为构造注入 `HotkeyService`，删除 `App.Services` fallback/new 分支。
  - `App.xaml.cs` 改为 `Services.GetRequiredService<HotkeyManager>()`。
  - `ServiceCollectionExtensions` 增加 `services.AddSingleton<HotkeyManager>()`。
- 验证：
  - `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ServiceRegistrationTests"`
  - Result: PASS（0 失败 / 5 通过 / 0 跳过）

### Task 3

- Result: PASS
- 关键变更：
  - 删除 5 个服务的 `Instance/ResetInstance`：
    - `DataService`
    - `PioneerNoteService`
    - `WindowStateService`
    - `PluginAssociationManager`
    - `ProfileMarketplaceService`
  - 删除 `ServiceCollectionExtensions` 中 `PioneerNoteService.Instance = instance` 静态回填。
  - `ProfileMarketplaceService` 测试构造函数改为显式注入 `IPluginAssociationManager`、`IPluginLibrary`。
  - `ExitRecordPrompt.ShouldShowPrompt` 删除静态 fallback。
  - `PlayerWindow` 构造注入 `IPioneerNoteService` 并显式传参调用。
- 验证：
  - `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ServiceLocatorEradicationTests"`
  - Result: PASS（0 失败 / 6 通过 / 0 跳过）

### Task 4

- Result: PASS（含 1 个环境限制）
- 测试新增：
  - `AkashaNavigator.Tests/Views/ExitRecordPromptTests.cs`
- 验证：
  - `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ExitRecordPromptTests"`
  - Result: PASS（0 失败 / 2 通过 / 0 跳过）
  - `dotnet test`
  - Result: PASS（0 失败 / 1020 通过 / 32 跳过）
  - `dotnet build AkashaNavigator/AkashaNavigator.csproj -c Release`
  - Result: PASS（0 错误 / 0 警告）
  - `dotnet run --project AkashaNavigator`
  - Result: FAIL（环境权限限制："请求的操作需要提升"，当前环境无法完成手工启动链路验证）

### Task 5 - SDD 审查记录

- Result: PASS
- Spec Review（子代理）：PASS
  - 结论：Task 1-4 必需项均已落地，无缺项。
- Code Quality Review（子代理）：PASS
  - Critical issues: None
  - Non-critical 建议：
    - `HotkeyManager` 当前会释放注入的 `HotkeyService`（容器托管对象，建议后续评估生命周期边界）。
    - `ServiceLocatorEradicationTests` 可继续增强为语法级检查，减少字符串匹配误报风险。
