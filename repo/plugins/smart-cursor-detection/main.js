// main.js - 智能鼠标检测插件 v1.0.0
// 检测游戏内鼠标状态，自动切换播放器点击穿透模式

// 保存原始透明度，用于恢复
var originalOpacity = 1.0;
var manualClickThroughEnabled = false;
var pluginEffectActive = false;

/**
 * 解析进程白名单字符串
 * @param {string} str - 逗号分隔的进程名列表
 * @returns {string[]} - 进程名数组
 */
function parseProcessList(str) {
    if (!str || typeof str !== 'string') {
        return [];
    }
    return str.split(',')
        .map(function(s) { return s.trim(); })
        .filter(function(s) { return s.length > 0; });
}

/**
 * 鼠标显示事件处理（游戏 UI 模式）
 * 降低透明度 + 启用穿透，让用户能点击游戏 UI
 */
function onCursorShown() {
    if (!manualClickThroughEnabled) {
        log.debug('手动穿透未开启，忽略智能鼠标效果');
        return;
    }

    var minOpacity = config.get('minOpacity', 0.3);

    if (pluginEffectActive) {
        return;
    }

    // 保存当前透明度
    originalOpacity = window.getOpacity();

    // 降低透明度
    window.setOpacity(minOpacity);

    // 启用自动点击穿透（不会触发定时器逻辑）
    window.setAutoClickThrough(true);
    pluginEffectActive = true;

    log.debug('游戏鼠标显示 - 降低透明度: ' + minOpacity + ', 启用穿透');
}

/**
 * 鼠标隐藏事件处理（游戏模式）
 * 恢复透明度 + 禁用穿透
 */
function onCursorHidden() {
    if (!pluginEffectActive) {
        return;
    }

    // 禁用自动点击穿透
    window.setAutoClickThrough(false);

    // 恢复透明度
    window.setOpacity(originalOpacity);
    pluginEffectActive = false;

    log.debug('游戏鼠标隐藏 - 恢复透明度: ' + originalOpacity + ', 禁用穿透');
}

/**
 * 手动穿透变化事件处理
 * 只有手动穿透开启时，才允许插件接管自动穿透效果
 */
function onClickThroughChanged(data) {
    if (!data || data.source !== 'manual') {
        return;
    }

    manualClickThroughEnabled = !!data.manualEnabled;

    if (!manualClickThroughEnabled) {
        onCursorHidden();
        log.debug('手动穿透关闭 - 立即禁用智能鼠标效果');
        return;
    }

    log.debug('手动穿透开启 - 立即刷新鼠标检测状态');
    window.refreshCursorDetectionState();
}

/**
 * 插件加载时调用
 */
function onLoad() {
    log.info(plugin.name + ' v' + plugin.version + ' 已加载');
    
    // 读取配置
    var processWhitelist = config.get('processWhitelist', '');
    var intervalMs = config.get('intervalMs', 200);
    
    // 解析进程白名单
    var processes = parseProcessList(processWhitelist);
    
    if (processes.length === 0) {
        log.info('进程白名单为空，不启动检测。请在插件设置中配置监控进程列表。');
        return;
    }
    
    log.info('监控进程: ' + processes.join(', '));
    log.info('检测间隔: ' + intervalMs + 'ms');

    manualClickThroughEnabled = window.isClickThrough();
    pluginEffectActive = false;

    // 注册鼠标状态事件
    window.on('cursorShown', onCursorShown);
    window.on('cursorHidden', onCursorHidden);
    window.on('clickThroughChanged', onClickThroughChanged);
    
    // 启动鼠标检测
    var started = window.startCursorDetection({
        processWhitelist: processes,
        intervalMs: intervalMs
    });
    
    if (started) {
        log.info('鼠标检测已启动');

        if (manualClickThroughEnabled) {
            window.refreshCursorDetectionState();
        }
    } else {
        log.warn('鼠标检测启动失败');
    }
}

/**
 * 插件卸载时调用
 */
function onUnload() {
    log.info(plugin.name + ' 正在卸载...');
    
    // 停止鼠标检测
    window.stopCursorDetection();

    // 确保卸载时清理插件效果
    onCursorHidden();
    
    // 取消事件订阅
    window.off('cursorShown');
    window.off('cursorHidden');
    window.off('clickThroughChanged');
    
    log.info(plugin.name + ' 已卸载');
}

// 导出停止函数供外部调用
var stop = onUnload;
