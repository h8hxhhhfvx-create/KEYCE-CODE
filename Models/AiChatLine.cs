namespace KeyceCode.Models;

public sealed record AiChatLine(string Role, string Content)
{
    public string Display => $"{(Role == "user" ? "Vous" : "KEYCE IA")} : {Content}";
}
