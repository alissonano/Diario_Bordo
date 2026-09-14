using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Policy;

namespace Diario_de_Bordo.Models
{
    // ========================= ENTIDADES (DB) =========================
    public class CQ
    {
        [Key]
        public int Id { get; set; }
        public DateTime Date_field { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public string Programa { get; set; } = string.Empty;
        public string Fase { get; set; } = string.Empty;
        public string OP { get; set; } = string.Empty;
        public string Turno { get; set; } = string.Empty;
        public string Responsavel { get; set; } = string.Empty;
        public string? Status { get; set; }
        public string? ComentarioGeral { get; set; }
        public ICollection<CQEvidencia> Evidencias { get; set; } = new List<CQEvidencia>();
    }

    public class CQEvidencia
    {
        public int id { get; set; }
        public int cq_id { get; set; }
        public CQ? CQ { get; set; }

        public string posto { get; set; } = string.Empty;
        public string item { get; set; } = string.Empty;
        public string resultado { get; set; } = "N/A";
        public string evidencia { get; set; } = string.Empty;
        public string? comentario { get; set; } = string.Empty;
    }

    public class ChecklistItem
    {
        public string Nome { get; set; } = string.Empty;
        public string Resultado { get; set; } = "N/A";
        public string? Comentario { get; set; }
        public List<IFormFile>? Evidencias { get; set; }
        public List<CQEvidencia>? EvidenciasSalvas { get; set; }
    }

    public class PostoChecklist
    {
        public string Posto { get; set; } = string.Empty;
        public List<ChecklistItem> Itens { get; set; } = new List<ChecklistItem>();
        public string? Comentario { get; set; }
    }

    // ========================= VIEWMODELS (UI) =========================
    public class CQViewModel
    {
        public CQ Inspecao { get; set; } = new CQ();
        public List<PostoChecklist> Postos { get; set; } = new List<PostoChecklist>();
        public string? ComentarioGeral { get; set; }

        public bool IsCQAdmin { get; set; }
        public bool IsLider { get; set; }
        public bool IsEngenheiro { get; set; }
        public bool IsOperador { get; set; }
    }
    public class CQCreateViewModel
    {
        public int IdCQ { get; set; }
        public List<CQItemViewModel> Itens { get; set; } = new List<CQItemViewModel>();
    }

    public class CQItemViewModel
    {
        public string Grupo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Julgamento { get; set; } = "N/A";
        public string? EvidenciaPath { get; set; }
        public IFormFile? EvidenciaFile { get; set; }
    }


    public class CQEvidenciasViewModel
    {
        public int CQId { get; set; }
        public string Cliente { get; set; }
        public string Modelo { get; set; }
        public string OP { get; set; }
        public string Fase { get; set; }
        public string Turno { get; set; }
        public string Status { get; set; }
        public bool isCQAdmin { get; set; }
        public string ComentarioGeral { get; set; }

        public List<PostoChecklistViewModel> Postos { get; set; }

        // **Propriedade nova para simplificar a view**
        public List<EvidenciaViewModel> Evidencias
        {
            get
            {
                return Postos?
                    .SelectMany(p => p.Itens)
                    .SelectMany(i => i.Evidencias)
                    .ToList() ?? new List<EvidenciaViewModel>();
            }
        }
    }


    public class PostoChecklistViewModel
    {
        public string Posto { get; set; } = string.Empty;
        public List<ChecklistItemViewModel> Itens { get; set; } = new List<ChecklistItemViewModel>();
        public string? Comentario { get; set; }
    }

    public class ChecklistItemViewModel
    {
        public string Nome { get; set; } = string.Empty;
        public string Resultado { get; set; } = "N/A";
        public string? Comentario { get; set; }
        public string? NovoComentario { get; set; }
        public List<IFormFile> NovasEvidencias { get; set; } = new List<IFormFile>();
        public List<EvidenciaViewModel> Evidencias { get; set; } = new List<EvidenciaViewModel>();
        public List<CQEvidencia> EvidenciasSalvas { get; set; } = new();
    }

    public class EvidenciaViewModel
    {
        public int Id { get; set; }
        public string Caminho { get; set; } = string.Empty;
        public List<string> Comentarios { get; set; } = new List<string>();
        public string? NovoComentario { get; set; }
        public byte[] Evidencia { get; set; }


        public bool TemArquivo
        {
            get
            {
                return Evidencia != null && Evidencia.Length > 0;
            }
        }
    }

    // Optional: para histórico de comentários
    public class ComentarioItemViewModel
    {
        public string Usuario { get; set; } = string.Empty;
        public DateTime Data { get; set; }
        public string Texto { get; set; } = string.Empty;
    }

    public class ComentarioEvidenciaDTO
    {
        public int Id { get; set; }
        public string Comentario { get; set; } = string.Empty;
    }
}
