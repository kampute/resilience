namespace Kampute.Resilience.Test.Strategies.Modifiers
{
    using Kampute.Resilience;
    using Kampute.Resilience.Strategies.Modifiers;
    using Moq;
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class MaxDelayModifierTests
    {
        [TestCase(1000, 500, 500)]
        [TestCase(1000, 1000, 1000)]
        [TestCase(1000, 1001, 1000)]
        [TestCase(0, 1000, 0)]
        public void TryGetRetryDelay_WhenInnerStrategyReturnsTrue_CapsTheDelay(int maxDelayMs, int innerDelayMs, int expectedDelayMs)
        {
            var innerDelay = TimeSpan.FromMilliseconds(innerDelayMs);
            var mockInner = new Mock<IRetryStrategy>();
            mockInner.Setup(s => s.TryGetRetryDelay(It.IsAny<TimeSpan>(), It.IsAny<uint>(), out innerDelay)).Returns(true);
            var strategy = new MaxDelayModifier(mockInner.Object, TimeSpan.FromMilliseconds(maxDelayMs));

            var result = strategy.TryGetRetryDelay(TimeSpan.Zero, 0, out var actualDelay);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.True);
                Assert.That(actualDelay, Is.EqualTo(TimeSpan.FromMilliseconds(expectedDelayMs)));
            }
        }

        [Test]
        public void TryGetRetryDelay_WhenInnerStrategyReturnsFalse_ReturnsFalse()
        {
            var mockInner = new Mock<IRetryStrategy>();
            mockInner.Setup(s => s.TryGetRetryDelay(It.IsAny<TimeSpan>(), It.IsAny<uint>(), out It.Ref<TimeSpan>.IsAny)).Returns(false);
            var strategy = new MaxDelayModifier(mockInner.Object, TimeSpan.FromSeconds(1));

            var result = strategy.TryGetRetryDelay(TimeSpan.Zero, 0, out var actualDelay);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.False);
                Assert.That(actualDelay, Is.Default);
            }
        }

        [Test]
        public void TryGetRetryDelay_PassesElapsedAndRetryCountToInnerStrategy()
        {
            var elapsed = TimeSpan.FromSeconds(7);
            var mockInner = new Mock<IRetryStrategy>();
            var strategy = new MaxDelayModifier(mockInner.Object, TimeSpan.FromSeconds(1));

            strategy.TryGetRetryDelay(elapsed, 3, out _);

            mockInner.Verify(s => s.TryGetRetryDelay(elapsed, 3, out It.Ref<TimeSpan>.IsAny), Times.Once);
        }

        [Test]
        public void Constructor_WhenMaxDelayIsNegative_ThrowsArgumentOutOfRangeException()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new MaxDelayModifier(Mock.Of<IRetryStrategy>(), TimeSpan.FromMilliseconds(-1)));

            Assert.That(ex.ParamName, Is.EqualTo("maxDelay"));
        }

        [Test]
        public void Constructor_WhenInnerStrategyIsNull_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new MaxDelayModifier(null!, TimeSpan.FromSeconds(1)));

            Assert.That(ex.ParamName, Is.EqualTo("innerStrategy"));
        }
    }
}
