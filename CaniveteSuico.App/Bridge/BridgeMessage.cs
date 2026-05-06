using System.Text.Json;

namespace CaniveteSuico.App.Bridge;

public record BridgeMessage(string Action, JsonElement Data);
