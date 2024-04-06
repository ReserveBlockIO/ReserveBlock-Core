namespace ReserveBlockCore.Dealer
{
    public class DealerResponse
    {
        public class DealerAddressRequest
        {
            public string Address { get; set; }
            public string Share { get; set; }
            public string? EncryptedShare { get; set; }
        }
    }
}
