// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Strategies
{
    using System;

    /// <summary>
    /// A retry strategy that linearly increases the delay before each retry attempt.
    /// </summary>
    /// <remarks>
    /// The <see cref="LinearBackoffStrategy"/> class calculates the delay between retry attempts by starting with an initial delay and then increasing it linearly with
    /// each subsequent retry. The delay grows steadily rather than rapidly, which suits operations whose retries should become gradually less
    /// frequent while failures continue.
    /// </remarks>
    public sealed class LinearBackoffStrategy : IRetryStrategy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LinearBackoffStrategy"/> class with a specified initial delay and step increment.
        /// </summary>
        /// <param name="initialDelay">
        /// The initial delay duration before the first retry attempt.
        /// </param>
        /// <param name="delayStep">
        /// The fixed amount of time by which the delay is incremented for each subsequent retry.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="initialDelay"/> or <paramref name="delayStep"/> is negative.
        /// </exception>
        public LinearBackoffStrategy(TimeSpan initialDelay, TimeSpan delayStep)
        {
            if (initialDelay < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(initialDelay), initialDelay, "The initial delay must not be negative.");
            if (delayStep < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(delayStep), delayStep, "The delay step must not be negative.");

            InitialDelay = initialDelay;
            DelayStep = delayStep;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinearBackoffStrategy"/> class with a specified initial delay.
        /// </summary>
        /// <param name="initialDelay">
        /// The initial delay duration before the first retry attempt.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="initialDelay"/> is negative.
        /// </exception>
        public LinearBackoffStrategy(TimeSpan initialDelay)
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
        /// Gets the fixed amount of time by which the delay is increased with each retry attempt.
        /// </summary>
        /// <value>
        /// The fixed amount of time by which the delay is increased with each retry attempt.
        /// </value>
        public TimeSpan DelayStep { get; }

        /// <summary>
        /// Calculates the delay for the next retry attempt, linearly increasing based on the number of attempts made so far.
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
            delay = retryCount != 0 && DelayStep.Ticks > (TimeSpan.MaxValue.Ticks - InitialDelay.Ticks) / retryCount
                ? TimeSpan.MaxValue
                : TimeSpan.FromTicks(InitialDelay.Ticks + DelayStep.Ticks * retryCount);

            return true;
        }
    }
}
