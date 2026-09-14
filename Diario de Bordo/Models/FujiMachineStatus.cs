namespace Diario_de_Bordo.Models
{
    public class FujiMachineStatus
    {
        // Agora refere-se à classe Module que está logo abaixo
        public List<Module> Modules { get; set; } = new();
    }

    public class Module
    {
        public string Label { get; set; }
        public int State { get; set; }
        public List<Lane> Lanes { get; set; } = new();
    }

    public class Lane
    {
        public string JobName { get; set; }
        public string JobSide { get; set; }
        public int Qty { get; set; }
    }
}