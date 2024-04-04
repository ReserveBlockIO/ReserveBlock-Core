namespace ReserveBlockCore.SecretSharing.Cryptography
{
    internal static class SharedSeparator
    {
        /// <summary>
        /// The separator between the X and Y coordinate
        /// </summary>
        internal const char CoordinateSeparator = '-';

        /// <summary>
        /// Separator array for <see cref="string.Split(char[])"/> method usage to avoid allocation of a new array.
        /// </summary>
        internal static readonly char[] CoordinateSeparatorArray = { CoordinateSeparator };
    }
}
