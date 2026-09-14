using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; // Importante para o LINQ
using Diario_de_Bordo.Models;
using Microsoft.Extensions.Logging;

namespace Diario_de_Bordo.Services.SetupParsers
{
    public class AssembleonCsvParser : ISetupParser
    {
        private readonly ILogger<AssembleonCsvParser> _logger;

        public AssembleonCsvParser(ILogger<AssembleonCsvParser> logger)
        {
            _logger = logger;
        }

        public bool SuportaMaquina(string maquinaTipo) => maquinaTipo == "Assembleon";

        public bool PodeProcessar(string nome) => nome.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

        public List<DtoFeederCheck> Processar(Stream fileStream)
        {
            var lista = new List<DtoFeederCheck>();

            using (var reader = new StreamReader(fileStream))
            {
                // PRIMEIRA LINHA É O NOME DO PROGRAMA
                string nomePrograma = reader.ReadLine();

                // Pular a segunda linha de cabeçalho
                reader.ReadLine();

                string linha;
                while ((linha = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(linha)) continue;

                    var colunas = linha.Split(',');

                    if (colunas.Length > 12)
                    {
                        lista.Add(new DtoFeederCheck
                        {
                            Programa = nomePrograma?.Trim(),
                            Partnumber = colunas[8].Trim(),
                            Maquina = colunas[6].Trim(),
                            Slot = colunas[12].Trim()
                        });
                    }
                }
            }

            // A MÁGICA DO LINQ: Remove duplicados e Ordena
            var listaProcessada = lista
                .GroupBy(x => new { x.Maquina, x.Slot, x.Partnumber }) // Remove duplicatas baseadas nestes 3 campos
                .Select(g => g.First())                               // Pega apenas a primeira ocorrência
                .OrderBy(x => x.Maquina)                              // Ordena Máquina (a-z)
                .ThenBy(x => x.Slot)                                  // Ordena Slot (a-z)
                .ThenBy(x => x.Partnumber)                            // Ordena Partnumber (a-z)
                .ToList();

            _logger.LogInformation("Assembleon CSV Parser finalizado. Itens originais: {orig}, Itens únicos processados: {count}", lista.Count, listaProcessada.Count);

            return listaProcessada;
        }
    }
}