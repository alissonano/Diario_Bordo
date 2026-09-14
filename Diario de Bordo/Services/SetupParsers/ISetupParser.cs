using System.Collections.Generic;
using System.IO;
using Diario_de_Bordo.Models;

namespace Diario_de_Bordo.Services.SetupParsers
{
    public interface ISetupParser
    {
        // Agora o Parser diz se ele suporta o tipo de máquina enviado
        bool SuportaMaquina(string maquinaTipo);
        List<DtoFeederCheck> Processar(Stream fileStream);
    }
}