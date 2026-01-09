using System;
using AkashaNavigator.Core.Events;
using Xunit;

namespace AkashaNavigator.Tests.Core
{
    /// <summary>
    /// EventBus 单元测试
    /// 测试事件发布/订阅机制
    /// </summary>
    public class EventBusTests
    {
        // 测试用事件类型
        private class TestEvent
        {
            public int Value { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        private class AnotherEvent
        {
            public string Data { get; set; } = string.Empty;
        }

        #region Publish / Subscribe Tests (3.1.1)

        /// <summary>
        /// Publish 应该调用所有已订阅的处理器
        /// </summary>
        [Fact]
        public void Publish_ShouldCallAllSubscribedHandlers()
        {
            // Arrange
            var eventBus = new EventBus();
            var callCount1 = 0;
            var callCount2 = 0;
            var callCount3 = 0;

            // Act
            eventBus.Subscribe<TestEvent>(e => callCount1++);
            eventBus.Subscribe<TestEvent>(e => callCount2++);
            eventBus.Subscribe<TestEvent>(e => callCount3++);

            eventBus.Publish(new TestEvent());

            // Assert
            Assert.Equal(1, callCount1);
            Assert.Equal(1, callCount2);
            Assert.Equal(1, callCount3);
        }

        /// <summary>
        /// Publish 应该传递正确的事件数据给处理器
        /// </summary>
        [Fact]
        public void Publish_ShouldPassCorrectEventData()
        {
            // Arrange
            var eventBus = new EventBus();
            TestEvent? receivedEvent = null;

            var testEvent = new TestEvent { Value = 42, Message = "Hello" };

            // Act
            eventBus.Subscribe<TestEvent>(e => receivedEvent = e);
            eventBus.Publish(testEvent);

            // Assert
            Assert.NotNull(receivedEvent);
            Assert.Same(testEvent, receivedEvent);
            Assert.Equal(42, receivedEvent.Value);
            Assert.Equal("Hello", receivedEvent.Message);
        }

        /// <summary>
        /// Publish 对没有订阅者的事件应该静默处理
        /// </summary>
        [Fact]
        public void Publish_WithNoSubscribers_ShouldNotThrow()
        {
            // Arrange
            var eventBus = new EventBus();

            // Act & Assert - 不应该抛出异常
            eventBus.Publish(new TestEvent());
            eventBus.Publish(new AnotherEvent());
        }

        /// <summary>
        /// Publish null 事件应该静默处理
        /// </summary>
        [Fact]
        public void Publish_NullEvent_ShouldReturnSilently()
        {
            // Arrange
            var eventBus = new EventBus();
            var callCount = 0;
            eventBus.Subscribe<TestEvent>(e => callCount++);

            // Act
            eventBus.Publish((TestEvent)null!);

            // Assert
            Assert.Equal(0, callCount);
        }

        /// <summary>
        /// Subscribe 相同的处理器多次应该被忽略（避免重复订阅）
        /// </summary>
        [Fact]
        public void Subscribe_SameHandlerMultipleTimes_ShouldOnlyRegisterOnce()
        {
            // Arrange
            var eventBus = new EventBus();
            var callCount = 0;
            Action<TestEvent> handler = e => callCount++;

            // Act
            eventBus.Subscribe(handler);
            eventBus.Subscribe(handler);
            eventBus.Subscribe(handler);

            eventBus.Publish(new TestEvent());

            // Assert
            Assert.Equal(1, callCount); // 只应该被调用一次
        }

        /// <summary>
        /// Subscribe null 处理器应该抛出 ArgumentNullException
        /// </summary>
        [Fact]
        public void Subscribe_NullHandler_ShouldThrowArgumentNullException()
        {
            // Arrange
            var eventBus = new EventBus();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => eventBus.Subscribe<TestEvent>(null!));
        }

        #endregion

        #region Unsubscribe Tests (3.1.2)

        /// <summary>
        /// Unsubscribe 应该移除指定的处理器
        /// </summary>
        [Fact]
        public void Unsubscribe_ShouldRemoveSpecifiedHandler()
        {
            // Arrange
            var eventBus = new EventBus();
            var callCount1 = 0;
            var callCount2 = 0;

            Action<TestEvent> handler1 = e => callCount1++;
            Action<TestEvent> handler2 = e => callCount2++;

            eventBus.Subscribe(handler1);
            eventBus.Subscribe(handler2);

            // Act
            eventBus.Unsubscribe(handler1);
            eventBus.Publish(new TestEvent());

            // Assert
            Assert.Equal(0, callCount1); // handler1 不应该被调用
            Assert.Equal(1, callCount2); // handler2 应该被调用
        }

