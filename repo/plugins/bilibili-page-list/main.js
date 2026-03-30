// main.js - B站分P列表插件 v1.2.1
// 显示B站视频分P列表独立窗口，支持快速切换分P

// ============================================================================
// 状态管理
// ============================================================================

/**
 * 插件状态
 */
var state = {
    currentVideoId: null,      // 当前视频ID (BVid 或 AVid)
    currentVideoIdType: null,  // ID类型: 'bvid' | 'avid'
    currentPage: 1,            // 当前分P
    pageList: [],              // 分P列表
    isVisible: false,          // 面板是否可见
    scrollOffset: 0,           // 滚动偏移
    danmakuEnabled: true,      // 弹幕开关状态（默认开启）
    danmakuStateKnown: false,  // 是否已成功同步弹幕状态
    subtitleEnabled: false,    // 字幕开关状态
    subtitleLanguage: '',      // 当前字幕语言（由 SubtitleService 提供）
    subtitleReady: false,      // 是否已加载可用字幕
    pendingSubtitleEnable: false, // 等待字幕加载后自动开启
    lastToggleAt: 0,           // 面板开关防抖时间戳
    lastSubtitleToggleAt: 0,   // 字幕开关防抖时间戳
    pendingOpen: false,        // 启动阶段延迟打开标记
    lastPageNavigateAt: 0,     // 分P切换防抖时间戳
    pendingNavigationPage: 0   // 最近一次导航目标分P（用于快速切换保护）
};

var PAGE_NAVIGATION_DEBOUNCE_MS = 320;

// ============================================================================
// URL 解析模块
// ============================================================================

/**
 * 解析B站视频URL
 * @param {string} url - 要解析的URL
 * @returns {Object} 解析结果 { isBilibili, videoId, videoIdType, currentPage }
 */
function parseUrl(url) {
    var result = {
        isBilibili: false,
        videoId: null,
        videoIdType: null,
        currentPage: 1,
        hasPageParam: false
    };
    
    if (!url || typeof url !== 'string') {
        return result;
    }
    
    // 检查是否为B站视频URL
    var bilibiliPattern = /bilibili\.com\/video\/(BV[a-zA-Z0-9]+|av\d+)/i;
    var match = url.match(bilibiliPattern);
    
    if (!match) {
        return result;
    }
    
    result.isBilibili = true;
    var videoIdStr = match[1];
    
    // 判断ID类型
    if (videoIdStr.toLowerCase().startsWith('bv')) {
        result.videoId = videoIdStr;
        result.videoIdType = 'bvid';
    } else if (videoIdStr.toLowerCase().startsWith('av')) {
        result.videoId = videoIdStr.substring(2); // 去掉 'av' 前缀
        result.videoIdType = 'avid';
    }
    
    // 提取分P参数
    var pageMatch = url.match(/[?&]p=(\d+)/);
    if (pageMatch) {
        var parsedPage = parseInt(pageMatch[1], 10);
        if (parsedPage > 0) {
            result.currentPage = parsedPage;
            result.hasPageParam = true;
        }
    }
    
    return result;
}

// ============================================================================
// API 客户端模块
// ============================================================================

/**
 * 获取分P列表
 * @param {string} videoId - 视频ID
 * @param {string} idType - ID类型 ('bvid' | 'avid')
 * @returns {Array} 分P列表
 */
function fetchPageList(videoId, idType) {
    var apiUrl = 'https://api.bilibili.com/x/player/pagelist';
    
    if (idType === 'bvid') {
        apiUrl += '?bvid=' + videoId;
    } else {
        apiUrl += '?aid=' + videoId;
    }
    
    log.info('正在请求分P列表: ' + apiUrl);
    
    // http.get 返回同步结果，不是 Promise
    var response = http.get(apiUrl);
    
    log.info('HTTP响应: success=' + response.success + ', status=' + response.status);
    
    if (response.data) {
        log.info('响应数据长度: ' + response.data.length + ' 字节');
    }
    
    return parseApiResponse(response);
}

/**
 * 尝试从页面状态/DOM读取当前分P（用于URL未及时更新时兜底）
 * @returns {number} 当前分P，失败返回 0
 */
