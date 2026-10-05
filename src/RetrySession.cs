// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience
{
    using Kampute.Resilience.Internal;
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the retry state of one operation that a retry strategy governs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The session counts the retries and measures the time since it was created, and asks its <see cref="Strategy"/> for the delay before each
    /// retry. Use one session per operation; the strategy can be shared.
    /// </para>
    /// <para>
    /// A derived class can take the decision or the delay from elsewhere, such as a retry time that the failure suggests or the state of the
    /// application, by overriding <see cref="TryGetRetryDelay"/>. The session still counts the retries and applies its delay limit.
    /// </para>
    /// <para>
    /// The longest delay a session waits is <see cref="int.MaxValue"/> milliseconds (about 24.8 days), the limit of <see cref="Task.Delay(TimeSpan, CancellationToken)"/>
    /// on .NET Framework. If the strategy, or an override of <see cref="TryGetRetryDelay"/>, returns a longer delay, the session does not retry.
    /// </para>
    /// </remarks>
    public class RetrySession : IRetrySession
    {
        private readonly Stopwatch _timer = Stopwatch.StartNew();
        private uint _retryCount = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="RetrySession"/> class with a specified retry strategy.
        /// </summary>
        /// <param name="strategy">
        /// The retry strategy that decides the delay before each retry.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> is <see langword="null"/>.
        /// </exception>
        public RetrySession(IRetryStrategy strategy)
        {
            Strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        }

        /// <summary>
        /// Gets the retry strategy of this session.
        /// </summary>
        /// <value>
        /// The <see cref="IRetryStrategy"/> that decides the delay before each retry.
        /// </value>
        public virtual IRetryStrategy Strategy { get; }

        /// <summary>
        /// Gets the number of retries this session has allowed.
        /// </summary>
        /// <value>
        /// The number of retries allowed since the session was created or last reset, not counting the initial attempt.
        /// </value>
        public virtual uint RetryCount => _retryCount;

        /// <summary>
        /// Gets the time elapsed since the session was created or last reset.
        /// </summary>
        /// <value>
        /// The elapsed time as a <see cref="TimeSpan"/>.
        /// </value>
        public virtual TimeSpan Elapsed => _timer.Elapsed;

        /// <summary>
        /// Determines whether another retry is allowed and, if so, asynchronously waits for the delay before it.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that can be used to cancel the wait.
        /// </param>
        /// <returns>
        /// A task that resolves to <see langword="true"/> if a retry should be attempted after the wait; otherwise, <see langword="false"/>.
        /// </returns>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled.
        /// </exception>
        /// <remarks>
        /// <see cref="TryGetRetryDelay"/> decides whether to retry and the delay; by default, the <see cref="Strategy"/> does.
        /// </remarks>
        public virtual async Task<bool> WaitToRetryAsync(CancellationToken cancellationToken)
        {
            if (!TryScheduleRetry(out var delay))
                return false;

            await RetryDelay.WaitAsync(delay, cancellationToken).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Determines whether another retry is allowed and, if so, blocks the calling thread for the delay before it.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that can be used to cancel the wait.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if a retry should be attempted after the wait; otherwise, <see langword="false"/>.
        /// </returns>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled. A wait in progress ends as soon as the token is canceled.
        /// </exception>
        /// <remarks>
        /// <see cref="TryGetRetryDelay"/> decides whether to retry and the delay; by default, the <see cref="Strategy"/> does.
        /// </remarks>
        public virtual bool WaitToRetry(CancellationToken cancellationToken)
        {
            if (!TryScheduleRetry(out var delay))
                return false;

            RetryDelay.Wait(delay, cancellationToken);
            return true;
        }

        /// <summary>
        /// Resets the session to its initial state: no retries, and the elapsed time restarted.
        /// </summary>
        public virtual void Reset()
        {
            _timer.Restart();
            _retryCount = 0;
        }

        /// <summary>
        /// Updates the state of the session when a retry is allowed.
        /// </summary>
        /// <remarks>
        /// This method is called when <see cref="TryGetRetryDelay"/> allows another retry with a delay that the session can wait, before the wait
        /// begins. The base implementation counts the retry.
        /// </remarks>
        protected virtual void OnRetryScheduled()
        {
            ++_retryCount;
        }

        /// <summary>
        /// Decides whether another retry is allowed and, if so, the delay before it.
        /// </summary>
        /// <param name="delay">
        /// When this method returns <see langword="true"/>, the delay to wait before the next retry.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if another retry is allowed; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <see cref="WaitToRetry(CancellationToken)"/> and <see cref="WaitToRetryAsync(CancellationToken)"/> call this method once for each
        /// requested retry. The base implementation asks the <see cref="Strategy"/>, passing <see cref="Elapsed"/> and <see cref="RetryCount"/>.
        /// Override it to base the decision or the delay on more than the strategy; call the base implementation to keep the strategy's decision
        /// and limits.
        /// </para>
        /// <para>
        /// The session applies the result: a negative delay is waited as zero, a delay longer than <see cref="int.MaxValue"/> milliseconds stops
        /// retrying, and an allowed retry is counted through <see cref="OnRetryScheduled"/>. An override therefore does not count the retry itself.
        /// </para>
        /// </remarks>
        protected virtual bool TryGetRetryDelay(out TimeSpan delay)
            => Strategy.TryGetRetryDelay(Elapsed, RetryCount, out delay);

        /// <summary>
        /// Takes the decision of <see cref="TryGetRetryDelay"/>, applies the longest supported delay, and counts the retry if it is allowed.
        /// </summary>
        /// <param name="delay">
        /// When this method returns <see langword="true"/>, the delay to wait before the next retry.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if another retry is allowed; otherwise, <see langword="false"/>.
        /// </returns>
        private bool TryScheduleRetry(out TimeSpan delay)
        {
            if (!TryGetRetryDelay(out delay) || !RetryDelay.TryAccept(ref delay))
                return false;

            OnRetryScheduled();
            return true;
        }
    }
}
