namespace ReserveBlockCore.SecretSharing.Cryptography
{
    using Math;

    /// <inheritdoc />
    public class ShamirsSecretSharing<TNumber> : ShamirsSecretSharing<TNumber, IExtendedGcdAlgorithm<TNumber>, ExtendedGcdResult<TNumber>>
    {
        /// <inheritdoc />
        public ShamirsSecretSharing(IExtendedGcdAlgorithm<TNumber> extendedGcd) : base(extendedGcd) { }
    }
}
