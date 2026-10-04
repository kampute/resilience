namespace Kampute.Resilience.Test.Strategies
{
    using Kampute.Resilience.Strategies;
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class NoRetryStrategyTests
    {
        [Test]
        public void Instance_IsNotNull()
        {
            var instance = NoRetryStrategy.Instance;

            Assert.That(instance, Is.Not.Null);
        }

        [Test]
        public void Instance_IsSingleton()
        {
            var instance1 = NoRetryStrategy.Instance;
            var instance2 = NoRetryStrategy.Instance;

            Assert.That(instance1, Is.SameAs(instance2));
        }

        [TestCase(0u, 0)]
        [TestCase(1u, 5)]
        [TestCase(10u, 20)]
        public void TryGetRetryDelay_ReturnsFalseAndDefaultDelay(uint retryCount, int elapsedMs)
        {
            var elapsed = TimeSpan.FromMilliseconds(elapsedMs);

            var result = NoRetryStrategy.Instance.TryGetRetryDelay(elapsed, retryCount, out var delay);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.False);
                Assert.That(delay, Is.Default);
            }
        }
    }
}
