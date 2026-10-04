// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Internal
{
    using System;

    /// <summary>
    /// Applies a <see cref="RetryPolicy{T}"/>, which retries both exceptions and returned results.
    /// </summary>
    /// <typeparam name="T">
    /// The type returned by the operation.
    /// </typeparam>
    internal readonly struct ResultPolicyHooks<T> : IRetryHooks<T>
    {
        private readonly RetryPolicy<T> _policy;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResultPolicyHooks{T}"/> struct.
        /// </summary>
        /// <param name="policy">
        /// The policy to apply.
        /// </param>
        public ResultPolicyHooks(RetryPolicy<T> policy) => _policy = policy;

        /// <inheritdoc/>
        public bool ShouldRetry(Exception exception) => _policy.ExceptionFilter is null || _policy.ExceptionFilter(exception);

        /// <inheritdoc/>
        public bool ShouldRetry(T result) => _policy.ResultFilter(result);

        /// <inheritdoc/>
        public bool TryOverrideDelay(in RetryContext<T> context, out TimeSpan delay)
        {
            if (_policy.DelayOverride is null)
            {
                delay = default;
                return false;
            }

            delay = _policy.DelayOverride(context);
            return true;
        }

        /// <inheritdoc/>
        public void OnRetry(in RetryContext<T> context) => _policy.RetryHandler?.Invoke(context);

        /// <inheritdoc/>
        public void OnDiscarded(T result) => _policy.DiscardHandler?.Invoke(result);
    }
}
