namespace ReserveBlockCore.Models
{
    public class AdjVoteInReqs
    {
        public string RBXAddress { get; set; }
        public string IPAddress { get; set; }
        public Provider ProviderForMachine { get; set; }
        public string MachineType { get; set; }
        public OS MachineOS { get; set; }
        public int MachineRam { get; set; } 
        public string MachineCPU { get; set; }
        public int MachineCPUCores { get; set; }
        public int MachineCPUThreads { get; set; }
        public int MachineHDDSize { get; set; }  
        public HDSizeSpecifier MachineHDDSpecifier { get; set; }
        public int InternetSpeedUp { get; set; }
        public int InternetSpeedDown { get; set; }
        public int Bandwith { get; set; }
        public string TechnicalBackground { get; set; }
        public string ReasonForAdjJoin { get; set; }
        public string? GithubLink { get; set; }
        public string? SupplementalURLs { get; set; }    
    }

    public enum Provider
    {
        OnlineCloudVPS,
        OnlineDedicated,
        LocalDedicated,
        HomeMachine,
        OfficeMachine
    }

    public enum OS
    {
        Linux,
        Windows,
        Mac
    }
    public enum HDSizeSpecifier
    {
        GB,
        TB,
        PB
    }
}