function detectCurrentPageFromPageState() {
    try {
        var result = webview.executeScriptSync("(function(){" +
            "var readFromUrl=function(){" +
                "try{" +
                    "var m=String(location.href||'').match(/[?&]p=(\\d+)/);" +
                    "if(m&&m[1]){" +
                        "var n=parseInt(m[1],10);" +
                        "if(n>0) return n;" +
                    "}" +
                "}catch(_){}" +
                "return 0;" +
            "};" +
            "var readFromInitialState=function(){" +
                "try{" +
                    "var st=window.__INITIAL_STATE__||null;" +
                    "if(!st) return 0;" +
                    "if(typeof st.p==='number'&&st.p>0) return st.p;" +
                    "if(st.videoData&&typeof st.videoData.p==='number'&&st.videoData.p>0) return st.videoData.p;" +
                    "var pages=(st.videoData&&st.videoData.pages&&st.videoData.pages.length)?st.videoData.pages:null;" +
                    "if(!pages) return 0;" +
                    "var cid=0;" +
                    "try{" +
                        "if(window.player&&typeof window.player.getVideoMessage==='function'){" +
                            "var msg=window.player.getVideoMessage();" +
                            "cid=msg&&msg.cid?parseInt(msg.cid,10):0;" +
                        "}" +
                    "}catch(_){cid=0;}" +
                    "if(cid>0){" +
                        "for(var i=0;i<pages.length;i++){" +
                            "var item=pages[i]||{};" +
                            "if(parseInt(item.cid,10)===cid){" +
                                "var page=item.page||item.id||(i+1);" +
                                "page=parseInt(page,10);" +
                                "return page>0?page:(i+1);" +
                            "}" +
                        "}" +
                    "}" +
                "}catch(_){}" +
                "return 0;" +
            "};" +
            "var readFromDom=function(){" +
                "try{" +
                    "var activeSelectors=[" +
                        "'.multi-page .on'," +
                        "'.multi-page .active'," +
                        "'.video-pod__list .active'," +
                        "'.list-box li.on'," +
                        "'.list-box li.active'," +
                        "'.video-episode-card__item.active'" +
                    "];" +
                    "for(var i=0;i<activeSelectors.length;i++){" +
                        "var node=document.querySelector(activeSelectors[i]);" +
                        "if(!node) continue;" +
                        "var pageAttr=node.getAttribute('data-page')||node.getAttribute('data-index')||'';" +
                        "if(pageAttr){" +
                            "var attrNum=parseInt(pageAttr,10);" +
                            "if(attrNum>0) return attrNum;" +
                        "}" +
                        "var text=String(node.textContent||'');" +
                        "var m=text.match(/(?:^|\\s)P\\s*(\\d+)(?:\\s|$)/i)||text.match(/(\\d+)/);" +
                        "if(m&&m[1]){" +
                            "var t=parseInt(m[1],10);" +
                            "if(t>0) return t;" +
                        "}" +
                    "}" +
                "}catch(_){}" +
                "return 0;" +
            "};" +
            "var v=readFromUrl();" +
            "if(v>0) return String(v);" +
            "v=readFromInitialState();" +
            "if(v>0) return String(v);" +
            "v=readFromDom();" +
            "if(v>0) return String(v);" +
            "return '';" +
        "})();");

        if (typeof result === 'number') {
            return result > 0 ? result : 0;
        }

        if (typeof result === 'string') {
            var page = parseInt(result, 10);
            return page > 0 ? page : 0;
        }
    } catch (e) {
        log.debug('从页面状态读取当前分P失败（忽略）: ' + e.message);
    }

    return 0;
}

/**
 * 在 URL 无法准确反映当前播放分P时，尝试同步 currentPage
 */
function syncCurrentPageFromPageState() {
    var detectedPage = detectCurrentPageFromPageState();
    if (detectedPage <= 0) {
        return;
    }

    if (state.pageList && state.pageList.length > 0) {
        if (detectedPage > state.pageList.length) {
            detectedPage = state.pageList.length;
        }
    }

    if (detectedPage !== state.currentPage) {
        log.info('同步当前分P: ' + state.currentPage + ' -> ' + detectedPage);
        state.currentPage = detectedPage;
    }
}

/**
 * 解析API响应
 * @param {Object} response - API响应
 * @returns {Array} 解析后的分P列表
 */
function parseApiResponse(response) {
    if (!response || typeof response !== 'object') {
        log.error('API响应无效: response=' + JSON.stringify(response));
        return [];
    }
    
    // 检查请求是否成功
    if (!response.success) {
        log.error('HTTP请求失败: ' + (response.error || 'Unknown error'));
        return [];
    }
    
    // 解析响应数据
    var data = response.data;
    log.info('原始响应数据类型: ' + typeof data);
    
    if (typeof data === 'string') {
        log.info('响应数据是字符串，尝试解析JSON');
        try {
            data = JSON.parse(data);
            log.info('JSON解析成功');
        } catch (e) {
            log.error('JSON解析失败: ' + e.message);
            log.error('原始数据: ' + data.substring(0, 200));
            return [];
        }
    }
    
    log.info('解析后的数据类型: ' + typeof data);
    log.info('data.code = ' + data.code);
    
    if (!data || typeof data !== 'object') {
        log.error('解析后的数据不是对象');
        return [];
    }
    
    if (data.code !== 0) {
        log.error('API返回错误码: ' + data.code + ', 消息: ' + data.message);
        return [];
    }
    
    if (!data.data) {
        log.error('data.data 不存在');
        return [];
    }
    
    if (!Array.isArray(data.data)) {
        log.error('data.data 不是数组，类型: ' + typeof data.data);
        return [];
    }
    
    log.info('成功获取分P列表，共 ' + data.data.length + ' 个分P');
    
    return data.data.map(function(item) {
        return {
            cid: item.cid,
            page: item.page,
            part: item.part || ('P' + item.page),
            duration: item.duration || 0
        };
    });
}

// ============================================================================
// 导航 URL 构建模块
// ============================================================================

/**
 * 构建导航URL
 * @param {string} videoId - 视频ID
 * @param {string} idType - ID类型
 * @param {number} page - 分P页码
 * @returns {string} 导航URL
 */
function buildNavigationUrl(videoId, idType, page) {
    var baseUrl = 'https://www.bilibili.com/video/';
    
    if (idType === 'bvid') {
        baseUrl += videoId;
    } else {
        baseUrl += 'av' + videoId;
    }
    
    if (page > 1) {
        baseUrl += '?p=' + page;
    }
    
    return baseUrl;
}

// ============================================================================
// 状态管理模块
// ============================================================================

/**
 * 合并配置
 * @param {Object} userConfig - 用户配置
 * @param {Object} defaults - 默认配置
 * @returns {Object} 合并后的配置
 */
