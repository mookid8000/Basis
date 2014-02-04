namespace Basis.Tests
{
    public class UrlHelper
    {
        static int _port = 3000;
        public static string GetNextLocalhostUrl()
        {
            const string eventStoreListenUriFormatString = "http://localhost:{0}";

            return string.Format(eventStoreListenUriFormatString, _port++);
        } 
    }
}