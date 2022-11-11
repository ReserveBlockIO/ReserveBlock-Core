namespace ReserveBlockCore.Models
{
    public class TaskNumberAnswerV2
    {
        public string Address { get; set; }
        public string Answer { get; set; }
        public long NextBlockHeight { get; set; }
        public DateTime SubmitTime { get; set; }
    }
}
