using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Diario_de_Bordo.Models;
using Diario_de_Bordo.Services.SetupParsers;
using Diario_de_Bordo.Data;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Diario_de_Bordo.Services
{
    public class SetupExtractorService
    {
        private readonly IEnumerable<ISetupParser> _parsers;
        private readonly DiarioContext _context;

        // Injeta o contexto aqui
        public SetupExtractorService(IEnumerable<ISetupParser> parsers, DiarioContext context)
        {
            _parsers = parsers;
            _context = context;
        }

        public List<DtoFeederCheck> ProcessarArquivo(IFormFile file, string maquinaTipo)
        {
            // Seleciona o parser baseado na string vinda do Select do HTML
            var parser = _parsers.FirstOrDefault(p => p.SuportaMaquina(maquinaTipo));

            if (parser == null)
                throw new NotSupportedException($"Nenhum parser configurado para a máquina: {maquinaTipo}");

            return parser.Processar(file.OpenReadStream());
        }


        public async Task SalvarSetup(string linha, string programa, List<DtoFeederCheck> novosDados)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Remove registros antigos
                var antigos = _context.tbl_feedercheck
                    .Where(x => x.Linha == linha && x.Programa == programa);
                _context.tbl_feedercheck.RemoveRange(antigos);

                // 2. Mapeia e Insere
                var entidades = novosDados.Select(d => new FeederCheck
                {
                    Linha = linha,
                    Programa = programa,
                    Partnumber = d.Partnumber,
                    Maquina = d.Maquina,
                    Slot = d.Slot,
                    DataCriacao = DateTime.Now
                });

                await _context.tbl_feedercheck.AddRangeAsync(entidades);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


    }
}