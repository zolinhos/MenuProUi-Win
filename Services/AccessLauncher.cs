using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using MenuProUI.Models;

namespace MenuProUI.Services;

public static class AccessLauncher
{
    public static void Open(AccessEntry e)
    {
        switch (e.Tipo)
        {
            case AccessType.URL:
                OpenUrl(e.Url);
                break;

            case AccessType.SSH:
                OpenSsh(e);
                break;

            case AccessType.RDP:
                OpenRdp(e);
                break;
        }
    }

    // ======================
    // URL (Windows)
    // ======================
    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    // ======================
    // SSH (Windows) - abre terminal e executa
    // ======================
    private static void OpenSsh(AccessEntry e)
    {
        if (string.IsNullOrWhiteSpace(e.Host)) return;

        var port = (e.Porta is > 0) ? e.Porta.Value : 22;
        var userAt = string.IsNullOrWhiteSpace(e.Usuario) ? e.Host : $"{e.Usuario}@{e.Host}";
        var cmd = $"ssh -p {port} {userAt}";

        OpenCmdTerminal(cmd);
    }

    private static void OpenCmdTerminal(string command)
    {
        // Executa no CMD para não ter dor de cabeça com aspas/escape do PowerShell
        var quoted = "\"" + command.Replace("\"", "\\\"") + "\"";

        if (HasWindowsTerminal())
        {
            // Windows Terminal -> CMD -> /k "<comando>"
            Process.Start(new ProcessStartInfo
            {
                FileName = "wt.exe",
                Arguments = $"cmd.exe /k {quoted}",
                UseShellExecute = true
            });
            return;
        }

        // fallback: cmd normal
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/k {quoted}",
            UseShellExecute = true
        });
    }

    private static bool HasWindowsTerminal()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = "wt.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p is null) return false;

            var outp = p.StandardOutput.ReadToEnd().Trim();
            return !string.IsNullOrWhiteSpace(outp);
        }
        catch
        {
            return false;
        }
    }

    // ======================
    // RDP (Windows) - gera .rdp com username domínio e pede senha no mstsc
    // ======================
    private static void OpenRdp(AccessEntry e)
    {
        if (string.IsNullOrWhiteSpace(e.Host)) return;
        var prefs = new AppPreferencesService().Load();

        var port = (e.Porta is > 0) ? e.Porta.Value : 3389;

        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var dir = Path.Combine(docs, "MenuProUI", "rdp");
        Directory.CreateDirectory(dir);

        var file = Path.Combine(dir, $"MenuProUI_{e.Id}.rdp");

        var lines = new List<string>
        {
            $"full address:s:{e.Host}:{port}",
            "prompt for credentials:i:1",
            // reduz exigência de autenticação/cert (mstsc pode avisar mesmo assim, mas não bloqueia)
            "authentication level:i:0",
            "redirectclipboard:i:1",
            "drivestoredirect:s:",
        };

        // usuário + domínio (o mstsc respeita quando vem no .rdp)
        var user = BuildRdpUsername(e);
        if (!string.IsNullOrWhiteSpace(user))
            lines.Add($"username:s:{user}");

        // Primeira abertura: força tela cheia para melhor experiência inicial.
        var fullScreen = prefs.RdpForceFullscreen || e.RdpFullScreen || e.OpenCount == 0;

        // tela / tamanho
        if (fullScreen)
        {
            lines.Add("screen mode id:i:2");
        }
        else
        {
            lines.Add("screen mode id:i:1");
            // Tamanho fixo só deve ser aplicado quando NÃO há resolução dinâmica.
            if (!e.RdpDynamicResolution && e.RdpWidth is > 0 && e.RdpHeight is > 0)
            {
                lines.Add($"desktopwidth:i:{e.RdpWidth}");
                lines.Add($"desktopheight:i:{e.RdpHeight}");
            }
        }

        // Suporte a redimensionamento da sessão no cliente RDP.
        lines.Add($"dynamic resolution:i:{(e.RdpDynamicResolution ? 1 : 0)}");

        // Smart sizing mantém escalonamento quando em modo janela.
        lines.Add(e.RdpDynamicResolution ? "smart sizing:i:1" : "smart sizing:i:0");

        // grava em Unicode (formato comum de .rdp no Windows)
        File.WriteAllLines(file, lines, Encoding.Unicode);

        Process.Start(new ProcessStartInfo
        {
            FileName = "mstsc.exe",
            // /f força fullscreen na inicialização quando solicitado.
            Arguments = fullScreen ? $"/f \"{file}\"" : $"\"{file}\"",
            UseShellExecute = true
        });
    }

    private static string? BuildRdpUsername(AccessEntry e)
    {
        var u = (e.Usuario ?? "").Trim();
        var d = (e.Dominio ?? "").Trim();

        if (string.IsNullOrWhiteSpace(u) && string.IsNullOrWhiteSpace(d))
            return null;

        if (!string.IsNullOrWhiteSpace(d) && !string.IsNullOrWhiteSpace(u))
            return d + "\\" + u;

        // se só usuário, passa usuário
        if (!string.IsNullOrWhiteSpace(u))
            return u;

        // se só domínio (não faz sentido sem usuário), ignora
        return null;
    }
}
