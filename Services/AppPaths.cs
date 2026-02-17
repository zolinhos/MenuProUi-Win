using System;
using System.IO;

namespace MenuProUI.Services;

/// <summary>
/// Gerencia os caminhos de arquivos e diretórios da aplicação.
/// Dados são armazenados em ~/.config/MenuProUI (AppData no Windows).
/// </summary>
public static class AppPaths
{
    /// <summary>
    /// Diretório de dados da aplicação. Cria o diretório automaticamente se não existir.
    /// Windows: %APPDATA%\MenuProUI
    /// Linux: ~/.config/MenuProUI
    /// </summary>
    public static string AppDir
    {
        get
        {
            var candidates = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Path.GetTempPath()
            };

            foreach (var baseDir in candidates)
            {
                if (string.IsNullOrWhiteSpace(baseDir))
                    continue;

                try
                {
                    var dir = Path.Combine(baseDir, "MenuProUI");
                    Directory.CreateDirectory(dir);
                    return dir;
                }
                catch
                {
                    // tenta próximo caminho candidato
                }
            }

            throw new InvalidOperationException("Não foi possível determinar/criar diretório de dados da aplicação.");
        }
    }

    /// <summary>Caminho completo do arquivo CSV com a lista de clientes</summary>
    public static string ClientsPath => Path.Combine(AppDir, "clientes.csv");
    
    /// <summary>Caminho completo do arquivo CSV com a lista de acessos</summary>
    public static string AccessesPath => Path.Combine(AppDir, "acessos.csv");

    /// <summary>Caminho legado do arquivo de acessos (usado em versões antigas)</summary>
    public static string LegacyAccessesPath => Path.Combine(AppDir, "acessos_legacy.csv");

    /// <summary>Caminho do arquivo CSV de log de eventos (criação, alteração, remoção e abertura)</summary>
    public static string AuditLogPath => Path.Combine(AppDir, "eventos.csv");
}
