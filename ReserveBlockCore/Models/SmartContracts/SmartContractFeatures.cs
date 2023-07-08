namespace ReserveBlockCore.Models
{
    public class SmartContractFeatures
    {
        public FeatureName FeatureName { get; set; } //Royalty, Evolving, Music, Ticket, etc.
        public object FeatureFeatures { get; set; }
    }

    public enum FeatureName
    { 
        Evolving, //returns a list of EvolvingFeatures
        Royalty, // returns a class of RoyaltyFeatures
        MultiAsset,
        Tokenization,//class
        Music, //Class with a list of songs
        MultiOwner, //List of MultiOwnerFeatures
        SelfDestruct,//class
        Consumable,//class
        Fractionalized,//class
        Paired,//class
        Wrapped,//class
        Soulbound,//class
        Ticket,//class
        Token //class
    }
}