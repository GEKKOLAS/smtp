using System.Text;

namespace MailTemplateHub.Application.Features.Assets;

internal static class StorageKeys
{
    /// <summary>Object key: assets/{userId}/{assetId}/{safeName}. Ties the object to its owner.</summary>
    public static string For(Guid userId, Guid assetId, string filename) =>
        $"assets/{userId}/{assetId}/{Sanitize(filename)}";

    /// <summary>Keeps only filename-safe characters; caps length; guarantees non-empty.</summary>
    public static string Sanitize(string filename)
    {
        var name = Path.GetFileName(filename);
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            builder.Append(char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_');
        }
        var safe = builder.ToString().Trim('.', '_');
        if (safe.Length == 0) safe = "file";
        return safe.Length > 120 ? safe[^120..] : safe;
    }
}
