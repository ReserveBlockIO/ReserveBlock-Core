namespace ReserveBlockCore.SecretSharing.Math
{
    using System.Collections.ObjectModel;

    /// <summary>
    /// Represents the result of the extended greatest common divisor computation.
    /// </summary>
    /// <typeparam name="TNumber">Numeric data type</typeparam>
    public interface IExtendedGcdResult<TNumber>
    {
        /// <summary>
        /// Gets the greatest common divisor
        /// </summary>
        Calculator<TNumber> GreatestCommonDivisor { get; }

        /// <summary>
        /// Gets the Bézout coefficients
        /// </summary>
        ReadOnlyCollection<Calculator<TNumber>> BezoutCoefficients { get; }

        /// <summary>
        /// Gets the quotients by the gcd
        /// </summary>
        ReadOnlyCollection<Calculator<TNumber>> Quotients { get; }
    }
}
