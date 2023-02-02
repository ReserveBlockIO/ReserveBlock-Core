namespace ReserveBlockCore.Utilities
{
    public class ReadKeyUtility
    {
        internal static string OP { get; private set; }
        public static async Task ReadKeys()
        {
            ConsoleKeyInfo key = new ConsoleKeyInfo();

            while (!Console.KeyAvailable && key.Key != ConsoleKey.Escape)
            {

                key = Console.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                        OP += ("4oaR").ToStringFromBase64();
                        ZZrot(OP);
                        break;
                    case ConsoleKey.DownArrow:
                        OP += ("4oaT").ToStringFromBase64();
                        ZZrot(OP);
                        break;

                    case ConsoleKey.RightArrow:
                        OP += ("4oaS").ToStringFromBase64();
                        ZZrot(OP);
                        break;

                    case ConsoleKey.LeftArrow:
                        OP += ("4oaQ").ToStringFromBase64();
                        ZZrot(OP);
                        break;
                    case ConsoleKey.A:
                        OP += ("QQ==").ToStringFromBase64();
                        ZZrot(OP);
                        break;
                    case ConsoleKey.B:
                        OP += ("Qg==").ToStringFromBase64();
                        ZZrot(OP);
                        break;

                    case ConsoleKey.Escape:
                        break;

                    default:
                        if (Console.CapsLock && Console.NumberLock)
                        {
                            Console.WriteLine(key.KeyChar);
                        }
                        break;
                }
            }
        }

        private static void ZZrot(string ko)
        {
            if (ko == ("4oaR4oaR4oaT4oaT4oaQ4oaS4oaQ4oaSQkE=").ToStringFromBase64())
            {
                Console.WriteLine(" ");
                Console.WriteLine(" ");
                Console.WriteLine(("WW91IGFyZSB0aGUgZ3JhbmQgbWFzdGVyIG9mIGVnZyBodW50ZXJzLiBIZXJlIGlzIHlvdXIga2V5OiBrb25hbWlrZXk4MA==").ToStringFromBase64());
            }
        }
    }
}
