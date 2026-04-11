using System;
using System.IO;
using System.Text.Json;
using Microsoft.ClearScript.V8;
using Xunit;

namespace AkashaNavigator.Tests;

public class PlaybackStateScriptTests
{
    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AkashaNavigator.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }

    [Fact]
    public void InjectedScript_ShouldContainExplicitRateIntentGate()
    {
        var root = GetRepositoryRoot();
        var path = Path.Combine(root, "AkashaNavigator", "Scripts", "InjectedScripts.js");
        var script = File.ReadAllText(path);

        Assert.Contains("function markExplicitRateIntent", script, StringComparison.Ordinal);
        Assert.Contains("function hasRecentExplicitRateIntent", script, StringComparison.Ordinal);
        Assert.Contains("function shouldPersistRateAsAuthority", script, StringComparison.Ordinal);
        Assert.Contains("let navigationWindowUntil", script, StringComparison.Ordinal);
        Assert.Contains("function beginNavigationWindow", script, StringComparison.Ordinal);
        Assert.Contains("function isWithinNavigationWindow", script, StringComparison.Ordinal);
        Assert.Contains("function shouldOpenNavigationWindowForRestore", script, StringComparison.Ordinal);
        Assert.DoesNotContain("lastUserInteractionAt", script, StringComparison.Ordinal);
    }

    [Fact]
    public void InjectedScript_VideoRateChange_ShouldNotPersistAuthorityFromGenericUserInteraction()
    {
        var root = GetRepositoryRoot();
        var path = Path.Combine(root, "AkashaNavigator", "Scripts", "InjectedScripts.js");
        var script = File.ReadAllText(path);

        Assert.DoesNotContain("markExplicitRateRequest(rate, 'video-ratechange')", script, StringComparison.Ordinal);
        Assert.Contains("shouldPersistRateAsAuthority(rate, 'video-ratechange')", script, StringComparison.Ordinal);
        Assert.Contains("saveSnapshot('video-ratechange', null, null, false)", script, StringComparison.Ordinal);
    }

    [Fact]
    public void InjectedScript_ShouldContainPlaybackRateSyncMessageSupport()
    {
        var root = GetRepositoryRoot();
        var path = Path.Combine(root, "AkashaNavigator", "Scripts", "InjectedScripts.js");
        var script = File.ReadAllText(path);

        Assert.Contains("type: 'playback_rate_sync'", script, StringComparison.Ordinal);
        Assert.Contains("function postPlaybackRateSync", script, StringComparison.Ordinal);
    }

    [Fact]
    public void InjectedScript_ExplicitBilibiliRateChange_ShouldPostPlaybackRateSync()
    {
        var root = GetRepositoryRoot();
        var path = Path.Combine(root, "AkashaNavigator", "Scripts", "InjectedScripts.js");
        var script = File.ReadAllText(path);

        Assert.Contains("postPlaybackRateSync(rate, 'bilibili-rate-menu')", script, StringComparison.Ordinal);
    }

    [Fact]
    public void InjectedScript_ExplicitBilibiliRateChange_ShouldPostSinglePlaybackRateSyncMessage()
    {
        using var harness = CreatePlaybackRateMenuHarness();

        harness.SetupTrackingEvents();
        harness.SetVideoPlaybackRate(1.0);
        harness.BindVideoEvents();
        harness.TriggerDocumentClick(new TestJsElement(
            ".bilibili-player-video-btn-speed-menu-list .bilibili-player-video-btn-speed-menu-list-item",
            TextContent: "1.5x"));
        harness.SetVideoPlaybackRate(1.5);
        harness.TriggerVideoEvent("ratechange");

        var messages = harness.GetPostedMessages();

        Assert.Single(messages);
        Assert.Equal("playback_rate_sync", messages[0].GetProperty("type").GetString());
        Assert.Equal(1.5, messages[0].GetProperty("rate").GetDouble());
        Assert.Equal("bilibili-rate-menu", messages[0].GetProperty("source").GetString());
    }

    [Fact]
    public void InjectedScript_ExplicitBilibiliRateChange_ShouldPostSelectedRateFromRateChangePath()
    {
        using var harness = CreatePlaybackRateMenuHarness();

        harness.SetupTrackingEvents();
        harness.SetVideoPlaybackRate(1.0);
        harness.BindVideoEvents();
        harness.TriggerDocumentClick(new TestJsElement(
            ".bpx-player-ctrl-playbackrate-menu-item",
            Attributes: new() { ["data-value"] = "1.75" },
            TextContent: "1.75x"));
        harness.SetVideoPlaybackRate(1.75);
        harness.TriggerVideoEvent("ratechange");

        var messages = harness.GetPostedMessages();

        Assert.Single(messages);
        Assert.Equal("playback_rate_sync", messages[0].GetProperty("type").GetString());
        Assert.Equal(1.75, messages[0].GetProperty("rate").GetDouble());
        Assert.Equal("bilibili-rate-menu", messages[0].GetProperty("source").GetString());
    }

    [Fact]
    public void InjectedScript_ExplicitBilibiliRateChange_ShouldSuppressIdenticalConsecutivePlaybackRateSyncMessages()
    {
        using var harness = CreatePlaybackRateMenuHarness();

        harness.SetupTrackingEvents();
        harness.SetVideoPlaybackRate(1.0);
        harness.BindVideoEvents();
        harness.TriggerDocumentClick(new TestJsElement(
            ".bpx-player-ctrl-playbackrate-menu-item",
            Attributes: new() { ["data-value"] = "1.5" },
            TextContent: "1.5x"));
        harness.SetVideoPlaybackRate(1.5);
        harness.TriggerVideoEvent("ratechange");

        harness.MarkExplicitRateIntent(1.5, "bilibili-rate-menu");
        harness.TriggerVideoEvent("ratechange");

        var messages = harness.GetPostedMessages();

        Assert.Single(messages);
        Assert.Equal("playback_rate_sync", messages[0].GetProperty("type").GetString());
        Assert.Equal(1.5, messages[0].GetProperty("rate").GetDouble());
    }

    [Fact]
    public void InjectedScript_ExplicitBilibiliRateChange_AfterNavigationContext_ShouldPostSameRateAgain()
    {
        using var harness = CreatePlaybackRateMenuHarness();

        harness.SetupTrackingEvents();
        harness.SetVideoPlaybackRate(1.0);
        harness.BindVideoEvents();
        harness.TriggerDocumentClick(new TestJsElement(
            ".bpx-player-ctrl-playbackrate-menu-item",
            Attributes: new() { ["data-value"] = "1.5" },
            TextContent: "1.5x"));
        harness.SetVideoPlaybackRate(1.5);
        harness.TriggerVideoEvent("ratechange");

        harness.MarkExplicitRateIntent(1.5, "bilibili-rate-menu");
        harness.TriggerVideoEvent("ratechange");

        harness.NoteVideoNavigation("pushState");
        harness.SetLocation("https://www.bilibili.com/video/test-next");
        harness.TriggerDocumentClick(new TestJsElement(
            ".bpx-player-ctrl-playbackrate-menu-item",
            Attributes: new() { ["data-value"] = "1.5" },
            TextContent: "1.5x"));
        harness.SetVideoPlaybackRate(1.5);
        harness.TriggerVideoEvent("ratechange");

        var messages = harness.GetPostedMessages();

        Assert.Equal(2, messages.Length);
        Assert.Equal("https://www.bilibili.com/video/test", messages[0].GetProperty("url").GetString());
        Assert.Equal("https://www.bilibili.com/video/test-next", messages[1].GetProperty("url").GetString());
        Assert.Equal(1.5, messages[1].GetProperty("rate").GetDouble());
    }

    [Fact]
    public void InjectedScript_ExplicitRateChange_OnNonBilibiliPage_ShouldNotPostPlaybackRateSyncMessage()
    {
        using var harness = CreatePlaybackRateMenuHarness("https://www.youtube.com/watch?v=test");

        harness.SetupTrackingEvents();
        harness.SetVideoPlaybackRate(1.0);
        harness.BindVideoEvents();
        harness.TriggerDocumentClick(new TestJsElement(
            ".bpx-player-ctrl-playbackrate-menu-item",
            Attributes: new() { ["data-value"] = "1.5" },
            TextContent: "1.5x"));
        harness.SetVideoPlaybackRate(1.5);
        harness.TriggerVideoEvent("ratechange");

        Assert.Empty(harness.GetPostedMessages());
    }

    [Fact]
    public void InjectedScript_HostDrivenPlaybackRateApplication_ShouldNotPostPlaybackRateSyncMessage()
    {
        using var harness = CreatePlaybackRateMenuHarness();

        harness.SetupTrackingEvents();
        harness.BindVideoEvents();
        harness.DispatchHostSetPlaybackRate(1.75);

        Assert.Empty(harness.GetPostedMessages());
    }

    [Fact]
    public void InjectedScript_HostDrivenRateChangePath_ShouldNotPostPlaybackRateSyncMessage()
    {
        using var harness = CreatePlaybackRateMenuHarness();

        harness.SetupTrackingEvents();
        harness.SetVideoPlaybackRate(1.0);
        harness.BindVideoEvents();
        harness.DispatchHostSetPlaybackRate(1.75);
        harness.SetVideoPlaybackRate(1.75);
        harness.TriggerVideoEvent("ratechange");

        Assert.Empty(harness.GetPostedMessages());
    }

    [Fact]
    public void InjectedScript_ShouldRecognizeBilibiliPlaybackRateMenuInteraction()
    {
        var harness = CreatePlaybackRateMenuHarness();

        Assert.True(harness.IsPlaybackRateMenuElement(new TestJsElement(
            ".bpx-player-ctrl-playbackrate-menu-item",
            TextContent: "1.5x")));

        Assert.True(harness.IsPlaybackRateMenuElement(new TestJsElement(
            ".bilibili-player-video-btn-speed-menu-list .bilibili-player-video-btn-speed-menu-list-item",
            TextContent: "1.25倍速")));

        Assert.False(harness.IsPlaybackRateMenuElement(new TestJsElement(
            ".unrelated-menu-item",
            TextContent: "1.5x")));

        Assert.DoesNotContain("[class*=\"playbackrate-menu\"] [data-value]", harness.Script, StringComparison.Ordinal);
        Assert.DoesNotContain("[class*=\"speed-menu\"] [data-value]", harness.Script, StringComparison.Ordinal);
    }

    [Fact]
    public void InjectedScript_PlaybackRateMenuClick_ShouldMarkExplicitRateIntent()
    {
        var harness = CreatePlaybackRateMenuHarness();

        Assert.Equal(1.75, harness.ExtractPlaybackRateFromElement(new TestJsElement(
            ".bpx-player-ctrl-playbackrate-menu-item",
            Attributes: new() { ["data-value"] = "1.75" },
            TextContent: "ignored")));

        Assert.Equal(1.25, harness.ExtractPlaybackRateFromElement(new TestJsElement(
            ".bpx-player-ctrl-playbackrate-menu-item",
            Attributes: new() { ["data-rate"] = "1.25" },
            TextContent: "ignored")));

        Assert.Equal(1.5, harness.ExtractPlaybackRateFromElement(new TestJsElement(
            ".bilibili-player-video-btn-speed-menu-list .bilibili-player-video-btn-speed-menu-list-item",
            TextContent: "1.5x")));

        Assert.Equal(1.5, harness.ExtractPlaybackRateFromElement(new TestJsElement(
            ".bilibili-player-video-btn-speed-menu-list .bilibili-player-video-btn-speed-menu-list-item",
            TextContent: "1.5倍速")));

        Assert.Equal(1.5, harness.ExtractPlaybackRateFromElement(new TestJsElement(
            ".bilibili-player-video-btn-speed-menu-list .bilibili-player-video-btn-speed-menu-list-item",
            TextContent: "\n 1.5倍速 \n")));

        Assert.Null(harness.ExtractPlaybackRateFromElement(new TestJsElement(
            ".bilibili-player-video-btn-speed-menu-list .bilibili-player-video-btn-speed-menu-list-item",
            TextContent: "第2集")));

        harness.SetupTrackingEventsAndTriggerClick(new TestJsElement(
            ".bilibili-player-video-btn-speed-menu-list .bilibili-player-video-btn-speed-menu-list-item",
            TextContent: "1.5x"));

        Assert.True(harness.HasRecentExplicitRateIntent(1.5));
        Assert.True(harness.ShouldPersistRateAsAuthority(1.5, "video-ratechange"));

        var paddedTextHarness = CreatePlaybackRateMenuHarness();
        paddedTextHarness.SetupTrackingEventsAndTriggerClick(new TestJsElement(
            ".bilibili-player-video-btn-speed-menu-list .bilibili-player-video-btn-speed-menu-list-item",
            TextContent: "\n 1.5倍速 \n"));

        Assert.True(paddedTextHarness.HasRecentExplicitRateIntent(1.5));
        Assert.True(paddedTextHarness.ShouldPersistRateAsAuthority(1.5, "video-ratechange"));

        var unrelatedClickHarness = CreatePlaybackRateMenuHarness();
        unrelatedClickHarness.SetupTrackingEventsAndTriggerClick(new TestJsElement(
            ".unrelated-menu-item",
            TextContent: "1.5x"));

        Assert.False(unrelatedClickHarness.HasRecentExplicitRateIntent(1.5));
        Assert.False(unrelatedClickHarness.ShouldPersistRateAsAuthority(1.5, "video-ratechange"));

        Assert.Contains("markExplicitRateIntent(rate, 'bilibili-rate-menu')", harness.Script, StringComparison.Ordinal);
    }

    [Fact]
    public void InjectedScript_HostSetPlaybackRate_ShouldUseExplicitIntentAndAuthoritativeWritePath()
    {
        using var harness = CreatePlaybackRateMenuHarness();

        harness.SetupTrackingEvents();
        harness.DispatchHostSetPlaybackRate(1.75);

        Assert.True(harness.HasRecentExplicitRateIntent(1.75));
        Assert.Equal(1.75, harness.GetGlobalRate());
        Assert.True(harness.ShouldPersistRateAsAuthority(1.75, "video-ratechange"));
    }

    [Fact]
    public void InjectedScript_NavigationWindow_ShouldSuppressOnlyNonExplicitAuthorityWrites()
    {
        var harness = CreatePlaybackRateMenuHarness();

        Assert.False(harness.IsWithinNavigationWindow());

        harness.BeginNavigationWindow();

        Assert.True(harness.IsWithinNavigationWindow());

        Assert.False(harness.ShouldPersistRateAsAuthority(1.5, "video-ratechange"));
    }

    [Fact]
    public void InjectedScript_ShouldPersistAuthorityOnlyForExplicitRateIntent()
    {
        var explicitHarness = CreatePlaybackRateMenuHarness();
        explicitHarness.MarkExplicitRateIntent(1.5, "test-explicit-rate");

        Assert.True(explicitHarness.HasRecentExplicitRateIntent(1.5));
        Assert.True(explicitHarness.ShouldPersistRateAsAuthority(1.5, "video-ratechange"));

        var genericInteractionHarness = CreatePlaybackRateMenuHarness();

        Assert.False(genericInteractionHarness.HasRecentExplicitRateIntent(1.5));
        Assert.False(genericInteractionHarness.ShouldPersistRateAsAuthority(1.5, "video-ratechange"));
    }

    [Fact]
    public void InjectedScript_NavigationWindow_ShouldAllowFreshExplicitAuthorityWrites()
    {
        var harness = CreatePlaybackRateMenuHarness();
        harness.BeginNavigationWindow();

        harness.MarkExplicitRateIntent(1.5, "test-explicit-rate");

        Assert.True(harness.ShouldPersistRateAsAuthority(1.5, "video-ratechange"));
    }

    [Fact]
    public void InjectedScript_RateMenuSelectionDuringNavigationWindow_ShouldWriteAuthorityForExplicitChoices()
    {
        using var nonDefaultHarness = CreatePlaybackRateMenuHarness();
        nonDefaultHarness.SetupTrackingEvents();
        nonDefaultHarness.SetVideoPlaybackRate(1.0);
        nonDefaultHarness.BindVideoEvents();
        nonDefaultHarness.BeginNavigationWindow();
        nonDefaultHarness.SetupTrackingEventsAndTriggerClick(new TestJsElement(
            ".bilibili-player-video-btn-speed-menu-list .bilibili-player-video-btn-speed-menu-list-item",
            TextContent: "1.5x"));
        nonDefaultHarness.SetVideoPlaybackRate(1.5);
        nonDefaultHarness.TriggerVideoEvent("ratechange");

        Assert.Equal(1.5, nonDefaultHarness.GetGlobalRate());

        using var resetHarness = CreatePlaybackRateMenuHarness();
        resetHarness.SetGlobalRate(1.75);
        resetHarness.SetupTrackingEvents();
        resetHarness.SetVideoPlaybackRate(1.75);
        resetHarness.BindVideoEvents();
        resetHarness.BeginNavigationWindow();
        resetHarness.SetupTrackingEventsAndTriggerClick(new TestJsElement(
            ".bilibili-player-video-btn-speed-menu-list .bilibili-player-video-btn-speed-menu-list-item",
            TextContent: "1.0x"));
        resetHarness.SetVideoPlaybackRate(1.0);
        resetHarness.TriggerVideoEvent("ratechange");

        Assert.Equal(1.0, resetHarness.GetGlobalRate());
    }

    [Fact]
    public void InjectedScript_NonNavigationRestoreReasons_ShouldNotSuppressExplicitAuthorityWrites()
    {
        var harness = CreatePlaybackRateMenuHarness();
        harness.MarkExplicitRateIntent(1.5, "test-explicit-rate");

        Assert.False(harness.ShouldOpenNavigationWindowForRestore("video-playing"));
        Assert.False(harness.OpenNavigationWindowForRestore("video-playing"));
        Assert.False(harness.IsWithinNavigationWindow());
        Assert.True(harness.ShouldPersistRateAsAuthority(1.5, "video-ratechange"));
    }

    [Fact]
    public void InjectedScript_RepeatedRestoreRetries_ShouldNotReopenNavigationWindowDuringRestoreCycle()
    {
        var harness = CreatePlaybackRateMenuHarness();

        Assert.True(harness.ShouldOpenNavigationWindowForRestore("video-loadedmetadata"));
        Assert.True(harness.OpenNavigationWindowForRestore("video-loadedmetadata"));

        harness.AdvanceTimeBy(4000);
        harness.MarkExplicitRateIntent(1.5, "test-explicit-rate");

        Assert.True(harness.ShouldPersistRateAsAuthority(1.5, "video-ratechange"));

        harness.AdvanceTimeBy(500);

        Assert.False(harness.OpenNavigationWindowForRestore("video-loadedmetadata"));

        harness.AdvanceTimeBy(501);

        Assert.False(harness.IsWithinNavigationWindow());
        Assert.True(harness.ShouldPersistRateAsAuthority(1.5, "video-ratechange"));
    }

    [Fact]
    public void InjectedScript_ShouldOpenNavigationWindowForPartAndHistoryNavigation()
    {
        using (var partNavigationHarness = CreatePlaybackRateMenuHarness())
        {
            partNavigationHarness.SetupTrackingEventsAndTriggerClick(new TestJsElement(
                ".bpx-player-ctrl-next",
                TextContent: "下一P"));

            Assert.True(partNavigationHarness.IsWithinNavigationWindow());
        }

        using (var pushStateHarness = CreatePlaybackRateMenuHarness())
        {
            pushStateHarness.PatchHistoryNavigation();
            pushStateHarness.InvokePushState();

            Assert.True(pushStateHarness.IsWithinNavigationWindow());
        }

        using (var replaceStateHarness = CreatePlaybackRateMenuHarness())
        {
            replaceStateHarness.PatchHistoryNavigation();
            replaceStateHarness.InvokeReplaceState();

            Assert.True(replaceStateHarness.IsWithinNavigationWindow());
        }

        using (var popStateHarness = CreatePlaybackRateMenuHarness())
        {
            popStateHarness.PatchHistoryNavigation();
            popStateHarness.TriggerWindowEvent("popstate");

            Assert.True(popStateHarness.IsWithinNavigationWindow());
        }

        using (var beforeUnloadHarness = CreatePlaybackRateMenuHarness())
        {
            beforeUnloadHarness.SetupTrackingEvents();
            beforeUnloadHarness.TriggerWindowEvent("beforeunload");

            Assert.True(beforeUnloadHarness.IsWithinNavigationWindow());
        }

        using (var pageHideHarness = CreatePlaybackRateMenuHarness())
        {
            pageHideHarness.SetupTrackingEvents();
            pageHideHarness.TriggerWindowEvent("pagehide");

            Assert.True(pageHideHarness.IsWithinNavigationWindow());
        }

        using (var navigationCompletedHarness = CreatePlaybackRateMenuHarness())
        {
            navigationCompletedHarness.SetupTrackingEvents();
            navigationCompletedHarness.TriggerWindowEvent("akasha:navigation-completed");

            Assert.True(navigationCompletedHarness.IsWithinNavigationWindow());
        }
    }

    [Fact]
    public void InjectedScript_NavigationWindow_ShouldSuppressTransientAuthorityWriteButRespectExplicit1xIntent()
    {
        var harness = CreatePlaybackRateMenuHarness();

        harness.SetGlobalRate(1.5);
        harness.NoteVideoNavigation("pushState");

        Assert.True(harness.IsWithinNavigationWindow());
        Assert.True(harness.ShouldSkipGlobalRateOverwrite(1.0, "pushState"));

        harness.MarkExplicitRateIntent(1.0, "bilibili-rate-menu");

        Assert.False(harness.ShouldSkipGlobalRateOverwrite(1.0, "pushState"));
    }

    [Fact]
    public void InjectedScript_VideoEndedHandoff_ShouldOpenNavigationWindowAndPreservePendingRate()
    {
        using var harness = CreatePlaybackRateMenuHarness();

        harness.SetVideoPlaybackRate(1.5);
        harness.BindVideoEvents();
        harness.TriggerVideoEvent("ended");

        Assert.True(harness.IsWithinNavigationWindow());
        Assert.Equal(1.5, harness.GetPendingSnapshotPlaybackRate());

        harness.SetGlobalRate(1.5);
        harness.SetVideoPlaybackRate(1.0);

        Assert.True(harness.ShouldSkipGlobalRateOverwrite(1.0, "video-ratechange"));
    }

    private static PlaybackRateMenuHarness CreatePlaybackRateMenuHarness(string locationHref = "https://www.bilibili.com/video/test")
    {
        var script = ReadInjectedScript();
        var selectors = ExtractConstDefinition(script, "PLAYBACK_RATE_MENU_SELECTORS");
        var partNavigationSelectors = ExtractConstDefinition(script, "PART_NAVIGATION_SELECTORS");
        var webFullscreenSelectors = ExtractConstDefinition(script, "WEB_FULLSCREEN_SELECTORS");
        var documentFullscreenSelectors = ExtractConstDefinition(script, "DOCUMENT_FULLSCREEN_SELECTORS");
        var navigationWindowMs = ExtractScalarConstDefinition(script, "NAVIGATION_WINDOW_MS");
        var storageKeyPrefix = ExtractScalarConstDefinition(script, "PENDING_STORAGE_KEY_PREFIX");
        var approxEqual = ExtractFunctionDefinition(script, "approxEqual");
        var markExplicitRateRequest = ExtractFunctionDefinition(script, "markExplicitRateRequest");
        var markExplicitRateIntent = ExtractFunctionDefinition(script, "markExplicitRateIntent");
        var hasRecentExplicitRateIntent = ExtractFunctionDefinition(script, "hasRecentExplicitRateIntent");
        var matchesRecentExplicitRateRequest = ExtractFunctionDefinition(script, "matchesRecentExplicitRateRequest");
        var beginNavigationWindow = ExtractFunctionDefinition(script, "beginNavigationWindow");
        var isWithinNavigationWindow = ExtractFunctionDefinition(script, "isWithinNavigationWindow");
        var noteVideoNavigation = ExtractFunctionDefinition(script, "noteVideoNavigation");
        var shouldOpenNavigationWindowForRestore = ExtractFunctionDefinition(script, "shouldOpenNavigationWindowForRestore");
        var openNavigationWindowForRestore = ExtractFunctionDefinition(script, "openNavigationWindowForRestore");
        var shouldPersistRateAsAuthority = ExtractFunctionDefinition(script, "shouldPersistRateAsAuthority");
        var shouldSkipGlobalRateOverwrite = ExtractFunctionDefinition(script, "shouldSkipGlobalRateOverwrite");
        var saveGlobalRate = ExtractFunctionDefinition(script, "saveGlobalRate");
        var isRuleMatched = ExtractFunctionDefinition(script, "isRuleMatched");
        var isBilibiliVideoPage = ExtractFunctionDefinition(script, "isBilibiliVideoPage");
        var isPartNavigationElement = ExtractFunctionDefinition(script, "isPartNavigationElement");
        var pickStablePlaybackRate = ExtractFunctionDefinition(script, "pickStablePlaybackRate");
        var postPlaybackRateSync = ExtractFunctionDefinition(script, "postPlaybackRateSync");
        var shouldUseNavigationRateFallback = ExtractFunctionDefinition(script, "shouldUseNavigationRateFallback");
        var buildSnapshot = ExtractFunctionDefinition(script, "buildSnapshot");
        var savePendingSnapshot = ExtractFunctionDefinition(script, "savePendingSnapshot");
        var bindVideoEvents = ExtractFunctionDefinition(script, "bindVideoEvents");
        var isMenuElement = ExtractFunctionDefinition(script, "isPlaybackRateMenuElement");
        var extractRate = ExtractFunctionDefinition(script, "extractPlaybackRateFromElement");
        var setupTrackingEvents = ExtractFunctionDefinition(script, "setupTrackingEvents");
        var patchHistoryNavigation = ExtractFunctionDefinition(script, "patchHistoryNavigation");

        var location = new Uri(locationHref);
        var bootstrapScript = @"
class Element {
    constructor(selectorMatch, attributes, textContent) {
        this.selectorMatch = selectorMatch || null;
        this.attributes = attributes || {};
        this.textContent = textContent || '';
    }

    closest(selectorList) {
        if (!this.selectorMatch) {
            return null;
        }

        const selectors = String(selectorList || '').split(',').map(selector => selector.trim());
        return selectors.includes(this.selectorMatch) ? this : null;
    }

    getAttribute(name) {
        return Object.prototype.hasOwnProperty.call(this.attributes, name)
            ? this.attributes[name]
            : null;
    }
}

const document = {
    _listeners: {},
    hidden: false,
    documentElement: {},
    body: {},
    addEventListener(type, handler) {
        this._listeners[type] = handler;
    },
    removeEventListener(type) {
        delete this._listeners[type];
    }
};

const window = {
    _listeners: {},
    location: {
        hostname: '__HOSTNAME__',
        pathname: '__PATHNAME__',
        href: '__HREF__'
    },
    chrome: {
        webview: {
            postMessage(message) {
                __postedMessages.push(message);
            }
        }
    },
    addEventListener(type, handler) {
        this._listeners[type] = handler;
    }
};

const __postedMessages = [];

const history = {
    pushState() {
        return 'pushState';
    },
    replaceState() {
        return 'replaceState';
    }
};

const sessionStorage = {
    _items: {},
    setItem(key, value) {
        this._items[key] = String(value);
    },
    getItem(key) {
        return Object.prototype.hasOwnProperty.call(this._items, key) ? this._items[key] : null;
    },
    removeItem(key) {
        delete this._items[key];
    }
};

const localStorage = {
    _items: {},
    setItem(key, value) {
        this._items[key] = String(value);
        if (String(key).indexOf('akasha:playback-global-rate:v1:') === 0) {
            __globalRate = Number(JSON.parse(String(value)).playbackRate) || 1.0;
        }
    },
    getItem(key) {
        return Object.prototype.hasOwnProperty.call(this._items, key) ? this._items[key] : null;
    },
    removeItem(key) {
        delete this._items[key];
    }
};

let __nowMs = 100000;
Date.now = function () { return __nowMs; };
function advanceNowMs(deltaMs) {
    __nowMs += Number(deltaMs) || 0;
}

let __globalRate = 1.0;
let __videoElement = null;
function readGlobalRate() {
    return __globalRate;
}

function getGlobalRateKey() {
    return 'akasha:playback-global-rate:v1:' + window.location.hostname;
}

function setGlobalRate(rate) {
    __globalRate = Number(rate) || 1.0;
}

function postPlaybackDebug() {}
function isTargetSite() { return true; }
function getPendingStorageKey() { return PENDING_STORAGE_KEY_PREFIX + ':' + window.location.hostname; }
function getFullscreenMode() { return 'none'; }
function scheduleRestore(reason) { openNavigationWindowForRestore(reason); }

let lastExplicitRateRequest = null;
let lastPostedPlaybackRateSync = null;
let navigationWindowUntil = 0;
let navigationWindowOpenedForRestoreCycle = false;
function ensureVideoBindingObserver() {}
function getVideoElement() { return __videoElement; }
function saveSnapshot() {}
function readPendingSnapshot() { return null; }
function readSnapshot() { return null; }

class TestVideoElement {
    constructor() {
        this.playbackRate = 1.0;
        this._listeners = {};
        this.__akashaPlaybackStateBound = false;
    }

    addEventListener(type, handler) {
        this._listeners[type] = handler;
    }

    trigger(type) {
        if (!this._listeners[type]) {
            throw new Error(type + ' video listener was not registered');
        }

        this._listeners[type]({});
    }
}

function ensureTestVideoElement() {
    if (!__videoElement) {
        __videoElement = new TestVideoElement();
    }

    return __videoElement;
}

function setVideoPlaybackRate(rate) {
    ensureTestVideoElement().playbackRate = Number(rate) || 1.0;
}

function triggerVideoEvent(type) {
    ensureTestVideoElement().trigger(type);
}

function getPendingSnapshotPlaybackRate() {
    const raw = sessionStorage.getItem(getPendingStorageKey());
    if (!raw) {
        return null;
    }

    return Number(JSON.parse(raw).playbackRate);
}

function triggerDocumentClick(target) {
    if (!document._listeners.click) {
        throw new Error('click listener was not registered');
    }

    document._listeners.click({ target: target });
}

function triggerWindowEvent(type) {
    if (!window._listeners[type]) {
        throw new Error(type + ' listener was not registered');
    }

    window._listeners[type]({});
}

function dispatchHostSetPlaybackRate(rate) {
    if (!window._listeners['akasha:set-playback-rate']) {
        throw new Error('akasha:set-playback-rate listener was not registered');
    }

    window._listeners['akasha:set-playback-rate']({
        detail: {
            rate: Number(rate) || 1.0
        }
    });
}

function invokePushState() {
    return history.pushState({}, '', '/video/test');
}

function invokeReplaceState() {
    return history.replaceState({}, '', '/video/test');
}

function getPostedMessagesJson() {
    return JSON.stringify(__postedMessages);
}

function clearPostedMessages() {
    __postedMessages.length = 0;
}

function setLocation(href) {
    const normalizedHref = String(href || '');
    const withoutProtocol = normalizedHref.replace(/^https?:\/\//, '');
    const slashIndex = withoutProtocol.indexOf('/');
    const hostname = slashIndex >= 0 ? withoutProtocol.slice(0, slashIndex) : withoutProtocol;
    const pathWithQuery = slashIndex >= 0 ? withoutProtocol.slice(slashIndex) : '/';
    const queryIndex = pathWithQuery.indexOf('?');

    window.location.hostname = hostname;
    window.location.pathname = queryIndex >= 0 ? pathWithQuery.slice(0, queryIndex) : pathWithQuery;
    window.location.href = normalizedHref;
}
";

        bootstrapScript = bootstrapScript
            .Replace("__HOSTNAME__", location.Host, StringComparison.Ordinal)
            .Replace("__PATHNAME__", location.AbsolutePath, StringComparison.Ordinal)
            .Replace("__HREF__", locationHref, StringComparison.Ordinal);

        var engine = new V8ScriptEngine();
        engine.Execute(bootstrapScript);
        engine.Execute(partNavigationSelectors);
        engine.Execute(selectors);
        engine.Execute(webFullscreenSelectors);
        engine.Execute(documentFullscreenSelectors);
        engine.Execute(navigationWindowMs);
        engine.Execute(storageKeyPrefix);
        engine.Execute(approxEqual);
        engine.Execute(markExplicitRateRequest);
        engine.Execute(markExplicitRateIntent);
        engine.Execute(hasRecentExplicitRateIntent);
        engine.Execute(matchesRecentExplicitRateRequest);
        engine.Execute(beginNavigationWindow);
        engine.Execute(isWithinNavigationWindow);
        engine.Execute(noteVideoNavigation);
        engine.Execute(shouldOpenNavigationWindowForRestore);
        engine.Execute(openNavigationWindowForRestore);
        engine.Execute(shouldPersistRateAsAuthority);
        engine.Execute(shouldSkipGlobalRateOverwrite);
        engine.Execute(saveGlobalRate);
        engine.Execute(isRuleMatched);
        engine.Execute(isBilibiliVideoPage);
        engine.Execute(isPartNavigationElement);
        engine.Execute(pickStablePlaybackRate);
        engine.Execute(postPlaybackRateSync);
        engine.Execute(shouldUseNavigationRateFallback);
        engine.Execute(buildSnapshot);
        engine.Execute(savePendingSnapshot);
        engine.Execute(bindVideoEvents);
        engine.Execute(isMenuElement);
        engine.Execute(extractRate);
        engine.Execute(setupTrackingEvents);
        engine.Execute(patchHistoryNavigation);

        return new PlaybackRateMenuHarness(engine, script);
    }

    private static string ReadInjectedScript()
    {
        var root = GetRepositoryRoot();
        var path = Path.Combine(root, "AkashaNavigator", "Scripts", "InjectedScripts.js");
        return File.ReadAllText(path);
    }

    private static string ExtractConstDefinition(string script, string constName)
    {
        var marker = $"const {constName} = [";
        var start = script.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find const definition for {constName}.");

        var bracketStart = script.IndexOf('[', start);
        var bracketEnd = FindMatchingToken(script, bracketStart, '[', ']');
        var semicolonIndex = script.IndexOf(';', bracketEnd);
        Assert.True(semicolonIndex >= 0, $"Could not find const terminator for {constName}.");

        return script.Substring(start, semicolonIndex - start + 1);
    }

    private static string ExtractScalarConstDefinition(string script, string constName)
    {
        var marker = $"const {constName} = ";
        var start = script.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find const definition for {constName}.");

        var semicolonIndex = script.IndexOf(';', start);
        Assert.True(semicolonIndex >= 0, $"Could not find const terminator for {constName}.");

        return script.Substring(start, semicolonIndex - start + 1);
    }

    private static string ExtractFunctionDefinition(string script, string functionName)
    {
        var marker = $"function {functionName}";
        var start = script.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find function definition for {functionName}.");

        var bodyStart = script.IndexOf('{', start);
        var bodyEnd = FindMatchingToken(script, bodyStart, '{', '}');
        return script.Substring(start, bodyEnd - start + 1);
    }

    private static int FindMatchingToken(string script, int startIndex, char openToken, char closeToken)
    {
        var depth = 0;
        for (var i = startIndex; i < script.Length; i++)
        {
            var ch = script[i];
            if (ch == openToken)
            {
                depth++;
            }
            else if (ch == closeToken)
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        throw new InvalidOperationException($"Could not find matching '{closeToken}' token.");
    }

    private sealed class PlaybackRateMenuHarness : IDisposable
    {
        private readonly V8ScriptEngine _engine;

        public PlaybackRateMenuHarness(V8ScriptEngine engine, string script)
        {
            _engine = engine;
            Script = script;
        }

        public string Script { get; }

        public bool IsPlaybackRateMenuElement(TestJsElement element)
        {
            using var jsElement = new TestJsElementHandle(_engine, element);
            return _engine.Script.isPlaybackRateMenuElement(jsElement.Value);
        }

        public double? ExtractPlaybackRateFromElement(TestJsElement element)
        {
            using var jsElement = new TestJsElementHandle(_engine, element);
            var result = _engine.Script.extractPlaybackRateFromElement(jsElement.Value);
            return result is null ? null : Convert.ToDouble(result);
        }

        public void SetupTrackingEventsAndTriggerClick(TestJsElement element)
        {
            _engine.Script.setupTrackingEvents();
            using var jsElement = new TestJsElementHandle(_engine, element);
            _engine.Script.triggerDocumentClick(jsElement.Value);
        }

        public void TriggerDocumentClick(TestJsElement element)
        {
            using var jsElement = new TestJsElementHandle(_engine, element);
            _engine.Script.triggerDocumentClick(jsElement.Value);
        }

        public void SetupTrackingEvents()
        {
            _engine.Script.setupTrackingEvents();
        }

        public void TriggerWindowEvent(string eventName)
        {
            _engine.Script.triggerWindowEvent(eventName);
        }

        public void DispatchHostSetPlaybackRate(double rate)
        {
            _engine.Script.dispatchHostSetPlaybackRate(rate);
        }

        public void PatchHistoryNavigation()
        {
            _engine.Script.patchHistoryNavigation();
        }

        public void InvokePushState()
        {
            _engine.Script.invokePushState();
        }

        public void InvokeReplaceState()
        {
            _engine.Script.invokeReplaceState();
        }

        public void BindVideoEvents()
        {
            _engine.Script.bindVideoEvents();
        }

        public void SetVideoPlaybackRate(double rate)
        {
            _engine.Script.setVideoPlaybackRate(rate);
        }

        public void TriggerVideoEvent(string eventName)
        {
            _engine.Script.triggerVideoEvent(eventName);
        }

        public double? GetPendingSnapshotPlaybackRate()
        {
            var result = _engine.Script.getPendingSnapshotPlaybackRate();
            return result is null ? null : Convert.ToDouble(result);
        }

        public bool HasRecentExplicitRateIntent(double rate)
        {
            return _engine.Script.hasRecentExplicitRateIntent(rate);
        }

        public void MarkExplicitRateIntent(double rate, string source)
        {
            _engine.Script.markExplicitRateIntent(rate, source);
        }

        public void BeginNavigationWindow()
        {
            _engine.Script.beginNavigationWindow();
        }

        public void NoteVideoNavigation(string reason)
        {
            _engine.Script.noteVideoNavigation(reason);
        }

        public bool ShouldOpenNavigationWindowForRestore(string reason)
        {
            return _engine.Script.shouldOpenNavigationWindowForRestore(reason);
        }

        public bool OpenNavigationWindowForRestore(string reason)
        {
            return _engine.Script.openNavigationWindowForRestore(reason);
        }

        public bool IsWithinNavigationWindow()
        {
            return _engine.Script.isWithinNavigationWindow();
        }

        public void AdvanceTimeBy(int deltaMs)
        {
            _engine.Script.advanceNowMs(deltaMs);
        }

        public bool ShouldPersistRateAsAuthority(double rate, string reason)
        {
            return _engine.Script.shouldPersistRateAsAuthority(rate, reason);
        }

        public bool ShouldSkipGlobalRateOverwrite(double rate, string reason)
        {
            return _engine.Script.shouldSkipGlobalRateOverwrite(rate, reason);
        }

        public double GetGlobalRate()
        {
            return Convert.ToDouble(_engine.Script.readGlobalRate());
        }

        public void SetGlobalRate(double rate)
        {
            _engine.Script.setGlobalRate(rate);
        }

        public JsonElement[] GetPostedMessages()
        {
            var json = Convert.ToString(_engine.Script.getPostedMessagesJson()) ?? "[]";
            using var document = JsonDocument.Parse(json);
            var messages = new JsonElement[document.RootElement.GetArrayLength()];

            for (var i = 0; i < messages.Length; i++)
            {
                var element = document.RootElement[i];
                if (element.ValueKind == JsonValueKind.String)
                {
                    using var nestedDocument = JsonDocument.Parse(element.GetString() ?? "null");
                    messages[i] = nestedDocument.RootElement.Clone();
                    continue;
                }

                messages[i] = element.Clone();
            }

            return messages;
        }

        public void ClearPostedMessages()
        {
            _engine.Script.clearPostedMessages();
        }

        public void SetLocation(string href)
        {
            _engine.Script.setLocation(href);
        }

        public void Dispose()
        {
            _engine.Dispose();
        }
    }

    private sealed class TestJsElementHandle : IDisposable
    {
        public TestJsElementHandle(V8ScriptEngine engine, TestJsElement element)
        {
            Value = engine.Evaluate(@$"new Element({ToJsStringLiteral(element.SelectorMatch)}, {ToJsObjectLiteral(element.Attributes)}, {ToJsStringLiteral(element.TextContent)})");
        }

        public object Value { get; }

        public void Dispose()
        {
            (Value as IDisposable)?.Dispose();
        }
    }

    private sealed record TestJsElement(string? SelectorMatch, Dictionary<string, string>? Attributes = null, string? TextContent = null)
    {
        public Dictionary<string, string> Attributes { get; } = Attributes ?? new(StringComparer.Ordinal);
        public string TextContent { get; } = TextContent ?? string.Empty;
    }

    private static string ToJsObjectLiteral(Dictionary<string, string> attributes)
    {
        if (attributes.Count == 0)
        {
            return "{}";
        }

        var parts = new List<string>();
        foreach (var pair in attributes)
        {
            parts.Add($"{ToJsStringLiteral(pair.Key)}: {ToJsStringLiteral(pair.Value)}");
        }

        return "{" + string.Join(", ", parts) + "}";
    }

    private static string ToJsStringLiteral(string? value)
    {
        if (value is null)
        {
            return "null";
        }

        return "'"
            + value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("'", "\\'", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal)
            + "'";
    }
}
