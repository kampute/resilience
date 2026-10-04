// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Internal
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Runs operations with retries, keeping the retry state of each execution in local variables.
    /// </summary>
    /// <remarks>
    /// The operation and hooks are struct type arguments and the retry state is local, so a synchronous execution allocates nothing for its
    /// retries; an asynchronous execution allocates only what awaiting and <see cref="Task.Delay(TimeSpan, CancellationToken)"/> require.
    /// </remarks>
    internal static class RetryExecutor
    {
        /// <summary>
        /// Runs a synchronous operation, retrying the outcomes that the hooks select while the strategy allows.
        /// </summary>
        /// <typeparam name="T">
        /// The type returned by the operation.
        /// </typeparam>
        /// <typeparam name="TOperation">
        /// The operation adapter.
        /// </typeparam>
        /// <typeparam name="THooks">
        /// The outcome classification and callbacks.
        /// </typeparam>
        /// <param name="strategy">
        /// The strategy deciding whether and when to retry.
        /// </param>
        /// <param name="operation">
        /// The operation to run.
        /// </param>
        /// <param name="hooks">
        /// The outcome classification and callbacks.
        /// </param>
        /// <param name="cancellationToken">
        /// The token passed to the operation and the waits between attempts.
        /// </param>
        /// <returns>
        /// The first result that is not retried, or the last result when the strategy allows no more retries.
        /// </returns>
        internal static T Execute<T, TOperation, THooks>(IRetryStrategy strategy, TOperation operation, THooks hooks, CancellationToken cancellationToken)
            where TOperation : struct, IRetryOperation<T>
            where THooks : struct, IRetryHooks<T>
        {
            var startTimestamp = Stopwatch.GetTimestamp();
            for (var attempt = 1u; ; attempt = NextAttempt(attempt))
            {
                T result;
                try
                {
                    result = operation.Invoke(cancellationToken);
                }
                catch (Exception error) when (IsRetryable<T, THooks>(ref hooks, error, cancellationToken))
                {
                    var context = new RetryContext<T>(attempt, RetryDelay.ElapsedSince(startTimestamp), default!, error, default);
                    if (!TryScheduleRetry(strategy, ref hooks, ref context))
                        throw;

                    cancellationToken.ThrowIfCancellationRequested();
                    hooks.OnRetry(in context);
                    RetryDelay.Wait(context.Delay, cancellationToken);
                    continue;
                }

                var pendingRelease = true;
                try
                {
                    var context = new RetryContext<T>(attempt, RetryDelay.ElapsedSince(startTimestamp), result, null, default);
                    if (!hooks.ShouldRetry(result) || !TryScheduleRetry(strategy, ref hooks, ref context))
                    {
                        pendingRelease = false;
                        return result;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        hooks.OnRetry(in context);
                    }
                    finally
                    {
                        pendingRelease = false;
                        hooks.OnDiscarded(result);
                    }
                    RetryDelay.Wait(context.Delay, cancellationToken);
                }
                finally
                {
                    if (pendingRelease)
                        hooks.OnDiscarded(result);
                }
            }
        }

        /// <summary>
        /// Runs an asynchronous operation, retrying the outcomes that the hooks select while the strategy allows.
        /// </summary>
        /// <typeparam name="T">
        /// The type returned by the operation.
        /// </typeparam>
        /// <typeparam name="TOperation">
        /// The operation adapter.
        /// </typeparam>
        /// <typeparam name="THooks">
        /// The outcome classification and callbacks.
        /// </typeparam>
        /// <param name="strategy">
        /// The strategy deciding whether and when to retry.
        /// </param>
        /// <param name="operation">
        /// The operation to run.
        /// </param>
        /// <param name="hooks">
        /// The outcome classification and callbacks.
        /// </param>
        /// <param name="cancellationToken">
        /// The token passed to the operation and the waits between attempts.
        /// </param>
        /// <returns>
        /// A task resolving to the first result that is not retried, or the last result when the strategy allows no more retries.
        /// </returns>
        internal static async Task<T> ExecuteAsync<T, TOperation, THooks>(IRetryStrategy strategy, TOperation operation, THooks hooks, CancellationToken cancellationToken)
            where TOperation : struct, IAsyncRetryOperation<T>
            where THooks : struct, IRetryHooks<T>
        {
            var startTimestamp = Stopwatch.GetTimestamp();
            for (var attempt = 1u; ; attempt = NextAttempt(attempt))
            {
                T result;
                try
                {
                    var task = operation.Invoke(cancellationToken);
                    await task.ConfigureAwait(false);
                    result = operation.GetResult(task);
                }
                catch (Exception error) when (IsRetryable<T, THooks>(ref hooks, error, cancellationToken))
                {
                    var context = new RetryContext<T>(attempt, RetryDelay.ElapsedSince(startTimestamp), default!, error, default);
                    if (!TryScheduleRetry(strategy, ref hooks, ref context))
                        throw;

                    cancellationToken.ThrowIfCancellationRequested();
                    hooks.OnRetry(in context);
                    await RetryDelay.WaitAsync(context.Delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var pendingRelease = true;
                try
                {
                    var context = new RetryContext<T>(attempt, RetryDelay.ElapsedSince(startTimestamp), result, null, default);
                    if (!hooks.ShouldRetry(result) || !TryScheduleRetry(strategy, ref hooks, ref context))
                    {
                        pendingRelease = false;
                        return result;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        hooks.OnRetry(in context);
                    }
                    finally
                    {
                        pendingRelease = false;
                        hooks.OnDiscarded(result);
                    }
                    await RetryDelay.WaitAsync(context.Delay, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    if (pendingRelease)
                        hooks.OnDiscarded(result);
                }
            }
        }

        /// <summary>
        /// Excludes caller cancellation and applies the exception classification inside the exception filter.
        /// </summary>
        /// <typeparam name="T">
        /// The type returned by the operation.
        /// </typeparam>
        /// <typeparam name="THooks">
        /// The outcome classification and callbacks.
        /// </typeparam>
        /// <param name="hooks">
        /// The outcome classification and callbacks.
        /// </param>
        /// <param name="error">
        /// The exception thrown by the attempt.
        /// </param>
        /// <param name="cancellationToken">
        /// The caller's token, used to recognize caller cancellation.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the exception may be retried; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// Running inside an exception filter means that if the classification throws, the original exception propagates.
        /// </remarks>
        private static bool IsRetryable<T, THooks>(ref THooks hooks, Exception error, CancellationToken cancellationToken)
            where THooks : struct, IRetryHooks<T>
        {
            if (error is OperationCanceledException && cancellationToken.IsCancellationRequested)
                return false;

            return hooks.ShouldRetry(error);
        }

        /// <summary>
        /// Asks the strategy for permission and a delay, applies the delay override, and accepts the final delay.
        /// </summary>
        /// <typeparam name="T">
        /// The type returned by the operation.
        /// </typeparam>
        /// <typeparam name="THooks">
        /// The outcome classification and callbacks.
        /// </typeparam>
        /// <param name="strategy">
        /// The strategy deciding whether and when to retry.
        /// </param>
        /// <param name="hooks">
        /// The outcome classification and callbacks.
        /// </param>
        /// <param name="context">
        /// The failed attempt; on success, it carries the delay to wait.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if a retry is scheduled; <see langword="false"/> if the strategy refuses it or the final delay is longer than the
        /// supported maximum.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the delay override returns a negative delay.
        /// </exception>
        private static bool TryScheduleRetry<T, THooks>(IRetryStrategy strategy, ref THooks hooks, ref RetryContext<T> context)
            where THooks : struct, IRetryHooks<T>
        {
            if (!strategy.TryGetRetryDelay(context.Elapsed, context.AttemptNumber - 1, out var delay))
                return false;

            if (hooks.TryOverrideDelay(context.WithDelay(delay), out var overridden))
            {
                if (overridden < TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException("delay", overridden, "The delay override must not return a negative delay.");

                delay = overridden;
            }

            if (!RetryDelay.TryAccept(ref delay))
                return false;

            context = context.WithDelay(delay);
            return true;
        }

        /// <summary>
        /// Advances the attempt number, holding it at its maximum instead of wrapping around.
        /// </summary>
        /// <param name="attempt">
        /// The current attempt number.
        /// </param>
        /// <returns>
        /// The next attempt number.
        /// </returns>
        private static uint NextAttempt(uint attempt) => attempt == uint.MaxValue ? attempt : attempt + 1;
    }
}
