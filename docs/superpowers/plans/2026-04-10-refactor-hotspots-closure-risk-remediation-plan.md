# Refactor Hotspots Closure Risk Remediation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 `refactor-hotspots-closure` 分支一次性收敛高风险技术债，优先修复插件边界破坏点与生命周期泄漏点，并完成“可硬切”的单例入口清理。

**Architecture:** 采用“先护栏测试，再最小修复，再全量验证”的闭环。先锁定 branch/worktree 不漂移，再按风险优先级从安全边界到资源生命周期，再到 DI 纯化（移除剩余 `Instance` 入口）。不做大改框架，不引入过度工程化抽象。

**Tech Stack:** .NET 8, WPF, Microsoft.Extensions.DependencyInjection, xUnit, ClearScript V8

---

## Branch/Worktree Guardrails (Mandatory)

**Execution target (fixed):**
- Worktree: `C:/Users/alin9/code/C-Sharp/AkashaNavigator/.worktrees/refactor-hotspots-closure`
- Branch: `refactor-hotspots-closure`

**Rules:**
- 禁止 `git worktree add`
- 禁止新建分支
- 禁止在主工作区（`.../AkashaNavigator`）改代码文件

- [ ] **Step 1: 校验当前分支**

Run: `git rev-parse --abbrev-ref HEAD`
Expected: `refactor-hotspots-closure`

- [ ] **Step 2: 校验 worktree 路径**

Run: `git worktree list`
Expected: 当前执行目录位于 `.../.worktrees/refactor-hotspots-closure`

- [ ] **Step 3: 记录基线状态**

Run: `git status --short --branch`
Expected: 输出被记录，后续只允许出现本计划涉及文件变更

## File Structure

### Plugin boundary and path safety
- Modify: `AkashaNavigator/Plugins/Core/PluginContext.cs`
  - Responsibility: 入口脚本路径标准化并强制目录边界校验。
- Modify: `AkashaNavigator/Plugins/Core/PluginEngine.cs`
  - Responsibility: 模块搜索路径仅允许插件根目录内路径。

### Unsubscribe deletion safety
- Modify: `AkashaNavigator/Services/PluginHost.cs`
  - Responsibility: 仅允许删除 InstalledPlugins 下的目录，禁止误删内置插件目录。
- Modify: `AkashaNavigator/Services/PluginLibrary.cs` (if needed for helper)
  - Responsibility: 提供稳定目录判断辅助，避免散落字符串比较。

### Lifecycle cleanup completeness
- Modify: `AkashaNavigator/Plugins/Core/PluginApi.cs`
  - Responsibility: 卸载时统一清理可释放 API（Http/Window cursor hooks）。
- Modify: `AkashaNavigator/Plugins/Apis/WindowApi.cs`
  - Responsibility: 提供可重入清理入口，确保事件解绑对称。

### Full singleton eradication (hard cut)
- Modify: `AkashaNavigator/Services/ConfigService.cs`
- Modify: `AkashaNavigator/Services/NotificationService.cs`
- Modify: `AkashaNavigator/Services/PluginLibrary.cs`
- Modify: `AkashaNavigator/Services/SubscriptionManager.cs`
- Modify: `AkashaNavigator/Services/ProfileRegistry.cs`
- Modify: `AkashaNavigator/Services/PluginRegistry.cs`
- Modify: `AkashaNavigator/Services/DataMigration.cs`
  - Responsibility: 删除静态 `Instance` 入口，不保留 fallback/new。
- Modify: `AkashaNavigator/Core/ServiceCollectionExtensions.cs`
  - Responsibility: 确保上述服务全部通过 DI 注册与注入。

### Tests
- Create/Modify: `AkashaNavigator.Tests/Architecture/ServiceLocatorEradicationTests.cs`
  - Responsibility: 增加“禁止新增 `Instance/ResetInstance` 入口”护栏。
- Create/Modify: `AkashaNavigator.Tests/Plugins/PluginPathSafetyTests.cs`
  - Responsibility: 覆盖 main/library 越界路径拒绝逻辑。
- Create/Modify: `AkashaNavigator.Tests/Services/PluginHostUnsubscribeTests.cs`
  - Responsibility: 覆盖 built-in 不可删、installed 可删。
- Create/Modify: `AkashaNavigator.Tests/Plugins/PluginCleanupTests.cs`
  - Responsibility: 覆盖插件卸载后资源释放与事件解绑。

## Task 1: Add Failing Guardrail Tests First (TDD Red)

**Files:**
- Modify: `AkashaNavigator.Tests/Architecture/ServiceLocatorEradicationTests.cs`
- Create: `AkashaNavigator.Tests/Plugins/PluginPathSafetyTests.cs`
- Create: `AkashaNavigator.Tests/Services/PluginHostUnsubscribeTests.cs`
- Create: `AkashaNavigator.Tests/Plugins/PluginCleanupTests.cs`

- [ ] **Step 1: 扩展架构护栏，禁止新增静态单例入口**

