using System;
using System.Linq;
using AkashaNavigator.Models.Config;
using AkashaNavigator.Models.Profile;

namespace AkashaNavigator.Helpers
{
/// <summary>
/// 鼠标检测配置解析器
/// 用于合并全局配置和 Profile 配置
/// </summary>
public static class CursorDetectionConfigResolver
{
/// <summary>
/// 解析鼠标检测配置（合并全局 + Profile）
/// </summary>
/// <param name="globalConfig">全局鼠标检测配置</param>
/// <param name="profileConfig">Profile 级别的鼠标检测配置</param>
/// <param name="foregroundProcess">前台进程名（不含扩展名）</param>
/// <returns>元组：是否启用、最低透明度、检测间隔、调试日志</returns>
public static (bool enabled, double minOpacity, int intervalMs, bool debugLog) Resolve(
GlobalCursorDetectionConfig? globalConfig,
CursorDetectionConfig? profileConfig,
string? foregroundProcess)
{
// 如果前台进程为空，不启用检测
if (string.IsNullOrEmpty(foregroundProcess))
return (false, 0.3, 200, false);

// 1. 检查 Profile 白名单（仅当 Profile 白名单非空时检查）
bool inProfileWhitelist = profileConfig?.ProcessWhitelist != null &&
profileConfig.ProcessWhitelist.Count > 0 &&
profileConfig.ProcessWhitelist.Any(p => p.Equals(foregroundProcess, StringComparison.OrdinalIgnoreCase));

// 2. 检查全局白名单（仅当全局白名单非空时检查）
bool inGlobalWhitelist = globalConfig?.ProcessWhitelist != null &&
globalConfig.ProcessWhitelist.Count > 0 &&
globalConfig.ProcessWhitelist.Any(p => p.Equals(foregroundProcess, StringComparison.OrdinalIgnoreCase));

// 3. 确定是否启用
bool enabled = false;
if (inProfileWhitelist)
{
// Profile 白名单匹配：使用 Profile 配置（可继承全局 Enabled）
enabled = profileConfig?.Enabled ?? globalConfig?.Enabled ?? false;
}
else if (inGlobalWhitelist)
{
// 仅全局白名单匹配：使用全局配置
enabled = globalConfig?.Enabled ?? false;
}

// 4. 合并其他配置（Profile 优先，全局兜底）
// EnableDebugLog 逻辑：
// - 如果 profileConfig 在白名单中（inProfileWhitelist = true），使用 profile 的 EnableDebugLog
// - 否则，使用全局的 EnableDebugLog
bool debugLog;
if (inProfileWhitelist)
{
    debugLog = profileConfig?.EnableDebugLog ?? globalConfig?.EnableDebugLog ?? false;
}
else
{
    debugLog = globalConfig?.EnableDebugLog ?? false;
}

double minOpacity = profileConfig?.MinOpacity ?? globalConfig?.MinOpacity ?? 0.3;
int intervalMs = profileConfig?.CheckIntervalMs ?? globalConfig?.CheckIntervalMs ?? 200;

return (enabled, minOpacity, intervalMs, debugLog);
}
}
}
