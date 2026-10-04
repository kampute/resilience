namespace Kampute.Resilience.Test.Strategies.Modifiers
{
    using Kampute.Resilience;
    using Kampute.Resilience.Strategies.Modifiers;
    using Moq;
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class MaxRetriesModifierTests
    {
        [TestCase(0u, 0u, false)]
        [TestCase(1u, 0u, true)]
        [TestCase(1u, 1u, false)]
        [TestCase(2u, 1u, true)]
        [TestCase(2u, 3u, false)]
        public void TryGetRetryDelay_WhenInnerStrategyReturnsTrue_ReturnsExpectedResult(uint maxRetries, uint retryCount, bool expectedResult)
        {
            var mockInner = new Mock<IRetryStrategy>();
            mockInner.Setup(s => s.TryGetRetryDelay(It.IsAny<TimeSpan>(), It.IsAny<uint>(), out It.Ref<TimeSpan>.IsAny))
                     .Returns((TimeSpan elapsed, uint retryCount, out TimeSpan delay) =>
                     {
                         delay = TimeSpan.FromSeconds(1);
                         return true;
                     });
            var strategy = new MaxRetriesModifier(mockInner.Object, maxRetries);

            var result = strategy.TryGetRetryDelay(TimeSpan.Zero, retryCount, out var actualDelay);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.EqualTo(expectedResult));
                Assert.That(actualDelay, expectedResult ? Is.Not.Default : Is.Default);
            }
        }

        [Test]
        public void TryGetRetryDelay_WhenInnerStrategyReturnsFalse_ReturnsFalse()
        {
            var mockInner = new Mock<IRetryStrategy>();
            mockInner.Setup(s => s.TryGetRetryDelay(It.IsAny<TimeSpan>(), It.IsAny<uint>(), out It.Ref<TimeSpan>.IsAny)).Returns(false);

            var strategy = new MaxRetriesModifier(mockInner.Object, 3);

            var result = strategy.TryGetRetryDelay(TimeSpan.Zero, 0, out var actualDelay);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.False);
                Assert.That(actualDelay, Is.Default);
            }
        }
    }
}
