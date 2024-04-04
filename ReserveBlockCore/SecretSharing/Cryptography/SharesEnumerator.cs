namespace ReserveBlockCore.SecretSharing.Cryptography
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Supports a iteration over <see cref="Shares{TNumber}"/> collection.
    /// </summary>
    /// <typeparam name="TNumber">The type of integer which is used by the <see cref="FinitePoint{TNumber}"/> items of the
    /// <see cref="Shares{TNumber}"/> collection.</typeparam>
    public sealed class SharesEnumerator<TNumber> : IEnumerator<FinitePoint<TNumber>>
    {
        /// <summary>
        /// Saves a list of <see cref="FinitePoint{TNumber}"/>.
        /// </summary>
        private readonly ReadOnlyCollection<FinitePoint<TNumber>> shareList;

        /// <summary>
        /// Saves the current of the enumerator
        /// </summary>
        private int position = -1;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharesEnumerator{TNumber}"/> class.
        /// </summary>
        /// <param name="shares">A collection of <see cref="FinitePoint{TNumber}"/> items representing the shares.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="shares"/> is <see langword="null"/></exception>
        public SharesEnumerator(Collection<FinitePoint<TNumber>> shares)
        {
            _ = shares ?? throw new ArgumentNullException(nameof(shares));
            this.shareList = new ReadOnlyCollection<FinitePoint<TNumber>>(shares);
        }

        /// <summary>
        /// Advances the enumerator to the next element of the <see cref="Shares{TNumber}"/> collection.
        /// </summary>
        /// <returns><see langword="true"/> if the enumerator was successfully advanced to the next element;
        /// <see langword="false"/> if the enumerator has passed the end of the <see cref="Shares{TNumber}"/> collection.</returns>
        public bool MoveNext()
        {
            this.position++;
            return this.position < this.shareList.Count;
        }

        /// <summary>
        /// Sets the enumerator to its initial position, which is before the first element in the <see cref="Shares{TNumber}"/> collection.
        /// </summary>
        public void Reset() => this.position = -1;

        /// <summary>
        /// Performs tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() { }

        /// <summary>
        /// Gets the element in the <see cref="Shares{TNumber}"/> collection at the current position of the enumerator.
        /// </summary>
        object IEnumerator.Current => this.Current;

        /// <summary>
        /// Gets the element in the <see cref="Shares{TNumber}"/> collection at the current position of the enumerator.
        /// </summary>
        public FinitePoint<TNumber> Current
        {
            get
            {
                try
                {
                    return this.shareList[this.position];
                }
                catch (IndexOutOfRangeException)
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }
}
