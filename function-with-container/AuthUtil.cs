using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;

namespace ImageProcessorCore
{
    public class AuthUtil
    {
        public static async Task<string> GetToken()
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var token = await azureServiceTokenProvider.GetAccessTokenAsync("https://management.azure.com/");

            return token;
        }
    }
}