namespace Kampute.Resilience.Test.Strategies.Modifiers
{
    using Kampute.Resilience;
    using Kampute.Resilience.Strategies.Modifiers;
    using Moq;
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class MaxElapsedTimeModifierTests
    {
        [TestCase(1000, 0, 100, 100, true)]
        [TestCase(1000, 500, 100, 100, true)]
        [TestCase(1000, 950, 100, 50, true)]
        [TestCase(1000, 1000, 100, 0, false)]
        [TestCase(1000, 1500, 100, 0, false)]
        public void TryGetRetryDelay_WhenInnerStrategyReturnsTrue_ReturnsExpectedResult(int maxElapsedTimeMs, int elapsedMs, int baseDelayMs, int expectedDelayMs, bool expectedResult)
        {
            var maxElapsedTime = TimeSpan.FromMilliseconds(maxElapsedTimeMs);
            var elapsed = TimeSpan.FromMilliseconds(elapsedMs);
            var baseDelay = TimeSpan.FromMilliseconds(baseDelayMs);
            var expectedDelay = TimeSpan.FromMilliseconds(expectedDelayMs);

            var mockInner = new Mock<IRetryStrategy>();
            mockInner.Setup(s => s.TryGetRetryDelay(It.IsAny<TimeSpan>(), It.IsAny<uint>(), out It.Ref<TimeSpan>.IsAny))
                     .Returns((TimeSpan elapsed, uint retryCount, out TimeSpan delay) =>
                     {
                         delay = expectedDelay;
                         return true;
                     });
            var strategy = new MaxElapsedTimeModifier(mockInner.Object, maxElapsedTime);

            var result = strategy.TryGetRetryDelay(elapsed, 0, out var actualDelay);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.EqualTo(expectedResult));
                Assert.That(actualDelay, Is.EqualTo(expectedDelay));
            }
        }

        [Test]
        public void TryGetRetryDelay_WhenInnerStrategyReturnsFalse_ReturnsFalse()
        {
            var mockInner = new Mock<IRetryStrategy>();
            mockInner.Setup(s => s.TryGetRetryDelay(It.IsAny<TimeSpan>(), It.IsAny<uint>(), out It.Ref<TimeSpan>.IsAny)).Returns(false);
            var strategy = new MaxElapsedTimeModifier(mockInner.Object, TimeSpan.FromSeconds(10));

            var result = strategy.TryGetRetryDelay(TimeSpan.Zero, 0, out var actualDelay);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.False);
                Assert.That(actualDelay, Is.Default);
            }
        }
    }
}
