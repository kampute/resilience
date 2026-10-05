// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Strategies.Modifiers
{
    using System;

    /// <summary>
    /// A retry strategy that adds random jitter to the delays of another retry strategy.
    /// </summary>
    /// <remarks>
    /// Jitter spreads retry attempts over time. When many operations fail at the same moment and share a strategy, jitter keeps them from all
    /// retrying at the same moment again.
    /// </remarks>
    public sealed class JitterModifier : IRetryStrategy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JitterModifier"/> class with a specified inner retry strategy and jitter factor.
        /// </summary>
        /// <param name="innerStrategy">
        /// The retry strategy whose delays are jittered.
        /// </param>
        /// <param name="jitterFactor">
        /// The factor to apply to the delay to introduce jitter, represented as a value between 0 and 1.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="innerStrategy"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="jitterFactor"/> is not between 0 and 1.
        /// </exception>
        /// <remarks>
        /// The jitter factor allows fine-tuning of the randomness applied to the retry delay, enabling a balance between predictability and the
        /// benefits of desynchronization. It is a double value between 0 and 1 that determines the maximum proportion of the delay that can be
        /// adjusted randomly to introduce jitter. A value of 0 means no jitter, while 1 allows the delay to vary by up to ±100% of the base delay.
        /// </remarks>
        public JitterModifier(IRetryStrategy innerStrategy, double jitterFactor)
        {
            if (double.IsNaN(jitterFactor) || jitterFactor < 0.0 || jitterFactor > 1.0)
                throw new ArgumentOutOfRangeException(nameof(jitterFactor), "Jitter factor must be a value between 0 and 1, inclusive.");

            InnerStrategy = innerStrategy ?? throw new ArgumentNullException(nameof(innerStrategy));
            JitterFactor = jitterFactor;
        }

        /// <summary>
        /// Gets the retry strategy whose delays are jittered.
        /// </summary>
        /// <value>
        /// The <see cref="IRetryStrategy"/> that decides whether to retry and the delay to which jitter is added.
        /// </value>
        public IRetryStrategy InnerStrategy { get; }

        /// <summary>
        /// Gets the factor to apply to the delay to introduce jitter.
        /// </summary>
        /// <value>
        /// The factor to apply to the delay to introduce jitter. It is a floating-point number between 0 and 1, inclusive.
        /// </value>
        public double JitterFactor { get; }

        /// <summary>
        /// Calculates the delay for the next retry attempt, adding random jitter based on the jitter factor to the delay provided by the inner strategy.
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
                var sample = SharedRandom.NextDouble();
                var ticks = delay.Ticks + delay.Ticks * JitterFactor * (2 * sample - 1);
                if (double.IsNaN(ticks) || ticks >= TimeSpan.MaxValue.Ticks)
                    delay = TimeSpan.MaxValue;
                else if (ticks <= TimeSpan.MinValue.Ticks)
                    delay = TimeSpan.MinValue;
                else
                    delay = TimeSpan.FromTicks((long)ticks);

                return true;
            }
            return false;
        }

        /// <summary>
        /// Supplies random samples that are independent between modifiers and safe to use from any thread.
        /// </summary>
        /// <remarks>
        /// Modifiers share one source rather than each owning a <see cref="Random"/>: on .NET Framework, instances created by the parameterless
        /// constructor within the same clock tick share a seed and produce the same sequence, which would make the retries of separately
        /// created strategies coincide.
        /// </remarks>
        private static class SharedRandom
        {
#if NET6_0_OR_GREATER
            /// <summary>
            /// Returns a random number between 0.0, inclusive, and 1.0, exclusive.
            /// </summary>
            /// <returns>
            /// The random number.
            /// </returns>
            internal static double NextDouble() => Random.Shared.NextDouble();
#else
            private static readonly Random Seeds = new(Guid.NewGuid().GetHashCode());

            [ThreadStatic]
            private static Random? _threadRandom;

            /// <summary>
            /// Returns a random number between 0.0, inclusive, and 1.0, exclusive.
            /// </summary>
            /// <returns>
            /// The random number.
            /// </returns>
            /// <remarks>
            /// Each thread draws from its own generator, seeded from a process-wide generator, so that sampling takes no lock.
            /// </remarks>
            internal static double NextDouble()
            {
                var random = _threadRandom;
                if (random is null)
                {
                    int seed;
                    lock (Seeds)
                    {
                        seed = Seeds.Next();
                    }
                    _threadRandom = random = new Random(seed);
                }
                return random.NextDouble();
            }
#endif
        }
    }
}
