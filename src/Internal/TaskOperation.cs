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
    /// Adapts an asynchronous operation that returns a value.
    /// </summary>
    /// <typeparam name="T">
    /// The type returned by the operation.
    /// </typeparam>
    internal readonly struct TaskOperation<T> : IAsyncRetryOperation<T>
    {
        private readonly Func<CancellationToken, Task<T>> _operation;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskOperation{T}"/> struct.
        /// </summary>
        /// <param name="operation">
        /// The operation to run.
        /// </param>
        public TaskOperation(Func<CancellationToken, Task<T>> operation) => _operation = operation;

        /// <inheritdoc/>
        public Task Invoke(CancellationToken cancellationToken) => _operation(cancellationToken);

        /// <inheritdoc/>
        public T GetResult(Task completed) => ((Task<T>)completed).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Adapts an asynchronous operation that returns no value.
    /// </summary>
    internal readonly struct TaskOperation : IAsyncRetryOperation<VoidResult>
    {
        private readonly Func<CancellationToken, Task> _operation;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskOperation"/> struct.
        /// </summary>
        /// <param name="operation">
        /// The operation to run.
        /// </param>
        public TaskOperation(Func<CancellationToken, Task> operation) => _operation = operation;

        /// <inheritdoc/>
        public Task Invoke(CancellationToken cancellationToken) => _operation(cancellationToken);

        /// <inheritdoc/>
        public VoidResult GetResult(Task completed) => default;
    }
}