function mergeConfig(userConfig, defaults) {
    var result = {};
    
    // 复制默认值
    for (var key in defaults) {
        if (defaults.hasOwnProperty(key)) {
            if (typeof defaults[key] === 'object' && defaults[key] !== null && !Array.isArray(defaults[key])) {
                result[key] = mergeConfig(userConfig && userConfig[key] || {}, defaults[key]);
            } else {
                result[key] = defaults[key];
            }
        }
    }
    
    // 覆盖用户配置
    if (userConfig && typeof userConfig === 'object') {
        for (var key in userConfig) {
            if (userConfig.hasOwnProperty(key)) {
                if (typeof userConfig[key] === 'object' && userConfig[key] !== null && !Array.isArray(userConfig[key])) {
                    result[key] = mergeConfig(userConfig[key], result[key] || {});
                } else {
                    result[key] = userConfig[key];
                }
            }
        }
    }
    
    return result;
}

// ============================================================================
// 分P列表渲染模块
// ============================================================================

// 渲染配置
var renderConfig = {
    itemHeight: 40,
    maxVisibleItems: 10,
    windowWidth: 320,
    windowHeight: 400,
    activeColor: '#00a1d6',
    hoverColor: '#e5e9ef',
    textColor: '#222222',
    bgColor: '#ffffff',
    borderColor: '#e3e5e7'
};

/**
 * 切换面板显示/隐藏
 */
function toggleVisibility() {
    var now = Date.now();
    if (now - state.lastToggleAt < 250) {
        return;
    }
    state.lastToggleAt = now;

    // 每次打开前都同步一次当前 URL，确保高亮分P与当前播放一致
    if (!state.isVisible) {
        var latestUrl = player.getUrl();
        if (!latestUrl || latestUrl === 'about:blank') {
            state.pendingOpen = true;
            log.info('播放器尚未就绪，延迟打开分P面板');
            if (typeof osd !== 'undefined') {
                osd.show('播放器加载中，稍后自动打开分P', '📋');
            }
            return;
        }

        if (latestUrl) {
            onUrlChanged(latestUrl);
        }
    }

    // 如果分P列表为空，尝试重新获取
    if (!state.pageList || state.pageList.length === 0) {
        log.info('分P列表为空，尝试重新获取');
        var currentUrl = player.getUrl();
        log.info('当前URL: ' + currentUrl);
        
        if (currentUrl) {
            onUrlChanged(currentUrl);
        }
        
        // 再次检查
        if (!state.pageList || state.pageList.length === 0) {
            log.info('仍然无法获取分P列表');
            // 显示 OSD 提示
            if (typeof osd !== 'undefined') {
                osd.show('无法获取分P信息', '📋');
            }
            return;
        }
    }
    
    // 单P视频不显示
    if (state.pageList.length === 1) {
        log.info('单P视频，不显示面板');
        // 显示 OSD 提示
        if (typeof osd !== 'undefined') {
            osd.show('当前视频只有1个分P', '📋');
        }
        return;
    }
    
    if (state.isVisible) {
        hideOverlay();
    } else {
        showOverlay();
    }
}

/**
 * 显示面板
 */
function showOverlay() {
    var x = config.get('panel.x', config.get('overlay.x', 100));
    var y = config.get('panel.y', config.get('overlay.y', 100));
    var width = config.get('panel.width', renderConfig.windowWidth);
    var height = config.get('panel.height', renderConfig.windowHeight);
    var topmost = config.get('panel.topmost', false);
    
    log.info('显示面板: x=' + x + ', y=' + y + ', width=' + width + ', height=' + height);
    
    // 设置位置和大小
    panel.setPosition(x, y);
    panel.setSize(width, height);
    panel.setTopmost(topmost);
    panel.setHeader('分P列表 (' + state.pageList.length + ')', '单击条目可快速跳转');
    var danmakuSynced = syncDanmakuStateFromPage();
    if (!danmakuSynced) {
        log.warn('显示面板取消：无法获取弹幕状态');
        if (typeof osd !== 'undefined') {
            osd.show('弹幕状态未就绪，请稍后再试', '⚠️');
        }
        return;
    }

    syncSubtitleStateFromPage();
    refreshActionButtons();
    
    // 显示面板
    panel.show();
    
    state.isVisible = true;
    renderPageList();
}

/**
 * 在需要时根据当前 URL 同步分P信息
 * @returns {boolean} 是否成功拿到分P列表
 */
function ensurePageListReady() {
    var currentUrl = player.getUrl();
    if (!currentUrl || currentUrl === 'about:blank') {
        log.info('播放器尚未就绪，无法同步分P信息');
        return false;
    }

    onUrlChanged(currentUrl);
    return !!(state.pageList && state.pageList.length > 0);
}

/**
 * 从页面同步弹幕开关状态（最佳努力）
 */
function syncDanmakuStateFromPage() {
    try {
        var result = webview.executeScriptSync("(function(){" +
            "var candidates=[" +
                "'.bpx-player-ctrl-dm .bui-switch-input'," +
                "'.bpx-player-dm-switch input'," +
                "'.bpx-player-ctrl-dm-switch input'," +
                "'.bilibili-player-video-danmaku-switch input'," +
                "'.bui-switch-input'" +
            "];" +
            "for(var i=0;i<candidates.length;i++){" +
                "var el=document.querySelector(candidates[i]);" +
                "if(!el) continue;" +
                "if(typeof el.checked==='boolean') return el.checked ? '1' : '0';" +
                "if(el.getAttribute){" +
                    "var aria=el.getAttribute('aria-checked');" +
                    "if(aria==='true') return '1';" +
                    "if(aria==='false') return '0';" +
                "}" +
            "}" +
            "return '';" +
        "})();");

        if (result === '1' || result === 1 || result === true || result === 'true') {
            state.danmakuEnabled = true;
            state.danmakuStateKnown = true;
            return true;
        } else if (result === '0' || result === 0 || result === false || result === 'false') {
            state.danmakuEnabled = false;
            state.danmakuStateKnown = true;
            return true;
        }
    } catch (e) {
        log.debug('同步弹幕状态失败（忽略）: ' + e.message);
    }

    state.danmakuStateKnown = false;
    return false;
}

