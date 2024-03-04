namespace ReserveBlockCore.Bitcoin.Services
{
    public class FeeCalcService
    {
        public static int EstimateTransactionSize(int numInputs, int numOutputs)
        {
            // Formula for estimating transaction size: 
            // size = (10 * numInputs) + (numOutputs * 34) + 10

            int size = (74 * numInputs) + (numOutputs * 34) + 10;
            return size;
        }
    }
}
