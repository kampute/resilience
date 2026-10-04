// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience
{
    using Kampute.Resilience.Internal;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a retry policy: a strategy that decides whether and when to retry, the exceptions that are retried, and callbacks for each retry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A policy is immutable. Each configuration method returns a new policy and leaves the original unchanged, so a policy can be built once,
    /// stored, and shared by any number of concurrent executions. Each execution keeps its own retry count and elapsed time.
    /// </para>
    /// <para>
    /// Without a call to a <c>RetryOn</c> method, the policy retries every exception except an <see cref="OperationCanceledException"/> thrown
    /// after the caller's token is canceled, which is never retried. Call <see cref="RetryOnResult{T}(Func{T, bool})"/> to also retry returned
    /// values; it returns a <see cref="RetryPolicy{T}"/> that keeps this policy's configuration.
    /// </para>
    /// <para>
    /// Callbacks run on the execution path. If an exception classifier throws, the operation's exception propagates as if it was not retried;
    /// an exception from a delay override or retry handler propagates in place of the operation's outcome, and the operation is not retried.
    /// </para>
    /// </remarks>
    /// <example>
    /// This policy retries timeouts up to five times with exponential backoff and logs each retry:
    /// <code>
    /// var retry = RetryStrategies.Exponential(TimeSpan.FromSeconds(1))
    ///     .WithMaxRetries(5)
    ///     .RetryOn&lt;TimeoutException&gt;()
    ///     .OnRetry(context => Console.WriteLine($"Attempt {context.AttemptNumber} failed; retrying in {context.Delay}."));
    ///
    /// var value = await retry.ExecuteAsync(ct => LoadAsync(ct), cancellationToken);
    /// </code>
    /// </example>
    public sealed class RetryPolicy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RetryPolicy"/> class that retries every exception as the strategy decides.
        /// </summary>
        /// <param name="strategy">
        /// The strategy that decides whether and when to retry.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="strategy"/> is <see langword="null"/>.
        /// </exception>
        public RetryPolicy(IRetryStrategy strategy)
        {
            Strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RetryPolicy"/> class with all of its configuration.
        /// </summary>
        /// <param name="strategy">
        /// The strategy that decides whether and when to retry.
        /// </param>
        /// <param name="exceptionFilter">
        /// The exception classifier, or <see langword="null"/> to retry every exception.
        /// </param>
        /// <param name="delayOverride">
        /// The delay override, or <see langword="null"/>.
        /// </param>
        /// <param name="retryHandler">
        /// The retry handlers, or <see langword="null"/>.
        /// </param>
        private RetryPolicy(IRetryStrategy strategy, Func<Exception, bool>? exceptionFilter, Func<RetryContext, TimeSpan>? delayOverride, Action<RetryContext>? retryHandler)
        {
            Strategy = strategy;
            ExceptionFilter = exceptionFilter;
            DelayOverride = delayOverride;
            RetryHandler = retryHandler;
        }

        /// <summary>
        /// Gets the strategy of this policy.
        /// </summary>
        /// <value>
        /// The <see cref="IRetryStrategy"/> that decides whether and when to retry.
        /// </value>
        public IRetryStrategy Strategy { get; }

        /// <summary>
        /// Gets the exception classifier.
        /// </summary>
        /// <value>
        /// A function returning <see langword="true"/> for retried exceptions, or <see langword="null"/> to retry every exception.
        /// </value>
        internal Func<Exception, bool>? ExceptionFilter { get; }

        /// <summary>
        /// Gets the delay override.
        /// </summary>
        /// <value>
        /// The function replacing the strategy's delay, or <see langword="null"/>.
        /// </value>
        internal Func<RetryContext, TimeSpan>? DelayOverride { get; }

        /// <summary>
        /// Gets the retry handlers.
        /// </summary>
        /// <value>
        /// The handlers invoked for each approved retry, or <see langword="null"/>.
        /// </value>
        internal Action<RetryContext>? RetryHandler { get; }

        /// <summary>
        /// Creates a policy that also retries exceptions of the specified type.
        /// </summary>
        /// <typeparam name="TException">
        /// The type of the exceptions to retry, including derived types.
        /// </typeparam>
        /// <returns>
        /// A new policy that retries the exceptions this policy retries and the exceptions of type <typeparamref name="TException"/>.
        /// </returns>
        /// <remarks>
        /// The first <c>RetryOn</c> call limits retries to the exceptions it selects; later calls add to them.
        /// </remarks>
        public RetryPolicy RetryOn<TException>() where TException : Exception
            => WithExceptionFilter(ExceptionFilters.Or(ExceptionFilter, ExceptionFilters.OfType<TException>()));

        /// <summary>
        /// Creates a policy that also retries exceptions of the specified type that satisfy a condition.
        /// </summary>
        /// <typeparam name="TException">
        /// The type of the exceptions to retry, including derived types.
        /// </typeparam>
        /// <param name="predicate">
        /// A function that returns <see langword="true"/> for the exceptions of type <typeparamref name="TException"/> to retry.
        /// </param>
        /// <returns>
        /// A new policy that retries the exceptions this policy retries and the exceptions that <paramref name="predicate"/> selects.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="predicate"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// The first <c>RetryOn</c> call limits retries to the exceptions it selects; later calls add to them. If <paramref name="predicate"/>
        /// throws, the operation's exception propagates without a retry.
        /// </remarks>
        public RetryPolicy RetryOn<TException>(Func<TException, bool> predicate) where TException : Exception
            => WithExceptionFilter(ExceptionFilters.Or(ExceptionFilter, ExceptionFilters.OfType(predicate)));

        /// <summary>
        /// Creates a policy that also retries the exceptions that satisfy a condition.
        /// </summary>
        /// <param name="predicate">
        /// A function that returns <see langword="true"/> for the exceptions to retry.
        /// </param>
        /// <returns>
        /// A new policy that retries the exceptions this policy retries and the exceptions that <paramref name="predicate"/> selects.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="predicate"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// The first <c>RetryOn</c> call limits retries to the exceptions it selects; later calls add to them. If <paramref name="predicate"/>
        /// throws, the operation's exception propagates without a retry.
        /// </remarks>
        public RetryPolicy RetryOn(Func<Exception, bool> predicate)
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            return WithExceptionFilter(ExceptionFilters.Or(ExceptionFilter, predicate));
        }

        /// <summary>
        /// Creates a policy for operations returning <typeparamref name="T"/> that also retries the results that satisfy a condition.
        /// </summary>
        /// <typeparam name="T">
        /// The type returned by the operations the new policy runs.
        /// </typeparam>
        /// <param name="predicate">
        /// A function that returns <see langword="true"/> for the results to retry.
        /// </param>
        /// <returns>
        /// A new <see cref="RetryPolicy{T}"/> with this policy's strategy, retried exceptions, delay override, and retry handlers.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="predicate"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// When the strategy allows no more retries, execution returns the last result even though <paramref name="predicate"/> selected it.
        /// The delay override and retry handlers of this policy receive a <see cref="RetryContext"/> for both exceptions and retried results.
        /// </remarks>
        public RetryPolicy<T> RetryOnResult<T>(Func<T, bool> predicate)
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            var delayOverride = DelayOverride;
            var retryHandler = RetryHandler;

            return new RetryPolicy<T>(
                Strategy,
                ExceptionFilter,
                predicate,
                delayOverride is null ? null : context => delayOverride(context),
                retryHandler is null ? null : context => retryHandler(context),
                null);
        }

        /// <summary>
        /// Creates a policy that replaces the strategy's delay with one computed from the failed attempt.
        /// </summary>
        /// <param name="delay">
        /// A function that receives the failed attempt, whose <see cref="RetryContext.Delay"/> is the delay that the strategy proposes, and
        /// returns the nonnegative delay to wait. Return <see cref="RetryContext.Delay"/> to keep the proposed delay.
        /// </param>
        /// <returns>
        /// A new policy whose delays <paramref name="delay"/> selects. It replaces any delay override of this policy.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="delay"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The override runs only after the strategy allows a retry, and its delay replaces the proposed delay after all strategy modifiers, so it
        /// can exceed a delay cap or the time left under an elapsed-time limit. Enforce any required deadline in the override or with the caller's
        /// cancellation token.
        /// </para>
        /// <para>
        /// A negative delay throws <see cref="ArgumentOutOfRangeException"/> from the execution, without a retry. A delay longer than
        /// <see cref="int.MaxValue"/> milliseconds stops retrying, as when the strategy allows no more retries.
        /// </para>
        /// </remarks>
        public RetryPolicy OverrideDelay(Func<RetryContext, TimeSpan> delay)
        {
            if (delay is null)
                throw new ArgumentNullException(nameof(delay));

            return new(Strategy, ExceptionFilter, delay, RetryHandler);
        }

        /// <summary>
        /// Creates a policy that invokes a handler for each approved retry.
        /// </summary>
        /// <param name="handler">
        /// The handler, which receives the failed attempt and the delay that execution waits before the retry.
        /// </param>
        /// <returns>
        /// A new policy that invokes the handlers of this policy and then <paramref name="handler"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="handler"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// Handlers run before the wait, and only when a retry follows: not when the strategy allows no more retries, and not for exceptions that
        /// are not retried. If a handler throws, later handlers do not run.
        /// </remarks>
        public RetryPolicy OnRetry(Action<RetryContext> handler)
        {
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            return new(Strategy, ExceptionFilter, DelayOverride, RetryHandler + handler);
        }

        /// <summary>
        /// Runs a blocking operation and retries it as this policy decides when it fails.
        /// </summary>
        /// <param name="operation">
        /// The operation to run. It receives <paramref name="cancellationToken"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// A token for canceling the operation and the waits between attempts (optional).
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// The calling thread is blocked during the waits between attempts. When an exception is not retried, or the strategy allows no more
        /// retries, the last exception is rethrown with its original stack trace.
        /// </remarks>
        public void Execute(Action<CancellationToken> operation, CancellationToken cancellationToken = default)
        {
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            RetryExecutor.Execute<VoidResult, ActionOperation, ExceptionPolicyHooks<VoidResult>>(Strategy, new(operation), new(this), cancellationToken);
        }

        /// <summary>
        /// Runs a blocking operation that returns a value and retries it as this policy decides when it fails.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the value the operation returns.
        /// </typeparam>
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
        /// Thrown if <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// The calling thread is blocked during the waits between attempts. When an exception is not retried, or the strategy allows no more
        /// retries, the last exception is rethrown with its original stack trace.
        /// </remarks>
        public T Execute<T>(Func<CancellationToken, T> operation, CancellationToken cancellationToken = default)
        {
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            return RetryExecutor.Execute<T, FuncOperation<T>, ExceptionPolicyHooks<T>>(Strategy, new(operation), new(this), cancellationToken);
        }

        /// <summary>
        /// Runs an asynchronous operation and retries it as this policy decides when it fails.
        /// </summary>
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
        /// Thrown, before a task is returned, if <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// When an exception is not retried, or the strategy allows no more retries, the last exception is rethrown with its original stack trace.
        /// </remarks>
        public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            return RetryExecutor.ExecuteAsync<VoidResult, TaskOperation, ExceptionPolicyHooks<VoidResult>>(Strategy, new(operation), new(this), cancellationToken);
        }

        /// <summary>
        /// Runs an asynchronous operation that returns a value and retries it as this policy decides when it fails.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the value the operation returns.
        /// </typeparam>
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
        /// Thrown, before a task is returned, if <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// When an exception is not retried, or the strategy allows no more retries, the last exception is rethrown with its original stack trace.
        /// </remarks>
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            return RetryExecutor.ExecuteAsync<T, TaskOperation<T>, ExceptionPolicyHooks<T>>(Strategy, new(operation), new(this), cancellationToken);
        }

        /// <summary>
        /// Runs a blocking operation that receives a state, and retries it as this policy decides when it fails.
        /// </summary>
        /// <typeparam name="TState">
        /// The type of the state passed to the operation.
        /// </typeparam>
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
        /// Thrown if <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The calling thread is blocked during the waits between attempts. When an exception is not retried, or the strategy allows no more
        /// retries, the last exception is rethrown with its original stack trace.
        /// </para>
        /// <para>
        /// Pass the values that the operation needs as <paramref name="state"/> and use an operation that captures no variables, such as a
        /// <see langword="static"/> lambda; the compiler then reuses one delegate instance, so the call allocates no closure. Use a tuple to
        /// pass several values.
        /// </para>
        /// </remarks>
        public void Execute<TState>(TState state, Action<TState, CancellationToken> operation, CancellationToken cancellationToken = default)
        {
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            RetryExecutor.Execute<VoidResult, StatefulActionOperation<TState>, ExceptionPolicyHooks<VoidResult>>(Strategy, new(state, operation), new(this), cancellationToken);
        }

        /// <summary>
        /// Runs a blocking operation that receives a state and returns a value, and retries it as this policy decides when it fails.
        /// </summary>
        /// <typeparam name="TState">
        /// The type of the state passed to the operation.
        /// </typeparam>
        /// <typeparam name="T">
        /// The type of the value the operation returns.
        /// </typeparam>
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
        /// Thrown if <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The calling thread is blocked during the waits between attempts. When an exception is not retried, or the strategy allows no more
        /// retries, the last exception is rethrown with its original stack trace.
        /// </para>
        /// <para>
        /// Pass the values that the operation needs as <paramref name="state"/> and use an operation that captures no variables, such as a
        /// <see langword="static"/> lambda; the compiler then reuses one delegate instance, so the call allocates no closure. Use a tuple to
        /// pass several values.
        /// </para>
        /// </remarks>
        public T Execute<TState, T>(TState state, Func<TState, CancellationToken, T> operation, CancellationToken cancellationToken = default)
        {
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            return RetryExecutor.Execute<T, StatefulFuncOperation<TState, T>, ExceptionPolicyHooks<T>>(Strategy, new(state, operation), new(this), cancellationToken);
        }

        /// <summary>
        /// Runs an asynchronous operation that receives a state, and retries it as this policy decides when it fails.
        /// </summary>
        /// <typeparam name="TState">
        /// The type of the state passed to the operation.
        /// </typeparam>
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
        /// Thrown, before a task is returned, if <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// <para>
        /// When an exception is not retried, or the strategy allows no more retries, the last exception is rethrown with its original stack trace.
        /// </para>
        /// <para>
        /// Pass the values that the operation needs as <paramref name="state"/> and use an operation that captures no variables, such as a
        /// <see langword="static"/> lambda; the compiler then reuses one delegate instance, so the call allocates no closure. Use a tuple to
        /// pass several values.
        /// </para>
        /// </remarks>
        public Task ExecuteAsync<TState>(TState state, Func<TState, CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            return RetryExecutor.ExecuteAsync<VoidResult, StatefulTaskOperation<TState>, ExceptionPolicyHooks<VoidResult>>(Strategy, new(state, operation), new(this), cancellationToken);
        }

        /// <summary>
        /// Runs an asynchronous operation that receives a state and returns a value, and retries it as this policy decides when it fails.
        /// </summary>
        /// <typeparam name="TState">
        /// The type of the state passed to the operation.
        /// </typeparam>
        /// <typeparam name="T">
        /// The type of the value the operation returns.
        /// </typeparam>
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
        /// Thrown, before a task is returned, if <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// <para>
        /// When an exception is not retried, or the strategy allows no more retries, the last exception is rethrown with its original stack trace.
        /// </para>
        /// <para>
        /// Pass the values that the operation needs as <paramref name="state"/> and use an operation that captures no variables, such as a
        /// <see langword="static"/> lambda; the compiler then reuses one delegate instance, so the call allocates no closure. Use a tuple to
        /// pass several values.
        /// </para>
        /// </remarks>
        public Task<T> ExecuteAsync<TState, T>(TState state, Func<TState, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            return RetryExecutor.ExecuteAsync<T, StatefulTaskOperation<TState, T>, ExceptionPolicyHooks<T>>(Strategy, new(state, operation), new(this), cancellationToken);
        }

        /// <summary>
        /// Creates a copy of this policy with a different exception classifier.
        /// </summary>
        /// <param name="exceptionFilter">
        /// The new exception classifier.
        /// </param>
        /// <returns>
        /// A new policy.
        /// </returns>
        private RetryPolicy WithExceptionFilter(Func<Exception, bool> exceptionFilter)
            => new(Strategy, exceptionFilter, DelayOverride, RetryHandler);
    }

    /// <summary>
    /// Represents a retry policy for operations returning <typeparamref name="T"/>: a strategy that decides whether and when to retry, the
    /// exceptions and results that are retried, and callbacks for each retry.
    /// </summary>
    /// <typeparam name="T">
    /// The type returned by the operations the policy runs.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// Create this policy with <see cref="RetryPolicy.RetryOnResult{T}(Func{T, bool})"/> or
    /// <see cref="RetryStrategyExtensions.RetryOnResult{T}(IRetryStrategy, Func{T, bool})"/>. Like <see cref="RetryPolicy"/>, it is immutable:
    /// each configuration method returns a new policy, and a policy can be shared by concurrent executions.
    /// </para>
    /// <para>
    /// When the strategy allows no more retries after a retried result, execution returns that result. Results that execution does not return
    /// are passed to the handlers registered with <see cref="OnDiscarded(Action{T})"/>; the result that execution returns remains the caller's.
    /// </para>
    /// <para>
    /// Callbacks run on the execution path. If an exception classifier throws, the operation's exception propagates as if it was not retried;
    /// an exception from a result classifier, delay override, retry handler, or discard handler propagates in place of the operation's outcome,
    /// and the operation is not retried.
    /// </para>
    /// </remarks>
    public sealed class RetryPolicy<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RetryPolicy{T}"/> class with all of its configuration.
        /// </summary>
        /// <param name="strategy">
        /// The strategy that decides whether and when to retry.
        /// </param>
        /// <param name="exceptionFilter">
        /// The exception classifier, or <see langword="null"/> to retry every exception.
        /// </param>
        /// <param name="resultFilter">
        /// The result classifier.
        /// </param>
        /// <param name="delayOverride">
        /// The delay override, or <see langword="null"/>.
        /// </param>
        /// <param name="retryHandler">
        /// The retry handlers, or <see langword="null"/>.
        /// </param>
        /// <param name="discardHandler">
        /// The discard handlers, or <see langword="null"/>.
        /// </param>
        internal RetryPolicy(IRetryStrategy strategy, Func<Exception, bool>? exceptionFilter, Func<T, bool> resultFilter,
            Func<RetryContext<T>, TimeSpan>? delayOverride, Action<RetryContext<T>>? retryHandler, Action<T>? discardHandler)
        {
            Strategy = strategy;
            ExceptionFilter = exceptionFilter;
            ResultFilter = resultFilter;
            DelayOverride = delayOverride;
            RetryHandler = retryHandler;
            DiscardHandler = discardHandler;
        }

        /// <summary>
        /// Gets the strategy of this policy.
        /// </summary>
        /// <value>
        /// The <see cref="IRetryStrategy"/> that decides whether and when to retry.
        /// </value>
        public IRetryStrategy Strategy { get; }

        /// <summary>
        /// Gets the exception classifier.
        /// </summary>
        /// <value>
        /// A function returning <see langword="true"/> for retried exceptions, or <see langword="null"/> to retry every exception.
        /// </value>
        internal Func<Exception, bool>? ExceptionFilter { get; }

        /// <summary>
        /// Gets the result classifier.
        /// </summary>
        /// <value>
        /// A function returning <see langword="true"/> for retried results.
        /// </value>
        internal Func<T, bool> ResultFilter { get; }

        /// <summary>
        /// Gets the delay override.
        /// </summary>
        /// <value>
        /// The function replacing the strategy's delay, or <see langword="null"/>.
        /// </value>
        internal Func<RetryContext<T>, TimeSpan>? DelayOverride { get; }

        /// <summary>
        /// Gets the retry handlers.
        /// </summary>
        /// <value>
        /// The handlers invoked for each approved retry, or <see langword="null"/>.
        /// </value>
        internal Action<RetryContext<T>>? RetryHandler { get; }

        /// <summary>
        /// Gets the discard handlers.
        /// </summary>
        /// <value>
        /// The handlers invoked for each result that execution does not return, or <see langword="null"/>.
        /// </value>
        internal Action<T>? DiscardHandler { get; }

        /// <summary>
        /// Creates a policy that also retries exceptions of the specified type.
        /// </summary>
        /// <typeparam name="TException">
        /// The type of the exceptions to retry, including derived types.
        /// </typeparam>
        /// <returns>
        /// A new policy that retries the exceptions this policy retries and the exceptions of type <typeparamref name="TException"/>.
        /// </returns>
        /// <remarks>
        /// The first <c>RetryOn</c> call limits retries to the exceptions it selects; later calls add to them.
        /// </remarks>
        public RetryPolicy<T> RetryOn<TException>() where TException : Exception
            => WithExceptionFilter(ExceptionFilters.Or(ExceptionFilter, ExceptionFilters.OfType<TException>()));

        /// <summary>
        /// Creates a policy that also retries exceptions of the specified type that satisfy a condition.
        /// </summary>
        /// <typeparam name="TException">
        /// The type of the exceptions to retry, including derived types.
        /// </typeparam>
        /// <param name="predicate">
        /// A function that returns <see langword="true"/> for the exceptions of type <typeparamref name="TException"/> to retry.
        /// </param>
        /// <returns>
        /// A new policy that retries the exceptions this policy retries and the exceptions that <paramref name="predicate"/> selects.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="predicate"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// The first <c>RetryOn</c> call limits retries to the exceptions it selects; later calls add to them. If <paramref name="predicate"/>
        /// throws, the operation's exception propagates without a retry.
        /// </remarks>
        public RetryPolicy<T> RetryOn<TException>(Func<TException, bool> predicate) where TException : Exception
            => WithExceptionFilter(ExceptionFilters.Or(ExceptionFilter, ExceptionFilters.OfType(predicate)));

        /// <summary>
        /// Creates a policy that also retries the exceptions that satisfy a condition.
        /// </summary>
        /// <param name="predicate">
        /// A function that returns <see langword="true"/> for the exceptions to retry.
        /// </param>
        /// <returns>
        /// A new policy that retries the exceptions this policy retries and the exceptions that <paramref name="predicate"/> selects.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="predicate"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// The first <c>RetryOn</c> call limits retries to the exceptions it selects; later calls add to them. If <paramref name="predicate"/>
        /// throws, the operation's exception propagates without a retry.
        /// </remarks>
        public RetryPolicy<T> RetryOn(Func<Exception, bool> predicate)
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            return WithExceptionFilter(ExceptionFilters.Or(ExceptionFilter, predicate));
        }

        /// <summary>
        /// Creates a policy that also retries the results that satisfy a condition.
        /// </summary>
        /// <param name="predicate">
        /// A function that returns <see langword="true"/> for the results to retry.
        /// </param>
        /// <returns>
        /// A new policy that retries the results this policy retries and the results that <paramref name="predicate"/> selects.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="predicate"/> is <see langword="null"/>.
        /// </exception>
        public RetryPolicy<T> RetryOnResult(Func<T, bool> predicate)
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            var resultFilter = ResultFilter;
            return new(Strategy, ExceptionFilter, result => resultFilter(result) || predicate(result), DelayOverride, RetryHandler, DiscardHandler);
        }

        /// <summary>
        /// Creates a policy that replaces the strategy's delay with one computed from the failed attempt.
        /// </summary>
        /// <param name="delay">
        /// A function that receives the failed attempt, whose <see cref="RetryContext{T}.Delay"/> is the delay that the strategy proposes, and
        /// returns the nonnegative delay to wait. Return <see cref="RetryContext{T}.Delay"/> to keep the proposed delay.
        /// </param>
        /// <returns>
        /// A new policy whose delays <paramref name="delay"/> selects. It replaces any delay override of this policy.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="delay"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The override runs only after the strategy allows a retry, and its delay replaces the proposed delay after all strategy modifiers, so it
        /// can exceed a delay cap or the time left under an elapsed-time limit. Enforce any required deadline in the override or with the caller's
        /// cancellation token.
        /// </para>
        /// <para>
        /// A negative delay throws <see cref="ArgumentOutOfRangeException"/> from the execution, without a retry. A delay longer than
        /// <see cref="int.MaxValue"/> milliseconds stops retrying, as when the strategy allows no more retries.
        /// </para>
        /// </remarks>
        public RetryPolicy<T> OverrideDelay(Func<RetryContext<T>, TimeSpan> delay)
        {
            if (delay is null)
                throw new ArgumentNullException(nameof(delay));

            return new(Strategy, ExceptionFilter, ResultFilter, delay, RetryHandler, DiscardHandler);
        }

        /// <summary>
        /// Creates a policy that invokes a handler for each approved retry.
        /// </summary>
        /// <param name="handler">
        /// The handler, which receives the failed attempt and the delay that execution waits before the retry.
        /// </param>
        /// <returns>
        /// A new policy that invokes the handlers of this policy and then <paramref name="handler"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="handler"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// Handlers run before the wait, and only when a retry follows: not when the strategy allows no more retries, and not for outcomes that
        /// are not retried. For a retried result, they run before the discard handlers. If a handler throws, later handlers do not run.
        /// </remarks>
        public RetryPolicy<T> OnRetry(Action<RetryContext<T>> handler)
        {
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            return new(Strategy, ExceptionFilter, ResultFilter, DelayOverride, RetryHandler + handler, DiscardHandler);
        }

        /// <summary>
        /// Creates a policy that invokes a handler for each result that execution does not return, such as to release its resources.
        /// </summary>
        /// <param name="handler">
        /// The handler, which receives the discarded result.
        /// </param>
        /// <returns>
        /// A new policy that invokes the discard handlers of this policy and then <paramref name="handler"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="handler"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// A retried result is discarded once: after the retry handlers and before the wait, or when a result classifier, delay override, retry
        /// handler, or wait fails. A result that is not retried, and the last result returned when the strategy allows no more retries, are not
        /// discarded and remain the caller's.
        /// </para>
        /// <para>
        /// If a handler throws, later handlers do not run, and its exception propagates in place of any earlier callback or wait exception.
        /// </para>
        /// </remarks>
        public RetryPolicy<T> OnDiscarded(Action<T> handler)
        {
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            return new(Strategy, ExceptionFilter, ResultFilter, DelayOverride, RetryHandler, DiscardHandler + handler);
        }

        /// <summary>
        /// Runs a blocking operation and retries it as this policy decides when it fails or returns a retried result.
        /// </summary>
        /// <param name="operation">
        /// The operation to run. It receives <paramref name="cancellationToken"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// A token for canceling the operation and the waits between attempts (optional).
        /// </param>
        /// <returns>
        /// The first result that is not retried, or the last result when the strategy allows no more retries.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// The calling thread is blocked during the waits between attempts. When an exception is not retried, or the strategy allows no more
        /// retries, the last exception is rethrown with its original stack trace.
        /// </remarks>
        public T Execute(Func<CancellationToken, T> operation, CancellationToken cancellationToken = default)
        {
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            return RetryExecutor.Execute<T, FuncOperation<T>, ResultPolicyHooks<T>>(Strategy, new(operation), new(this), cancellationToken);
        }

        /// <summary>
        /// Runs an asynchronous operation and retries it as this policy decides when it fails or returns a retried result.
        /// </summary>
        /// <param name="operation">
        /// The operation to run. It receives <paramref name="cancellationToken"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// A token for canceling the operation and the waits between attempts (optional).
        /// </param>
        /// <returns>
        /// A task that resolves to the first result that is not retried, or the last result when the strategy allows no more retries.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown, before a task is returned, if <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// When an exception is not retried, or the strategy allows no more retries, the last exception is rethrown with its original stack trace.
        /// </remarks>
        public Task<T> ExecuteAsync(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            return RetryExecutor.ExecuteAsync<T, TaskOperation<T>, ResultPolicyHooks<T>>(Strategy, new(operation), new(this), cancellationToken);
        }

        /// <summary>
        /// Runs a blocking operation that receives a state, and retries it as this policy decides when it fails or returns a retried result.
        /// </summary>
        /// <typeparam name="TState">
        /// The type of the state passed to the operation.
        /// </typeparam>
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
        /// The first result that is not retried, or the last result when the strategy allows no more retries.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The calling thread is blocked during the waits between attempts. When an exception is not retried, or the strategy allows no more
        /// retries, the last exception is rethrown with its original stack trace.
        /// </para>
        /// <para>
        /// Pass the values that the operation needs as <paramref name="state"/> and use an operation that captures no variables, such as a
        /// <see langword="static"/> lambda; the compiler then reuses one delegate instance, so the call allocates no closure. Use a tuple to
        /// pass several values.
        /// </para>
        /// </remarks>
        public T Execute<TState>(TState state, Func<TState, CancellationToken, T> operation, CancellationToken cancellationToken = default)
        {
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            return RetryExecutor.Execute<T, StatefulFuncOperation<TState, T>, ResultPolicyHooks<T>>(Strategy, new(state, operation), new(this), cancellationToken);
        }

        /// <summary>
        /// Runs an asynchronous operation that receives a state, and retries it as this policy decides when it fails or returns a retried result.
        /// </summary>
        /// <typeparam name="TState">
        /// The type of the state passed to the operation.
        /// </typeparam>
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
        /// A task that resolves to the first result that is not retried, or the last result when the strategy allows no more retries.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown, before a task is returned, if <paramref name="operation"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is canceled while waiting before a retry.
        /// </exception>
        /// <remarks>
        /// <para>
        /// When an exception is not retried, or the strategy allows no more retries, the last exception is rethrown with its original stack trace.
        /// </para>
        /// <para>
        /// Pass the values that the operation needs as <paramref name="state"/> and use an operation that captures no variables, such as a
        /// <see langword="static"/> lambda; the compiler then reuses one delegate instance, so the call allocates no closure. Use a tuple to
        /// pass several values.
        /// </para>
        /// </remarks>
        public Task<T> ExecuteAsync<TState>(TState state, Func<TState, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            return RetryExecutor.ExecuteAsync<T, StatefulTaskOperation<TState, T>, ResultPolicyHooks<T>>(Strategy, new(state, operation), new(this), cancellationToken);
        }

        /// <summary>
        /// Creates a copy of this policy with a different exception classifier.
        /// </summary>
        /// <param name="exceptionFilter">
        /// The new exception classifier.
        /// </param>
        /// <returns>
        /// A new policy.
        /// </returns>
        private RetryPolicy<T> WithExceptionFilter(Func<Exception, bool> exceptionFilter)
            => new(Strategy, exceptionFilter, ResultFilter, DelayOverride, RetryHandler, DiscardHandler);
    }
}