/**
 * 从页面同步字幕开关与语言状态
 */
function syncSubtitleStateFromPage() {
    try {
        var result = webview.executeScriptSync("(function(){" +
            "var enabled=null;" +
            "var closeSwitch=document.querySelector('.bpx-player-ctrl-subtitle-close-switch[data-action=close]');" +
            "if(closeSwitch&&closeSwitch.classList){enabled=!closeSwitch.classList.contains('bpx-state-active');}" +
            "var active=document.querySelector('.bpx-player-ctrl-subtitle-language-item.bpx-state-active[data-lan]');" +
            "var lang=active?(active.getAttribute('data-lan')||''):'';" +
            "if(enabled===null){enabled=!!lang;}" +
            "var hasZh=!!document.querySelector('.bpx-player-ctrl-subtitle-language-item[data-lan=ai-zh],.bpx-player-ctrl-subtitle-language-item[data-lan=zh-CN],.bpx-player-ctrl-subtitle-language-item[data-lan^=zh]');" +
            "return (enabled?'1':'0') + '|' + lang + '|' + (hasZh?'1':'0');" +
        "})();");

        if (typeof result !== 'string') {
            return;
        }

        var parts = result.split('|');
        if (parts.length >= 3) {
            state.subtitleEnabled = parts[0] === '1';
            state.subtitleLanguage = parts[1] || '';
            state.subtitleReady = parts[2] === '1' || state.subtitleReady;
        }
    } catch (e) {
        log.debug('同步字幕状态失败（忽略）: ' + e.message);
    }
}

function disableImmersiveBilingualSubtitle() {
    webview.injectScript("(function(){try{" +
        "var styleId='akasha-hide-imt-subtitle';" +
        "if(!document.getElementById(styleId)){" +
            "var style=document.createElement('style');" +
            "style.id=styleId;" +
            "style.textContent='.imt-quick-subtitle-button,.imt-quick-subtitle-pop-content,[class*=\\'immersive-translate\\']{display:none !important;pointer-events:none !important;}';" +
            "document.head.appendChild(style);" +
        "}" +
        "var overlays=document.querySelectorAll('.imt-quick-subtitle-button,.imt-quick-subtitle-pop-content,[class*=\\'immersive-translate\\']');" +
        "for(var i=0;i<overlays.length;i++){overlays[i].style.display='none';}" +
    "}catch(e){console.error('[bilibili-page-list] disable immersive subtitle failed',e);}})();");
}

function blockAltSForPageExtensions() {
    webview.injectScript("(function(){try{" +
        "if(window.__akashaAltSBlocked) return;" +
        "window.__akashaAltSBlocked=true;" +
        "var block=function(e){" +
            "var key=(e.key||'').toLowerCase();" +
            "if(e.altKey && !e.ctrlKey && !e.metaKey && key==='s'){" +
                "e.preventDefault();" +
                "e.stopPropagation();" +
                "if(e.stopImmediatePropagation) e.stopImmediatePropagation();" +
            "}" +
        "};" +
        "window.addEventListener('keydown', block, true);" +
        "window.addEventListener('keyup', block, true);" +
    "}catch(e){console.error('[bilibili-page-list] block Alt+S failed',e);}})();");
}

function installImmersiveSubtitleGuard() {
    webview.injectScript("(function(){try{" +
        "if(window.__akashaImtGuardInstalled) return;" +
        "window.__akashaImtGuardInstalled=true;" +
        "var hideNode=function(node){" +
            "if(!node||!node.style) return;" +
            "node.style.display='none';" +
            "node.style.pointerEvents='none';" +
        "};" +
        "var shouldHide=function(el){" +
            "if(!el||!el.className) return false;" +
            "var cls=String(el.className);" +
            "return cls.indexOf('imt-quick-subtitle')>=0 || cls.indexOf('immersive-translate')>=0;" +
        "};" +
        "var sweep=function(){" +
            "var all=document.querySelectorAll('[class*=\\'imt-quick-subtitle\\'],[class*=\\'immersive-translate\\']');" +
            "for(var i=0;i<all.length;i++){hideNode(all[i]);}" +
        "};" +
        "sweep();" +
        "var obs=new MutationObserver(function(muts){" +
            "for(var i=0;i<muts.length;i++){" +
                "var m=muts[i];" +
                "for(var j=0;j<m.addedNodes.length;j++){" +
                    "var n=m.addedNodes[j];" +
                    "if(n&&n.nodeType===1){" +
                        "if(shouldHide(n)) hideNode(n);" +
                        "var inner=n.querySelectorAll?n.querySelectorAll('[class*=\\'imt-quick-subtitle\\'],[class*=\\'immersive-translate\\']'):[];" +
                        "for(var k=0;k<inner.length;k++){hideNode(inner[k]);}" +
                    "}" +
                "}" +
            "}" +
        "});" +
        "obs.observe(document.documentElement||document.body,{childList:true,subtree:true});" +
    "}catch(e){console.error('[bilibili-page-list] install guard failed',e);}})();");
}

