
namespace KeyceCode.Models
{
    public class Diagnostic
    {
        // Identifiant de l'erreur
        public string Code { get; set; } = "";

        // Message explicatif
        public string Message { get; set; } = "";

        // Type : Erreur ou Avertissement
        public string Severity { get; set; } = "";

        // Position de l'erreur
        public int Line { get; set; }

        public int Column { get; set; }

        // Position de fin
        public int EndLine { get; set; }

        public int EndColumn { get; set; }
    }
}
