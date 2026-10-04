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
    /// Owns the rules that retry sessions and policies share for measuring time, accepting a delay, and waiting it.
    /// </summary>
    internal static class RetryDelay
    {
        /// <summary>
        /// The number of <see cref="TimeSpan"/> ticks in one <see cref="Stopwatch"/> tick.
        /// </summary>
        private static readonly double TicksPerTimestamp = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;

        /// <summary>
        /// Gets the time elapsed since a <see cref="Stopwatch.GetTimestamp"/> value.
        /// </summary>
        /// <param name="startTimestamp">
        /// The timestamp at which measurement started.
        /// </param>
        /// <returns>
        /// The elapsed time.
        /// </returns>
        /// <remarks>
        /// The conversion uses floating point so that long executions cannot overflow the tick arithmetic.
        /// </remarks>
        internal static TimeSpan ElapsedSince(long startTimestamp)
            => TimeSpan.FromTicks((long)((Stopwatch.GetTimestamp() - startTimestamp) * TicksPerTimestamp));

        /// <summary>
        /// Accepts a delay for waiting, treating a negative delay as zero.
        /// </summary>
        /// <param name="delay">
        /// The delay to accept; on return, the nonnegative delay to wait.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the delay can be waited; <see langword="false"/> if it is longer than <see cref="int.MaxValue"/> milliseconds,
        /// the limit of <see cref="Task.Delay(TimeSpan, CancellationToken)"/> on .NET Framework.
        /// </returns>
        internal static bool TryAccept(ref TimeSpan delay)
        {
            if (delay.TotalMilliseconds > int.MaxValue)
                return false;

            if (delay < TimeSpan.Zero)
                delay = TimeSpan.Zero;

            return true;
        }

        /// <summary>
        /// Asynchronously waits for a positive delay, or observes cancellation without waiting for a zero delay.
        /// </summary>
        /// <param name="delay">
        /// The accepted, nonnegative delay.
        /// </param>
        /// <param name="cancellationToken">
        /// The token canceling the wait.
        /// </param>
        /// <returns>
        /// A task that completes when the delay ends, or is canceled when the token is canceled during the delay.
        /// </returns>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the delay is zero and the token is canceled.
        /// </exception>
        /// <remarks>
        /// Callers are async methods, which store the synchronous exception in their own task.
        /// </remarks>
        internal static Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            if (delay > TimeSpan.Zero)
                return Task.Delay(delay, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Blocks for an accepted delay, ending the wait promptly when the token is canceled.
        /// </summary>
        /// <param name="delay">
        /// The accepted, nonnegative delay.
        /// </param>
        /// <param name="cancellationToken">
        /// The token canceling the wait.
        /// </param>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the token is canceled.
        /// </exception>
        internal static void Wait(TimeSpan delay, CancellationToken cancellationToken)
        {
            if (delay > TimeSpan.Zero)
            {
                if (cancellationToken.CanBeCanceled)
                    cancellationToken.WaitHandle.WaitOne(delay);
                else
                    Thread.Sleep(delay);
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