function getPreferredSubtitleLanguageFromDom() {
    try {
        var result = webview.executeScriptSync("(function(){" +
            "var ai=document.querySelector('.bpx-player-ctrl-subtitle-major-inner .bpx-player-ctrl-subtitle-language-item[data-lan=ai-zh],.bpx-player-ctrl-subtitle-language-item[data-lan=ai-zh]');" +
            "if(ai) return 'ai-zh';" +
            "var zhcn=document.querySelector('.bpx-player-ctrl-subtitle-major-inner .bpx-player-ctrl-subtitle-language-item[data-lan=zh-CN],.bpx-player-ctrl-subtitle-language-item[data-lan=zh-CN]');" +
            "if(zhcn) return 'zh-CN';" +
            "var zh=document.querySelector('.bpx-player-ctrl-subtitle-major-inner .bpx-player-ctrl-subtitle-language-item[data-lan^=zh],.bpx-player-ctrl-subtitle-language-item[data-lan^=zh]');" +
            "if(zh) return zh.getAttribute('data-lan') || 'zh';" +
            "return '';" +
        "})();");

        return typeof result === 'string' ? result : '';
    } catch (e) {
        return '';
    }
}

/**
 * 刷新顶部动作按钮状态
 */
function refreshActionButtons() {
    var hasMultiplePages = state.pageList && state.pageList.length > 1;
    var canGoPrev = hasMultiplePages && state.currentPage > 1;
    var canGoNext = hasMultiplePages && state.currentPage < state.pageList.length;
    
    panel.setActionButtons({
        prev: '◀',
        next: '▶',
        danmaku: '弹',
        subtitle: '字',
        prevEnabled: canGoPrev,
        nextEnabled: canGoNext,
        danmakuEnabled: state.danmakuEnabled,
        subtitleEnabled: state.subtitleEnabled
    });
}

/**
 * 保存窗口位置到配置
 */
function saveOverlayPosition() {
    var position = panel.getPosition();
    var bounds = panel.getBounds();
    if (position) {
        config.set('panel.x', position.x);
        config.set('panel.y', position.y);
        config.set('overlay.x', position.x);
        config.set('overlay.y', position.y);
    }

    if (bounds) {
        config.set('panel.width', bounds.width);
        config.set('panel.height', bounds.height);
    }

    if (position) {
        log.debug('已保存窗口位置: x=' + position.x + ', y=' + position.y);
    }
}

/**
 * 隐藏面板
 */
function hideOverlay() {
    // 保存窗口位置
    saveOverlayPosition();
    
    panel.hide();
    state.isVisible = false;
}

/**
 * 渲染分P列表
 */
function renderPageList() {
    if (state.pageList.length === 0) {
        log.warn('renderPageList: 分P列表为空');
        return;
    }

    panel.setHeader('分P列表 (' + state.pageList.length + ')', '单击条目可快速跳转');
    panel.setPageList(state.pageList, state.currentPage);
}

// ============================================================================
// 分P导航模块
// ============================================================================

function emitGlobalPlaybackSnapshotHint() {
    try {
        webview.injectScript("(function(){try{window.dispatchEvent(new CustomEvent('akasha:save-playback-state'));}catch(e){}})();");
    } catch (e) {
        log.debug('发送全局播放状态快照提示失败（忽略）: ' + e.message);
    }
}

/**
 * 导航到指定分P
 * @param {number} page - 目标分P页码
 */
function navigateToPage(page) {
    if (!state.currentVideoId || !state.currentVideoIdType) {
        log.warn('无法导航：视频ID未初始化');
        return;
    }
    
    // 查找分P信息
    var pageItem = null;
    for (var i = 0; i < state.pageList.length; i++) {
        if (state.pageList[i].page === page) {
            pageItem = state.pageList[i];
            break;
        }
    }
    
    if (!pageItem) {
        log.warn('无法导航：分P ' + page + ' 不存在');
        return;
    }

    var now = Date.now();
    if (now - state.lastPageNavigateAt < PAGE_NAVIGATION_DEBOUNCE_MS) {
        log.debug('分P切换过快，已忽略: target=' + page);
        return;
    }
    
    var navUrl = buildNavigationUrl(state.currentVideoId, state.currentVideoIdType, page);
    
    log.info('导航到分P ' + page + ': ' + navUrl);

    state.lastPageNavigateAt = now;
    state.pendingNavigationPage = page;

    // 先更新本地状态，避免在 URL 事件返回前面板仍显示旧分P
    state.currentPage = page;
    if (state.isVisible) {
        renderPageList();
        refreshActionButtons();
    }
    
    // 显示OSD提示
    if (typeof osd !== 'undefined') {
        var title = pageItem.part || ('P' + page);
        osd.show('P' + page + '/' + state.pageList.length + ': ' + title, '▶️');
    }

    // 提示全局状态管理器在 URL 切换前先保存一次播放状态
    emitGlobalPlaybackSnapshotHint();
    
    // 导航到新页面
    player.navigate(navUrl);
}

/**
 * 跳转到下一个分P
 */
