namespace Kampute.Resilience.Test.Strategies
{
    using Kampute.Resilience.Strategies;
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class ConstantBackoffStrategyTests
    {
        [Test]
        public void Constructor_SetsDelayProperty()
        {
            var expectedDelay = TimeSpan.FromSeconds(5);

            var strategy = new ConstantBackoffStrategy(expectedDelay);

            Assert.That(strategy.Delay, Is.EqualTo(expectedDelay));
        }

        [TestCase(0u, 0)]
        [TestCase(5u, 10)]
        [TestCase(100u, 1000)]
        public void TryGetRetryDelay_ReturnsExpectedDelay(uint retryCount, int delayMilliseconds)
        {
            var expectedDelay = TimeSpan.FromMilliseconds(delayMilliseconds);
            var strategy = new ConstantBackoffStrategy(expectedDelay);

            var result = strategy.TryGetRetryDelay(TimeSpan.Zero, retryCount, out var actualDelay);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.True);
                Assert.That(actualDelay, Is.EqualTo(expectedDelay));
            }
        }

        [TestCase(0u)]
        [TestCase(5u)]
        [TestCase(100u)]
        public void TryGetRetryDelay_IgnoresElapsedAndRetryCount(uint retryCount)
        {
            var expectedDelay = TimeSpan.FromSeconds(10);
            var strategy = new ConstantBackoffStrategy(expectedDelay);

            var result = strategy.TryGetRetryDelay(TimeSpan.FromHours(1), retryCount, out var actualDelay);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.True);
                Assert.That(actualDelay, Is.EqualTo(expectedDelay));
            }
        }
    }
}
