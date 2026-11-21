using System.Collections.ObjectModel;
using KeyceCode.Models;
using KeyceCode.Services;

namespace KeyceCode.ViewModels;

public sealed class AiChatViewModel : ViewModelBase
{
    private readonly OllamaChatService chatService;
    private readonly AppSettings settings;
    private string inputText = string.Empty;
    private string contextText = string.Empty;
    private bool isBusy;
    private CancellationTokenSource? cancellation;

    public AiChatViewModel(AppSettings settings, OllamaChatService? chatService = null)
    {
        this.settings = settings;
        this.chatService = chatService ?? new OllamaChatService();
        SendCommand = new RelayCommand(() => _ = SendAsync(), () => !IsBusy && !string.IsNullOrWhiteSpace(InputText));
        StopCommand = new RelayCommand(() => cancellation?.Cancel(), () => IsBusy);
        ClearCommand = new RelayCommand(() => Messages.Clear());
        Messages.Add(new AiChatLine("assistant", "Bonjour. Je peux vous aider sur le code du projet. Vérifiez qu’Ollama est démarré avec le modèle configuré."));
    }

    public ObservableCollection<AiChatLine> Messages { get; } = new();
    public RelayCommand SendCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ClearCommand { get; }

    public string InputText
    {
        get => inputText;
        set { if (SetProperty(ref inputText, value)) RelayCommand.Refresh(); }
    }

    public string ContextText
    {
        get => contextText;
        set => SetProperty(ref contextText, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set { if (SetProperty(ref isBusy, value)) RelayCommand.Refresh(); }
    }

    private async Task SendAsync()
    {
        var prompt = InputText.Trim();
        if (prompt.Length == 0) return;
        InputText = string.Empty;
        Messages.Add(new AiChatLine("user", prompt));
        IsBusy = true;
        cancellation = new CancellationTokenSource();
        try
        {
            var messages = new List<(string Role, string Content)>
            {
                ("system", "Tu es l’assistant de KEYCE CODE. Réponds en français, sois précis, explique le raisonnement et n’invente pas les API. Le projet cible Windows, C# et .NET. " +
                    "Tu ne dois jamais prétendre avoir modifié un fichier : tu proposes seulement des conseils dans ce chat."),
            };
            if (!string.IsNullOrWhiteSpace(ContextText))
                messages.Add(("user", "Contexte du fichier courant :\n```csharp\n" + ContextText + "\n```"));
            messages.AddRange(Messages.Where(line => line.Role is "user" or "assistant").TakeLast(12).Select(line => (line.Role, line.Content)));
            var response = await chatService.ChatAsync(settings.OllamaEndpoint, settings.OllamaModel, messages, cancellation.Token);
            Messages.Add(new AiChatLine("assistant", response));
        }
        catch (OperationCanceledException) { Messages.Add(new AiChatLine("assistant", "Réponse arrêtée.")); }
        catch (Exception ex) { Messages.Add(new AiChatLine("assistant", $"Impossible de contacter Ollama : {ex.Message}")); }
        finally
        {
            cancellation.Dispose();
            cancellation = null;
            IsBusy = false;
        }
    }
}
