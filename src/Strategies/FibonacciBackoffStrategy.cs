// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Strategies
{
    using System;

    /// <summary>
    /// A retry strategy that increases the delay before each retry attempt based on the Fibonacci sequence.
    /// </summary>
    /// <remarks>
    /// The <see cref="FibonacciBackoffStrategy"/> class uses the Fibonacci sequence to adjust the wait time between retries, starting with an initial
    /// delay and scaling the delay between subsequent attempts according to Fibonacci numbers. This approach provides a more moderate
    /// and controlled increase in delay times compared to the <see cref="ExponentialBackoffStrategy"/> approach, making it suitable for scenarios where
    /// a less aggressive increase in delay is desired.
    /// </remarks>
    public sealed class FibonacciBackoffStrategy : IRetryStrategy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FibonacciBackoffStrategy"/> class with a specified initial delay and step increment.
        /// </summary>
        /// <param name="initialDelay">
        /// The initial delay duration before the first retry attempt.
        /// </param>
        /// <param name="delayStep">
        /// The fixed amount of time that is scaled by the Fibonacci sequence and added to the initial delay for each subsequent retry attempt.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="initialDelay"/> or <paramref name="delayStep"/> is negative.
        /// </exception>
        public FibonacciBackoffStrategy(TimeSpan initialDelay, TimeSpan delayStep)
        {
            if (initialDelay < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(initialDelay), initialDelay, "The initial delay must not be negative.");
            if (delayStep < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(delayStep), delayStep, "The delay step must not be negative.");

            InitialDelay = initialDelay;
            DelayStep = delayStep;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FibonacciBackoffStrategy"/> class with a specified initial delay.
        /// </summary>
        /// <param name="initialDelay">
        /// The initial delay duration before the first retry attempt.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="initialDelay"/> is negative.
        /// </exception>
        public FibonacciBackoffStrategy(TimeSpan initialDelay)
        {
            if (initialDelay < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(initialDelay), initialDelay, "The initial delay must not be negative.");

            InitialDelay = initialDelay;
            DelayStep = initialDelay;
        }

        /// <summary>
        /// Gets the initial delay duration before the first retry attempt.
        /// </summary>
        /// <value>
        /// The initial delay duration before the first retry attempt.
        /// </value>
        public TimeSpan InitialDelay { get; }

        /// <summary>
        /// Gets the fixed amount of time that is scaled by the Fibonacci sequence and added to the initial delay for each subsequent retry attempt.
        /// </summary>
        /// <value>
        /// The fixed amount of time that is scaled by the Fibonacci sequence and added to the initial delay for each subsequent retry attempt.
        /// </value>
        public TimeSpan DelayStep { get; }

        /// <summary>
        /// Calculates the delay for the next retry attempt based on the Fibonacci sequence.
        /// </summary>
        /// <param name="elapsed">
        /// The total time elapsed since the start of retry attempts. This parameter is ignored in this implementation.
        /// </param>
        /// <param name="retryCount">
        /// The number of retry attempts made so far.
        /// </param>
        /// <param name="delay">
        /// When this method returns, contains the calculated delay for the next retry attempt. This parameter is passed uninitialized.
        /// </param>
        /// <returns>
        /// Always returns <see langword="true"/>, indicating that a retry attempt should be made after the calculated <paramref name="delay"/>.
        /// </returns>
        public bool TryGetRetryDelay(TimeSpan elapsed, uint retryCount, out TimeSpan delay)
        {
            // A zero step never grows the delay; multiplying it by an overflowed Fibonacci number would produce NaN.
            if (DelayStep == TimeSpan.Zero)
            {
                delay = InitialDelay;
                return true;
            }

            var ticks = InitialDelay.Ticks + DelayStep.Ticks * FibonacciNumber(retryCount);
            delay = ticks >= TimeSpan.MaxValue.Ticks
                ? TimeSpan.MaxValue
                : TimeSpan.FromTicks((long)ticks);

            return true;
        }

        /// <summary>
        /// Calculates the Fibonacci number at a given position in the sequence using Binet's formula.
        /// </summary>
        /// <param name="n">
        /// The position in the Fibonacci sequence for which to calculate the number.
        /// </param>
        /// <returns>
        /// The Fibonacci number at the specified position.
        /// </returns>
        private static double FibonacciNumber(uint n)
        {
            if (n == 0) return 0;

            var sqrt5 = Math.Sqrt(5);
            var phi = (1 + sqrt5) / 2;
            return Math.Round(Math.Pow(phi, n) / sqrt5);
        }
    }
}
