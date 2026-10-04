namespace Kampute.Resilience.Test
{
    using Moq;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;

    [TestFixture]
    public class RetryStrategyExtensionsTests
    {
        private static readonly IRetryStrategy Inner = Mock.Of<IRetryStrategy>();

        [Test]
        public void WithMaxElapsedTime_WithMilliseconds_SetsTheLimitInMilliseconds()
        {
            var strategy = Inner.WithMaxElapsedTime(1500);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(strategy.InnerStrategy, Is.SameAs(Inner));
                Assert.That(strategy.MaxElapsedTime, Is.EqualTo(TimeSpan.FromMilliseconds(1500)));
            }
        }

        [Test]
        public void WithMaxDelay_WrapsTheStrategyWithTheCap()
        {
            var strategy = Inner.WithMaxDelay(TimeSpan.FromSeconds(30));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(strategy.InnerStrategy, Is.SameAs(Inner));
                Assert.That(strategy.MaxDelay, Is.EqualTo(TimeSpan.FromSeconds(30)));
            }
        }

        [Test]
        public void WithMaxDelay_WithMilliseconds_SetsTheCapInMilliseconds()
        {
            var strategy = Inner.WithMaxDelay(1500);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(strategy.InnerStrategy, Is.SameAs(Inner));
                Assert.That(strategy.MaxDelay, Is.EqualTo(TimeSpan.FromMilliseconds(1500)));
            }
        }

        [Test]
        public void WithMaxDelay_CapsTheDelaysOfAGrowingStrategy()
        {
            var strategy = RetryStrategies.Exponential(TimeSpan.FromSeconds(1)).WithMaxDelay(TimeSpan.FromSeconds(5));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(strategy.TryGetRetryDelay(TimeSpan.Zero, 2, out var belowCap), Is.True);
                Assert.That(belowCap, Is.EqualTo(TimeSpan.FromSeconds(4)));
                Assert.That(strategy.TryGetRetryDelay(TimeSpan.Zero, 100, out var aboveCap), Is.True);
                Assert.That(aboveCap, Is.EqualTo(TimeSpan.FromSeconds(5)));
            }
        }

        private static IEnumerable<TestCaseData> InvalidMilliseconds()
        {
            yield return Case("WithMaxElapsedTime(-1)", () => Inner.WithMaxElapsedTime(-1), "millisecondsMaxElapsedTime");
            yield return Case("WithMaxDelay(-1)", () => Inner.WithMaxDelay(-1), "millisecondsMaxDelay");

            static TestCaseData Case(string name, Action call, string expectedParamName)
                => new TestCaseData(call, expectedParamName).SetName($"Modifiers_WithNegativeMilliseconds_ThrowArgumentOutOfRangeException({name})");
        }

        [TestCaseSource(nameof(InvalidMilliseconds))]
        public void Modifiers_WithNegativeMilliseconds_ThrowArgumentOutOfRangeException(Action call, string expectedParamName)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(call);

            Assert.That(ex.ParamName, Is.EqualTo(expectedParamName));
        }

        private static IEnumerable<TestCaseData> NullStrategyCalls()
        {
            yield return Case("WithJitter", () => RetryStrategyExtensions.WithJitter(null!));
            yield return Case("WithMaxRetries", () => RetryStrategyExtensions.WithMaxRetries(null!, 1));
            yield return Case("WithMaxElapsedTime(TimeSpan)", () => RetryStrategyExtensions.WithMaxElapsedTime(null!, TimeSpan.FromSeconds(1)));
            yield return Case("WithMaxElapsedTime(int)", () => RetryStrategyExtensions.WithMaxElapsedTime(null!, 1000));
            yield return Case("WithMaxDelay(TimeSpan)", () => RetryStrategyExtensions.WithMaxDelay(null!, TimeSpan.FromSeconds(1)));
            yield return Case("WithMaxDelay(int)", () => RetryStrategyExtensions.WithMaxDelay(null!, 1000));
            yield return Case("StartSession", () => RetryStrategyExtensions.StartSession(null!));

            static TestCaseData Case(string name, Action call)
                => new TestCaseData(call).SetName($"Extensions_WithNullStrategy_ThrowArgumentNullException({name})");
        }

        [TestCaseSource(nameof(NullStrategyCalls))]
        public void Extensions_WithNullStrategy_ThrowArgumentNullException(Action call)
        {
            var ex = Assert.Throws<ArgumentNullException>(call);

            Assert.That(ex.ParamName, Is.EqualTo("strategy"));
        }
    }
}
