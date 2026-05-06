using System.Text.Json;

namespace CaniveteSuico.App.Bridge;

public interface IBridgeHandler
{
    string Action { get; }
    Task HandleAsync(JsonElement data, Action<object> reply);
}
