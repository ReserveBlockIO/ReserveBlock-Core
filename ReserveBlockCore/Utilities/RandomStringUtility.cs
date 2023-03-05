namespace ReserveBlockCore.Utilities
{
    public class RandomStringUtility
    {
        public static string GetRandomString(int numOfChars, bool addTimeStamp = false)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var stringChars = new char[numOfChars];
            var random = new Random();

            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            var finalString = addTimeStamp == false ? (new string(stringChars)) + TimeUtil.GetTime().ToString() : new string(stringChars);

            return finalString;
        }

        public static string GetRandomStringOnlyLetters(int numOfChars, bool addTimeStamp = false)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            var stringChars = new char[numOfChars];
            var random = new Random();

            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            var finalString = addTimeStamp == false ? (new string(stringChars)) + TimeUtil.GetTime().ToString() : new string(stringChars);

            return finalString;
        }
    }
}