function goToNextPage() {
    if (!state.pageList || state.pageList.length === 0) {
        log.info('分P列表为空，尝试在快捷键触发时同步加载');
        if (!ensurePageListReady()) {
            log.info('分P列表同步失败，无法切换');
            if (typeof osd !== 'undefined') {
                osd.show('无法获取分P信息', '⚠️');
            }
            return;
        }
    }

    if (state.pageList.length <= 1) {
        log.info('无分P列表或只有一个分P，无法切换');
        if (typeof osd !== 'undefined') {
            osd.show('没有更多分P', '⚠️');
        }
        return;
    }
    
    syncCurrentPageFromPageState();
    var nextPage = state.currentPage + 1;
    
    // 检查是否到达边界
    if (nextPage > state.pageList.length) {
        log.info('已经是最后一个分P');
        if (typeof osd !== 'undefined') {
            osd.show('已经是最后一个分P', '⚠️');
        }
        return;
    }
    
    navigateToPage(nextPage);
}

/**
 * 跳转到上一个分P
 */
function goToPreviousPage() {
    if (!state.pageList || state.pageList.length === 0) {
        log.info('分P列表为空，尝试在快捷键触发时同步加载');
        if (!ensurePageListReady()) {
            log.info('分P列表同步失败，无法切换');
            if (typeof osd !== 'undefined') {
                osd.show('无法获取分P信息', '⚠️');
            }
            return;
        }
    }

    if (state.pageList.length <= 1) {
        log.info('无分P列表或只有一个分P，无法切换');
        if (typeof osd !== 'undefined') {
            osd.show('没有更多分P', '⚠️');
        }
        return;
    }
    
    syncCurrentPageFromPageState();
    var prevPage = state.currentPage - 1;
    
    // 检查是否到达边界
    if (prevPage < 1) {
        log.info('已经是第一个分P');
        if (typeof osd !== 'undefined') {
            osd.show('已经是第一个分P', '⚠️');
        }
        return;
    }
    
    navigateToPage(prevPage);
}

// ============================================================================
// 事件处理
// ============================================================================

/**
 * URL变化处理
 * @param {string} url - 新URL
 */
function onUrlChanged(url) {
    log.info('URL变化事件触发: ' + url);
    
    var parseResult = parseUrl(url);
    
    log.info('URL解析结果: isBilibili=' + parseResult.isBilibili + ', videoId=' + parseResult.videoId + ', idType=' + parseResult.videoIdType);
    
    if (!parseResult.isBilibili) {
        // 非B站视频，隐藏面板
        if (state.isVisible) {
            hideOverlay();
        }
        state.pendingOpen = false;
        state.currentVideoId = null;
        state.pageList = [];
        return;
    }
    
    // 更新当前页码（URL 没有 p 参数时，尝试从页面状态兜底）
    if (parseResult.hasPageParam) {
        state.currentPage = parseResult.currentPage;
    } else {
        syncCurrentPageFromPageState();
    }
    if (state.pendingNavigationPage === parseResult.currentPage) {
        state.pendingNavigationPage = 0;
    }
    
    // 如果是同一个视频，只更新页码
    if (state.currentVideoId === parseResult.videoId) {
        if (!state.pageList || state.pageList.length === 0) {
            log.info('同一视频但分P列表为空，尝试重新获取');
            state.pageList = fetchPageList(parseResult.videoId, parseResult.videoIdType);
        }

        if (state.isVisible) {
            renderPageList();
            refreshActionButtons();
        }
        return;
    }
    
    // 新视频，获取分P列表
    state.currentVideoId = parseResult.videoId;
    state.currentVideoIdType = parseResult.videoIdType;
    
    // 同步获取分P列表
    var pageList = fetchPageList(parseResult.videoId, parseResult.videoIdType);
    state.pageList = pageList;
    
    // 单P视频不显示列表
    if (pageList.length <= 1) {
        log.info('单P视频，不显示分P列表');
        return;
    }
    
    log.info('获取到 ' + pageList.length + ' 个分P');
    
    // 如果面板已显示，刷新渲染
    if (state.isVisible) {
        renderPageList();
        refreshActionButtons();
    }

    if (state.pendingOpen && !state.isVisible && state.pageList.length > 1) {
        state.pendingOpen = false;
        showOverlay();
    }
}

/**
 * 面板分P点击处理
 * @param {Object} payload - 点击数据
 */
function onPanelPageClick(payload) {
    if (!payload) {
        return;
    }

    var targetPage = payload.page;
    if (!targetPage || !state.pageList || state.pageList.length === 0) {
        return;
    }

    var pageItem = null;
    for (var i = 0; i < state.pageList.length; i++) {
        if (state.pageList[i].page === targetPage) {
            pageItem = state.pageList[i];
            break;
        }
    }

    if (!pageItem) {
        return;
    }

    navigateToPage(pageItem.page);
    
    // 隐藏面板
    hideOverlay();
}

/**
 * 面板按钮点击处理
 * @param {Object} payload - 按钮数据
 */
function onPanelActionClick(payload) {
    if (!payload || !payload.action) {
        return;
    }

    if (payload.action === 'prev') {
        goToPreviousPage();
        return;
    }

    if (payload.action === 'next') {
        goToNextPage();
        return;
    }

    if (payload.action === 'danmaku') {
        toggleDanmaku();
        return;
    }

    if (payload.action === 'subtitle') {
        toggleSubtitle();
    }
}

/**
 * 切换弹幕开关（通过 B 站 web/config 接口）
 */