```csharp
[Theory]
[InlineData("AkashaNavigator/Services/ConfigService.cs")]
[InlineData("AkashaNavigator/Services/NotificationService.cs")]
[InlineData("AkashaNavigator/Services/PluginLibrary.cs")]
[InlineData("AkashaNavigator/Services/SubscriptionManager.cs")]
[InlineData("AkashaNavigator/Services/ProfileRegistry.cs")]
[InlineData("AkashaNavigator/Services/PluginRegistry.cs")]
[InlineData("AkashaNavigator/Services/DataMigration.cs")]
public void TargetServices_ShouldNotContainStaticInstanceEntrypoints(string relativePath)
{
    var root = GetRepositoryRoot();
    var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    var text = File.ReadAllText(path);

    Assert.DoesNotContain("public static", text);
    Assert.DoesNotContain(" Instance", text);
    Assert.DoesNotContain("ResetInstance", text);
}
```

- [ ] **Step 2: 新增插件入口文件越界测试（先红灯）**

```csharp
[Fact]
public void LoadScript_ShouldFail_WhenMainEscapesPluginDirectory()
{
    // manifest.Main = "..\\..\\evil.js"
    // Assert.False(context.LoadScript());
}
```

- [ ] **Step 3: 新增 library 搜索路径越界测试（先红灯）**

```csharp
[Fact]
public void BuildSearchPath_ShouldIgnore_LibraryPathOutsidePluginRoot()
{
    // libraryPaths contains "..\\..\\"
    // Assert.DoesNotContain(outsidePath, engine.DocumentSettings.SearchPath);
}
```

- [ ] **Step 4: 新增取消订阅删除范围测试（先红灯）**

```csharp
[Fact]
public void UnsubscribePlugin_ShouldNotDelete_BuiltInPluginDirectory()
{
    // plugin.PluginDirectory -> BuiltInPluginsDirectory
    // Assert.True(result.IsSuccess);
    // Assert.True(Directory.Exists(builtInPath));
}
```

- [ ] **Step 5: 新增卸载清理测试（先红灯）**

```csharp
[Fact]
public void Cleanup_ShouldDisposeHttpApi_AndDetachWindowCursorHandlers()
{
    // Arrange plugin api with http/window api
    // Act pluginApi.Cleanup();
    // Assert disposed/unsubscribed flags
}
```

- [ ] **Step 6: 运行新测试确认失败（红灯）**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ServiceLocatorEradicationTests|FullyQualifiedName~PluginPathSafetyTests|FullyQualifiedName~PluginHostUnsubscribeTests|FullyQualifiedName~PluginCleanupTests"`
Expected: FAIL（新约束未实现）

## Task 2: Fix Plugin Path Boundary (P0)

**Files:**
- Modify: `AkashaNavigator/Plugins/Core/PluginContext.cs`
- Modify: `AkashaNavigator/Plugins/Core/PluginEngine.cs`
- Test: `AkashaNavigator.Tests/Plugins/PluginPathSafetyTests.cs`

- [ ] **Step 1: 为 main.js 入口增加 full path + prefix 校验**

```csharp
var pluginRootFull = Path.GetFullPath(PluginDirectory);
var mainRelative = Manifest.Main ?? "main.js";
var mainFullPath = Path.GetFullPath(Path.Combine(pluginRootFull, mainRelative));

if (!mainFullPath.StartsWith(pluginRootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
    && !string.Equals(mainFullPath, pluginRootFull, StringComparison.OrdinalIgnoreCase))
{
    LastError = "入口文件路径越界";
    Log("入口文件路径越界: {MainFile}", mainRelative);
    return false;
}
```

- [ ] **Step 2: 限制 library 搜索路径只允许插件根目录内路径**

```csharp
var pluginRootFull = Path.GetFullPath(pluginDir);
var candidate = Path.GetFullPath(Path.Combine(pluginRootFull, path));

if (!candidate.StartsWith(pluginRootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
    && !string.Equals(candidate, pluginRootFull, StringComparison.OrdinalIgnoreCase))
{
    LogService.Instance.Warn(nameof(PluginEngine), "Ignored out-of-root library path: {Path}", path);
    continue;
}
```

- [ ] **Step 3: 运行路径安全测试（绿灯）**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~PluginPathSafetyTests"`
Expected: PASS

## Task 3: Fix Unsubscribe Deletion Scope (P0)

**Files:**
- Modify: `AkashaNavigator/Services/PluginHost.cs`
- Test: `AkashaNavigator.Tests/Services/PluginHostUnsubscribeTests.cs`

- [ ] **Step 1: 删除目录前增加 installed-root 强校验**

```csharp
var installedRoot = Path.GetFullPath(AppPaths.InstalledPluginsDirectory);
var pluginDirFull = Path.GetFullPath(pluginDir);
var isUnderInstalled = pluginDirFull.StartsWith(installedRoot + Path.DirectorySeparatorChar,
                                                 StringComparison.OrdinalIgnoreCase)
                       || string.Equals(pluginDirFull, installedRoot, StringComparison.OrdinalIgnoreCase);

if (Directory.Exists(pluginDirFull) && isUnderInstalled)
{
    Directory.Delete(pluginDirFull, recursive: true);
}
else if (Directory.Exists(pluginDirFull))
{
    Log("跳过删除非 InstalledPlugins 目录: {PluginDir}", pluginDirFull);
}
```

