// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Strategies.Modifiers
{
    using System;

    /// <summary>
    /// A retry strategy that allows the retries of another retry strategy only within a time limit.
    /// </summary>
    /// <remarks>
    /// This class wraps another retry strategy and allows a retry only while the time elapsed since the start of retry attempts is less than
    /// <see cref="MaxElapsedTime"/>. A delay that would end after the limit is shortened to end at the limit. The limit does not cancel an
    /// operation that is running.
    /// </remarks>
    public sealed class MaxElapsedTimeModifier : IRetryStrategy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MaxElapsedTimeModifier"/> class with a specified inner retry strategy and time limit.
        /// </summary>
        /// <param name="innerStrategy">
        /// The retry strategy whose retries are limited.
        /// </param>
        /// <param name="maxElapsedTime">
        /// The time since the start of retry attempts after which no further retry is allowed.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="innerStrategy"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="maxElapsedTime"/> is negative.
        /// </exception>
        public MaxElapsedTimeModifier(IRetryStrategy innerStrategy, TimeSpan maxElapsedTime)
        {
            if (innerStrategy is null)
                throw new ArgumentNullException(nameof(innerStrategy));
            if (maxElapsedTime < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(maxElapsedTime), maxElapsedTime, "The time limit must not be negative.");

            InnerStrategy = innerStrategy;
            MaxElapsedTime = maxElapsedTime;
        }

        /// <summary>
        /// Gets the retry strategy whose retries are limited.
        /// </summary>
        /// <value>
        /// The <see cref="IRetryStrategy"/> that decides the delays while the time limit has not been reached.
        /// </value>
        public IRetryStrategy InnerStrategy { get; }

        /// <summary>
        /// Gets the time since the start of retry attempts after which no further retry is allowed.
        /// </summary>
        /// <value>
        /// The time since the start of retry attempts after which no further retry is allowed.
        /// </value>
        public TimeSpan MaxElapsedTime { get; }

        /// <summary>
        /// Calculates the delay for the next retry attempt, enforcing the time limit.
        /// </summary>
        /// <param name="elapsed">
        /// The total time elapsed since the start of retry attempts.
        /// </param>
        /// <param name="retryCount">
        /// The number of retries made so far, not counting the initial attempt.
        /// </param>
        /// <param name="delay">
        /// When this method returns, contains the calculated delay for the next retry attempt. This parameter is passed uninitialized.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="elapsed"/> is less than <see cref="MaxElapsedTime"/> and the inner strategy indicates that a retry should be attempted; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryGetRetryDelay(TimeSpan elapsed, uint retryCount, out TimeSpan delay)
        {
            var remaining = MaxElapsedTime - elapsed;
            if (remaining > TimeSpan.Zero && InnerStrategy.TryGetRetryDelay(elapsed, retryCount, out delay))
            {
                if (delay > remaining)
                    delay = remaining;

                return true;
            }

            delay = default;
            return false;
        }
    }
}
