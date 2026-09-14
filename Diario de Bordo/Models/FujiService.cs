using System.Text.Json;

namespace Diario_de_Bordo.Models
{
    public class FujiService : IFujiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _baseUrl = "http://172.20.100.4/fujiweb/api/EquipInfo"; // Endpoint do servidor central

        public FujiService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<FujiMachineStatus?> GetStatusByNameAsync(string machineName, string line)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                // No Accessory Software, geralmente passamos o nome da máquina via QueryString
                // Verifique se o endpoint é algo como EquipInfo?machineName=L1NXT3A
                var url = $"{_baseUrl}?machineName={machineName}&lineName={line}";

                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<FujiMachineStatus>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                return null;
            }
            catch (Exception ex)
            {
                // Log de erro aqui
                return null;
            }
        }
    }
}
