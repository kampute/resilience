// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Internal
{
    using System;
    using System.Threading;

    /// <summary>
    /// Adapts a synchronous operation that receives a caller-supplied state and returns a value.
    /// </summary>
    /// <typeparam name="TState">
    /// The type of the state passed to the operation.
    /// </typeparam>
    /// <typeparam name="T">
    /// The type returned by the operation.
    /// </typeparam>
    internal readonly struct StatefulFuncOperation<TState, T> : IRetryOperation<T>
    {
        private readonly TState _state;
        private readonly Func<TState, CancellationToken, T> _operation;

        /// <summary>
        /// Initializes a new instance of the <see cref="StatefulFuncOperation{TState, T}"/> struct.
        /// </summary>
        /// <param name="state">
        /// The state passed to each attempt.
        /// </param>
        /// <param name="operation">
        /// The operation to run.
        /// </param>
        public StatefulFuncOperation(TState state, Func<TState, CancellationToken, T> operation)
        {
            _state = state;
            _operation = operation;
        }

        /// <inheritdoc/>
        public T Invoke(CancellationToken cancellationToken) => _operation(_state, cancellationToken);
    }
}
