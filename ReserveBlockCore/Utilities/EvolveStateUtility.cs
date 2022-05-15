namespace ReserveBlockCore.Utilities
{
    public static class EvolveStateUtility
    {
        public static string GetEvolveStateLetter(int count)
        {
            var output = EvolveStateLetterDict[count];

            return output;
        }

        public static readonly Dictionary<int, string> EvolveStateLetterDict = new Dictionary<int, string> {
            {1, "A" },
            {2, "B"},
            {3, "C"},
            {4, "D"},
            {5, "E"},
            {6, "F"},
            {7, "H"},
            {8, "I"},
            {9, "J" },
            {10, "K"},
            {11, "L"},
            {12, "M"},
            {13, "N"},
            {14, "O"},
            {15, "P"},
            {16, "Q"},
            {17, "R"},
            {18, "S"},
            {19, "T"},
            {20, "U"},
            {21, "V"},
            {22, "W"},
            {23, "X"},
            {24, "Y"},
            {25, "Z"},
        };
    }
}
