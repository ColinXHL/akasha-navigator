using System;
using System.Collections.Generic;
using System.IO;
using FloatWebPlayer.Models;
using FloatWebPlayer.Plugins;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace FloatWebPlayer.Tests
{
    /// <summary>
    /// SpeechApi 属性测试
    /// </summary>
    public class SpeechApiTests : IDisposable
    {
        private readonly string _tempDir;

        public SpeechApiTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"speech_api_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, true);
                }
                catch
                {
                    // 忽略清理错误
                }
            }
        }

        /// <summary>
        /// 创建测试用的插件清单
        /// </summary>
        private PluginManifest CreateTestManifest(string id = "test-plugin")
        {
            return new PluginManifest
            {
                Id = id,
                Name = "Test Plugin",
                Version = "1.0.0",
                Main = "main.js",
                Permissions = new List<string> { "audio" }
            };
        }

        /// <summary>
        /// 创建测试用的 JavaScript 文件
        /// </summary>
        private void CreateJsFile(string content)
        {
            File.WriteAllText(Path.Combine(_tempDir, "main.js"), content);
        }

        /// <summary>
        /// 创建测试用的 SpeechApi
        /// </summary>
        private SpeechApi CreateSpeechApi()
        {
            var manifest = CreateTestManifest();
            CreateJsFile("function onLoad() {} function onUnload() {}");
            var context = new PluginContext(manifest, _tempDir);
            return new SpeechApi(context);
        }

        #region Property 6: 关键词回调分发

        /// <summary>
        /// **Feature: game-plugin-system, Property 6: 关键词回调分发**
        /// *对于任意*注册了关键词监听的插件，当语音识别结果包含该关键词时，对应的回调函数应被调用
        /// **Validates: Requirements 3.1**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property KeywordCallback_ShouldBeInvoked_WhenTextContainsKeyword(NonEmptyString keyword)
        {
            // 过滤掉空白关键词
            var keywordStr = keyword.Get.Trim();
            if (string.IsNullOrEmpty(keywordStr))
                return true.Label("Skipped: empty keyword after trim");

            var speechApi = CreateSpeechApi();
            var callbackInvoked = false;
            string? receivedKeyword = null;
            string? receivedText = null;

            // 注册关键词监听
            speechApi.OnKeyword(new[] { keywordStr }, (kw, text) =>
            {
                callbackInvoked = true;
                receivedKeyword = kw;
                receivedText = text;
            });

            // 构造包含关键词的文本
            var testText = $"这是一段包含 {keywordStr} 的测试文本";

            // 模拟语音识别结果
            speechApi.ProcessSpeechResult(testText);

            // 清理
            speechApi.RemoveAllListeners();

            return (callbackInvoked && receivedKeyword == keywordStr && receivedText == testText)
                .Label($"Keyword: '{keywordStr}', Invoked: {callbackInvoked}, " +
                       $"ReceivedKW: '{receivedKeyword}', ReceivedText: '{receivedText}'");
        }

        /// <summary>
        /// **Feature: game-plugin-system, Property 6: 关键词回调分发（不匹配）**
        /// *对于任意*注册了关键词监听的插件，当语音识别结果不包含该关键词时，回调函数不应被调用
        /// **Validates: Requirements 3.1**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property KeywordCallback_ShouldNotBeInvoked_WhenTextDoesNotContainKeyword(
            NonEmptyString keyword, NonEmptyString otherText)
        {
            var keywordStr = keyword.Get.Trim();
            var textStr = otherText.Get.Trim();
            
            // 确保文本不包含关键词
            if (string.IsNullOrEmpty(keywordStr) || string.IsNullOrEmpty(textStr))
                return true.Label("Skipped: empty strings");
            
            if (textStr.Contains(keywordStr, StringComparison.OrdinalIgnoreCase))
                return true.Label("Skipped: text contains keyword");

            var speechApi = CreateSpeechApi();
            var callbackInvoked = false;

            // 注册关键词监听
            speechApi.OnKeyword(new[] { keywordStr }, (kw, text) =>
            {
                callbackInvoked = true;
            });

            // 模拟语音识别结果（不包含关键词）
            speechApi.ProcessSpeechResult(textStr);

            // 清理
            speechApi.RemoveAllListeners();

            return (!callbackInvoked)
                .Label($"Keyword: '{keywordStr}', Text: '{textStr}', Invoked: {callbackInvoked}");
        }

        /// <summary>
        /// **Feature: game-plugin-system, Property 6: 关键词回调分发（多关键词）**
        /// *对于任意*注册了多个关键词的监听器，当文本包含其中任一关键词时，回调应被调用
        /// **Validates: Requirements 3.1**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property KeywordCallback_ShouldBeInvoked_WhenTextContainsAnyKeyword(PositiveInt keywordIndex)
        {
            var keywords = new[] { "东", "西", "南", "北" };
            var selectedKeyword = keywords[keywordIndex.Get % keywords.Length];

            var speechApi = CreateSpeechApi();
            var callbackInvoked = false;
            string? receivedKeyword = null;

            // 注册多个关键词监听
            speechApi.OnKeyword(keywords, (kw, text) =>
            {
                callbackInvoked = true;
                receivedKeyword = kw;
            });

            // 构造只包含选中关键词的文本
            var testText = $"往{selectedKeyword}走";

            // 模拟语音识别结果
            speechApi.ProcessSpeechResult(testText);

            // 清理
            speechApi.RemoveAllListeners();

            return (callbackInvoked && receivedKeyword == selectedKeyword)
                .Label($"Selected: '{selectedKeyword}', Invoked: {callbackInvoked}, Received: '{receivedKeyword}'");
        }

        /// <summary>
        /// **Feature: game-plugin-system, Property 6: 关键词回调分发（大小写不敏感）**
        /// *对于任意*关键词，匹配应该不区分大小写
        /// **Validates: Requirements 3.1**
        /// </summary>
        [Property(MaxTest = 100)]
        public Property KeywordCallback_ShouldBeCaseInsensitive(NonEmptyString keyword)
        {
            var keywordStr = keyword.Get.Trim();
            if (string.IsNullOrEmpty(keywordStr) || !ContainsLetters(keywordStr))
                return true.Label("Skipped: no letters in keyword");

            var speechApi = CreateSpeechApi();
            var callbackInvoked = false;

            // 注册小写关键词
            speechApi.OnKeyword(new[] { keywordStr.ToLower() }, (kw, text) =>
            {
                callbackInvoked = true;
            });

            // 使用大写文本测试
            var testText = $"Text with {keywordStr.ToUpper()} inside";

            // 模拟语音识别结果
            speechApi.ProcessSpeechResult(testText);

            // 清理
            speechApi.RemoveAllListeners();

            return callbackInvoked
                .Label($"Keyword: '{keywordStr.ToLower()}', Text: '{testText}', Invoked: {callbackInvoked}");
        }

        /// <summary>
        /// 检查字符串是否包含字母
        /// </summary>
        private static bool ContainsLetters(string s)
        {
            foreach (var c in s)
            {
                if (char.IsLetter(c))
                    return true;
            }
            return false;
        }

        #endregion

        #region Unit Tests

        /// <summary>
        /// OnText 回调应该在收到文本时被调用
        /// </summary>
        [Fact]
        public void OnText_ShouldInvokeCallback_WhenTextReceived()
        {
            var speechApi = CreateSpeechApi();
            var callbackInvoked = false;
            string? receivedText = null;

            speechApi.OnText(text =>
            {
                callbackInvoked = true;
                receivedText = text;
            });

            var testText = "测试文本";
            speechApi.ProcessSpeechResult(testText);

            Assert.True(callbackInvoked);
            Assert.Equal(testText, receivedText);

            speechApi.RemoveAllListeners();
        }

        /// <summary>
        /// RemoveAllListeners 应该移除所有监听器
        /// </summary>
        [Fact]
        public void RemoveAllListeners_ShouldRemoveAllListeners()
        {
            var speechApi = CreateSpeechApi();
            var keywordCallbackInvoked = false;
            var textCallbackInvoked = false;

            speechApi.OnKeyword(new[] { "测试" }, (kw, text) => keywordCallbackInvoked = true);
            speechApi.OnText(text => textCallbackInvoked = true);

            // 移除所有监听器
            speechApi.RemoveAllListeners();

            // 模拟语音识别结果
            speechApi.ProcessSpeechResult("这是测试文本");

            Assert.False(keywordCallbackInvoked);
            Assert.False(textCallbackInvoked);
        }

        /// <summary>
        /// 多个监听器应该都被调用
        /// </summary>
        [Fact]
        public void MultipleListeners_ShouldAllBeInvoked()
        {
            var speechApi = CreateSpeechApi();
            var callback1Invoked = false;
            var callback2Invoked = false;

            speechApi.OnKeyword(new[] { "东" }, (kw, text) => callback1Invoked = true);
            speechApi.OnKeyword(new[] { "西" }, (kw, text) => callback2Invoked = true);

            // 包含两个关键词的文本
            speechApi.ProcessSpeechResult("往东西方向走");

            Assert.True(callback1Invoked);
            Assert.True(callback2Invoked);

            speechApi.RemoveAllListeners();
        }

        /// <summary>
        /// 空文本不应该触发回调
        /// </summary>
        [Fact]
        public void EmptyText_ShouldNotInvokeCallback()
        {
            var speechApi = CreateSpeechApi();
            var callbackInvoked = false;

            speechApi.OnText(text => callbackInvoked = true);
            speechApi.OnKeyword(new[] { "测试" }, (kw, text) => callbackInvoked = true);

            speechApi.ProcessSpeechResult("");
            speechApi.ProcessSpeechResult("   ");

            Assert.False(callbackInvoked);

            speechApi.RemoveAllListeners();
        }

        /// <summary>
        /// 回调异常不应该影响其他回调
        /// </summary>
        [Fact]
        public void CallbackException_ShouldNotAffectOtherCallbacks()
        {
            var speechApi = CreateSpeechApi();
            var callback2Invoked = false;

            speechApi.OnText(text => throw new Exception("Test exception"));
            speechApi.OnText(text => callback2Invoked = true);

            // 不应该抛出异常
            speechApi.ProcessSpeechResult("测试文本");

            Assert.True(callback2Invoked);

            speechApi.RemoveAllListeners();
        }

        #endregion
    }
}
