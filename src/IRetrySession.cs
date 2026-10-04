// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the retry state of one operation: it decides whether the operation is retried after a failure, and waits before the retry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A session is created for one operation and is used for all its failures, so that the retries it counts and the time it
    /// measures cover the whole operation. <see cref="RetrySession"/> applies an <see cref="IRetryStrategy"/>; other implementations can take the
    /// decision from elsewhere, such as the state of the application.
    /// </para>
    /// <para>
    /// Sessions serve retry loops that you write yourself. <see cref="RetryPolicy"/> and the <c>Execute</c> methods of a strategy keep their
    /// own retry state and do not use a session. To base the delay on the failure, such as a retry time that it suggests, use
    /// <see cref="RetryPolicy.OverrideDelay(Func{RetryContext, TimeSpan})"/>.
    /// </para>
    /// </remarks>
    public interface IRetrySession
    {
        /// <summary>
        /// Determines whether the operation should be retried and, if so, waits for the appropriate time before the retry.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that can be used to cancel the wait.
        /// </param>
        /// <returns>
        /// A task that resolves to <see langword="true"/> if a retry should be attempted after the wait; otherwise, <see langword="false"/>.
        /// </returns>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting.
        /// </exception>
        /// <remarks>
        /// Implementations can base the decision on more than elapsed time and retries, such as information carried by the failure or the state
        /// of the application, and must observe <paramref name="cancellationToken"/>.
        /// </remarks>
        Task<bool> WaitToRetryAsync(CancellationToken cancellationToken);
    }
}
