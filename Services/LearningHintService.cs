namespace KeyceCode.Services;

/// <summary>Fournit des pistes pédagogiques sans corriger automatiquement le code.</summary>
public sealed class LearningHintService
{
    private static readonly IReadOnlyDictionary<string, string> Hints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["CS1002"] = "Vérifiez la fin de l’instruction : il manque souvent un point-virgule.",
        ["CS0103"] = "Vérifiez l’orthographe du nom et la portée de la variable ou de la méthode.",
        ["CS0246"] = "Vérifiez le nom du type et ajoutez le using ou la référence de projet nécessaire.",
        ["CS0029"] = "Les deux types ne sont pas compatibles : vérifiez l’affectation ou effectuez une conversion explicite.",
        ["CS0168"] = "La variable est déclarée mais jamais utilisée : utilisez-la ou supprimez sa déclaration.",
        ["CS8600"] = "Une valeur potentiellement null est affectée : vérifiez la nullabilité avant l’affectation."
    };

    public string GetHint(string diagnosticCode) =>
        Hints.TryGetValue(diagnosticCode, out var hint)
            ? hint
            : "Lisez le message, vérifiez la ligne indiquée et isolez le plus petit morceau de code en erreur.";
}
