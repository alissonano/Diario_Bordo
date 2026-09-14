namespace Diario_de_Bordo.Models
{
    public interface IFujiService
    {
        Task<FujiMachineStatus?> GetStatusByNameAsync(string machineName, string line);
    }
}