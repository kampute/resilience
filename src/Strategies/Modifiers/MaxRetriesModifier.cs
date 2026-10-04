// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Strategies.Modifiers
{
    using System;

    /// <summary>
    /// A retry strategy that limits the number of retries of another retry strategy.
    /// </summary>
    /// <remarks>
    /// This class wraps another strategy and allows at most <see cref="MaxRetries"/> retries after the initial attempt. Once the limit is reached,
    /// no further retries are suggested.
    /// </remarks>
    public sealed class MaxRetriesModifier : IRetryStrategy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MaxRetriesModifier"/> class with a specified inner retry strategy and maximum number of retries.
        /// </summary>
        /// <param name="innerStrategy">
        /// The retry strategy whose retries are limited.
        /// </param>
        /// <param name="maxRetries">
        /// The maximum number of retries after the initial attempt. Zero allows no retry.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="innerStrategy"/> is <see langword="null"/>.
        /// </exception>
        public MaxRetriesModifier(IRetryStrategy innerStrategy, uint maxRetries)
        {
            InnerStrategy = innerStrategy ?? throw new ArgumentNullException(nameof(innerStrategy));
            MaxRetries = maxRetries;
        }

        /// <summary>
        /// Gets the retry strategy whose retries are limited.
        /// </summary>
        /// <value>
        /// The <see cref="IRetryStrategy"/> that decides the delays while the limit has not been reached.
        /// </value>
        public IRetryStrategy InnerStrategy { get; }

        /// <summary>
        /// Gets the maximum number of retries after the initial attempt.
        /// </summary>
        /// <value>
        /// The maximum number of retries after the initial attempt.
        /// </value>
        public uint MaxRetries { get; }

        /// <summary>
        /// Calculates the delay for the next retry attempt, enforcing the maximum number of retries.
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
        /// <see langword="true"/> if <paramref name="retryCount"/> is less than <see cref="MaxRetries"/> and the inner strategy indicates that a retry should be attempted; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryGetRetryDelay(TimeSpan elapsed, uint retryCount, out TimeSpan delay)
        {
            if (retryCount < MaxRetries & InnerStrategy.TryGetRetryDelay(elapsed, retryCount, out delay))
                return true;

            delay = default;
            return false;
        }
    }
}
