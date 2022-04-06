namespace ReserveBlockCore.Models
{
    public class TaskAnswer
    {
        public string Address { get; set; }
        public string Answer { get; set; }
        public Block Block { get; set; }
        public DateTime SubmitTime { get; set; }
    }
}
