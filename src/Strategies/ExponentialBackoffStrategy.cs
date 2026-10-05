// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Strategies
{
    using System;

    /// <summary>
    /// A retry strategy that exponentially increases the delay before each retry attempt.
    /// </summary>
    /// <remarks>
    /// The <see cref="ExponentialBackoffStrategy"/> class calculates the delay between retry attempts by starting with an initial delay and then increasing it exponentially
    /// with each subsequent retry. It suits operations whose retries should become quickly less frequent while failures continue, giving the
    /// cause of the failure more time to clear before each further attempt.
    /// </remarks>
    public sealed class ExponentialBackoffStrategy : IRetryStrategy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExponentialBackoffStrategy"/> class with a specified initial delay and multiplier.
        /// </summary>
        /// <param name="initialDelay">
        /// The initial delay duration before the first retry attempt.
        /// </param>
        /// <param name="multiplier">
        /// The factor by which the delay is multiplied for each further retry.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="initialDelay"/> is negative, or <paramref name="multiplier"/> is less than 1 or is <see cref="double.NaN"/>.
        /// </exception>
        public ExponentialBackoffStrategy(TimeSpan initialDelay, double multiplier)
        {
            if (initialDelay < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(initialDelay), initialDelay, "The initial delay must not be negative.");
            if (double.IsNaN(multiplier) || multiplier < 1.0)
                throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be at least 1.");

            InitialDelay = initialDelay;
            Multiplier = multiplier;
        }

        /// <summary>
        /// Gets the initial delay duration before the first retry attempt.
        /// </summary>
        /// <value>
        /// The initial delay duration before the first retry attempt.
        /// </value>
        public TimeSpan InitialDelay { get; }

        /// <summary>
        /// Gets the factor by which the delay is multiplied for each further retry.
        /// </summary>
        /// <value>
        /// The factor by which the delay is multiplied for each further retry. It is at least 1.
        /// </value>
        public double Multiplier { get; }

        /// <summary>
        /// Calculates the delay for the next retry attempt, exponentially increasing based on the number of attempts made so far.
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
            // A zero delay never grows; multiplying it by an overflowed power would produce NaN.
            if (InitialDelay == TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
                return true;
            }

            var ticks = InitialDelay.Ticks * Math.Pow(Multiplier, retryCount);
            delay = ticks >= TimeSpan.MaxValue.Ticks
                ? TimeSpan.MaxValue
                : TimeSpan.FromTicks((long)ticks);

            return true;
        }
    }
}