        /// <summary>
        /// Unsubscribe 不存在的处理器应该静默处理
        /// </summary>
        [Fact]
        public void Unsubscribe_NonExistentHandler_ShouldNotThrow()
        {
            // Arrange
            var eventBus = new EventBus();
            Action<TestEvent> handler = e => { };

            // Act & Assert - 不应该抛出异常
            eventBus.Unsubscribe(handler);
        }

        /// <summary>
        /// Unsubscribe null 处理器应该静默处理
        /// </summary>
        [Fact]
        public void Unsubscribe_NullHandler_ShouldNotThrow()
        {
            // Arrange
            var eventBus = new EventBus();
            eventBus.Subscribe<TestEvent>(e => { });

            // Act & Assert - 不应该抛出异常
            eventBus.Unsubscribe<TestEvent>(null!);
        }

        /// <summary>
        /// Unsubscribe 最后一个处理器后应该移除事件类型
        /// </summary>
        [Fact]
        public void Unsubscribe_LastHandler_ShouldRemoveEventType()
        {
            // Arrange
            var eventBus = new EventBus();
            Action<TestEvent> handler = e => { };

            eventBus.Subscribe(handler);

            // Act
            eventBus.Unsubscribe(handler);

            // 再次发布不应该有任何问题
            eventBus.Publish(new TestEvent());
        }

        #endregion

        #region Multiple Subscribers Tests (3.1.3)

        /// <summary>
        /// 多个订阅者应该都能收到事件
        /// </summary>
        [Fact]
        public void MultipleSubscribers_ShouldAllReceiveEvent()
        {
            // Arrange
            var eventBus = new EventBus();
            var results = new System.Collections.Generic.List<int>();

            // Act
            eventBus.Subscribe<TestEvent>(e => results.Add(1));
            eventBus.Subscribe<TestEvent>(e => results.Add(2));
            eventBus.Subscribe<TestEvent>(e => results.Add(3));
            eventBus.Subscribe<TestEvent>(e => results.Add(4));
            eventBus.Subscribe<TestEvent>(e => results.Add(5));

            eventBus.Publish(new TestEvent());

            // Assert
            Assert.Equal(5, results.Count);
            Assert.Contains(1, results);
            Assert.Contains(2, results);
            Assert.Contains(3, results);
            Assert.Contains(4, results);
            Assert.Contains(5, results);
        }

        /// <summary>
        /// 不同事件类型的订阅者应该各自独立
        /// </summary>
        [Fact]
        public void MultipleEventTypes_ShouldBeIndependent()
        {
            // Arrange
            var eventBus = new EventBus();
            var testEventCount = 0;
            var anotherEventCount = 0;

            // Act
            eventBus.Subscribe<TestEvent>(e => testEventCount++);
            eventBus.Subscribe<AnotherEvent>(e => anotherEventCount++);

            eventBus.Publish(new TestEvent());
            eventBus.Publish(new TestEvent());
            eventBus.Publish(new AnotherEvent());

            // Assert
            Assert.Equal(2, testEventCount);
            Assert.Equal(1, anotherEventCount);
        }

        /// <summary>
        /// 同一事件类型的多个订阅者按订阅顺序执行
        /// </summary>
        [Fact]
        public void MultipleSubscribers_ShouldExecuteInSubscriptionOrder()
        {
            // Arrange
            var eventBus = new EventBus();
            var executionOrder = new System.Collections.Generic.List<string>();

            // Act
            eventBus.Subscribe<TestEvent>(e => executionOrder.Add("first"));
            eventBus.Subscribe<TestEvent>(e => executionOrder.Add("second"));
            eventBus.Subscribe<TestEvent>(e => executionOrder.Add("third"));

            eventBus.Publish(new TestEvent());

            // Assert
            Assert.Equal(3, executionOrder.Count);
            Assert.Equal("first", executionOrder[0]);
            Assert.Equal("second", executionOrder[1]);
            Assert.Equal("third", executionOrder[2]);
        }

