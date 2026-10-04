// Copyright (C) Kampute
//
// This file is part of the Kampute.Resilience package and is released under the terms of the MIT license.
// See the LICENSE file in the project root for the full license text.

namespace Kampute.Resilience.Internal
{
    using System;

    /// <summary>
    /// Builds the exception classifiers of retry policies.
    /// </summary>
    internal static class ExceptionFilters
    {
        /// <summary>
        /// Gets a classifier that selects the exceptions of a type.
        /// </summary>
        /// <typeparam name="TException">
        /// The type of the exceptions to select.
        /// </typeparam>
        /// <returns>
        /// A cached classifier.
        /// </returns>
        internal static Func<Exception, bool> OfType<TException>() where TException : Exception => TypeFilter<TException>.Instance;

        /// <summary>
        /// Creates a classifier that selects the exceptions of a type that satisfy a condition.
        /// </summary>
        /// <typeparam name="TException">
        /// The type of the exceptions to select.
        /// </typeparam>
        /// <param name="predicate">
        /// The condition.
        /// </param>
        /// <returns>
        /// The classifier.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="predicate"/> is <see langword="null"/>.
        /// </exception>
        internal static Func<Exception, bool> OfType<TException>(Func<TException, bool> predicate) where TException : Exception
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            return exception => exception is TException typed && predicate(typed);
        }

        /// <summary>
        /// Combines an existing classifier with another, selecting the exceptions that either selects.
        /// </summary>
        /// <param name="existing">
        /// The existing classifier, or <see langword="null"/> when no exceptions have been selected yet.
        /// </param>
        /// <param name="added">
        /// The classifier to add.
        /// </param>
        /// <returns>
        /// <paramref name="added"/> when <paramref name="existing"/> is <see langword="null"/>; otherwise, their combination.
        /// </returns>
        internal static Func<Exception, bool> Or(Func<Exception, bool>? existing, Func<Exception, bool> added)
            => existing is null ? added : exception => existing(exception) || added(exception);

        /// <summary>
        /// Caches the classifier that selects the exceptions of one type.
        /// </summary>
        /// <typeparam name="TException">
        /// The type of the exceptions to select.
        /// </typeparam>
        private static class TypeFilter<TException> where TException : Exception
        {
            /// <summary>
            /// The cached classifier.
            /// </summary>
            internal static readonly Func<Exception, bool> Instance = exception => exception is TException;
        }
    }
}
