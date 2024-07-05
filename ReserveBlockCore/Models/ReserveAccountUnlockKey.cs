using System.Security;

namespace ReserveBlockCore.Models
{
    public class ReserveAccountUnlockKey
    {
        public SecureString Password { get; set; }
        public long DeleteAfterTime { get; set; }
        public int UnlockTimeHours { get; set; }
    }
}