        /// <summary>
        /// 取消部分订阅者后，剩余订阅者仍能收到事件
        /// </summary>
        [Fact]
        public void AfterPartialUnsubscribe_RemainingSubscribersShouldReceiveEvents()
        {
            // Arrange
            var eventBus = new EventBus();
            var count1 = 0;
            var count2 = 0;
            var count3 = 0;

            Action<TestEvent> handler1 = e => count1++;
            Action<TestEvent> handler2 = e => count2++;
            Action<TestEvent> handler3 = e => count3++;

            eventBus.Subscribe(handler1);
            eventBus.Subscribe(handler2);
            eventBus.Subscribe(handler3);

            // Act - 取消中间的订阅者
            eventBus.Unsubscribe(handler2);
            eventBus.Publish(new TestEvent());

            // Assert
            Assert.Equal(1, count1);
            Assert.Equal(0, count2);
            Assert.Equal(1, count3);
        }

        #endregion

        #region Event Arguments Tests (3.1.4)

        /// <summary>
        /// 事件参数应该正确传递给订阅者
        /// </summary>
        [Fact]
        public void EventArguments_ShouldBePassedCorrectly()
        {
            // Arrange
            var eventBus = new EventBus();
            int? receivedValue = null;
            string? receivedMessage = null;

            // Act
            eventBus.Subscribe<TestEvent>(e =>
            {
                receivedValue = e.Value;
                receivedMessage = e.Message;
            });

            eventBus.Publish(new TestEvent { Value = 123, Message = "Test Message" });

            // Assert
            Assert.Equal(123, receivedValue);
            Assert.Equal("Test Message", receivedMessage);
        }

        /// <summary>
        /// 多个订阅者应该收到相同的事件参数
        /// </summary>
        [Fact]
        public void MultipleSubscribers_ShouldReceiveSameEventArguments()
        {
            // Arrange
            var eventBus = new EventBus();
            var event1 = new TestEvent { Value = 999, Message = "Shared" };
            var event2 = new TestEvent { Value = 999, Message = "Shared" };
            var event3 = new TestEvent { Value = 999, Message = "Shared" };

            // Act
            eventBus.Subscribe<TestEvent>(e => { event1 = e; });
            eventBus.Subscribe<TestEvent>(e => { event2 = e; });
            eventBus.Subscribe<TestEvent>(e => { event3 = e; });

            var originalEvent = new TestEvent { Value = 999, Message = "Shared" };
            eventBus.Publish(originalEvent);

            // Assert - 所有订阅者收到的是同一个实例
            Assert.Same(originalEvent, event1);
            Assert.Same(originalEvent, event2);
            Assert.Same(originalEvent, event3);
        }

        /// <summary>
        /// 事件参数修改应该在所有订阅者中可见
        /// </summary>
        [Fact]
        public void EventMutation_ShouldBeVisibleAcrossSubscribers()
        {
            // Arrange
            var eventBus = new EventBus();
            var sharedEvent = new TestEvent { Value = 100, Message = "Original" };

            // Act - 第一个订阅者修改事件
            eventBus.Subscribe<TestEvent>(e =>
            {
                e.Value = 200;
                e.Message = "Modified";
            });

            // 第二个订阅者读取修改后的值
            int? readValue = null;
            string? readMessage = null;

            eventBus.Subscribe<TestEvent>(e =>
            {
                readValue = e.Value;
                readMessage = e.Message;
            });

            eventBus.Publish(sharedEvent);

            // Assert
            Assert.Equal(200, readValue);
            Assert.Equal("Modified", readMessage);
        }

        /// <summary>
        /// 不同事件类型的参数应该互不干扰
        /// </summary>
        [Fact]
        public void DifferentEventTypes_ArgumentsShouldNotInterfere()
        {
            // Arrange
            var eventBus = new EventBus();
            var testEventReceived = false;
            var anotherEventReceived = false;

            // Act
            eventBus.Subscribe<TestEvent>(e =>
            {
                testEventReceived = true;
                Assert.NotNull(e as TestEvent);
            });

            eventBus.Subscribe<AnotherEvent>(e =>
            {
                anotherEventReceived = true;
                Assert.NotNull(e as AnotherEvent);
            });

            eventBus.Publish(new TestEvent { Value = 1, Message = "Test" });
            eventBus.Publish(new AnotherEvent { Data = "Another" });

            // Assert
            Assert.True(testEventReceived);
            Assert.True(anotherEventReceived);
        }

        /// <summary>
        /// 复杂对象作为事件参数应该正确传递
        /// </summary>
        [Fact]
        public void ComplexObjectAsEventArgument_ShouldPassCorrectly()
        {
            // Arrange
            var eventBus = new EventBus();

            var complexEvent = new TestEvent
            {
                Value = int.MaxValue,
                Message = new string('A', 1000) // 长字符串
            };

            TestEvent? received = null;

            // Act
            eventBus.Subscribe<TestEvent>(e => received = e);
            eventBus.Publish(complexEvent);

            // Assert
            Assert.NotNull(received);
            Assert.Equal(int.MaxValue, received.Value);
            Assert.Equal(1000, received.Message.Length);
        }

