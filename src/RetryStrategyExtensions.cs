// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience
{
    using Kampute.Resilience.Internal;
    using Kampute.Resilience.Strategies.Modifiers;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides extension methods for <see cref="IRetryStrategy"/> to limit its retries, cap and spread its delays, create retry policies, start sessions, and run operations with retries.
    /// </summary>
    public static partial class RetryStrategyExtensions
    {
        /// <summary>
        /// Adds random jitter to the delays of a retry strategy.
        /// </summary>
        /// <param name="strategy">
        /// The retry strategy to modify.
        /// </param>
        /// <param name="jitterFactor">
        /// The largest proportion of each delay, between 0 and 1, by which the delay is randomly lengthened or shortened (optional). The default is 0.5.
        /// </param>
        /// <returns>
        /// A <see cref="JitterModifier"/> that wraps <paramref name="strategy"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="jitterFactor"/> is not between 0 and 1.
        /// </exception>
        public static JitterModifier WithJitter(this IRetryStrategy strategy, double jitterFactor = 0.5)
        {
            if (strategy is null)
                throw new ArgumentNullException(nameof(strategy));

            return new(strategy, jitterFactor);
        }

        /// <summary>
        /// Limits the number of retries a retry strategy allows.
        /// </summary>
        /// <param name="strategy">
        /// The retry strategy to modify.
        /// </param>
        /// <param name="maxRetries">
        /// The maximum number of retries after the initial attempt. Zero allows no retry.
        /// </param>
        /// <returns>
        /// A <see cref="MaxRetriesModifier"/> that wraps <paramref name="strategy"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> is <see langword="null"/>.
        /// </exception>
        public static MaxRetriesModifier WithMaxRetries(this IRetryStrategy strategy, uint maxRetries)
        {
            if (strategy is null)
                throw new ArgumentNullException(nameof(strategy));

            return new(strategy, maxRetries);
        }

        /// <summary>
        /// Limits the time during which a retry strategy allows retries.
        /// </summary>
        /// <param name="strategy">
        /// The retry strategy to modify.
        /// </param>
        /// <param name="maxElapsedTime">
        /// The time since the start of retry attempts after which no further retry is allowed. A delay that would end after it is shortened to end at it.
        /// </param>
        /// <returns>
        /// A <see cref="MaxElapsedTimeModifier"/> that wraps <paramref name="strategy"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="maxElapsedTime"/> is negative.
        /// </exception>
        /// <remarks>
        /// The limit applies to the decision to retry, not to the operation: an operation that is running when the limit is reached is not canceled.
        /// </remarks>
        public static MaxElapsedTimeModifier WithMaxElapsedTime(this IRetryStrategy strategy, TimeSpan maxElapsedTime)
        {
            if (strategy is null)
                throw new ArgumentNullException(nameof(strategy));

            return new(strategy, maxElapsedTime);
        }

        /// <summary>
        /// Limits the time, in milliseconds, during which a retry strategy allows retries.
        /// </summary>
        /// <param name="strategy">
        /// The retry strategy to modify.
        /// </param>
        /// <param name="millisecondsMaxElapsedTime">
        /// The number of milliseconds since the start of retry attempts after which no further retry is allowed. A delay that would end after it is
        /// shortened to end at it.
        /// </param>
        /// <returns>
        /// A <see cref="MaxElapsedTimeModifier"/> that wraps <paramref name="strategy"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="millisecondsMaxElapsedTime"/> is negative.
        /// </exception>
        /// <remarks>
        /// The limit applies to the decision to retry, not to the operation: an operation that is running when the limit is reached is not canceled.
        /// </remarks>
        public static MaxElapsedTimeModifier WithMaxElapsedTime(this IRetryStrategy strategy, int millisecondsMaxElapsedTime)
        {
            if (strategy is null)
                throw new ArgumentNullException(nameof(strategy));
            if (millisecondsMaxElapsedTime < 0)
                throw new ArgumentOutOfRangeException(nameof(millisecondsMaxElapsedTime), millisecondsMaxElapsedTime, "The number of milliseconds must not be negative.");

            return new(strategy, TimeSpan.FromMilliseconds(millisecondsMaxElapsedTime));
        }

        /// <summary>
        /// Caps each delay of a retry strategy.
        /// </summary>
        /// <param name="strategy">
        /// The retry strategy to modify.
        /// </param>
        /// <param name="maxDelay">
        /// The longest delay before a retry. Longer delays of <paramref name="strategy"/> are shortened to it.
        /// </param>
        /// <returns>
        /// A <see cref="MaxDelayModifier"/> that wraps <paramref name="strategy"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="maxDelay"/> is negative.
        /// </exception>
        /// <remarks>
        /// The cap does not limit the number of retries. Its position relative to <see cref="WithJitter"/> matters: jitter added after the cap spreads
        /// the capped delays, so they can exceed <paramref name="maxDelay"/> by the jitter factor; jitter added before the cap keeps every delay within
        /// <paramref name="maxDelay"/>.
        /// </remarks>
        public static MaxDelayModifier WithMaxDelay(this IRetryStrategy strategy, TimeSpan maxDelay)
        {
            if (strategy is null)
                throw new ArgumentNullException(nameof(strategy));

            return new(strategy, maxDelay);
        }

        /// <summary>
        /// Caps each delay of a retry strategy at a number of milliseconds.
        /// </summary>
        /// <param name="strategy">
        /// The retry strategy to modify.
        /// </param>
        /// <param name="millisecondsMaxDelay">
        /// The longest delay before a retry, in milliseconds. Longer delays of <paramref name="strategy"/> are shortened to it.
        /// </param>
        /// <returns>
        /// A <see cref="MaxDelayModifier"/> that wraps <paramref name="strategy"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="millisecondsMaxDelay"/> is negative.
        /// </exception>
        /// <remarks>
        /// The cap does not limit the number of retries. Its position relative to <see cref="WithJitter"/> matters: jitter added after the cap spreads
        /// the capped delays, so they can exceed the cap by the jitter factor; jitter added before the cap keeps every delay within it.
        /// </remarks>
        public static MaxDelayModifier WithMaxDelay(this IRetryStrategy strategy, int millisecondsMaxDelay)
        {
            if (strategy is null)
                throw new ArgumentNullException(nameof(strategy));
            if (millisecondsMaxDelay < 0)
                throw new ArgumentOutOfRangeException(nameof(millisecondsMaxDelay), millisecondsMaxDelay, "The number of milliseconds must not be negative.");

            return new(strategy, TimeSpan.FromMilliseconds(millisecondsMaxDelay));
        }

        /// <summary>
        /// Creates a retry policy that retries the exceptions of the specified type as the strategy decides.
        /// </summary>
        /// <typeparam name="TException">
        /// The type of the exceptions to retry, including derived types.
        /// </typeparam>
        /// <param name="strategy">
        /// The strategy that decides whether and when to retry.
        /// </param>
        /// <returns>
        /// A new <see cref="RetryPolicy"/> that retries only the exceptions of type <typeparamref name="TException"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// Chain further <c>RetryOn</c> calls to retry other exceptions as well.
        /// </remarks>
        public static RetryPolicy RetryOn<TException>(this IRetryStrategy strategy) where TException : Exception
            => new RetryPolicy(strategy).RetryOn<TException>();

        /// <summary>
        /// Creates a retry policy that retries the exceptions of the specified type that satisfy a condition, as the strategy decides.
        /// </summary>
        /// <typeparam name="TException">
        /// The type of the exceptions to retry, including derived types.
        /// </typeparam>
        /// <param name="strategy">
        /// The strategy that decides whether and when to retry.
        /// </param>
        /// <param name="predicate">
        /// A function that returns <see langword="true"/> for the exceptions of type <typeparamref name="TException"/> to retry.
        /// </param>
        /// <returns>
        /// A new <see cref="RetryPolicy"/> that retries only the exceptions that <paramref name="predicate"/> selects.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> or <paramref name="predicate"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// Chain further <c>RetryOn</c> calls to retry other exceptions as well.
        /// </remarks>
        public static RetryPolicy RetryOn<TException>(this IRetryStrategy strategy, Func<TException, bool> predicate) where TException : Exception
            => new RetryPolicy(strategy).RetryOn(predicate);

        /// <summary>
        /// Creates a retry policy that retries the exceptions that satisfy a condition, as the strategy decides.
        /// </summary>
        /// <param name="strategy">
        /// The strategy that decides whether and when to retry.
        /// </param>
        /// <param name="predicate">
        /// A function that returns <see langword="true"/> for the exceptions to retry.
        /// </param>
        /// <returns>
        /// A new <see cref="RetryPolicy"/> that retries only the exceptions that <paramref name="predicate"/> selects.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> or <paramref name="predicate"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// Chain further <c>RetryOn</c> calls to retry other exceptions as well.
        /// </remarks>
        public static RetryPolicy RetryOn(this IRetryStrategy strategy, Func<Exception, bool> predicate)
            => new RetryPolicy(strategy).RetryOn(predicate);

        /// <summary>
        /// Creates a retry policy for operations returning <typeparamref name="T"/> that retries every exception and the results that satisfy a
        /// condition, as the strategy decides.
        /// </summary>
        /// <typeparam name="T">
        /// The type returned by the operations the policy runs.
        /// </typeparam>
        /// <param name="strategy">
        /// The strategy that decides whether and when to retry.
        /// </param>
        /// <param name="predicate">
        /// A function that returns <see langword="true"/> for the results to retry.
        /// </param>
        /// <returns>
        /// A new <see cref="RetryPolicy{T}"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> or <paramref name="predicate"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// Chain <c>RetryOn</c> calls to retry only selected exceptions.
        /// </remarks>
        public static RetryPolicy<T> RetryOnResult<T>(this IRetryStrategy strategy, Func<T, bool> predicate)
            => new RetryPolicy(strategy).RetryOnResult(predicate);

        /// <summary>
        /// Creates a retry policy that retries every exception, replacing the strategy's delay with one computed from the failed attempt.
        /// </summary>
        /// <param name="strategy">
        /// The strategy that decides whether to retry and proposes the delay.
        /// </param>
        /// <param name="delay">
        /// A function that receives the failed attempt, whose <see cref="RetryContext.Delay"/> is the delay that the strategy proposes, and
        /// returns the nonnegative delay to wait.
        /// </param>
        /// <returns>
        /// A new <see cref="RetryPolicy"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> or <paramref name="delay"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// See <see cref="RetryPolicy.OverrideDelay(Func{RetryContext, TimeSpan})"/> for how the delay is applied.
        /// </remarks>
        public static RetryPolicy OverrideDelay(this IRetryStrategy strategy, Func<RetryContext, TimeSpan> delay)
            => new RetryPolicy(strategy).OverrideDelay(delay);

        /// <summary>
        /// Creates a retry policy that retries every exception and invokes a handler for each approved retry.
        /// </summary>
        /// <param name="strategy">
        /// The strategy that decides whether and when to retry.
        /// </param>
        /// <param name="handler">
        /// The handler, which receives the failed attempt and the delay that execution waits before the retry.
        /// </param>
        /// <returns>
        /// A new <see cref="RetryPolicy"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> or <paramref name="handler"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// See <see cref="RetryPolicy.OnRetry(Action{RetryContext})"/> for when the handler runs.
        /// </remarks>
        public static RetryPolicy OnRetry(this IRetryStrategy strategy, Action<RetryContext> handler)
            => new RetryPolicy(strategy).OnRetry(handler);

        /// <summary>
        /// Runs an asynchronous operation and retries it as the strategy decides when it throws an exception.
        /// </summary>
        /// <param name="strategy">
        /// The retry strategy that decides whether and when to retry.
        /// </param>
        /// <param name="operation">
        /// The operation to run. It receives <paramref name="cancellationToken"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// A token for canceling the operation and the waits between attempts (optional).
        /// </param>
        /// <returns>
        /// A task that completes when the operation succeeds.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown, before a task is returned, if <paramref name="strategy"/> or <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Every exception is retried except an <see cref="OperationCanceledException"/> thrown after <paramref name="cancellationToken"/> has been
        /// canceled. To retry only selected exceptions, start a policy with <see cref="RetryOn{TException}(IRetryStrategy)"/>.
        /// </para>
        /// <para>
        /// When the strategy allows no more retries, the last exception is rethrown with its original stack trace.
        /// </para>
        /// </remarks>
        public static Task ExecuteAsync(this IRetryStrategy strategy, Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            if (strategy is null)
                throw new ArgumentNullException(nameof(strategy));
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            return RetryExecutor.ExecuteAsync<VoidResult, TaskOperation, DefaultHooks<VoidResult>>(strategy, new(operation), default, cancellationToken);
        }

        /// <summary>
        /// Runs an asynchronous operation that returns a value and retries it as the strategy decides when it throws an exception.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the value the operation returns.
        /// </typeparam>
        /// <param name="strategy">
        /// The retry strategy that decides whether and when to retry.
        /// </param>
        /// <param name="operation">
        /// The operation to run. It receives <paramref name="cancellationToken"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// A token for canceling the operation and the waits between attempts (optional).
        /// </param>
        /// <returns>
        /// A task that resolves to the value returned by the first successful run of the operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown, before a task is returned, if <paramref name="strategy"/> or <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Every exception is retried except an <see cref="OperationCanceledException"/> thrown after <paramref name="cancellationToken"/> has been
        /// canceled. To retry only selected exceptions, start a policy with <see cref="RetryOn{TException}(IRetryStrategy)"/>.
        /// </para>
        /// <para>
        /// When the strategy allows no more retries, the last exception is rethrown with its original stack trace.
        /// </para>
        /// </remarks>
        public static Task<T> ExecuteAsync<T>(this IRetryStrategy strategy, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            if (strategy is null)
                throw new ArgumentNullException(nameof(strategy));
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            return RetryExecutor.ExecuteAsync<T, TaskOperation<T>, DefaultHooks<T>>(strategy, new(operation), default, cancellationToken);
        }

        /// <summary>
        /// Runs a blocking operation and retries it as the strategy decides when it throws an exception.
        /// </summary>
        /// <param name="strategy">
        /// The retry strategy that decides whether and when to retry.
        /// </param>
        /// <param name="operation">
        /// The operation to run. It receives <paramref name="cancellationToken"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// A token for canceling the operation and the waits between attempts (optional).
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> or <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The calling thread is blocked during the waits between attempts; a wait ends as soon as <paramref name="cancellationToken"/> is canceled.
        /// Every exception is retried except an <see cref="OperationCanceledException"/> thrown after <paramref name="cancellationToken"/> has been
        /// canceled. To retry only selected exceptions, start a policy with <see cref="RetryOn{TException}(IRetryStrategy)"/>.
        /// </para>
        /// <para>
        /// When the strategy allows no more retries, the last exception is rethrown with its original stack trace.
        /// </para>
        /// </remarks>
        public static void Execute(this IRetryStrategy strategy, Action<CancellationToken> operation, CancellationToken cancellationToken = default)
        {
            if (strategy is null)
                throw new ArgumentNullException(nameof(strategy));
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            RetryExecutor.Execute<VoidResult, ActionOperation, DefaultHooks<VoidResult>>(strategy, new(operation), default, cancellationToken);
        }

        /// <summary>
        /// Runs a blocking operation that returns a value and retries it as the strategy decides when it throws an exception.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the value the operation returns.
        /// </typeparam>
        /// <param name="strategy">
        /// The retry strategy that decides whether and when to retry.
        /// </param>
        /// <param name="operation">
        /// The operation to run. It receives <paramref name="cancellationToken"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// A token for canceling the operation and the waits between attempts (optional).
        /// </param>
        /// <returns>
        /// The value returned by the first successful run of the operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> or <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The calling thread is blocked during the waits between attempts; a wait ends as soon as <paramref name="cancellationToken"/> is canceled.
        /// Every exception is retried except an <see cref="OperationCanceledException"/> thrown after <paramref name="cancellationToken"/> has been
        /// canceled. To retry only selected exceptions, start a policy with <see cref="RetryOn{TException}(IRetryStrategy)"/>.
        /// </para>
        /// <para>
        /// When the strategy allows no more retries, the last exception is rethrown with its original stack trace.
        /// </para>
        /// </remarks>
        public static T Execute<T>(this IRetryStrategy strategy, Func<CancellationToken, T> operation, CancellationToken cancellationToken = default)
        {
            if (strategy is null)
                throw new ArgumentNullException(nameof(strategy));
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            return RetryExecutor.Execute<T, FuncOperation<T>, DefaultHooks<T>>(strategy, new(operation), default, cancellationToken);
        }

        /// <summary>
        /// Runs an asynchronous operation that receives a state, and retries it as the strategy decides when it throws an exception.
        /// </summary>
        /// <typeparam name="TState">
        /// The type of the state passed to the operation.
        /// </typeparam>
        /// <param name="strategy">
        /// The retry strategy that decides whether and when to retry.
        /// </param>
        /// <param name="state">
        /// The state passed to every attempt of the operation.
        /// </param>
        /// <param name="operation">
        /// The operation to run. It receives <paramref name="state"/> and <paramref name="cancellationToken"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// A token for canceling the operation and the waits between attempts (optional).
        /// </param>
        /// <returns>
        /// A task that completes when the operation succeeds.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown, before a task is returned, if <paramref name="strategy"/> or <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Every exception is retried except an <see cref="OperationCanceledException"/> thrown after <paramref name="cancellationToken"/> has been
        /// canceled. When the strategy allows no more retries, the last exception is rethrown with its original stack trace.
        /// </para>
        /// <para>
        /// Pass the values that the operation needs as <paramref name="state"/> and use an operation that captures no variables, such as a
        /// <see langword="static"/> lambda; the compiler then reuses one delegate instance, so the call allocates no closure. Use a tuple to
        /// pass several values.
        /// </para>
        /// </remarks>
        public static Task ExecuteAsync<TState>(this IRetryStrategy strategy, TState state, Func<TState, CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            if (strategy is null)
                throw new ArgumentNullException(nameof(strategy));
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            return RetryExecutor.ExecuteAsync<VoidResult, StatefulTaskOperation<TState>, DefaultHooks<VoidResult>>(strategy, new(state, operation), default, cancellationToken);
        }

        /// <summary>
        /// Runs an asynchronous operation that receives a state and returns a value, and retries it as the strategy decides when it throws an exception.
        /// </summary>
        /// <typeparam name="TState">
        /// The type of the state passed to the operation.
        /// </typeparam>
        /// <typeparam name="T">
        /// The type of the value the operation returns.
        /// </typeparam>
        /// <param name="strategy">
        /// The retry strategy that decides whether and when to retry.
        /// </param>
        /// <param name="state">
        /// The state passed to every attempt of the operation.
        /// </param>
        /// <param name="operation">
        /// The operation to run. It receives <paramref name="state"/> and <paramref name="cancellationToken"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// A token for canceling the operation and the waits between attempts (optional).
        /// </param>
        /// <returns>
        /// A task that resolves to the value returned by the first successful run of the operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown, before a task is returned, if <paramref name="strategy"/> or <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Every exception is retried except an <see cref="OperationCanceledException"/> thrown after <paramref name="cancellationToken"/> has been
        /// canceled. When the strategy allows no more retries, the last exception is rethrown with its original stack trace.
        /// </para>
        /// <para>
        /// Pass the values that the operation needs as <paramref name="state"/> and use an operation that captures no variables, such as a
        /// <see langword="static"/> lambda; the compiler then reuses one delegate instance, so the call allocates no closure. Use a tuple to
        /// pass several values.
        /// </para>
        /// </remarks>
        public static Task<T> ExecuteAsync<TState, T>(this IRetryStrategy strategy, TState state, Func<TState, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            if (strategy is null)
                throw new ArgumentNullException(nameof(strategy));
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            return RetryExecutor.ExecuteAsync<T, StatefulTaskOperation<TState, T>, DefaultHooks<T>>(strategy, new(state, operation), default, cancellationToken);
        }

        /// <summary>
        /// Runs a blocking operation that receives a state, and retries it as the strategy decides when it throws an exception.
        /// </summary>
        /// <typeparam name="TState">
        /// The type of the state passed to the operation.
        /// </typeparam>
        /// <param name="strategy">
        /// The retry strategy that decides whether and when to retry.
        /// </param>
        /// <param name="state">
        /// The state passed to every attempt of the operation.
        /// </param>
        /// <param name="operation">
        /// The operation to run. It receives <paramref name="state"/> and <paramref name="cancellationToken"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// A token for canceling the operation and the waits between attempts (optional).
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> or <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The calling thread is blocked during the waits between attempts. Every exception is retried except an
        /// <see cref="OperationCanceledException"/> thrown after <paramref name="cancellationToken"/> has been canceled. When the strategy
        /// allows no more retries, the last exception is rethrown with its original stack trace.
        /// </para>
        /// <para>
        /// Pass the values that the operation needs as <paramref name="state"/> and use an operation that captures no variables, such as a
        /// <see langword="static"/> lambda; the compiler then reuses one delegate instance, so the call allocates no closure. Use a tuple to
        /// pass several values.
        /// </para>
        /// </remarks>
        public static void Execute<TState>(this IRetryStrategy strategy, TState state, Action<TState, CancellationToken> operation, CancellationToken cancellationToken = default)
        {
            if (strategy is null)
                throw new ArgumentNullException(nameof(strategy));
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            RetryExecutor.Execute<VoidResult, StatefulActionOperation<TState>, DefaultHooks<VoidResult>>(strategy, new(state, operation), default, cancellationToken);
        }

        /// <summary>
        /// Runs a blocking operation that receives a state and returns a value, and retries it as the strategy decides when it throws an exception.
        /// </summary>
        /// <typeparam name="TState">
        /// The type of the state passed to the operation.
        /// </typeparam>
        /// <typeparam name="T">
        /// The type of the value the operation returns.
        /// </typeparam>
        /// <param name="strategy">
        /// The retry strategy that decides whether and when to retry.
        /// </param>
        /// <param name="state">
        /// The state passed to every attempt of the operation.
        /// </param>
        /// <param name="operation">
        /// The operation to run. It receives <paramref name="state"/> and <paramref name="cancellationToken"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// A token for canceling the operation and the waits between attempts (optional).
        /// </param>
        /// <returns>
        /// The value returned by the first successful run of the operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> or <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The calling thread is blocked during the waits between attempts. Every exception is retried except an
        /// <see cref="OperationCanceledException"/> thrown after <paramref name="cancellationToken"/> has been canceled. When the strategy
        /// allows no more retries, the last exception is rethrown with its original stack trace.
        /// </para>
        /// <para>
        /// Pass the values that the operation needs as <paramref name="state"/> and use an operation that captures no variables, such as a
        /// <see langword="static"/> lambda; the compiler then reuses one delegate instance, so the call allocates no closure. Use a tuple to
        /// pass several values.
        /// </para>
        /// </remarks>
        public static T Execute<TState, T>(this IRetryStrategy strategy, TState state, Func<TState, CancellationToken, T> operation, CancellationToken cancellationToken = default)
        {
            if (strategy is null)
                throw new ArgumentNullException(nameof(strategy));
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            return RetryExecutor.Execute<T, StatefulFuncOperation<TState, T>, DefaultHooks<T>>(strategy, new(state, operation), default, cancellationToken);
        }

        /// <summary>
        /// Starts a retry session for one operation that the strategy governs.
        /// </summary>
        /// <param name="strategy">
        /// The retry strategy of the session.
        /// </param>
        /// <returns>
        /// A new <see cref="RetrySession"/>, with no retries and its elapsed time starting now.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> is <see langword="null"/>.
        /// </exception>
        public static RetrySession StartSession(this IRetryStrategy strategy) => new(strategy);
    }
}
