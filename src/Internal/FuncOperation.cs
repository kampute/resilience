// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Internal
{
    using System;
    using System.Threading;

    /// <summary>
    /// Adapts a synchronous operation that returns a value.
    /// </summary>
    /// <typeparam name="T">
    /// The type returned by the operation.
    /// </typeparam>
    internal readonly struct FuncOperation<T> : IRetryOperation<T>
    {
        private readonly Func<CancellationToken, T> _operation;

        /// <summary>
        /// Initializes a new instance of the <see cref="FuncOperation{T}"/> struct.
        /// </summary>
        /// <param name="operation">
        /// The operation to run.
        /// </param>
        public FuncOperation(Func<CancellationToken, T> operation) => _operation = operation;

        /// <inheritdoc/>
        public T Invoke(CancellationToken cancellationToken) => _operation(cancellationToken);
    }
}
