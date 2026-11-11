using System.Text.Json.Serialization;

namespace KeyFlip.Core;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(GitHubReleaseInfo))]
[JsonSerializable(typeof(UserDictionary.UserData))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
