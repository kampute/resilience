// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Internal
{
    using System.Threading;

    /// <summary>
    /// Runs one attempt of a synchronous operation.
    /// </summary>
    /// <typeparam name="T">
    /// The type returned by the operation.
    /// </typeparam>
    /// <remarks>
    /// Implementations are structs so that the executor is specialized for each operation shape without allocating an adapter.
    /// </remarks>
    internal interface IRetryOperation<T>
    {
        /// <summary>
        /// Runs one attempt.
        /// </summary>
        /// <param name="cancellationToken">
        /// The caller's token.
        /// </param>
        /// <returns>
        /// The value returned by the attempt.
        /// </returns>
        T Invoke(CancellationToken cancellationToken);
    }
}
