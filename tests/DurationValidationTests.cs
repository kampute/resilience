namespace Kampute.Resilience.Test
{
    using Kampute.Resilience.Strategies;
    using Kampute.Resilience.Strategies.Modifiers;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;

    [TestFixture]
    public class DurationValidationTests
    {
        /// <summary>
        /// Enumerates duration arguments exposed by constructors, factories, and modifier helpers.
        /// </summary>
        /// <returns>
        /// Calls paired with the name of the duration argument they validate.
        /// </returns>
        private static IEnumerable<TestCaseData> DurationArguments()
        {
            yield return Case("ConstantConstructor", value => new ConstantBackoffStrategy(value), "delay");
            yield return Case("LinearConstructorInitialDelay", value => new LinearBackoffStrategy(value), "initialDelay");
            yield return Case("LinearConstructorInitialDelayWithStep", value => new LinearBackoffStrategy(value, TimeSpan.Zero), "initialDelay");
            yield return Case("LinearConstructorStep", value => new LinearBackoffStrategy(TimeSpan.Zero, value), "delayStep");
            yield return Case("FibonacciConstructorInitialDelay", value => new FibonacciBackoffStrategy(value), "initialDelay");
            yield return Case("FibonacciConstructorInitialDelayWithStep", value => new FibonacciBackoffStrategy(value, TimeSpan.Zero), "initialDelay");
            yield return Case("FibonacciConstructorStep", value => new FibonacciBackoffStrategy(TimeSpan.Zero, value), "delayStep");
            yield return Case("ExponentialConstructor", value => new ExponentialBackoffStrategy(value, 2), "initialDelay");
            yield return Case("OnceFactory", value => RetryStrategies.Once(value), "delay");
            yield return Case("ConstantFactory", value => RetryStrategies.Constant(value), "delay");
            yield return Case("LinearFactoryInitialDelay", value => RetryStrategies.Linear(value), "initialDelay");
            yield return Case("LinearFactoryInitialDelayWithStep", value => RetryStrategies.Linear(value, TimeSpan.Zero), "initialDelay");
            yield return Case("LinearFactoryStep", value => RetryStrategies.Linear(TimeSpan.Zero, value), "delayStep");
            yield return Case("FibonacciFactoryInitialDelay", value => RetryStrategies.Fibonacci(value), "initialDelay");
            yield return Case("FibonacciFactoryInitialDelayWithStep", value => RetryStrategies.Fibonacci(value, TimeSpan.Zero), "initialDelay");
            yield return Case("FibonacciFactoryStep", value => RetryStrategies.Fibonacci(TimeSpan.Zero, value), "delayStep");
            yield return Case("ExponentialFactory", value => RetryStrategies.Exponential(value), "initialDelay");
            yield return Case("MaxElapsedTimeConstructor", value => new MaxElapsedTimeModifier(RetryStrategies.None, value), "maxElapsedTime");
            yield return Case("MaxElapsedTimeHelper", value => RetryStrategies.None.WithMaxElapsedTime(value), "maxElapsedTime");
            yield return Case("MaxDelayConstructor", value => new MaxDelayModifier(RetryStrategies.None, value), "maxDelay");
            yield return Case("MaxDelayHelper", value => RetryStrategies.None.WithMaxDelay(value), "maxDelay");
        }

        /// <summary>
        /// Names a duration validation case by its public entry point.
        /// </summary>
        /// <param name="name">
        /// The entry point and duration argument covered by the case.
        /// </param>
        /// <param name="call">
        /// The call accepting the duration under test.
        /// </param>
        /// <param name="parameter">
        /// The parameter name expected on a validation exception.
        /// </param>
        /// <returns>
        /// A named test case for negative and zero duration boundaries.
        /// </returns>
        private static TestCaseData Case(string name, Action<TimeSpan> call, string parameter)
            => new TestCaseData(call, parameter).SetName($"DurationArguments_RejectNegativeValuesAndAcceptZero({name})");

        [TestCaseSource(nameof(DurationArguments))]
        public void DurationArguments_RejectNegativeValuesAndAcceptZero(Action<TimeSpan> call, string expectedParameter)
        {
            var negativeDurations = new[] { TimeSpan.FromTicks(-1), TimeSpan.FromMilliseconds(-1), TimeSpan.MinValue };

            foreach (var duration in negativeDurations)
            {
                var error = Assert.Throws<ArgumentOutOfRangeException>(() => call(duration), $"Duration: {duration}");

                Assert.That(error.ParamName, Is.EqualTo(expectedParameter));
            }

            Assert.DoesNotThrow(() => call(TimeSpan.Zero));
        }

        [Test]
        public void Once_WithPastTimestamp_RetriesImmediatelyOnce()
        {
            var strategy = RetryStrategies.Once(DateTimeOffset.UtcNow.AddDays(-1));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(strategy.TryGetRetryDelay(TimeSpan.Zero, 0, out var delay), Is.True);
                Assert.That(delay, Is.EqualTo(TimeSpan.Zero));
                Assert.That(strategy.TryGetRetryDelay(TimeSpan.Zero, 1, out _), Is.False);
            }
        }

        [Test]
        public void MaxElapsedTime_WithZeroLimit_AllowsNoRetry()
        {
            var strategy = RetryStrategies.Constant(TimeSpan.Zero).WithMaxElapsedTime(TimeSpan.Zero);

            Assert.That(strategy.TryGetRetryDelay(TimeSpan.Zero, 0, out _), Is.False);
        }
    }
}
