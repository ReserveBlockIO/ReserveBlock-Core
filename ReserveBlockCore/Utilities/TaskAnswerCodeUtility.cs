namespace ReserveBlockCore.Utilities
{
    public class TaskAnswerCodeUtility
    {
        public static async Task<string> TaskAnswerCodeReason(int code)
        {
            string reason = "";

            switch(code)
            {
                case 0:
                    reason = "No Error.";
                    break;
                case 1:
                    reason = "Task Answer too large.";
                    break;
                case 2:
                    reason = "Answers block height did not match the adjudicators next block height. Please ensure blocks are up to date.";
                    break;
                case 3:
                    reason = "Validator address is not pressent in the validator pool. Please restart wallet.";
                    break;
                case 4:
                    reason = "Adjudicator is still booting up. Please wait.";
                    break;
                case 5:
                    reason = "Task answer was null. Should not be possible. Never send null Task Answer.";
                    break;
                case 6:
                    reason = "The signature was invalid.";
                    break;
                case 7:
                    reason = "Answer was already submitted.";
                    break;
                case 1337:
                    reason = "Unknown Error.";
                    break;
                    
                default: reason = "Error Code was not found.";
                    break;
            }

            return reason;
        }
    }
}