function toggleDanmaku() {
    var targetEnabled = !state.danmakuEnabled;
    var targetText = targetEnabled ? 'true' : 'false';

    var script = "(function(){" +
        "try{" +
            "var csrf='';" +
            "var parts=document.cookie?document.cookie.split(';'):[];" +
            "for(var i=0;i<parts.length;i++){" +
                "var part=(parts[i]||'').trim();" +
                "if(part.indexOf('bili_jct=')===0){csrf=decodeURIComponent(part.substring(9));break;}" +
            "}" +
            "if(!csrf){console.warn('[bilibili-page-list] bili_jct not found');return;}" +
            "var body='dm_switch=" + targetText + "&ts='+Date.now()+'&csrf='+encodeURIComponent(csrf)+'&csrf_token='+encodeURIComponent(csrf);" +
            "fetch('https://api.bilibili.com/x/v2/dm/web/config',{" +
                "method:'POST'," +
                "credentials:'include'," +
                "headers:{'accept':'application/json, text/plain, */*','content-type':'application/x-www-form-urlencoded'}," +
                "body:body" +
            "}).then(function(r){return r.text();}).then(function(t){console.log('[bilibili-page-list] dm_switch response',t);}).catch(function(e){console.error('[bilibili-page-list] dm_switch failed',e);});" +
            "var sels=['.bpx-player-ctrl-dm .bui-switch-input','.bpx-player-dm-switch input','.bpx-player-ctrl-dm-switch input','.bilibili-player-video-danmaku-switch input','.bui-switch-input'];" +
            "for(var j=0;j<sels.length;j++){" +
                "var el=document.querySelector(sels[j]);" +
                "if(!el) continue;" +
                "if(typeof el.checked==='boolean' && el.checked!==" + targetText + "){el.click();break;}" +
                "var aria=el.getAttribute?el.getAttribute('aria-checked'):null;" +
                "if((aria==='true')!==" + targetText + "){if(el.click) el.click();break;}" +
            "}" +
        "}catch(e){console.error('[bilibili-page-list] toggle danmaku script error',e);}" +
    "})();";

    webview.injectScript(script);
    state.danmakuEnabled = targetEnabled;
    refreshActionButtons();

    log.info('弹幕切换: ' + (state.danmakuEnabled ? '开启' : '关闭'));
    if (typeof osd !== 'undefined') {
        osd.show(state.danmakuEnabled ? '弹幕已开启' : '弹幕已关闭', state.danmakuEnabled ? '💬' : '🔇');
    }
}

/**
 * 切换字幕开关（预留）
 */
function toggleSubtitle() {
    var now = Date.now();
    if (now - state.lastSubtitleToggleAt < 900) {
        return;
    }
    state.lastSubtitleToggleAt = now;

    if (state.subtitleEnabled) {
        state.pendingSubtitleEnable = false;
        setBilibiliSubtitleSwitch(false);
        state.subtitleEnabled = false;
        refreshActionButtons();
        log.info('字幕切换: 关闭');
        if (typeof osd !== 'undefined') {
            osd.show('字幕已关闭', '📝');
        }
        return;
    }

    var hasSubtitles = false;
    try {
        hasSubtitles = !!(typeof subtitle !== 'undefined' && subtitle.hasSubtitles);
    } catch (e) {
        hasSubtitles = false;
    }

    if (!hasSubtitles || !state.subtitleReady) {
        if (typeof subtitle !== 'undefined' && subtitle && typeof subtitle.request === 'function') {
            state.pendingSubtitleEnable = true;
            subtitle.request();
            log.info('字幕切换: 请求加载字幕中');
            if (typeof osd !== 'undefined') {
                osd.show('正在加载字幕...', '📝');
            }
            return;
        }

        state.subtitleEnabled = false;
        refreshActionButtons();
        log.info('字幕切换: 无可用字幕');
        if (typeof osd !== 'undefined') {
            osd.show('没有可用字幕', '⚠️');
        }
        return;
    }

    state.pendingSubtitleEnable = false;
    enableSubtitleByDomPriority();
}

function setBilibiliSubtitleSwitch(enabled) {
    if (enabled) {
        return;
    }

    webview.injectScript("(function(){try{" +
        "var closeSwitch=document.querySelector('.bpx-player-ctrl-subtitle-close-switch[data-action=close]');" +
        "if(closeSwitch&&closeSwitch.classList&&!closeSwitch.classList.contains('bpx-state-active')&&closeSwitch.click){closeSwitch.click();}" +
    "}catch(e){console.error('[bilibili-page-list] set subtitle switch failed',e);}})();");
}

function enableSubtitleByDomPriority() {
    var preferredLanguage = getPreferredSubtitleLanguageFromDom();
    if (!preferredLanguage) {
        state.subtitleEnabled = false;
        refreshActionButtons();
        log.info('字幕切换: 页面中未找到中文字幕选项');
        if (typeof osd !== 'undefined') {
            osd.show('没有可用字幕', '⚠️');
        }
        return;
    }

    webview.injectScript("(function(){try{" +
        "var target=document.querySelector('.bpx-player-ctrl-subtitle-major-inner .bpx-player-ctrl-subtitle-language-item[data-lan=" + preferredLanguage + "],.bpx-player-ctrl-subtitle-language-item[data-lan=" + preferredLanguage + "]');" +
        "if(target&&target.click){target.click();}" +
    "}catch(e){console.error('[bilibili-page-list] subtitle on failed',e);}})();");

    sleep(150);
    syncSubtitleStateFromPage();

    if (!state.subtitleLanguage) {
        state.subtitleLanguage = preferredLanguage;
    }

    var language = (state.subtitleLanguage || '').toLowerCase();

    if (language === 'ai-zh') {
        state.subtitleEnabled = true;
        refreshActionButtons();
        log.info('字幕切换: 开启（中文AI字幕）');
        if (typeof osd !== 'undefined') {
            osd.show('已启用中文AI字幕', '📝');
        }
        return;
    }

    if (language === 'zh-cn' || language.indexOf('zh') === 0) {
        state.subtitleEnabled = true;
        refreshActionButtons();
        log.info('字幕切换: 开启（中文字幕）');
        if (typeof osd !== 'undefined') {
            osd.show('已启用中文字幕', '📝');
        }
        return;
    }

    state.subtitleEnabled = false;
    refreshActionButtons();
    log.info('字幕切换: 字幕存在但非中文，视为不可用 language=' + state.subtitleLanguage);
    if (typeof osd !== 'undefined') {
        osd.show('没有可用字幕', '⚠️');
    }
}

