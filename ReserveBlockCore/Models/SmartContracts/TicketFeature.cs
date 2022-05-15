namespace ReserveBlockCore.Models.SmartContracts
{
    public class TicketFeature
    {
        public string RedeemCode { get; set; }
        public int Quantity { get; set; }
        public string EventName { get; set; }
        public string EventDescription { get; set; }
        public string EventLocation { get; set; } 
        public DateTime EventDate { get; set; }
        public string EventCode { get; set; }
        public string EventWebsite { get; set; }
        public bool IsRedeemed { get; set; }
        public bool EvolveOnRedeem { get; set; }
        public string SeatInfo { get; set; }
        public DateTime? ExpireDate { get; set; }
    }
}
