// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Internal
{
    using System;

    /// <summary>
    /// Supplies the outcome classification and callbacks that the executor applies to one execution.
    /// </summary>
    /// <typeparam name="T">
    /// The type returned by the operation.
    /// </typeparam>
    /// <remarks>
    /// Implementations are structs so that the executor is specialized for each policy shape without allocating an adapter.
    /// </remarks>
    internal interface IRetryHooks<T>
    {
        /// <summary>
        /// Determines whether an exception thrown by an attempt is retried.
        /// </summary>
        /// <param name="exception">
        /// The exception, which is not a caller cancellation.
        /// </param>
        /// <returns>
        /// <see langword="true"/> to retry the exception; otherwise, <see langword="false"/>.
        /// </returns>
        bool ShouldRetry(Exception exception);

        /// <summary>
        /// Determines whether a result returned by an attempt is retried.
        /// </summary>
        /// <param name="result">
        /// The result.
        /// </param>
        /// <returns>
        /// <see langword="true"/> to retry the result; otherwise, <see langword="false"/>.
        /// </returns>
        bool ShouldRetry(T result);

        /// <summary>
        /// Replaces the delay that the strategy proposes, if the policy overrides delays.
        /// </summary>
        /// <param name="context">
        /// The failed attempt, carrying the proposed delay.
        /// </param>
        /// <param name="delay">
        /// When this method returns <see langword="true"/>, the delay that replaces the proposed one.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the policy overrides the delay; otherwise, <see langword="false"/>.
        /// </returns>
        bool TryOverrideDelay(in RetryContext<T> context, out TimeSpan delay);

        /// <summary>
        /// Notifies the policy's handlers of an approved retry.
        /// </summary>
        /// <param name="context">
        /// The failed attempt, carrying the delay that execution waits.
        /// </param>
        void OnRetry(in RetryContext<T> context);

        /// <summary>
        /// Releases a result that execution does not return.
        /// </summary>
        /// <param name="result">
        /// The discarded result.
        /// </param>
        void OnDiscarded(T result);
    }
}
