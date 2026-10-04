// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience
{
    using System;

    /// <summary>
    /// Describes a failed attempt and the delay before the operation is retried.
    /// </summary>
    /// <remarks>
    /// <see cref="RetryPolicy"/> passes this value to its delay override and retry handlers. It is a value type, so describing a retry allocates
    /// nothing. A policy that retries only exceptions always supplies an <see cref="Exception"/>; a <see cref="RetryPolicy{T}"/> converts its
    /// <see cref="RetryContext{T}"/> to this type for handlers registered before it became result-aware, and then <see cref="Exception"/> is
    /// <see langword="null"/> for a retried result.
    /// </remarks>
    public readonly struct RetryContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RetryContext"/> struct.
        /// </summary>
        /// <param name="attemptNumber">
        /// The one-based number of the failed attempt.
        /// </param>
        /// <param name="elapsed">
        /// The time since execution started when the outcome was evaluated.
        /// </param>
        /// <param name="exception">
        /// The exception thrown by the attempt, or <see langword="null"/> for a retried result.
        /// </param>
        /// <param name="delay">
        /// The delay before the next attempt.
        /// </param>
        internal RetryContext(uint attemptNumber, TimeSpan elapsed, Exception? exception, TimeSpan delay)
        {
            AttemptNumber = attemptNumber;
            Elapsed = elapsed;
            Exception = exception;
            Delay = delay;
        }

        /// <summary>
        /// Gets the number of the attempt that failed.
        /// </summary>
        /// <value>
        /// A one-based number within this execution; the initial attempt is 1. It is also the number of retries made once this retry starts.
        /// </value>
        public uint AttemptNumber { get; }

        /// <summary>
        /// Gets the time since execution started when the outcome of the attempt was evaluated.
        /// </summary>
        /// <value>
        /// The elapsed execution time, including earlier attempts and waits.
        /// </value>
        public TimeSpan Elapsed { get; }

        /// <summary>
        /// Gets the exception thrown by the attempt.
        /// </summary>
        /// <value>
        /// The original exception, or <see langword="null"/> when a <see cref="RetryPolicy{T}"/> retries a returned result.
        /// </value>
        public Exception? Exception { get; }

        /// <summary>
        /// Gets the delay before the next attempt.
        /// </summary>
        /// <value>
        /// In a delay override, the delay that the strategy proposes; in a retry handler, the delay that execution waits.
        /// </value>
        public TimeSpan Delay { get; }
    }

    /// <summary>
    /// Describes a failed attempt of an operation that returns a value, and the delay before the operation is retried.
    /// </summary>
    /// <typeparam name="T">
    /// The type returned by the operation.
    /// </typeparam>
    /// <remarks>
    /// <see cref="RetryPolicy{T}"/> passes this value to its delay override and retry handlers. An attempt fails either by throwing an exception
    /// that the policy retries or by returning a result that the policy retries; <see cref="HasResult"/> tells them apart.
    /// </remarks>
    public readonly struct RetryContext<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RetryContext{T}"/> struct.
        /// </summary>
        /// <param name="attemptNumber">
        /// The one-based number of the failed attempt.
        /// </param>
        /// <param name="elapsed">
        /// The time since execution started when the outcome was evaluated.
        /// </param>
        /// <param name="result">
        /// The returned result, or the default value of <typeparamref name="T"/> for an exception.
        /// </param>
        /// <param name="exception">
        /// The exception thrown by the attempt, or <see langword="null"/> for a returned result.
        /// </param>
        /// <param name="delay">
        /// The delay before the next attempt.
        /// </param>
        internal RetryContext(uint attemptNumber, TimeSpan elapsed, T result, Exception? exception, TimeSpan delay)
        {
            AttemptNumber = attemptNumber;
            Elapsed = elapsed;
            Result = result;
            Exception = exception;
            Delay = delay;
        }

        /// <summary>
        /// Gets the number of the attempt that failed.
        /// </summary>
        /// <value>
        /// A one-based number within this execution; the initial attempt is 1. It is also the number of retries made once this retry starts.
        /// </value>
        public uint AttemptNumber { get; }

        /// <summary>
        /// Gets the time since execution started when the outcome of the attempt was evaluated.
        /// </summary>
        /// <value>
        /// The elapsed execution time, including earlier attempts and waits.
        /// </value>
        public TimeSpan Elapsed { get; }

        /// <summary>
        /// Gets whether the attempt returned a result rather than throwing an exception.
        /// </summary>
        /// <value>
        /// <see langword="true"/> for a returned result, including a <see langword="null"/> result; <see langword="false"/> for an exception.
        /// </value>
        public bool HasResult => Exception is null;

        /// <summary>
        /// Gets the value returned by the attempt.
        /// </summary>
        /// <value>
        /// The returned value when <see cref="HasResult"/> is <see langword="true"/>; otherwise, the default value of <typeparamref name="T"/>.
        /// </value>
        public T Result { get; }

        /// <summary>
        /// Gets the exception thrown by the attempt.
        /// </summary>
        /// <value>
        /// The original exception, or <see langword="null"/> when the attempt returned a result.
        /// </value>
        public Exception? Exception { get; }

        /// <summary>
        /// Gets the delay before the next attempt.
        /// </summary>
        /// <value>
        /// In a delay override, the delay that the strategy proposes; in a retry handler, the delay that execution waits.
        /// </value>
        public TimeSpan Delay { get; }

        /// <summary>
        /// Converts a context to the form that does not carry the result.
        /// </summary>
        /// <param name="context">
        /// The context to convert.
        /// </param>
        /// <returns>
        /// A <see cref="RetryContext"/> with the same attempt number, elapsed time, exception, and delay.
        /// </returns>
        public static implicit operator RetryContext(RetryContext<T> context)
            => new(context.AttemptNumber, context.Elapsed, context.Exception, context.Delay);

        /// <summary>
        /// Creates a context with the same outcome and a different delay.
        /// </summary>
        /// <param name="delay">
        /// The new delay.
        /// </param>
        /// <returns>
        /// A copy of this context with <see cref="Delay"/> set to <paramref name="delay"/>.
        /// </returns>
        internal RetryContext<T> WithDelay(TimeSpan delay) => new(AttemptNumber, Elapsed, Result, Exception, delay);
    }
}
