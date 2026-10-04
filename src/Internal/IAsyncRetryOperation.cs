// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Internal
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Runs one attempt of an asynchronous operation.
    /// </summary>
    /// <typeparam name="T">
    /// The type returned by the operation.
    /// </typeparam>
    /// <remarks>
    /// Implementations are structs so that the executor is specialized for each operation shape without allocating an adapter.
    /// </remarks>
    internal interface IAsyncRetryOperation<T>
    {
        /// <summary>
        /// Starts one attempt.
        /// </summary>
        /// <param name="cancellationToken">
        /// The caller's token.
        /// </param>
        /// <returns>
        /// The task of the attempt.
        /// </returns>
        Task Invoke(CancellationToken cancellationToken);

        /// <summary>
        /// Gets the value of an attempt that completed successfully.
        /// </summary>
        /// <param name="completed">
        /// The task returned by <see cref="Invoke"/>, after it completed successfully.
        /// </param>
        /// <returns>
        /// The value returned by the attempt.
        /// </returns>
        T GetResult(Task completed);
    }
}
