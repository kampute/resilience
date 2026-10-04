// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Strategies.Modifiers
{
    using System;

    /// <summary>
    /// A retry strategy that caps each delay of another retry strategy.
    /// </summary>
    /// <remarks>
    /// This class wraps another strategy and shortens any delay longer than <see cref="MaxDelay"/> to <see cref="MaxDelay"/>. It does not change
    /// whether a retry is allowed, so a growing strategy keeps retrying at <see cref="MaxDelay"/> once its delay reaches the cap; combine it with
    /// <see cref="MaxRetriesModifier"/> or <see cref="MaxElapsedTimeModifier"/> to stop the retries.
    /// </remarks>
    public sealed class MaxDelayModifier : IRetryStrategy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MaxDelayModifier"/> class with a specified inner retry strategy and longest delay.
        /// </summary>
        /// <param name="innerStrategy">
        /// The retry strategy whose delays are capped.
        /// </param>
        /// <param name="maxDelay">
        /// The longest delay before a retry.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="innerStrategy"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="maxDelay"/> is negative.
        /// </exception>
        public MaxDelayModifier(IRetryStrategy innerStrategy, TimeSpan maxDelay)
        {
            if (maxDelay < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(maxDelay), maxDelay, "The longest delay must not be negative.");

            InnerStrategy = innerStrategy ?? throw new ArgumentNullException(nameof(innerStrategy));
            MaxDelay = maxDelay;
        }

        /// <summary>
        /// Gets the retry strategy whose delays are capped.
        /// </summary>
        /// <value>
        /// The <see cref="IRetryStrategy"/> that decides whether to retry and the delay before the cap is applied.
        /// </value>
        public IRetryStrategy InnerStrategy { get; }

        /// <summary>
        /// Gets the longest delay before a retry.
        /// </summary>
        /// <value>
        /// The longest delay before a retry. It is zero or positive.
        /// </value>
        public TimeSpan MaxDelay { get; }

        /// <summary>
        /// Calculates the delay for the next retry attempt, capped at <see cref="MaxDelay"/>.
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
        /// <see langword="true"/> if the inner strategy indicates that a retry should be attempted; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryGetRetryDelay(TimeSpan elapsed, uint retryCount, out TimeSpan delay)
        {
            if (InnerStrategy.TryGetRetryDelay(elapsed, retryCount, out delay))
            {
                if (delay > MaxDelay)
                    delay = MaxDelay;

                return true;
            }

            delay = default;
            return false;
        }
    }
}