/**
 * 字幕加载事件
 * @param {Object} data
 */
function onSubtitleLoaded(data) {
    state.subtitleLanguage = data && data.language ? String(data.language) : '';
    state.subtitleReady = !!(data && data.body && data.body.length > 0);
    log.info('字幕已加载: language=' + state.subtitleLanguage + ', ready=' + state.subtitleReady);

    if (state.pendingSubtitleEnable) {
        state.pendingSubtitleEnable = false;
        enableSubtitleByDomPriority();
    }
}

/**
 * 字幕清空事件
 */
function onSubtitleCleared() {
    state.subtitleLanguage = '';
    state.subtitleReady = false;
    state.pendingSubtitleEnable = false;
    state.subtitleEnabled = false;
    refreshActionButtons();
    log.info('字幕已清空');
}

/**
 * 面板可见性变化处理
 * @param {Object} payload - 可见性状态
 */
function onPanelHide(payload) {
    log.info('面板已隐藏，更新可见状态');
    state.isVisible = false;
    saveOverlayPosition();
}

// ============================================================================
// 插件生命周期
// ============================================================================

/**
 * 插件加载
 */
function onLoad() {
    log.info(plugin.name + ' v' + plugin.version + ' 已加载');
    
    // 加载配置
    var toggleHotkey = config.get('toggleHotkey', 'Alt+P');
    var danmakuHotkey = config.get('danmakuHotkey', 'Alt+D');
    var subtitleHotkey = config.get('subtitleHotkey', 'Alt+S');
    var prevPageHotkey = config.get('prevPageHotkey', 'Alt+Left');
    var nextPageHotkey = config.get('nextPageHotkey', 'Alt+Right');
    if (subtitleHotkey === 'Alt+Shift+S') {
        subtitleHotkey = 'Alt+S';
        config.set('subtitleHotkey', subtitleHotkey);
        log.info('已将历史字幕快捷键迁移为: ' + subtitleHotkey);
    }
    
    // 注册快捷键
    hotkey.register(toggleHotkey, toggleVisibility);
    log.info('已注册切换快捷键: ' + toggleHotkey);
    
    // 注册弹幕快捷键
    hotkey.register(danmakuHotkey, function() {
        toggleDanmaku();
    });

    // 注册字幕快捷键
    hotkey.register(subtitleHotkey, function() {
        toggleSubtitle();
    });
    
    // 注册分P导航快捷键
    hotkey.register(prevPageHotkey, function() {
        goToPreviousPage();
    });
    log.info('已注册上一个分P快捷键: ' + prevPageHotkey);
    
    hotkey.register(nextPageHotkey, function() {
        goToNextPage();
    });
    log.info('已注册下一个分P快捷键: ' + nextPageHotkey);
    
    // 监听URL变化
    player.on('urlChanged', onUrlChanged);

    // 监听字幕状态（用于“字”按钮优先级判断）
    if (typeof subtitle !== 'undefined' && subtitle && typeof subtitle.on === 'function') {
        subtitle.on('load', onSubtitleLoaded);
        subtitle.on('clear', onSubtitleCleared);
    }
    
    // 监听面板交互
    panel.on('pageClick', onPanelPageClick);
    panel.on('actionClick', onPanelActionClick);
    panel.on('hide', onPanelHide);
    
    // 监听面板移动和尺寸变化（保存位置）
    panel.on('move', function() {
        saveOverlayPosition();
    });
    panel.on('resize', function() {
        saveOverlayPosition();
    });
    
    // 检查当前URL
    var currentUrl = player.getUrl();
    log.info('当前URL: ' + currentUrl);
    if (currentUrl) {
        log.info('调用 onUrlChanged 处理当前URL');
        onUrlChanged(currentUrl);
    } else {
        log.info('当前URL为空，等待URL变化事件');
    }
    
    log.info('配置已加载: 窗口位置=' + JSON.stringify({
        x: config.get('panel.x', config.get('overlay.x', 100)),
        y: config.get('panel.y', config.get('overlay.y', 100))
    }));
}

/**
 * 插件卸载
 */
function onUnload() {
    log.info(plugin.name + ' 正在卸载...');
    
    // 保存窗口位置
    if (state.isVisible) {
        saveOverlayPosition();
    }
    
    // 隐藏面板
    hideOverlay();
    
    // 取消事件监听
    player.off('urlChanged');
    if (typeof subtitle !== 'undefined' && subtitle && typeof subtitle.off === 'function') {
        subtitle.off('load');
        subtitle.off('clear');
    }
    panel.off('pageClick');
    panel.off('actionClick');
    panel.off('hide');
    panel.off('move');
    panel.off('resize');
    
    // 注销快捷键
    hotkey.unregisterAll();
    
    log.info(plugin.name + ' 已卸载');
}

// 导出停止函数
var stop = onUnload;