- [ ] **Step 2: 运行取消订阅测试（绿灯）**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~PluginHostUnsubscribeTests"`
Expected: PASS

## Task 4: Complete Plugin Cleanup Lifecycle (P1)

**Files:**
- Modify: `AkashaNavigator/Plugins/Core/PluginApi.cs`
- Modify: `AkashaNavigator/Plugins/Apis/WindowApi.cs`
- Test: `AkashaNavigator.Tests/Plugins/PluginCleanupTests.cs`

- [ ] **Step 1: 给 WindowApi 增加统一 Cleanup()（幂等）**

```csharp
private bool _cleaned;

public void Cleanup()
{
    if (_cleaned)
        return;

    if (_isCursorDetectionStartedByThisApi || _cursorDetectionService.IsRunning)
    {
        _cursorDetectionService.CursorShown -= OnCursorShown;
        _cursorDetectionService.CursorHidden -= OnCursorHidden;
    }

    _isCursorDetectionStartedByThisApi = false;
    _cleaned = true;
}
```

- [ ] **Step 2: 在 PluginApi.Cleanup() 中补齐 HttpApi Dispose + WindowApi Cleanup**

```csharp
if (_windowApi != null)
    TryCleanupApi("WindowApi", () => _windowApi.Cleanup());

if (_httpApi != null)
    TryCleanupApi("HttpApi", () => _httpApi.Dispose());
```

- [ ] **Step 3: 运行清理测试（绿灯）**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~PluginCleanupTests"`
Expected: PASS

## Task 5: Fully Remove Remaining Service Singletons (Hard Cut, No Compatibility Layer)

**Files:**
- Modify: `AkashaNavigator/Services/ConfigService.cs`
- Modify: `AkashaNavigator/Services/NotificationService.cs`
- Modify: `AkashaNavigator/Services/PluginLibrary.cs`
- Modify: `AkashaNavigator/Services/SubscriptionManager.cs`
- Modify: `AkashaNavigator/Services/ProfileRegistry.cs`
- Modify: `AkashaNavigator/Services/PluginRegistry.cs`
- Modify: `AkashaNavigator/Services/DataMigration.cs`
- Modify: `AkashaNavigator/Core/ServiceCollectionExtensions.cs` (仅必要注入修补)
- Test: `AkashaNavigator.Tests/Architecture/ServiceLocatorEradicationTests.cs`

- [ ] **Step 1: 删除上述服务中的 `#region Singleton` 与 `Instance` 入口**

```csharp
// remove pattern
private static IConfigService? _instance;
public static IConfigService Instance { get { ... } set => ...; }
```

- [ ] **Step 2: 修复编译错误，全部改为构造注入/参数传递**

```csharp
// before
var config = ConfigService.Instance.Config;

// after
var config = _configService.Config;
```

- [ ] **Step 3: 运行架构护栏测试（绿灯）**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~ServiceLocatorEradicationTests"`
Expected: PASS

## Task 6: Final Verification Gate

**Files:**
- No new files required

- [ ] **Step 1: 跑插件相关回归测试**

Run: `dotnet test AkashaNavigator.Tests --filter "FullyQualifiedName~Plugin"`
Expected: PASS

- [ ] **Step 2: 跑全量测试**

Run: `dotnet test`
Expected: PASS（0 failed）

- [ ] **Step 3: 跑 Release 构建**

Run: `dotnet build AkashaNavigator/AkashaNavigator.csproj -c Release`
Expected: PASS（0 errors）

- [ ] **Step 4: 最小手工冒烟**

Run: `dotnet run --project AkashaNavigator`
Expected: 应用可启动；插件加载/卸载与退出提示链路无崩溃

## Commit Plan (Suggested)

- [ ] `test(architecture): add risk guardrail tests for singleton and plugin boundaries`
- [ ] `fix(plugin-security): enforce plugin root boundary for main and library paths`
- [ ] `fix(plugin-host): restrict unsubscribe deletion to installed plugins directory`
- [ ] `fix(plugin-lifecycle): cleanup http and cursor subscriptions on unload`
- [ ] `refactor(di): remove remaining static singleton service entrypoints`
- [ ] `test(regression): add coverage for unsubscribe safety and cleanup idempotency`

## Self-Review

- 覆盖性：已覆盖 P0（路径越界、误删目录）、P1（生命周期清理）、P1/P2（剩余静态单例入口）。
- 可执行性：每个任务具备明确文件、步骤、命令、预期结果。
- 非过度工程化：仅在现有架构内做边界加固与依赖纯化，不引入新框架。
- 分支约束：计划开头已固定 `refactor-hotspots-closure` worktree，不新建 worktree/分支。