        #endregion

        #region Handler Exception Tests

        /// <summary>
        /// 处理器抛出异常不应该影响其他处理器
        /// </summary>
        [Fact]
        public void HandlerException_ShouldNotAffectOtherHandlers()
        {
            // Arrange
            var eventBus = new EventBus();
            var normalHandlerCallCount = 0;

            // Act
            eventBus.Subscribe<TestEvent>(e => throw new InvalidOperationException("Test exception"));
            eventBus.Subscribe<TestEvent>(e => normalHandlerCallCount++);

            // 不应该抛出异常
            eventBus.Publish(new TestEvent());

            // Assert
            Assert.Equal(1, normalHandlerCallCount);
        }

        /// <summary>
        /// 多个处理器抛出异常不应该影响正常处理器
        /// </summary>
        [Fact]
        public void MultipleHandlerExceptions_ShouldNotAffectNormalHandlers()
        {
            // Arrange
            var eventBus = new EventBus();
            var normalHandlerCallCount = 0;

            // Act
            eventBus.Subscribe<TestEvent>(e => throw new Exception("First"));
            eventBus.Subscribe<TestEvent>(e => normalHandlerCallCount++);
            eventBus.Subscribe<TestEvent>(e => throw new Exception("Second"));
            eventBus.Subscribe<TestEvent>(e => normalHandlerCallCount++);

            eventBus.Publish(new TestEvent());

            // Assert
            Assert.Equal(2, normalHandlerCallCount);
        }

        #endregion

        #region Clear Tests

        /// <summary>
        /// Clear 应该移除所有订阅者
        /// </summary>
        [Fact]
        public void Clear_ShouldRemoveAllSubscribers()
        {
            // Arrange
            var eventBus = new EventBus();
            var callCount = 0;

            eventBus.Subscribe<TestEvent>(e => callCount++);
            eventBus.Subscribe<AnotherEvent>(e => callCount++);

            // Act
            eventBus.Clear();
            eventBus.Publish(new TestEvent());
            eventBus.Publish(new AnotherEvent());

            // Assert
            Assert.Equal(0, callCount);
        }

        /// <summary>
        /// Clear 后重新订阅应该正常工作
        /// </summary>
        [Fact]
        public void AfterClear_ResubscribeShouldWork()
        {
            // Arrange
            var eventBus = new EventBus();
            var callCount = 0;

            eventBus.Subscribe<TestEvent>(e => callCount++);
            eventBus.Clear();

            // Act
            eventBus.Subscribe<TestEvent>(e => callCount++);
            eventBus.Publish(new TestEvent());

            // Assert
            Assert.Equal(1, callCount);
        }

        #endregion

        #region Thread Safety Tests

        /// <summary>
        /// 并发订阅应该是线程安全的
        /// </summary>
        [Fact]
        public void ConcurrentSubscribe_ShouldBeThreadSafe()
        {
            // Arrange
            var eventBus = new EventBus();
            var callCount = 0;

            // Act - 使用多个线程并发订阅相同事件类型
            // 由于 EventBus 的避免重复订阅逻辑，这里测试线程安全性而非重复订阅
            System.Threading.Tasks.Parallel.For(0, 100, i =>
            {
                // 每次创建新的事件实例和独立的处理逻辑
                eventBus.Subscribe<TestEvent>(e =>
                {
                    System.Threading.Interlocked.Increment(ref callCount);
                });
            });

            // 由于避免重复订阅，需要检查至少有一个订阅者被注册
            eventBus.Publish(new TestEvent());

            // Assert - 至少应该有一个订阅者成功注册
            Assert.True(callCount >= 1);
            // 由于避免重复订阅的逻辑，预期只有一个订阅者
            Assert.Equal(1, callCount);
        }

        /// <summary>
        /// 并发发布应该是线程安全的
        /// </summary>
        [Fact]
        public void ConcurrentPublish_ShouldBeThreadSafe()
        {
            // Arrange
            var eventBus = new EventBus();
            var callCount = 0;

            eventBus.Subscribe<TestEvent>(e => System.Threading.Interlocked.Increment(ref callCount));

            // Act - 使用多个线程并发发布
            System.Threading.Tasks.Parallel.For(0, 100, _ =>
            {
                eventBus.Publish(new TestEvent());
            });

            // Assert
            Assert.Equal(100, callCount);
        }

        #endregion
    }
}
