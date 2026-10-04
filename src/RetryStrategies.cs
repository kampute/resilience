// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience
{
    using Kampute.Resilience.Strategies;
    using System;

    /// <summary>
    /// Provides factory methods for the built-in retry strategies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Except for <see cref="None"/> and <c>Once</c>, all of the factory  methods return strategies that retry without limit. Chain
    /// <see cref="RetryStrategyExtensions.WithMaxRetries"/> and <see cref="RetryStrategyExtensions.WithMaxElapsedTime(IRetryStrategy, TimeSpan)"/>
    /// to limit them, <see cref="RetryStrategyExtensions.WithMaxDelay(IRetryStrategy, TimeSpan)"/> to cap their delays, and
    /// <see cref="RetryStrategyExtensions.WithJitter"/> to spread their delays, in any combination.
    /// </para>
    /// <para>
    /// Each method that takes a <see cref="TimeSpan"/> has an overload that takes the same duration as a number of milliseconds.
    /// Both forms require nonnegative durations.
    /// </para>
    /// </remarks>
    /// <example>
    /// This strategy waits about one second before the first retry and doubles the delay for each further retry, up to 30 seconds.
    /// It allows at most five retries, and none after two minutes:
    /// <code>
    /// var retry = RetryStrategies
    ///     .Exponential(TimeSpan.FromSeconds(1))
    ///     .WithMaxDelay(TimeSpan.FromSeconds(30))
    ///     .WithJitter(0.2)
    ///     .WithMaxRetries(5)
    ///     .WithMaxElapsedTime(TimeSpan.FromMinutes(2));
    /// </code>
    /// </example>
    public static class RetryStrategies
    {
        /// <summary>
        /// Gets a strategy that never retries.
        /// </summary>
        /// <value>
        /// A strategy that never retries.
        /// </value>
        public static IRetryStrategy None => NoRetryStrategy.Instance;

        /// <summary>
        /// Creates a strategy that retries once, after the specified delay.
        /// </summary>
        /// <param name="delay">
        /// The delay before the retry.
        /// </param>
        /// <returns>
        /// A strategy that allows a single retry.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="delay"/> is negative.
        /// </exception>
        public static IRetryStrategy Once(TimeSpan delay)
        {
            return new ConstantBackoffStrategy(delay).WithMaxRetries(1);
        }

        /// <summary>
        /// Creates a strategy that retries once, after the specified number of milliseconds.
        /// </summary>
        /// <param name="millisecondsDelay">
        /// The number of milliseconds to wait before the retry.
        /// </param>
        /// <returns>
        /// A strategy that allows a single retry.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="millisecondsDelay"/> is negative.
        /// </exception>
        public static IRetryStrategy Once(int millisecondsDelay)
        {
            if (millisecondsDelay < 0)
                throw new ArgumentOutOfRangeException(nameof(millisecondsDelay), millisecondsDelay, "The number of milliseconds must not be negative.");

            return Once(TimeSpan.FromMilliseconds(millisecondsDelay));
        }

        /// <summary>
        /// Creates a strategy that retries once, at the specified time.
        /// </summary>
        /// <param name="after">
        /// The time of the retry. The delay is computed from it when this method is called; a past time produces a zero delay.
        /// </param>
        /// <returns>
        /// A strategy that allows a single retry.
        /// </returns>
        public static IRetryStrategy Once(DateTimeOffset after)
        {
            var delay = after - DateTimeOffset.UtcNow;
            if (delay < TimeSpan.Zero)
                delay = TimeSpan.Zero;

            return Once(delay);
        }

        /// <summary>
        /// Creates a strategy that waits the same delay before every retry.
        /// </summary>
        /// <param name="delay">
        /// The delay before each retry.
        /// </param>
        /// <returns>
        /// A strategy that retries without limit.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="delay"/> is negative.
        /// </exception>
        public static IRetryStrategy Constant(TimeSpan delay)
        {
            return new ConstantBackoffStrategy(delay);
        }

        /// <summary>
        /// Creates a strategy that waits the same number of milliseconds before every retry.
        /// </summary>
        /// <param name="millisecondsDelay">
        /// The number of milliseconds to wait before each retry.
        /// </param>
        /// <returns>
        /// A strategy that retries without limit.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="millisecondsDelay"/> is negative.
        /// </exception>
        public static IRetryStrategy Constant(int millisecondsDelay)
        {
            if (millisecondsDelay < 0)
                throw new ArgumentOutOfRangeException(nameof(millisecondsDelay), millisecondsDelay, "The number of milliseconds must not be negative.");

            return Constant(TimeSpan.FromMilliseconds(millisecondsDelay));
        }

        /// <summary>
        /// Creates a strategy whose delay grows by the initial delay before each further retry.
        /// </summary>
        /// <param name="initialDelay">
        /// The delay before the first retry, which is also the amount added for each further retry.
        /// </param>
        /// <returns>
        /// A strategy that retries without limit.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="initialDelay"/> is negative.
        /// </exception>
        public static IRetryStrategy Linear(TimeSpan initialDelay)
        {
            return new LinearBackoffStrategy(initialDelay);
        }

        /// <summary>
        /// Creates a strategy whose delay, in milliseconds, grows by the initial delay before each further retry.
        /// </summary>
        /// <param name="millisecondsInitialDelay">
        /// The number of milliseconds to wait before the first retry, which is also the amount added for each further retry.
        /// </param>
        /// <returns>
        /// A strategy that retries without limit.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="millisecondsInitialDelay"/> is negative.
        /// </exception>
        public static IRetryStrategy Linear(int millisecondsInitialDelay)
        {
            if (millisecondsInitialDelay < 0)
                throw new ArgumentOutOfRangeException(nameof(millisecondsInitialDelay), millisecondsInitialDelay, "The number of milliseconds must not be negative.");

            return Linear(TimeSpan.FromMilliseconds(millisecondsInitialDelay));
        }

        /// <summary>
        /// Creates a strategy whose delay grows by a fixed step before each further retry.
        /// </summary>
        /// <param name="initialDelay">
        /// The delay before the first retry.
        /// </param>
        /// <param name="delayStep">
        /// The amount added to the delay for each further retry.
        /// </param>
        /// <returns>
        /// A strategy that retries without limit.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="initialDelay"/> or <paramref name="delayStep"/> is negative.
        /// </exception>
        public static IRetryStrategy Linear(TimeSpan initialDelay, TimeSpan delayStep)
        {
            return new LinearBackoffStrategy(initialDelay, delayStep);
        }

        /// <summary>
        /// Creates a strategy whose delay, in milliseconds, grows by a fixed step before each further retry.
        /// </summary>
        /// <param name="millisecondsInitialDelay">
        /// The number of milliseconds to wait before the first retry.
        /// </param>
        /// <param name="millisecondsDelayStep">
        /// The number of milliseconds added to the delay for each further retry.
        /// </param>
        /// <returns>
        /// A strategy that retries without limit.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="millisecondsInitialDelay"/> or <paramref name="millisecondsDelayStep"/> is negative.
        /// </exception>
        public static IRetryStrategy Linear(int millisecondsInitialDelay, int millisecondsDelayStep)
        {
            if (millisecondsInitialDelay < 0)
                throw new ArgumentOutOfRangeException(nameof(millisecondsInitialDelay), millisecondsInitialDelay, "The number of milliseconds must not be negative.");
            if (millisecondsDelayStep < 0)
                throw new ArgumentOutOfRangeException(nameof(millisecondsDelayStep), millisecondsDelayStep, "The number of milliseconds must not be negative.");

            return Linear(TimeSpan.FromMilliseconds(millisecondsInitialDelay), TimeSpan.FromMilliseconds(millisecondsDelayStep));
        }

        /// <summary>
        /// Creates a strategy whose delay is multiplied by a fixed factor before each further retry.
        /// </summary>
        /// <param name="initialDelay">
        /// The delay before the first retry.
        /// </param>
        /// <param name="multiplier">
        /// The factor by which the delay is multiplied for each further retry (optional). The default is 2, which doubles the delay.
        /// </param>
        /// <returns>
        /// A strategy that retries without limit.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="initialDelay"/> is negative, or <paramref name="multiplier"/> is less than 1.
        /// </exception>
        public static IRetryStrategy Exponential(TimeSpan initialDelay, double multiplier = 2.0)
        {
            return new ExponentialBackoffStrategy(initialDelay, multiplier);
        }

        /// <summary>
        /// Creates a strategy whose delay, in milliseconds, is multiplied by a fixed factor before each further retry.
        /// </summary>
        /// <param name="millisecondsInitialDelay">
        /// The number of milliseconds to wait before the first retry.
        /// </param>
        /// <param name="multiplier">
        /// The factor by which the delay is multiplied for each further retry (optional). The default is 2, which doubles the delay.
        /// </param>
        /// <returns>
        /// A strategy that retries without limit.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="millisecondsInitialDelay"/> is negative, or <paramref name="multiplier"/> is less than 1.
        /// </exception>
        public static IRetryStrategy Exponential(int millisecondsInitialDelay, double multiplier = 2.0)
        {
            if (millisecondsInitialDelay < 0)
                throw new ArgumentOutOfRangeException(nameof(millisecondsInitialDelay), millisecondsInitialDelay, "The number of milliseconds must not be negative.");

            return Exponential(TimeSpan.FromMilliseconds(millisecondsInitialDelay), multiplier);
        }

        /// <summary>
        /// Creates a strategy whose delay grows with the Fibonacci sequence, scaled by the initial delay.
        /// </summary>
        /// <param name="initialDelay">
        /// The delay before the first retry, which is also the amount scaled by the Fibonacci sequence for each further retry.
        /// </param>
        /// <returns>
        /// A strategy that retries without limit.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="initialDelay"/> is negative.
        /// </exception>
        public static IRetryStrategy Fibonacci(TimeSpan initialDelay)
        {
            return new FibonacciBackoffStrategy(initialDelay);
        }

        /// <summary>
        /// Creates a strategy whose delay, in milliseconds, grows with the Fibonacci sequence, scaled by the initial delay.
        /// </summary>
        /// <param name="millisecondsInitialDelay">
        /// The number of milliseconds to wait before the first retry, which is also the amount scaled by the Fibonacci sequence for each further retry.
        /// </param>
        /// <returns>
        /// A strategy that retries without limit.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="millisecondsInitialDelay"/> is negative.
        /// </exception>
        public static IRetryStrategy Fibonacci(int millisecondsInitialDelay)
        {
            if (millisecondsInitialDelay < 0)
                throw new ArgumentOutOfRangeException(nameof(millisecondsInitialDelay), millisecondsInitialDelay, "The number of milliseconds must not be negative.");

            return Fibonacci(TimeSpan.FromMilliseconds(millisecondsInitialDelay));
        }

        /// <summary>
        /// Creates a strategy whose delay grows with the Fibonacci sequence, scaled by a fixed step.
        /// </summary>
        /// <param name="initialDelay">
        /// The delay before the first retry.
        /// </param>
        /// <param name="delayStep">
        /// The amount scaled by the Fibonacci sequence and added to the initial delay for each further retry.
        /// </param>
        /// <returns>
        /// A strategy that retries without limit.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="initialDelay"/> or <paramref name="delayStep"/> is negative.
        /// </exception>
        public static IRetryStrategy Fibonacci(TimeSpan initialDelay, TimeSpan delayStep)
        {
            return new FibonacciBackoffStrategy(initialDelay, delayStep);
        }

        /// <summary>
        /// Creates a strategy whose delay, in milliseconds, grows with the Fibonacci sequence, scaled by a fixed step.
        /// </summary>
        /// <param name="millisecondsInitialDelay">
        /// The number of milliseconds to wait before the first retry.
        /// </param>
        /// <param name="millisecondsDelayStep">
        /// The number of milliseconds scaled by the Fibonacci sequence and added to the initial delay for each further retry.
        /// </param>
        /// <returns>
        /// A strategy that retries without limit.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="millisecondsInitialDelay"/> or <paramref name="millisecondsDelayStep"/> is negative.
        /// </exception>
        public static IRetryStrategy Fibonacci(int millisecondsInitialDelay, int millisecondsDelayStep)
        {
            if (millisecondsInitialDelay < 0)
                throw new ArgumentOutOfRangeException(nameof(millisecondsInitialDelay), millisecondsInitialDelay, "The number of milliseconds must not be negative.");
            if (millisecondsDelayStep < 0)
                throw new ArgumentOutOfRangeException(nameof(millisecondsDelayStep), millisecondsDelayStep, "The number of milliseconds must not be negative.");

            return Fibonacci(TimeSpan.FromMilliseconds(millisecondsInitialDelay), TimeSpan.FromMilliseconds(millisecondsDelayStep));
        }
    }
}
