// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Internal
{
    using System;

    /// <summary>
    /// Retries every exception, no result, and has no callbacks.
    /// </summary>
    /// <typeparam name="T">
    /// The type returned by the operation.
    /// </typeparam>
    internal readonly struct DefaultHooks<T> : IRetryHooks<T>
    {
        /// <inheritdoc/>
        public bool ShouldRetry(Exception exception) => true;

        /// <inheritdoc/>
        public bool ShouldRetry(T result) => false;

        /// <inheritdoc/>
        public bool TryOverrideDelay(in RetryContext<T> context, out TimeSpan delay)
        {
            delay = default;
            return false;
        }

        /// <inheritdoc/>
        public void OnRetry(in RetryContext<T> context) { }

        /// <inheritdoc/>
        public void OnDiscarded(T result) { }
    }
}
