/**
 * FloatWebPlayer - WebView2 注入脚本
 * 
 * 此文件作为嵌入资源，通过 AddScriptToExecuteOnDocumentCreatedAsync 注入。
 * 样式由 InjectedStyles.css 提供，本文件仅负责 DOM 操作和事件处理。
 */
(function () {
    'use strict';

    console.log('[SandronePlayer] Script loaded');

    // 防止重复注入
    if (window.__sandronePlayerInjected) {
        console.log('[SandronePlayer] Already injected, skipping');
        return;
    }
    window.__sandronePlayerInjected = true;
    console.log('[SandronePlayer] Starting injection');

    // ========================================
    // DOM 元素创建
    // ========================================

    /**
     * 创建拖动区域
     */
    function createDragZone() {
        // 防止重复创建
        if (document.getElementById('float-player-drag-zone')) return;

        const dragZone = document.createElement('div');
        dragZone.id = 'float-player-drag-zone';

        dragZone.addEventListener('mousedown', function (e) {
            e.preventDefault();
            e.stopPropagation();
            window.chrome.webview.postMessage('drag');
        });

        document.body.appendChild(dragZone);
    }

    /**
     * 创建控制按钮
     */
    function createControlButtons() {
        // 防止重复创建
        if (document.getElementById('float-player-controls')) return null;

        const controls = document.createElement('div');
        controls.id = 'float-player-controls';

        // 最小化按钮
        const minimizeBtn = document.createElement('button');
        minimizeBtn.title = '最小化';
        minimizeBtn.textContent = '−';
        minimizeBtn.addEventListener('click', () => {
            window.chrome.webview.postMessage('minimize');
        });

        // 最大化/还原按钮
        const maximizeBtn = document.createElement('button');
        maximizeBtn.title = '最大化/还原';
        maximizeBtn.textContent = '□';
        maximizeBtn.addEventListener('click', () => {
            window.chrome.webview.postMessage('maximize');
        });

        // 关闭按钮
        const closeBtn = document.createElement('button');
        closeBtn.className = 'close';
        closeBtn.title = '关闭';
        closeBtn.textContent = '×';
        closeBtn.addEventListener('click', () => {
            window.chrome.webview.postMessage('close');
        });

        controls.appendChild(minimizeBtn);
        controls.appendChild(maximizeBtn);
        controls.appendChild(closeBtn);

        document.body.appendChild(controls);
        return controls;
    }

    // ========================================
    // 事件处理
    // ========================================

    /**
     * 设置控制按钮的显示/隐藏逻辑
     */
    function setupVisibilityHandlers(controls) {
        if (!controls) return;

        // 鼠标进入文档时显示按钮
        document.addEventListener('mouseenter', function () {
            controls.classList.add('visible');
        });

        // 鼠标离开文档时隐藏按钮
        document.addEventListener('mouseleave', function () {
            controls.classList.remove('visible');
        });

        // 鼠标进入控制按钮区域时保持显示
        controls.addEventListener('mouseenter', function () {
            controls.classList.add('visible');
        });
    }

    /**
     * 处理全屏状态变化
     * 当元素进入全屏时，将控制按钮移动到全屏元素内部
     */
    function handleFullscreenChange() {
        const controls = document.getElementById('float-player-controls');
        const dragZone = document.getElementById('float-player-drag-zone');
        
        if (!controls || !dragZone) return;

        // 获取当前全屏元素（兼容不同浏览器）
        const fullscreenElement = document.fullscreenElement || 
                                  document.webkitFullscreenElement || 
                                  document.msFullscreenElement;

        if (fullscreenElement) {
            // 将控制元素移动到全屏元素内
            console.log('[SandronePlayer] Moving controls to fullscreen element');
            fullscreenElement.appendChild(dragZone);
            fullscreenElement.appendChild(controls);
        } else {
            // 退出全屏，移回 body
            console.log('[SandronePlayer] Moving controls back to body');
            document.body.appendChild(dragZone);
            document.body.appendChild(controls);
        }
    }

    /**
     * 设置全屏状态监听
     */
    function setupFullscreenHandler() {
        // 监听全屏状态变化（兼容不同浏览器）
        document.addEventListener('fullscreenchange', handleFullscreenChange);
        document.addEventListener('webkitfullscreenchange', handleFullscreenChange);
        document.addEventListener('msfullscreenchange', handleFullscreenChange);
    }

    // ========================================
    // Playback State Manager
    // ========================================

    const PlaybackStateManager = (function () {
        const STORAGE_KEY_PREFIX = 'akasha:playback-state:v1';
        const PENDING_STORAGE_KEY_PREFIX = 'akasha:playback-pending:v1';
        const GLOBAL_RATE_KEY_PREFIX = 'akasha:playback-global-rate:v1';
        const STATE_TTL_MS = 2 * 60 * 60 * 1000;
        const RESTORE_MAX_ATTEMPTS = 20;
        const RESTORE_INTERVAL_MS = 220;
        const RESTORE_REASON_THROTTLE_MS = {
            'video-canplay': 700,
            'video-playing': 700
        };

        // Keep this list short and explicit.
        // Add more entries later when we want to enable state restore for other sites.
        const DOMAIN_RULES = [
            { hostSuffix: 'bilibili.com', pathContains: '/video/' }
        ];

        const WEB_FULLSCREEN_SELECTORS = [
            '.bpx-player-ctrl-web',
            '.bilibili-player-video-btn-web-fullscreen',
            '.bpx-player-ctrl-btn[data-action="web"]',
            '[title*="网页全屏"]',
            '[aria-label*="网页全屏"]'
        ];

        const DOCUMENT_FULLSCREEN_SELECTORS = [
            '.bpx-player-ctrl-full',
            '.bilibili-player-video-btn-fullscreen',
            '.bpx-player-ctrl-btn[data-action="full"]',
            '[title*="全屏"]:not([title*="网页"])',
            '[aria-label*="全屏"]:not([aria-label*="网页"])'
        ];

        let restoreTimer = null;
        let restoreAttempts = 0;
        let waitingUserGesture = false;
        let videoBindingObserver = null;
        let pendingFullscreenSnapshot = null;
        let statePollTimer = null;
        let lastObservedMode = null;
        let lastObservedRate = null;
        let activeRestoreTarget = null;

        function getRestoreReasonThrottleMs(reason) {
            if (!reason) {
                return 0;
            }

            return Number(RESTORE_REASON_THROTTLE_MS[reason]) || 0;
        }

        function isSameRestoreTarget(left, right) {
            if (!left || !right) {
                return false;
            }

            const leftMode = String(left.fullscreenMode || 'none');
            const rightMode = String(right.fullscreenMode || 'none');
            const leftRate = Number(left.playbackRate) || 1.0;
            const rightRate = Number(right.playbackRate) || 1.0;

            return leftMode === rightMode && approxEqual(leftRate, rightRate);
        }

        function postPlaybackDebug(message, data) {
            try {
                if (!window.chrome || !window.chrome.webview || typeof window.chrome.webview.postMessage !== 'function') {
                    return;
                }

                window.chrome.webview.postMessage(JSON.stringify({
                    type: 'playback_state_debug',
                    message: String(message || ''),
                    data: data || null,
                    url: window.location.href,
                    timestamp: Date.now()
                }));
            } catch (_) {
                // Ignore debug message failures.
            }
        }

        function getStorageKey() {
            return STORAGE_KEY_PREFIX + ':' + window.location.hostname;
        }

        function getPendingStorageKey() {
            return PENDING_STORAGE_KEY_PREFIX + ':' + window.location.hostname;
        }

        function getGlobalRateKey() {
            return GLOBAL_RATE_KEY_PREFIX + ':' + window.location.hostname;
        }

        function readGlobalRateSnapshot() {
            const key = getGlobalRateKey();

            try {
                const raw = localStorage.getItem(key);
                if (!raw) {
                    return null;
                }

                const parsed = JSON.parse(raw);
                if (!parsed || typeof parsed !== 'object') {
                    return null;
                }

                const rate = Number(parsed.playbackRate) || 1.0;
                const updatedAt = Number(parsed.updatedAt) || 0;
                if (rate <= 0 || !updatedAt || Date.now() - updatedAt > STATE_TTL_MS) {
                    return null;
                }

                return {
                    playbackRate: rate,
                    updatedAt: updatedAt
                };
            } catch (_) {
                return null;
            }
        }

        function readGlobalRate() {
            const snapshot = readGlobalRateSnapshot();
            return snapshot ? snapshot.playbackRate : 1.0;
        }

        function shouldSkipGlobalRateOverwrite(targetRate, reason) {
            const previousRate = readGlobalRate();
            const isDowngradeToDefault = approxEqual(targetRate, 1.0) && previousRate > 1.0;
            if (!isDowngradeToDefault) {
                return false;
            }

            const transientReasons = {
                'pushState': true,
                'replaceState': true,
                'beforeunload': true,
                'pagehide': true,
                'video-loadedmetadata': true,
                'video-playing': true,
                'mode-poll': true,
                'rate-poll': true
            };

            return !!transientReasons[String(reason || '')];
        }

        function saveGlobalRate(rate, reason) {
            const normalizedRate = Number(rate) || 1.0;
            if (normalizedRate <= 0) {
                return;
            }

            if (shouldSkipGlobalRateOverwrite(normalizedRate, reason)) {
                postPlaybackDebug('skipGlobalRateOverwrite', {
                    reason: reason,
                    targetRate: normalizedRate,
                    currentGlobalRate: readGlobalRate()
                });
                return;
            }

            const payload = {
                playbackRate: normalizedRate,
                updatedAt: Date.now()
            };

            try {
                localStorage.setItem(getGlobalRateKey(), JSON.stringify(payload));
            } catch (_) {
                // Ignore quota/storage failures.
            }
        }

        function isRuleMatched(rule) {
            const host = window.location.hostname || '';
            const path = window.location.pathname || '';

            const hostMatched = !rule.hostSuffix || host === rule.hostSuffix || host.endsWith('.' + rule.hostSuffix);
            if (!hostMatched) {
                return false;
            }

            if (!rule.pathContains) {
                return true;
            }

            return path.indexOf(rule.pathContains) >= 0;
        }

        function isTargetSite() {
            for (let i = 0; i < DOMAIN_RULES.length; i++) {
                if (isRuleMatched(DOMAIN_RULES[i])) {
                    return true;
                }
            }
            return false;
        }

        function getVideoElement() {
            return document.querySelector('video');
        }

        function isDocumentFullscreen() {
            return !!(document.fullscreenElement || document.webkitFullscreenElement || document.msFullscreenElement);
        }

        function getPlayerFullButton() {
            return document.querySelector(DOCUMENT_FULLSCREEN_SELECTORS.join(', '));
        }

        function getPlayerContainer() {
            return document.querySelector('.bpx-player-container, .bpx-player-container-wrap, .bilibili-player, #bilibili-player, .bpx-player-video-wrap');
        }

        function clickElementRobust(element) {
            if (!element) {
                return false;
            }

            try {
                if (typeof element.focus === 'function') {
                    element.focus();
                }

                const eventOptions = { bubbles: true, cancelable: true, view: window };
                element.dispatchEvent(new MouseEvent('mousedown', eventOptions));
                element.dispatchEvent(new MouseEvent('mouseup', eventOptions));
                element.dispatchEvent(new MouseEvent('click', eventOptions));

                if (typeof element.click === 'function') {
                    element.click();
                }

                return true;
            } catch (_) {
                return false;
            }
        }

        function createLegacyKeyEvent(type, key, code, keyCode) {
            const evt = new KeyboardEvent(type, {
                key: key,
                code: code,
                bubbles: true,
                cancelable: true,
                composed: true
            });

            try {
                Object.defineProperty(evt, 'keyCode', { get: function () { return keyCode; } });
                Object.defineProperty(evt, 'which', { get: function () { return keyCode; } });
            } catch (_) {
                // Ignore if browser disallows redefinition.
            }

            return evt;
        }

        function dispatchPlayerShortcut(key, code, keyCode) {
            try {
                const video = getVideoElement();
                const target = document.body || getPlayerContainer() || document.documentElement || document;
                if (!target) {
                    return false;
                }

                if (video) {
                    video.dispatchEvent(new MouseEvent('mousemove', {
                        clientX: 0,
                        clientY: 0,
                        bubbles: true,
                        cancelable: true,
                        view: window
                    }));
                }

                if (typeof target.focus === 'function') {
                    target.focus();
                }

                target.dispatchEvent(createLegacyKeyEvent('keydown', key, code, keyCode));
                target.dispatchEvent(createLegacyKeyEvent('keyup', key, code, keyCode));
                document.dispatchEvent(createLegacyKeyEvent('keydown', key, code, keyCode));
                document.dispatchEvent(createLegacyKeyEvent('keyup', key, code, keyCode));
                return true;
            } catch (_) {
                return false;
            }
        }

        function isPlayerFullscreen() {
            const button = getPlayerFullButton();
            if (!button) {
                const container = getPlayerContainer();
                if (!container) {
                    return false;
                }

                const containerClass = String(container.className || '').toLowerCase();
                if (containerClass.indexOf('fullscreen') >= 0 && containerClass.indexOf('webfullscreen') < 0) {
                    return true;
                }

                if (containerClass.indexOf('mode-full') >= 0) {
                    return true;
                }

                return false;
            }

            const label = String(button.getAttribute('aria-label') || button.getAttribute('title') || '').toLowerCase();
            if (label.indexOf('退出全屏') >= 0 || label.indexOf('exit fullscreen') >= 0) {
                return true;
            }

            const className = String(button.className || '').toLowerCase();
            if (className.indexOf('active') >= 0 || className.indexOf('entered') >= 0) {
                return true;
            }

            const container = getPlayerContainer();
            if (container) {
                const containerClass = String(container.className || '').toLowerCase();
                if (containerClass.indexOf('fullscreen') >= 0 && containerClass.indexOf('webfullscreen') < 0) {
                    return true;
                }
                if (containerClass.indexOf('mode-full') >= 0) {
                    return true;
                }
            }

            return false;
        }

        function hasActiveIndicator(element) {
            if (!element) {
                return false;
            }

            const className = String(element.className || '').toLowerCase();
            if (className.indexOf('active') >= 0 || className.indexOf('entered') >= 0) {
                return true;
            }

            const ariaChecked = String(element.getAttribute('aria-checked') || '').toLowerCase();
            if (ariaChecked === 'true') {
                return true;
            }

            const title = String(element.getAttribute('title') || element.getAttribute('aria-label') || '').toLowerCase();
            return title.indexOf('exit') >= 0 || title.indexOf('quit') >= 0 || title.indexOf('退出') >= 0;
        }

        function isWebFullscreen() {
            const htmlClass = String(document.documentElement.className || '').toLowerCase();
            const bodyClass = String(document.body ? document.body.className : '').toLowerCase();
            if (htmlClass.indexOf('webfullscreen') >= 0 || bodyClass.indexOf('webfullscreen') >= 0) {
                return true;
            }

            const container = document.querySelector('.bpx-player-container, .bilibili-player, #bilibili-player');
            if (container) {
                const cls = String(container.className || '').toLowerCase();
                if ((cls.indexOf('web') >= 0 && cls.indexOf('fullscreen') >= 0) || cls.indexOf('mode-web') >= 0) {
                    return true;
                }
            }

            const webButton = document.querySelector('.bpx-player-ctrl-web, .bilibili-player-video-btn-web-fullscreen');
            return hasActiveIndicator(webButton);
        }

        function getFullscreenMode() {
            if (isPlayerFullscreen() || isDocumentFullscreen()) {
                return 'full';
            }
            if (isWebFullscreen()) {
                return 'web';
            }
            return 'none';
        }

        function pickStablePlaybackRate(currentRate, fallbackSnapshot) {
            const rate = Number(currentRate) || 1.0;
            if (!fallbackSnapshot || typeof fallbackSnapshot !== 'object') {
                return rate;
            }

            const fallbackRate = Number(fallbackSnapshot.playbackRate) || 1.0;
            const fallbackUpdatedAt = Number(fallbackSnapshot.updatedAt) || 0;
            const fallbackIsFresh = fallbackUpdatedAt > 0 && (Date.now() - fallbackUpdatedAt) <= 15000;

            // Some sites briefly reset the next-part video element to 1.0x during route switch.
            // If we just had a fresh non-1.0 rate, keep that value for the handoff snapshot.
            if (fallbackIsFresh && approxEqual(rate, 1.0) && fallbackRate > 1.0) {
                return fallbackRate;
            }

            return rate;
        }

        function buildSnapshot(modeOverride, fallbackSnapshot) {
            const video = getVideoElement();
            const rawPlaybackRate = video && typeof video.playbackRate === 'number' ? video.playbackRate : 1.0;
            let playbackRate = pickStablePlaybackRate(rawPlaybackRate, fallbackSnapshot);
            const globalRate = readGlobalRate();
            if (approxEqual(playbackRate, 1.0) && globalRate > 0) {
                playbackRate = globalRate;
            }
            const mode = modeOverride || getFullscreenMode();

            return {
                playbackRate: Number(playbackRate) || 1.0,
                fullscreenMode: mode,
                href: window.location.href,
                updatedAt: Date.now()
            };
        }

        function saveSnapshot(reason, modeOverride, fallbackSnapshot) {
            if (!isTargetSite()) {
                return;
            }

            try {
                const snapshot = buildSnapshot(modeOverride, fallbackSnapshot);
                sessionStorage.setItem(getStorageKey(), JSON.stringify(snapshot));
                saveGlobalRate(snapshot.playbackRate, reason);
                console.log('[SandronePlayer] Playback state saved:', reason, snapshot);
            } catch (err) {
                console.warn('[SandronePlayer] Failed to save playback state:', err);
            }
        }

        function savePendingSnapshot(reason, modeOverride, fallbackSnapshot) {
            if (!isTargetSite()) {
                return;
            }

            try {
                const snapshot = buildSnapshot(modeOverride, fallbackSnapshot);
                sessionStorage.setItem(getPendingStorageKey(), JSON.stringify(snapshot));
                postPlaybackDebug('savePendingSnapshot', {
                    reason: reason,
                    mode: snapshot.fullscreenMode,
                    rate: snapshot.playbackRate
                });
            } catch (err) {
                console.warn('[SandronePlayer] Failed to save pending playback state:', err);
            }
        }

        function readSnapshot() {
            try {
                const raw = sessionStorage.getItem(getStorageKey());
                if (!raw) {
                    return null;
                }

                const parsed = JSON.parse(raw);
                if (!parsed || typeof parsed !== 'object') {
                    return null;
                }

                const updatedAt = Number(parsed.updatedAt) || 0;
                if (!updatedAt || Date.now() - updatedAt > STATE_TTL_MS) {
                    return null;
                }

                return parsed;
            } catch (err) {
                console.warn('[SandronePlayer] Failed to read playback state:', err);
                return null;
            }
        }

        function readPendingSnapshot() {
            try {
                const raw = sessionStorage.getItem(getPendingStorageKey());
                if (!raw) {
                    return null;
                }

                const parsed = JSON.parse(raw);
                if (!parsed || typeof parsed !== 'object') {
                    return null;
                }

                const updatedAt = Number(parsed.updatedAt) || 0;
                if (!updatedAt || Date.now() - updatedAt > STATE_TTL_MS) {
                    return null;
                }

                return parsed;
            } catch (err) {
                console.warn('[SandronePlayer] Failed to read pending playback state:', err);
                return null;
            }
        }

        function clearPendingSnapshot() {
            try {
                sessionStorage.removeItem(getPendingStorageKey());
            } catch (_) {
                // Ignore cleanup failures.
            }
        }

        function approxEqual(a, b) {
            return Math.abs(Number(a) - Number(b)) < 0.02;
        }

        function applyPlaybackRate(snapshot) {
            const video = getVideoElement();
            if (!video) {
                return false;
            }

            const targetRate = Number(snapshot.playbackRate) || 1.0;
            if (targetRate <= 0) {
                return true;
            }

            if (!approxEqual(video.playbackRate, targetRate)) {
                video.playbackRate = targetRate;
            }

            return approxEqual(video.playbackRate, targetRate);
        }

        function bindVideoEvents() {
            const video = getVideoElement();
            if (!video || video.__akashaPlaybackStateBound) {
                return;
            }

            video.__akashaPlaybackStateBound = true;

            video.addEventListener('ratechange', function () {
                saveSnapshot('video-ratechange');
            });

            video.addEventListener('loadedmetadata', function () {
                saveSnapshot('video-loadedmetadata');
                scheduleRestore('video-loadedmetadata');
            });

            video.addEventListener('canplay', function () {
                scheduleRestore('video-canplay');
            });

            video.addEventListener('playing', function () {
                saveSnapshot('video-playing');
                scheduleRestore('video-playing');
            });
        }

        function ensureVideoBindingObserver() {
            if (videoBindingObserver) {
                return;
            }

            bindVideoEvents();

            const root = document.documentElement || document.body;
            if (!root || typeof MutationObserver !== 'function') {
                return;
            }

            videoBindingObserver = new MutationObserver(function () {
                bindVideoEvents();
            });

            videoBindingObserver.observe(root, { childList: true, subtree: true });
        }

        function clickIfPresent(selector) {
            const element = document.querySelector(selector);
            if (!element) {
                return false;
            }
            return clickElementRobust(element);
        }

        function ensureWebFullscreen() {
            if (isWebFullscreen() || isDocumentFullscreen()) {
                return true;
            }

            const clicked = clickIfPresent(WEB_FULLSCREEN_SELECTORS.join(', '));
            return clicked;
        }

        function ensureDocumentFullscreen() {
            if (isPlayerFullscreen() || isDocumentFullscreen()) {
                return true;
            }

            // Important: only trigger Bilibili player's own fullscreen button.
            // Do NOT fallback to native requestFullscreen(), otherwise the page enters browser-default fullscreen UI.
            const clicked = clickIfPresent(DOCUMENT_FULLSCREEN_SELECTORS.join(', '));
            postPlaybackDebug('ensureDocumentFullscreen', {
                buttonFound: !!getPlayerFullButton(),
                clickedButton: clicked,
                buttonLabel: (function () {
                    const btn = getPlayerFullButton();
                    if (!btn) {
                        return '';
                    }
                    return String(btn.getAttribute('aria-label') || btn.getAttribute('title') || '');
                })()
            });

            if (clicked) {
                return true;
            }

            // Fallback: trigger Bilibili's own shortcut path.
            dispatchPlayerShortcut('f', 'KeyF', 70);
            postPlaybackDebug('ensureDocumentFullscreen-fallback-key', { key: 'F' });
            return false;
        }

        function isModeSatisfied(mode) {
            if (mode === 'none') {
                return true;
            }
            if (mode === 'web') {
                return isWebFullscreen() || isDocumentFullscreen();
            }
            if (mode === 'full') {
                return isPlayerFullscreen() || isDocumentFullscreen();
            }
            return true;
        }

        function startStatePoller() {
            if (statePollTimer) {
                return;
            }

            statePollTimer = setInterval(function () {
                if (!isTargetSite()) {
                    return;
                }

                const video = getVideoElement();
                if (!video) {
                    return;
                }

                const mode = getFullscreenMode();
                if (mode !== lastObservedMode) {
                    lastObservedMode = mode;
                    saveSnapshot('mode-poll', mode);
                }

                const rate = Number(video.playbackRate) || 1.0;
                if (lastObservedRate === null || !approxEqual(rate, lastObservedRate)) {
                    lastObservedRate = rate;
                    saveSnapshot('rate-poll');
                }
            }, 500);
        }

        function applyFullscreenMode(snapshot) {
            const mode = String(snapshot.fullscreenMode || 'none');
            if (mode === 'none') {
                return true;
            }

            if (isModeSatisfied(mode)) {
                return true;
            }

            if (mode === 'web') {
                ensureWebFullscreen();
            } else if (mode === 'full') {
                ensureDocumentFullscreen();
            }

            return isModeSatisfied(mode);
        }

        function clearRestoreTimer() {
            if (restoreTimer) {
                clearInterval(restoreTimer);
                restoreTimer = null;
            }
        }

        function registerUserGestureRetry() {
            if (waitingUserGesture || !pendingFullscreenSnapshot) {
                return;
            }

            waitingUserGesture = true;
            const onceHandler = function () {
                waitingUserGesture = false;
                document.removeEventListener('click', onceHandler, true);
                document.removeEventListener('keydown', onceHandler, true);

                const snapshot = pendingFullscreenSnapshot;
                if (snapshot) {
                    applyPlaybackRate(snapshot);
                    applyFullscreenMode(snapshot);
                }
            };

            document.addEventListener('click', onceHandler, true);
            document.addEventListener('keydown', onceHandler, true);
        }

        function scheduleRestore(reason) {
            if (!isTargetSite()) {
                clearRestoreTimer();
                return;
            }

            const reasonText = String(reason || 'unknown');
            const reasonThrottleMs = getRestoreReasonThrottleMs(reasonText);
            const nowMs = Date.now();
            if (reasonThrottleMs > 0) {
                if (!scheduleRestore._lastReasonTimestamps) {
                    scheduleRestore._lastReasonTimestamps = Object.create(null);
                }

                const lastReasonTs = Number(scheduleRestore._lastReasonTimestamps[reasonText]) || 0;
                if (lastReasonTs > 0 && (nowMs - lastReasonTs) < reasonThrottleMs) {
                    postPlaybackDebug('scheduleRestore-throttled', {
                        reason: reasonText,
                        elapsedMs: nowMs - lastReasonTs,
                        throttleMs: reasonThrottleMs
                    });
                    return;
                }

                scheduleRestore._lastReasonTimestamps[reasonText] = nowMs;
            }

            let snapshot = activeRestoreTarget || readPendingSnapshot();
            let fallbackToLatestRate = false;
            let applyGlobalRate = false;
            if (!snapshot) {
                const latestSnapshot = readSnapshot();
                if (latestSnapshot) {
                    snapshot = {
                        playbackRate: latestSnapshot.playbackRate,
                        fullscreenMode: 'none',
                        href: latestSnapshot.href,
                        updatedAt: latestSnapshot.updatedAt
                    };
                    fallbackToLatestRate = true;
                }
            }

            const globalRate = readGlobalRate();
            if (snapshot && globalRate > 0 && !approxEqual(snapshot.playbackRate, globalRate)) {
                snapshot = {
                    playbackRate: globalRate,
                    fullscreenMode: snapshot.fullscreenMode,
                    href: snapshot.href,
                    updatedAt: snapshot.updatedAt
                };
                applyGlobalRate = true;
            }

            if (!snapshot) {
                postPlaybackDebug('scheduleRestore-skip-no-snapshot', { reason: reason });
                return;
            }

            if (restoreTimer && activeRestoreTarget && isSameRestoreTarget(activeRestoreTarget, snapshot)) {
                postPlaybackDebug('scheduleRestore-reuse-active', {
                    reason: reasonText,
                    attempts: restoreAttempts,
                    mode: activeRestoreTarget.fullscreenMode,
                    rate: activeRestoreTarget.playbackRate
                });
                return;
            }

            if (!activeRestoreTarget) {
                activeRestoreTarget = snapshot;
                clearPendingSnapshot();
                postPlaybackDebug('scheduleRestore-acquire-pending', {
                    reason: reason,
                    targetMode: snapshot.fullscreenMode,
                    targetRate: snapshot.playbackRate
                });
            }
            else if (!isSameRestoreTarget(activeRestoreTarget, snapshot)) {
                activeRestoreTarget = snapshot;
                postPlaybackDebug('scheduleRestore-replace-target', {
                    reason: reasonText,
                    targetMode: snapshot.fullscreenMode,
                    targetRate: snapshot.playbackRate
                });
            }

            postPlaybackDebug('scheduleRestore-start', {
                reason: reason,
                fallbackToLatestRate: fallbackToLatestRate,
                applyGlobalRate: applyGlobalRate,
                targetMode: snapshot.fullscreenMode,
                targetRate: snapshot.playbackRate
            });

            pendingFullscreenSnapshot = String(snapshot.fullscreenMode || 'none') === 'full' ? snapshot : null;
            if (pendingFullscreenSnapshot) {
                registerUserGestureRetry();
            }

            clearRestoreTimer();
            restoreAttempts = 0;

            restoreTimer = setInterval(function () {
                restoreAttempts += 1;

                bindVideoEvents();

                const rateRestored = applyPlaybackRate(snapshot);
                const fullscreenRestored = applyFullscreenMode(snapshot);

                if (rateRestored && fullscreenRestored) {
                    clearRestoreTimer();
                    console.log('[SandronePlayer] Playback state restored:', reason, snapshot);
                    activeRestoreTarget = null;
                    pendingFullscreenSnapshot = null;
                    postPlaybackDebug('scheduleRestore-success', {
                        reason: reason,
                        attempts: restoreAttempts,
                        mode: snapshot.fullscreenMode,
                        rate: snapshot.playbackRate
                    });
                    return;
                }

                if (restoreAttempts === 1 || restoreAttempts % 5 === 0) {
                    postPlaybackDebug('scheduleRestore-progress', {
                        reason: reason,
                        attempts: restoreAttempts,
                        rateRestored: rateRestored,
                        fullscreenRestored: fullscreenRestored,
                        currentMode: getFullscreenMode(),
                        currentRate: (function () {
                            const video = getVideoElement();
                            return video ? Number(video.playbackRate) : null;
                        })()
                    });
                }

                if (restoreAttempts >= RESTORE_MAX_ATTEMPTS) {
                    clearRestoreTimer();
                    activeRestoreTarget = null;
                    pendingFullscreenSnapshot = null;
                    postPlaybackDebug('scheduleRestore-timeout', {
                        reason: reason,
                        targetMode: snapshot.fullscreenMode,
                        targetRate: snapshot.playbackRate,
                        currentMode: getFullscreenMode(),
                        currentRate: (function () {
                            const video = getVideoElement();
                            return video ? Number(video.playbackRate) : null;
                        })()
                    });

                    if (snapshot.fullscreenMode === 'full' && !isDocumentFullscreen()) {
                        registerUserGestureRetry();
                    }
                }
            }, RESTORE_INTERVAL_MS);
        }

        function patchHistoryNavigation() {
            try {
                const originalPushState = history.pushState;
                history.pushState = function () {
                    const previousSnapshot = readSnapshot();
                    const result = originalPushState.apply(this, arguments);
                    saveSnapshot('pushState', null, previousSnapshot);
                    savePendingSnapshot('pushState', null, previousSnapshot);
                    return result;
                };

                const originalReplaceState = history.replaceState;
                history.replaceState = function () {
                    const result = originalReplaceState.apply(this, arguments);
                    saveSnapshot('replaceState');
                    return result;
                };

                window.addEventListener('popstate', function () {
                    savePendingSnapshot('popstate');
                });
            } catch (err) {
                console.warn('[SandronePlayer] Failed to patch history API:', err);
            }
        }

        function setupTrackingEvents() {
            ensureVideoBindingObserver();

            document.addEventListener('fullscreenchange', function () {
                saveSnapshot('fullscreenchange');
            }, true);

            document.addEventListener('webkitfullscreenchange', function () {
                saveSnapshot('webkitfullscreenchange');
            }, true);

            document.addEventListener('click', function (event) {
                const target = event.target;
                if (!(target instanceof Element)) {
                    return;
                }

                if (target.closest(WEB_FULLSCREEN_SELECTORS.join(', '))) {
                    setTimeout(function () {
                        saveSnapshot('fullscreen-button-click-web', 'web');
                    }, 60);
                    return;
                }

                if (target.closest(DOCUMENT_FULLSCREEN_SELECTORS.join(', '))) {
                    setTimeout(function () {
                        saveSnapshot('fullscreen-button-click-full', 'full');
                    }, 60);
                }
            }, true);

            window.addEventListener('beforeunload', function () {
                saveSnapshot('beforeunload');
                savePendingSnapshot('beforeunload');
            });

            window.addEventListener('pagehide', function () {
                saveSnapshot('pagehide');
                savePendingSnapshot('pagehide');
            });

            document.addEventListener('visibilitychange', function () {
                if (!document.hidden) {
                    scheduleRestore('visibilitychange');
                }
            });

            window.addEventListener('akasha:navigation-completed', function () {
                scheduleRestore('host-navigation-completed');
            });

            window.addEventListener('akasha:save-playback-state', function () {
                saveSnapshot('external-save-hint');
                savePendingSnapshot('external-save-hint');
            });
        }

        function init() {
            if (!isTargetSite()) {
                return;
            }

            postPlaybackDebug('playbackStateManager-init', {
                host: window.location.hostname,
                path: window.location.pathname
            });

            setupTrackingEvents();
            patchHistoryNavigation();
            startStatePoller();
            scheduleRestore('init');

            if (!readSnapshot()) {
                saveSnapshot('init-no-existing-snapshot');
            }

            setTimeout(function () {
                scheduleRestore('delayed-init');
            }, 350);
        }

        return {
            init: init
        };
    })();

    // ========================================
    // 初始化
    // ========================================

    function initialize() {
        console.log('[SandronePlayer] initialize() called, document.body:', !!document.body, 'readyState:', document.readyState);
        
        // 等待 body 可用（AddScriptToExecuteOnDocumentCreatedAsync 可能在 body 创建前执行）
        if (!document.body) {
            // 使用多种方式确保初始化
            if (document.readyState === 'loading') {
                console.log('[SandronePlayer] Waiting for DOMContentLoaded');
                document.addEventListener('DOMContentLoaded', initialize);
            } else {
                // 短暂延迟后重试
                console.log('[SandronePlayer] Retrying in 10ms');
                setTimeout(initialize, 10);
            }
            return;
        }

        console.log('[SandronePlayer] Creating elements...');
        createDragZone();
        const controls = createControlButtons();
        setupVisibilityHandlers(controls);
        setupFullscreenHandler();
        PlaybackStateManager.init();
        console.log('[SandronePlayer] Injection complete!');
    }

    // 执行初始化
    initialize();
})();
