namespace ReserveBlockCore.SecretSharing.Math
{
    /// <summary>
    /// Provides mechanism to compute the extended greatest common divisor
    /// including Bézout coefficients.
    /// </summary>
    /// <typeparam name="TNumber">Numeric data type (An integer type)</typeparam>
    /// <typeparam name="TExtendedGcdResult">Data type of the extended GCD result</typeparam>
    public interface IExtendedGcdAlgorithm<TNumber, out TExtendedGcdResult> where TExtendedGcdResult : struct, IExtendedGcdResult<TNumber>
    {
        /// <summary>
        /// Computes, in addition to the greatest common divisor of integers <paramref name="a"/> and <paramref name="b"/>, also the coefficients of Bézout's identity.
        /// </summary>
        /// <param name="a">An integer</param>
        /// <param name="b">An integer</param>
        /// <returns>For details: <see cref="IExtendedGcdResult{TNumber}"/></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "a")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "b")]
        TExtendedGcdResult Compute(Calculator<TNumber> a, Calculator<TNumber> b);
    }
}
