// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Internal
{
    using System;
    using System.Threading;

    /// <summary>
    /// Adapts a synchronous operation that returns no value.
    /// </summary>
    internal readonly struct ActionOperation : IRetryOperation<VoidResult>
    {
        private readonly Action<CancellationToken> _operation;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionOperation"/> struct.
        /// </summary>
        /// <param name="operation">
        /// The operation to run.
        /// </param>
        public ActionOperation(Action<CancellationToken> operation) => _operation = operation;

        /// <inheritdoc/>
        public VoidResult Invoke(CancellationToken cancellationToken)
        {
            _operation(cancellationToken);
            return default;
        }
    }
}
