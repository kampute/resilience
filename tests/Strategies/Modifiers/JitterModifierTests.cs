namespace Kampute.Resilience.Test.Strategies.Modifiers
{
    using Kampute.Resilience;
    using Kampute.Resilience.Strategies.Modifiers;
    using Moq;
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class JitterModifierTests
    {
        [TestCase(-0.1)]
        [TestCase(1.1)]
        [TestCase(double.NaN)]
        public void Constructor_WhenJitterFactorIsOutOfRange_ThrowsArgumentOutOfRangeException(double jitterFactor)
        {
            var mockInner = new Mock<IRetryStrategy>();

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new JitterModifier(mockInner.Object, jitterFactor));

            Assert.That(ex.ParamName, Is.EqualTo("jitterFactor"));
        }

        [TestCase(0.0, 1000)]
        [TestCase(0.5, 1000)]
        [TestCase(1.0, 1000)]
        public void TryGetRetryDelay_WhenInnerStrategyReturnsTrue_ReturnsTrueWithJitteredDelay(double jitterFactor, int baseDelayMs)
        {
            var baseDelay = TimeSpan.FromMilliseconds(baseDelayMs);
            var mockInner = new Mock<IRetryStrategy>();
            mockInner.Setup(s => s.TryGetRetryDelay(It.IsAny<TimeSpan>(), It.IsAny<uint>(), out baseDelay)).Returns(true);
            var strategy = new JitterModifier(mockInner.Object, jitterFactor);

            var result = strategy.TryGetRetryDelay(TimeSpan.Zero, 0, out var actualDelay);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.True);
                Assert.That(actualDelay, Is.EqualTo(baseDelay).Within(TimeSpan.FromTicks((long)Math.Round(jitterFactor * baseDelay.Ticks))));
            }
        }

        [Test]
        public void TryGetRetryDelay_WhenInnerStrategyReturnsFalse_ReturnsFalse()
        {
            var mockInner = new Mock<IRetryStrategy>();
            mockInner.Setup(s => s.TryGetRetryDelay(It.IsAny<TimeSpan>(), It.IsAny<uint>(), out It.Ref<TimeSpan>.IsAny)).Returns(false);
            var strategy = new JitterModifier(mockInner.Object, 0.5);

            var result = strategy.TryGetRetryDelay(TimeSpan.Zero, 0, out var actualDelay);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.False);
                Assert.That(actualDelay, Is.Default);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TryGetRetryDelay_WithZeroJitter_PreservesExtremeDelay(bool negative)
        {
            var baseDelay = negative ? TimeSpan.MinValue : TimeSpan.MaxValue;
            var mockInner = new Mock<IRetryStrategy>();
            mockInner.Setup(s => s.TryGetRetryDelay(It.IsAny<TimeSpan>(), It.IsAny<uint>(), out baseDelay)).Returns(true);
            var strategy = new JitterModifier(mockInner.Object, 0);

            Assert.That(strategy.TryGetRetryDelay(TimeSpan.Zero, 0, out var delay), Is.True);
            Assert.That(delay, Is.EqualTo(baseDelay));
        }
    }
}
