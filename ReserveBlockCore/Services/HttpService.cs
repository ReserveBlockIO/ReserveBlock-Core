namespace ReserveBlockCore.Services
{
    public class HttpService
    {
        public IHttpClientFactory httpFactory;
        public HttpService(IHttpClientFactory httpFactory)
        {
            this.httpFactory = httpFactory;
        }
        public IHttpClientFactory HttpClientFactory()
        {
            return httpFactory;
        }
    }
}
