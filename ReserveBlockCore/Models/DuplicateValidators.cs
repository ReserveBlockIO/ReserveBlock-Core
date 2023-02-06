namespace ReserveBlockCore.Models
{
    public class DuplicateValidators
    {
        public string Address { get; set; }
        public ReasonFor Reason { get; set; }  
        public DateTime LastNotified { get; set; }
        public DateTime LastDetection { get; set; }
        public int NotifyCount { get; set; }
        public bool StopNotify { get; set; }   
        public string IPAddress { get; set; }

        public enum ReasonFor
        { 
            DuplicateIP,
            DuplicateAddress
        }

    }
}
