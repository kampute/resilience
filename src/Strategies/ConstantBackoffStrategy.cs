// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Strategies
{
    using System;

    /// <summary>
    /// A retry strategy that waits the same delay before every retry.
    /// </summary>
    /// <remarks>
    /// The <see cref="ConstantBackoffStrategy"/> class provides a retry mechanism with a constant wait time between retries. This strategy is useful for scenarios where a predictable
    /// retry pattern is desired, applying the same delay duration between each retry attempt without regard to the number of attempts made or the total elapsed time.
    /// </remarks>
    public sealed class ConstantBackoffStrategy : IRetryStrategy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConstantBackoffStrategy"/> class with a specified delay duration between retry attempts.
        /// </summary>
        /// <param name="delay">
        /// The delay duration between retry attempts.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="delay"/> is negative.
        /// </exception>
        public ConstantBackoffStrategy(TimeSpan delay)
        {
            if (delay < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(delay), delay, "The delay must not be negative.");

            Delay = delay;
        }

        /// <summary>
        /// Gets the delay duration between retries.
        /// </summary>
        /// <value>
        /// The delay duration between retries.
        /// </value>
        public TimeSpan Delay { get; }

        /// <summary>
        /// Returns the same delay for every retry attempt.
        /// </summary>
        /// <param name="elapsed">
        /// The total time elapsed since the start of retry attempts. This parameter is ignored in this implementation.
        /// </param>
        /// <param name="retryCount">
        /// The number of retry attempts made so far. This parameter is ignored in this implementation.
        /// </param>
        /// <param name="delay">
        /// When this method returns, contains the calculated delay for the next retry attempt. This parameter is passed uninitialized.
        /// </param>
        /// <returns>
        /// Always returns <see langword="true"/>, indicating that a retry attempt should be made after the calculated <paramref name="delay"/>.
        /// </returns>
        public bool TryGetRetryDelay(TimeSpan elapsed, uint retryCount, out TimeSpan delay)
        {
            delay = Delay;
            return true;
        }
    }
}
