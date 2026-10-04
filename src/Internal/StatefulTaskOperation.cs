// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Internal
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Adapts an asynchronous operation that receives a caller-supplied state and returns a value.
    /// </summary>
    /// <typeparam name="TState">
    /// The type of the state passed to the operation.
    /// </typeparam>
    /// <typeparam name="T">
    /// The type returned by the operation.
    /// </typeparam>
    internal readonly struct StatefulTaskOperation<TState, T> : IAsyncRetryOperation<T>
    {
        private readonly TState _state;
        private readonly Func<TState, CancellationToken, Task<T>> _operation;

        /// <summary>
        /// Initializes a new instance of the <see cref="StatefulTaskOperation{TState, T}"/> struct.
        /// </summary>
        /// <param name="state">
        /// The state passed to each attempt.
        /// </param>
        /// <param name="operation">
        /// The operation to run.
        /// </param>
        public StatefulTaskOperation(TState state, Func<TState, CancellationToken, Task<T>> operation)
        {
            _state = state;
            _operation = operation;
        }

        /// <inheritdoc/>
        public Task Invoke(CancellationToken cancellationToken) => _operation(_state, cancellationToken);

        /// <inheritdoc/>
        public T GetResult(Task completed) => ((Task<T>)completed).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Adapts an asynchronous operation that receives a caller-supplied state and returns no value.
    /// </summary>
    /// <typeparam name="TState">
    /// The type of the state passed to the operation.
    /// </typeparam>
    internal readonly struct StatefulTaskOperation<TState> : IAsyncRetryOperation<VoidResult>
    {
        private readonly TState _state;
        private readonly Func<TState, CancellationToken, Task> _operation;

        /// <summary>
        /// Initializes a new instance of the <see cref="StatefulTaskOperation{TState}"/> struct.
        /// </summary>
        /// <param name="state">
        /// The state passed to each attempt.
        /// </param>
        /// <param name="operation">
        /// The operation to run.
        /// </param>
        public StatefulTaskOperation(TState state, Func<TState, CancellationToken, Task> operation)
        {
            _state = state;
            _operation = operation;
        }

        /// <inheritdoc/>
        public Task Invoke(CancellationToken cancellationToken) => _operation(_state, cancellationToken);

        /// <inheritdoc/>
        public VoidResult GetResult(Task completed) => default;
    }
}
