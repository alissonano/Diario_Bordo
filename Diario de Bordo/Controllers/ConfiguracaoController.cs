using Diario_de_Bordo.Data;
using Diario_de_Bordo.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

public class ConfiguracaoController : Controller
{
    private readonly DiarioContext _context;

    public ConfiguracaoController(DiarioContext context)
    {
        _context = context;
    }

    // GET: Exibe a tela de configuração de uma linha específica
    public async Task<IActionResult> Index(string linha)
    {
        if (string.IsNullOrEmpty(linha)) return RedirectToAction("Index", "Home");

        // 1. Busca a configuração da linha no banco
        var configBanco = await _context.tbl_configuracao_linhas
                                        .FirstOrDefaultAsync(x => x.Linha == linha);

        // 2. Se não existir no banco, cria um objeto novo para não dar erro na View
        if (configBanco == null)
        {
            configBanco = new ConfiguracaoLinha { Linha = linha };
        }

        // 3. Busca as paradas planejadas (se você já tiver essa tabela no contexto)
        // Se ainda não tiver a tabela, pode passar uma lista vazia: new List<ParadaPlanejada>()
        var paradas = await _context.tbl_paradas_planejadas
                                    .Where(p => p.Linha == linha)
                                    .ToListAsync();

        // 4. Monta o ViewModel EXATAMENTE como a View espera
        var viewModel = new ConfiguracaoLinhaViewModel
        {
            Configuracao = configBanco,
            ParadasPlanejadas = paradas
        };

        // 5. Retorna o viewModel (agora o tipo coincide com o @model da View)
        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> SalvarConfiguracao([FromBody] ConfiguracaoLinhaViewModel data)
    {
        if (data == null || data.Configuracao == null)
            return Json(new { success = false, message = "Dados inválidos." });

        try
        {
            // 1. Atualizar ou Inserir a Configuração da Linha
            var configExistente = await _context.tbl_configuracao_linhas
                .FirstOrDefaultAsync(x => x.Linha == data.Configuracao.Linha);

            if (configExistente == null)
            {
                data.Configuracao.UltimaAtualizacao = DateTime.Now;
                _context.tbl_configuracao_linhas.Add(data.Configuracao);
            }
            else
            {
                // Atualiza os campos manualmente ou via Entry
                _context.Entry(configExistente).CurrentValues.SetValues(data.Configuracao);
                configExistente.UltimaAtualizacao = DateTime.Now;
            }

            // 2. Gerenciar Paradas Planejadas (Delete e Re-insert é o mais simples aqui)
            var paradasAntigas = _context.tbl_paradas_planejadas
                .Where(p => p.Linha == data.Configuracao.Linha);

            _context.tbl_paradas_planejadas.RemoveRange(paradasAntigas);

            if (data.ParadasPlanejadas != null && data.ParadasPlanejadas.Count > 0)
            {
                foreach (var parada in data.ParadasPlanejadas)
                {
                    parada.Linha = data.Configuracao.Linha; // Garante o vínculo
                    _context.tbl_paradas_planejadas.Add(parada);
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Configurações salvas com sucesso!" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Erro ao salvar: " + ex.Message });
        }
    }

}