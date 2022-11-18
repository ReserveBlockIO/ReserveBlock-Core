namespace ReserveBlockCore
{
    public class HTTP
    {
        private static IHttpClientFactory httpFactory;
        public HTTP(IHttpClientFactory httpFactory)
        {
            httpFactory = httpFactory;
        }
        public static HttpClient Client()
        {
            return httpFactory.CreateClient();            
        }
    }
}
