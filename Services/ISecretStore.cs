namespace KeyceCode.Services;

public interface ISecretStore
{
    string? Get(string name);
    void Set(string name, string value);
    void Delete(string name);
}
