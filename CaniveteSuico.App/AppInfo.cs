namespace CaniveteSuico.App;

/// <summary>
/// Nome e metadados apresentados ao utilizador (atualizar também &lt;AssemblyTitle&gt; no .csproj para o Explorador / atalhos).
/// </summary>
public static class AppInfo
{
    /// <summary>Nome do produto na barra de título, diálogos e na UI web.</summary>
    // ASCII aqui evita casos onde o Windows exibe o nome "zuado" em atalhos/taskbar
    // dependendo de como o app foi empacotado/instalado.
    public const string DisplayName = "Canivete Suico";

    /// <summary>
    /// Identificador do app no Windows (taskbar grouping + identidade do processo).
    /// </summary>
    public const string AppUserModelId = "Marc0zDev.CaniveteSuico";
}
