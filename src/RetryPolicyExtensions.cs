// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience
{
    using System;

    /// <summary>
    /// Provides extension methods for retry policies whose configuration depends on the result type.
    /// </summary>
    public static class RetryPolicyExtensions
    {
        /// <summary>
        /// Creates a policy that disposes each result that execution does not return.
        /// </summary>
        /// <typeparam name="T">
        /// The disposable type returned by the operations the policy runs.
        /// </typeparam>
        /// <param name="policy">
        /// The policy to extend.
        /// </param>
        /// <returns>
        /// A new policy that invokes the discard handlers of <paramref name="policy"/> and then disposes the discarded result.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="policy"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// A <see langword="null"/> result is skipped. The result that execution returns is not disposed; it remains the caller's.
        /// See <see cref="RetryPolicy{T}.OnDiscarded(Action{T})"/> for when a result is discarded.
        /// </remarks>
        public static RetryPolicy<T> DisposeDiscarded<T>(this RetryPolicy<T> policy) where T : IDisposable?
        {
            if (policy is null)
                throw new ArgumentNullException(nameof(policy));

            return policy.OnDiscarded(DisposeHandler<T>.Instance);
        }

        /// <summary>
        /// Caches the handler that disposes results of one type.
        /// </summary>
        /// <typeparam name="T">
        /// The disposable result type.
        /// </typeparam>
        private static class DisposeHandler<T> where T : IDisposable?
        {
            /// <summary>
            /// The cached handler.
            /// </summary>
            internal static readonly Action<T> Instance = result => result?.Dispose();
        }
    }
}
