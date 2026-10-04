namespace Kampute.Resilience.Test
{
    using Kampute.Resilience.Strategies;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    [TestFixture]
    public class RetryStrategiesTests
    {
        private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

        [Test]
        public void None_NeverRetries()
        {
            Assert.That(RetryStrategies.None.TryGetRetryDelay(TimeSpan.Zero, 0, out _), Is.False);
        }

        [Test]
        public void Once_RetriesOnceAfterTheDelay()
        {
            var strategy = RetryStrategies.Once(Second);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(strategy.TryGetRetryDelay(TimeSpan.Zero, 0, out var delay), Is.True);
                Assert.That(delay, Is.EqualTo(Second));
                Assert.That(strategy.TryGetRetryDelay(TimeSpan.Zero, 1, out _), Is.False);
            }
        }

        [Test]
        public void Once_WithTime_RetriesOnceAtThatTime()
        {
            var strategy = RetryStrategies.Once(DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(strategy.TryGetRetryDelay(TimeSpan.Zero, 0, out var delay), Is.True);
                Assert.That(delay, Is.EqualTo(TimeSpan.FromMinutes(1)).Within(TimeSpan.FromSeconds(5)));
                Assert.That(strategy.TryGetRetryDelay(TimeSpan.Zero, 1, out _), Is.False);
            }
        }

        [Test]
        public void Factories_CreateTheBuiltInStrategies()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(RetryStrategies.Constant(Second), Is.TypeOf<ConstantBackoffStrategy>());
                Assert.That(RetryStrategies.Linear(Second), Is.TypeOf<LinearBackoffStrategy>());
                Assert.That(RetryStrategies.Linear(Second, Second), Is.TypeOf<LinearBackoffStrategy>());
                Assert.That(RetryStrategies.Fibonacci(Second), Is.TypeOf<FibonacciBackoffStrategy>());
                Assert.That(RetryStrategies.Fibonacci(Second, Second), Is.TypeOf<FibonacciBackoffStrategy>());
                Assert.That(RetryStrategies.Exponential(Second), Is.TypeOf<ExponentialBackoffStrategy>());
            }
        }

        private static IEnumerable<TestCaseData> MillisecondFactories()
        {
            yield return Case("Once(250)", () => RetryStrategies.Once(250), 0, 250);
            yield return Case("Constant(0)", () => RetryStrategies.Constant(0), 5, 0);
            yield return Case("Constant(250)", () => RetryStrategies.Constant(250), 5, 250);
            yield return Case("Linear(100)", () => RetryStrategies.Linear(100), 2, 300);
            yield return Case("Linear(100, 50)", () => RetryStrategies.Linear(100, 50), 2, 200);
            yield return Case("Exponential(100)", () => RetryStrategies.Exponential(100), 3, 800);
            yield return Case("Exponential(100, 3)", () => RetryStrategies.Exponential(100, 3.0), 2, 900);
            yield return Case("Fibonacci(100)", () => RetryStrategies.Fibonacci(100), 3, 300);
            yield return Case("Fibonacci(100, 10)", () => RetryStrategies.Fibonacci(100, 10), 3, 120);

            static TestCaseData Case(string name, Func<IRetryStrategy> factory, uint retryCount, int expectedDelayMs)
                => new TestCaseData(factory, retryCount, expectedDelayMs).SetName($"Factories_WithMilliseconds_ReadTheArgumentsAsMilliseconds({name})");
        }

        [TestCaseSource(nameof(MillisecondFactories))]
        public void Factories_WithMilliseconds_ReadTheArgumentsAsMilliseconds(Func<IRetryStrategy> factory, uint retryCount, int expectedDelayMs)
        {
            var strategy = factory();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(strategy.TryGetRetryDelay(TimeSpan.Zero, retryCount, out var delay), Is.True);
                Assert.That(delay, Is.EqualTo(TimeSpan.FromMilliseconds(expectedDelayMs)));
            }
        }

        private static IEnumerable<TestCaseData> InvalidMillisecondFactories()
        {
            yield return Case("Once(-1)", () => RetryStrategies.Once(-1), "millisecondsDelay");
            yield return Case("Constant(-1)", () => RetryStrategies.Constant(-1), "millisecondsDelay");
            yield return Case("Linear(-1)", () => RetryStrategies.Linear(-1), "millisecondsInitialDelay");
            yield return Case("Linear(-1, 0)", () => RetryStrategies.Linear(-1, 0), "millisecondsInitialDelay");
            yield return Case("Linear(0, -1)", () => RetryStrategies.Linear(0, -1), "millisecondsDelayStep");
            yield return Case("Exponential(-1)", () => RetryStrategies.Exponential(-1), "millisecondsInitialDelay");
            yield return Case("Exponential(0, 0.5)", () => RetryStrategies.Exponential(0, 0.5), "multiplier");
            yield return Case("Fibonacci(-1)", () => RetryStrategies.Fibonacci(-1), "millisecondsInitialDelay");
            yield return Case("Fibonacci(-1, 0)", () => RetryStrategies.Fibonacci(-1, 0), "millisecondsInitialDelay");
            yield return Case("Fibonacci(0, -1)", () => RetryStrategies.Fibonacci(0, -1), "millisecondsDelayStep");

            static TestCaseData Case(string name, Action factory, string expectedParamName)
                => new TestCaseData(factory, expectedParamName).SetName($"Factories_WithInvalidMilliseconds_ThrowArgumentOutOfRangeException({name})");
        }

        [TestCaseSource(nameof(InvalidMillisecondFactories))]
        public void Factories_WithInvalidMilliseconds_ThrowArgumentOutOfRangeException(Action factory, string expectedParamName)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(factory);

            Assert.That(ex.ParamName, Is.EqualTo(expectedParamName));
        }

        [Test]
        public void Factories_CreateStrategiesWithoutLimits()
        {
            var strategies = new[]
            {
                RetryStrategies.Constant(Second),
                RetryStrategies.Linear(Second),
                RetryStrategies.Fibonacci(Second),
                RetryStrategies.Exponential(Second, 1.0),
            };

            Assert.That(strategies, Has.All.Matches<IRetryStrategy>(strategy => strategy.TryGetRetryDelay(TimeSpan.FromDays(365), 1000, out _)));
        }

        [Test]
        public void GrowingDelays_SaturateInsteadOfOverflowing()
        {
            var strategies = new[]
            {
                RetryStrategies.Linear(TimeSpan.FromDays(1)),
                RetryStrategies.Fibonacci(Second),
                RetryStrategies.Exponential(Second),
                RetryStrategies.Exponential(Second).WithJitter(0.5),
            };

            foreach (var strategy in strategies)
            {
                Assert.That(strategy.TryGetRetryDelay(TimeSpan.Zero, uint.MaxValue, out var delay), Is.True, strategy.GetType().Name);
                Assert.That(delay, Is.GreaterThanOrEqualTo(TimeSpan.FromDays(1_000_000)), strategy.GetType().Name);
            }
        }

        [Test]
        public void LinearDelay_PreservesTickPrecision()
        {
            var strategy = RetryStrategies.Linear(TimeSpan.FromTicks(1), TimeSpan.FromTicks(2));

            Assert.That(strategy.TryGetRetryDelay(TimeSpan.Zero, 3, out var delay), Is.True);
            Assert.That(delay, Is.EqualTo(TimeSpan.FromTicks(7)));
        }

        [Test]
        public void GrowingAndJitteredDelays_PreserveTickPrecision()
        {
            static TimeSpan Delay(IRetryStrategy strategy, uint retryCount)
            {
                strategy.TryGetRetryDelay(TimeSpan.Zero, retryCount, out var delay);
                return delay;
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(Delay(RetryStrategies.Exponential(TimeSpan.FromTicks(3)), 2), Is.EqualTo(TimeSpan.FromTicks(12)));
                Assert.That(Delay(RetryStrategies.Fibonacci(TimeSpan.FromTicks(1), TimeSpan.FromTicks(2)), 3), Is.EqualTo(TimeSpan.FromTicks(5)));
                Assert.That(Delay(RetryStrategies.Constant(TimeSpan.FromTicks(7)).WithJitter(0), 0), Is.EqualTo(TimeSpan.FromTicks(7)));
            }
        }

        [Test]
        public void MillisecondOverloads_PreserveLargestDuration()
        {
            var expectedDelay = TimeSpan.FromTicks((long)int.MaxValue * TimeSpan.TicksPerMillisecond);
            var strategies = new[]
            {
                RetryStrategies.Once(int.MaxValue),
                RetryStrategies.Constant(int.MaxValue),
                RetryStrategies.Linear(int.MaxValue),
                RetryStrategies.Linear(int.MaxValue, int.MaxValue),
                RetryStrategies.Fibonacci(int.MaxValue),
                RetryStrategies.Fibonacci(int.MaxValue, int.MaxValue),
                RetryStrategies.Exponential(int.MaxValue),
                RetryStrategies.Constant(int.MaxValue).WithMaxDelay(int.MaxValue).WithMaxElapsedTime(int.MaxValue),
            };

            foreach (var strategy in strategies)
            {
                Assert.That(strategy.TryGetRetryDelay(TimeSpan.Zero, 0, out var delay), Is.True);
                Assert.That(delay, Is.EqualTo(expectedDelay));
            }
        }

        [Test]
        public async Task Session_WithSaturatedDelay_StopsRetrying()
        {
            var session = RetryStrategies.Exponential(TimeSpan.FromMilliseconds(1), 1e12).StartSession();

            var results = new[]
            {
                await session.WaitToRetryAsync(CancellationToken.None),
                await session.WaitToRetryAsync(CancellationToken.None),
                await session.WaitToRetryAsync(CancellationToken.None),
            };

            Assert.That(results, Is.EqualTo(new[] { true, false, false }));
        }

        [Test]
        public void Exponential_DefaultsToMultiplierTwo()
        {
            var strategy = (ExponentialBackoffStrategy)RetryStrategies.Exponential(Second);

            Assert.That(strategy.Multiplier, Is.EqualTo(2.0));
        }

        [Test]
        public void Modifiers_CombineInAnyOrder()
        {
            var strategy = RetryStrategies.Constant(Second)
                .WithJitter(0.2)
                .WithMaxRetries(2)
                .WithMaxElapsedTime(TimeSpan.FromMinutes(1));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(strategy.TryGetRetryDelay(TimeSpan.Zero, 1, out var delay), Is.True);
                Assert.That(delay, Is.EqualTo(Second).Within(TimeSpan.FromMilliseconds(200)));
                Assert.That(strategy.TryGetRetryDelay(TimeSpan.Zero, 2, out _), Is.False);
                Assert.That(strategy.TryGetRetryDelay(TimeSpan.FromMinutes(2), 0, out _), Is.False);
            }
        }
    }
}
