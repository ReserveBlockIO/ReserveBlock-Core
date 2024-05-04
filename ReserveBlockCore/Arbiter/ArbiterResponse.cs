namespace ReserveBlockCore.Arbiter
{
    public class ArbiterResponse
    {
        public class ArbiterAddressRequest
        {
            public string Address { get; set; }
            public string Share { get; set; }
            public string? EncryptedShare { get; set; }
        }
    }
}
