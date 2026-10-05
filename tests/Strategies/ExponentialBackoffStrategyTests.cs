namespace Kampute.Resilience.Test.Strategies
{
    using Kampute.Resilience.Strategies;
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class ExponentialBackoffStrategyTests
    {
        [TestCase(0.5)]
        [TestCase(double.NaN)]
        public void Constructor_WhenMultiplierIsLessThanOneOrNaN_ThrowsArgumentOutOfRangeException(double multiplier)
        {
            var initialDelay = TimeSpan.FromSeconds(1);

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ExponentialBackoffStrategy(initialDelay, multiplier));

            Assert.That(ex.ParamName, Is.EqualTo("multiplier"));
        }

        [TestCase(0u, 0, 2.0, 0)]
        [TestCase(1u, 0, 2.0, 0)]
        [TestCase(0u, 1000, 2.0, 1000)]
        [TestCase(1u, 1000, 2.0, 2000)]
        [TestCase(3u, 1000, 2.0, 8000)]
        [TestCase(0u, 1000, 3.0, 1000)]
        [TestCase(1u, 1000, 3.0, 3000)]
        [TestCase(2u, 1000, 3.0, 9000)]
        public void TryGetRetryDelay_ReturnsExpectedDelay(uint retryCount, int initialDelayMs, double multiplier, int expectedDelayMs)
        {
            var initialDelay = TimeSpan.FromMilliseconds(initialDelayMs);
            var expectedDelay = TimeSpan.FromMilliseconds(expectedDelayMs);
            var strategy = new ExponentialBackoffStrategy(initialDelay, multiplier);

            var result = strategy.TryGetRetryDelay(TimeSpan.Zero, retryCount, out var actualDelay);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.True);
                Assert.That(actualDelay, Is.EqualTo(expectedDelay));
            }
        }

        [TestCase(0u, 0, 2.0, 0)]
        [TestCase(1u, 0, 2.0, 0)]
        [TestCase(0u, 1000, 2.0, 1000)]
        [TestCase(1u, 1000, 2.0, 2000)]
        [TestCase(3u, 1000, 2.0, 8000)]
        [TestCase(0u, 1000, 3.0, 1000)]
        [TestCase(1u, 1000, 3.0, 3000)]
        [TestCase(2u, 1000, 3.0, 9000)]
        public void TryGetRetryDelay_IgnoresElapsed(uint retryCount, int initialDelayMs, double multiplier, int expectedDelayMs)
        {
            var initialDelay = TimeSpan.FromMilliseconds(initialDelayMs);
            var expectedDelay = TimeSpan.FromMilliseconds(expectedDelayMs);
            var strategy = new ExponentialBackoffStrategy(initialDelay, multiplier);

            var result = strategy.TryGetRetryDelay(TimeSpan.FromHours(1), retryCount, out var actualDelay);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.True);
                Assert.That(actualDelay, Is.EqualTo(expectedDelay));
            }
        }
    }
}
