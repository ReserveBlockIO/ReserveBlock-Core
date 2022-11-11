namespace ReserveBlockCore.Models
{
    public class TaskNumberAnswerV3
    {
        public string Signature { get; set; }
        public string Answer { get; set; }
        public long NextBlockHeight { get; set; }
        public DateTime SubmitTime { get; set; }
    }
}
