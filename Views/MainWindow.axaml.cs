using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using MenuProUI.Dialogs;
using MenuProUI.Models;
using MenuProUI.Services;
using MenuProUI.ViewModels;

namespace MenuProUI.Views;

/// <summary>
/// Janela principal da aplicação MenuProUI.
/// Gerencia a interface de usuário, eventos e fluxo de interação com clientes e acessos.
/// Implementa padrão MVVM com ViewModel binding automático.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Atalho para acessar o ViewModel (DataContext da janela)</summary>
    private MainWindowViewModel VM => (MainWindowViewModel)DataContext!;
    private readonly CsvRepository _repo = new();
    private readonly AuditLogService _auditLog = new();
    private readonly AppPreferencesService _preferencesService = new();
    private readonly Dictionary<Guid, ConnectivityStatus> _connectivityByAccess = new();
    private static readonly string[] ClientsMenuIcons = { "\uE716", "\uE77B", "\uE8B7" };
    private static readonly string[] AccessesMenuIcons = { "\uE7F4", "\uE71B", "\uE774" };
    private AppPreferences _preferences = new();

    /// <summary>
    /// Inicializa a janela principal.
    /// Configura o ViewModel como DataContext e conecta handlers de eventos.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();

        // Conecta botões de menu aos handlers de toggle
        var clientsMenuBtn = this.FindControl<Button>("ClientsMenuBtn");
        if (clientsMenuBtn != null)
            clientsMenuBtn.Click += (s, e) => ToggleMenu("ClientsMenu");

        var accessesMenuBtn = this.FindControl<Button>("AccessesMenuBtn");
        if (accessesMenuBtn != null)
            accessesMenuBtn.Click += (s, e) => ToggleMenu("AccessesMenu");

        // Configura handler para tecla F1 (Help)
        this.KeyDown += MainWindow_KeyDown;
        this.Closing += (_, _) => SavePreferences();

        LoadPreferences();
    }

    /// <summary>Handler para teclas pressionadas - detecta atalhos de teclado</summary>
    private async void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        // Modifiers: Ctrl, Alt, Shift, Meta
        var hasCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var hasShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var hasAlt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        try
        {
            // F1 - Ajuda
            if (e.Key == Key.F1)
            {
                e.Handled = true;
                await ShowHelp();
                return;
            }

            // Escape - Fechar menus
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CloseMenus();
                return;
            }

            // Ctrl+Q - Sair (Close)
            if (hasCtrl && e.Key == Key.Q)
            {
                e.Handled = true;
                this.Close();
                return;
            }

            // Ctrl+R - Recarregar
            if (hasCtrl && e.Key == Key.R)
            {
                e.Handled = true;
                OnReload(null, new RoutedEventArgs());
                return;
            }

            // Ctrl+K - Busca global / paleta rápida
            if (hasCtrl && e.Key == Key.K)
            {
                e.Handled = true;
                var globalSearchBox = this.FindControl<TextBox>("GlobalSearchBox");
                if (globalSearchBox != null)
                {
                    globalSearchBox.Focus();
                    globalSearchBox.SelectAll();
                }
                return;
            }

            // Ctrl+F - Focar Busca Clientes
            if (hasCtrl && !hasShift && e.Key == Key.F)
            {
                e.Handled = true;
                var clientsSearchBox = this.FindControl<TextBox>("ClientsSearchBox");
                if (clientsSearchBox != null)
                {
                    clientsSearchBox.Focus();
                    clientsSearchBox.SelectAll();
                }
                return;
            }

            // Ctrl+Shift+F - Focar Busca Acessos
            if (hasCtrl && hasShift && e.Key == Key.F)
            {
                e.Handled = true;
                var accessesSearchBox = this.FindControl<TextBox>("AccessesSearchBox");
                if (accessesSearchBox != null)
                {
                    accessesSearchBox.Focus();
                    accessesSearchBox.SelectAll();
                }
                return;
            }

            // Ctrl+L - Limpar Buscas
            if (hasCtrl && e.Key == Key.L)
            {
                e.Handled = true;
                VM.ClientsSearchText = "";
                VM.AccessesSearchText = "";
                return;
            }

            // Ctrl+N - Novo Cliente
            if (hasCtrl && !hasShift && e.Key == Key.N)
            {
                e.Handled = true;
                OnNewClient(null, new RoutedEventArgs());
                return;
            }

            // Ctrl+Shift+N - Novo Acesso
            if (hasCtrl && hasShift && e.Key == Key.N)
            {
                e.Handled = true;
                OnNewAccess(null, new RoutedEventArgs());
                return;
            }

            // Ctrl+E - Editar Cliente
            if (hasCtrl && !hasShift && e.Key == Key.E)
            {
                e.Handled = true;
                OnEditClient(null, new RoutedEventArgs());
                return;
            }

            // Ctrl+Shift+E - Editar Acesso
            if (hasCtrl && hasShift && e.Key == Key.E)
            {
                e.Handled = true;
                OnEditAccess(null, new RoutedEventArgs());
                return;
            }

            // Ctrl+Delete - Excluir Cliente
            if (hasCtrl && e.Key == Key.Delete)
            {
                e.Handled = true;
                OnDeleteClient(null, new RoutedEventArgs());
                return;
            }

            // Ctrl+Shift+Delete - Excluir Acesso
            if (hasCtrl && hasShift && e.Key == Key.Delete)
            {
                e.Handled = true;
                OnDeleteAccess(null, new RoutedEventArgs());
                return;
            }

            // Ctrl+Shift+D - Clonar Acesso
            if (hasCtrl && hasShift && e.Key == Key.D)
            {
                e.Handled = true;
                OnCloneAccess(null, new RoutedEventArgs());
                return;
            }

            // Ctrl+Shift+K - Checar conectividade
            if (hasCtrl && hasShift && e.Key == Key.K)
            {
                e.Handled = true;
                OnCheckConnectivity(null, new RoutedEventArgs());
                return;
            }

            // Ctrl+Alt+C - Alternar ícone de Clientes
            if (hasCtrl && hasAlt && e.Key == Key.C)
            {
                e.Handled = true;
                CycleMenuIcon("ClientsMenuBtn", ClientsMenuIcons);
                return;
            }

            // Ctrl+Alt+A - Alternar ícone de Acessos
            if (hasCtrl && hasAlt && e.Key == Key.A)
            {
                e.Handled = true;
                CycleMenuIcon("AccessesMenuBtn", AccessesMenuIcons);
                return;
            }

            // Ctrl+Alt+T - Alternar tema
            if (hasCtrl && hasAlt && e.Key == Key.T)
            {
                e.Handled = true;
                OnToggleTheme(null, new RoutedEventArgs());
                return;
            }

            // Ctrl+Alt+J - Exibir log de eventos/últimos acessos
            if (hasCtrl && hasAlt && e.Key == Key.J)
            {
                e.Handled = true;
                await ShowAuditLog();
                return;
            }

            // Enter - Abrir Acesso
            if (e.Key == Key.Return)
            {
                // Verifica se está em um TextBox (não quer executar em campos de texto)
                var focusedControl = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
                if (focusedControl is TextBox)
                    return;

                e.Handled = true;
                OnOpenAccess(null, new RoutedEventArgs());
                return;
            }
        }
        catch
        {
            // Falha silenciosa em caso de erro no atalho
        }
    }

    /// <summary>
    /// Exibe o diálogo de ajuda/help com instruções sobre a aplicação.
    /// Acessível via tecla F1 ou menu Help.
    /// </summary>
    private async Task ShowHelp()
    {
        var helpText = @"MENU PRO UI - Ajuda Completa (F1)
════════════════════════════════════════════

FUNCIONALIDADES PRINCIPAIS:

👥 CLIENTES
  • Novo: Cria um novo cliente (organização/projeto)
  • Editar: Modifica nome e observações do cliente
  • Excluir: Remove cliente e todos seus acessos
  • Buscar: Filtra por nome ou observações em tempo real

🔓 ACESSOS
  • Novo: Cria acesso (SSH, RDP ou URL) para cliente
  • Editar: Modifica configurações do acesso
    • Clonar: Duplica o acesso selecionado com novo apelido
  • Excluir: Remove o acesso
  • Abrir: Abre/conecta ao acesso
  • Buscar: Filtra por apelido, host, usuário ou URL
    • Checar Conectividade: Testa portas TCP e mostra status

⌨️ ATALHOS DE TECLADO:

Navegação Geral:
  F1                    Abre esta ajuda
  Escape                Fecha menus abertos
  Ctrl+R                Recarrega dados do disco
  Ctrl+Q                Sair da aplicação
    Ctrl+K                Focar busca global

Clientes:
  Ctrl+N                Novo cliente
  Ctrl+E                Editar cliente selecionado
  Ctrl+Delete           Excluir cliente selecionado
  Ctrl+F                Focar campo de busca de clientes

Acessos:
  Ctrl+Shift+N          Novo acesso
    Ctrl+Shift+D          Clonar acesso selecionado
  Ctrl+Shift+E          Editar acesso selecionado
  Ctrl+Shift+Delete     Excluir acesso selecionado
    Ctrl+Shift+K          Checar conectividade
    Ctrl+Alt+A            Alterna ícone do menu de acessos
  Enter                 Abre/conecta ao acesso selecionado
  Ctrl+Shift+F          Focar campo de busca de acessos

Interface:
    Ctrl+Alt+C            Alterna ícone do menu de clientes
    Ctrl+Alt+T            Alterna tema claro/escuro
    Ctrl+Alt+J            Exibe log de eventos e últimos acessos

Busca:
  Ctrl+L                Limpa todos os campos de busca
  (Digite para filtrar em tempo real)

📁 ARMAZENAMENTO:
  Linux:   ~/.config/MenuProUI/
  Windows: %APPDATA%\MenuProUI\
  
  Arquivos:
  • clientes.csv - Lista de clientes
  • acessos.csv - Lista de acessos

🔧 TIPOS DE ACESSO:
  • SSH: Conexão segura para Linux/Unix (porta 22)
  • RDP: Área de trabalho remota Windows (porta 3389)
  • URL: Abrir página web no navegador padrão

💡 DICAS ÚTEIS:
  • Use Ctrl+F para encontrar rapidamente um cliente
  • Use Ctrl+Shift+F para procurar um acesso específico
  • Duplo-clique em um acesso também o abre
  • Acessos sem cliente são agrupados em 'Sem Cliente'
  • Dados são salvos automaticamente nas mudanças
  • Faça backup dos arquivos CSV manualmente se necesário

📋 CAMPOS POR TIPO DE ACESSO:

SSH: Host, Porta (padrão 22), Usuário
RDP: Host, Porta (padrão 3389), Usuário, Domínio
     Opções: Tela Cheia, Resolução Dinâmica, Ignorar Certificado
URL: Link completo (https://...)
Todos: Apelido, Observações

════════════════════════════════════════════

📚 DÚVIDAS OU SUGESTÕES?
GitHub: https://github.com/zolinhos/MenuProUI-Linux
Issues: https://github.com/zolinhos/MenuProUI-Linux/issues
Discussions: https://github.com/zolinhos/MenuProUI-Linux/discussions

Versão 1.7.5 - MenuProUI";

        var dlg = new HelpDialog(helpText);
        await dlg.ShowDialog<bool>(this);
    }

    /// <summary>
    /// Alterna visibilidade de um menu popup.
    /// Fecha outros menus se necessário (para manter apenas um aberto).
    /// </summary>
    /// <param name="popupName">Nome do popup a alternar (ClientsMenu ou AccessesMenu)</param>
    private void ToggleMenu(string popupName)
    {
        var popup = this.FindControl<Popup>(popupName);
        if (popup == null) return;

        var shouldOpen = !popup.IsOpen;

        var clientsMenu = this.FindControl<Popup>("ClientsMenu");
        var accessesMenu = this.FindControl<Popup>("AccessesMenu");

        if (clientsMenu != null) clientsMenu.IsOpen = false;
        if (accessesMenu != null) accessesMenu.IsOpen = false;

        popup.IsOpen = shouldOpen;
    }

    private void OnClientsLabelPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        ToggleMenu("ClientsMenu");
    }

    private void OnAccessesLabelPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        ToggleMenu("AccessesMenu");
    }

    /// <summary>Fecha todos os menus popup abertos</summary>
    private void CloseMenus()
    {
        var clientsMenu = this.FindControl<Popup>("ClientsMenu");
        var accessesMenu = this.FindControl<Popup>("AccessesMenu");
        if (clientsMenu != null) clientsMenu.IsOpen = false;
        if (accessesMenu != null) accessesMenu.IsOpen = false;
    }

    /// <summary>
    /// Handler para mudança de seleção na lista de clientes.
    /// Atualiza acessos exibidos quando um cliente é selecionado.
    /// </summary>
    private void OnClientSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        VM.SetSelectedClient(VM.SelectedClient);
    }

    /// <summary>
    /// Handler para botão Recarregar.
    /// Recarrega todos os dados do disco e reaplica filtros.
    /// </summary>
    private void OnReload(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        VM.Reload();
    }

    private void OnToggleTheme(object? sender, RoutedEventArgs e)
    {
        RequestedThemeVariant = RequestedThemeVariant == ThemeVariant.Light ? ThemeVariant.Dark : ThemeVariant.Light;
        _preferences.Theme = RequestedThemeVariant == ThemeVariant.Light ? "Light" : "Dark";
        SavePreferences();
    }

    private void OnToggleDensity(object? sender, RoutedEventArgs e)
    {
        var accessList = this.FindControl<ListBox>("AccessList");
        if (accessList == null) return;

        var compact = !(_preferences.CompactAccessRows);
        _preferences.CompactAccessRows = compact;
        accessList.FontSize = compact ? 12 : 13;
        SavePreferences();
    }

    private async void OnExportBackup(object? sender, RoutedEventArgs e)
    {
        CloseMenus();

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exportar backup",
            SuggestedFileName = $"MenuProUI-backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip"
        });

        if (file == null) return;

        var targetPath = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(targetPath)) return;

        using var zip = ZipFile.Open(targetPath, ZipArchiveMode.Create);
        if (File.Exists(AppPaths.ClientsPath)) zip.CreateEntryFromFile(AppPaths.ClientsPath, "clientes.csv");
        if (File.Exists(AppPaths.AccessesPath)) zip.CreateEntryFromFile(AppPaths.AccessesPath, "acessos.csv");

        await new ConfirmDialog("Backup exportado com sucesso.", "Backup")
            .ShowDialog<bool>(this);
    }

    private async void OnImportBackup(object? sender, RoutedEventArgs e)
    {
        CloseMenus();

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Importar backup",
            AllowMultiple = false
        });

        var file = files.FirstOrDefault();
        if (file == null) return;

        var sourcePath = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return;

        using var zip = ZipFile.OpenRead(sourcePath);
        var clientsEntry = zip.Entries.FirstOrDefault(x => x.FullName.EndsWith("clientes.csv", StringComparison.OrdinalIgnoreCase));
        var accessesEntry = zip.Entries.FirstOrDefault(x => x.FullName.EndsWith("acessos.csv", StringComparison.OrdinalIgnoreCase));

        clientsEntry?.ExtractToFile(AppPaths.ClientsPath, true);
        accessesEntry?.ExtractToFile(AppPaths.AccessesPath, true);

        VM.Reload();

        await new ConfirmDialog("Backup importado com sucesso.", "Backup")
            .ShowDialog<bool>(this);
    }

    // ============== HANDLERS DE CLIENTES ==============

    /// <summary>
    /// Handler para criar novo cliente.
    /// Exibe diálogo para entrada de nome e observações.
    /// </summary>
    private async void OnNewClient(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        var c = new Client { Nome = "Novo Cliente" };
        var dlg = new ClientDialog(c);

        var ok = await dlg.ShowDialog<bool>(this);
        if (!ok) return;

        var created = dlg.Result;
        created.Id = Guid.NewGuid();
        created.CriadoEm = DateTime.UtcNow;
        created.AtualizadoEm = DateTime.UtcNow;

        VM.Clients.Add(created);
        VM.SaveAll();
        VM.ApplyClientFilter();
        VM.SelectedClient = created;
        VM.RefreshAccesses();
        _auditLog.Append("create", "client", created.Nome, "Cliente criado");
    }

    /// <summary>
    /// Handler para editar cliente selecionado.
    /// Valida unicidade de nome antes de salvar.
    /// </summary>
    private async void OnEditClient(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        if (VM.SelectedClient is null) return;

        var dlg = new ClientDialog(VM.SelectedClient);
        var ok = await dlg.ShowDialog<bool>(this);
        if (!ok) return;

        var edited = dlg.Result;

        // Valida se outro cliente já tem esse nome
        var sameNameOther = VM.Clients.Any(x =>
            x.Id != edited.Id &&
            string.Equals(x.Nome, edited.Nome, StringComparison.OrdinalIgnoreCase));

        if (sameNameOther)
        {
            await new ConfirmDialog("Já existe um cliente com esse nome. Use um nome único.", "Atenção")
                .ShowDialog<bool>(this);
            return;
        }

        // Atualiza dados do cliente selecionado
        VM.SelectedClient.Nome = edited.Nome;
        VM.SelectedClient.Observacoes = edited.Observacoes;
        VM.SelectedClient.AtualizadoEm = DateTime.UtcNow;

        VM.SaveAll();
        VM.Reload();
        _auditLog.Append("update", "client", edited.Nome, "Cliente alterado");
    }

    /// <summary>
    /// Handler para excluir cliente selecionado.
    /// Exibe confirmação pois remove também todos os acessos do cliente.
    /// </summary>
    private async void OnDeleteClient(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        if (VM.SelectedClient is null) return;

        var client = VM.SelectedClient;

        // Pede confirmação (operação pode perder dados)
        var confirm = new ConfirmDialog(
            $"Excluir o cliente '{client.Nome}'?\n\nIsso também removerá TODOS os acessos desse cliente.",
            "Excluir Cliente");

        var ok = await confirm.ShowDialog<bool>(this);
        if (!ok) return;

        VM.Clients.Remove(client);
        VM.Accesses.Clear();

        VM.SaveAll();
        VM.Reload();
        _auditLog.Append("delete", "client", client.Nome, "Cliente removido");
    }

    // ============== HANDLERS DE ACESSOS ==============

    /// <summary>
    /// Handler para criar novo acesso.
    /// Requer cliente selecionado.
    /// </summary>
    private async void OnNewAccess(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        if (VM.SelectedClient is null)
        {
            await new ConfirmDialog("Selecione um cliente antes de criar um acesso.", "Atenção")
                .ShowDialog<bool>(this);
            return;
        }

        // Cria acesso padrão (URL vazio por padrão)
        var a = new AccessEntry
        {
            ClientId = VM.SelectedClient.Id,
            Tipo = AccessType.URL,
            Apelido = "Novo Acesso",
            Url = "https://",
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow
        };

        var dlg = new AccessDialog(a);
        var ok = await dlg.ShowDialog<bool>(this);
        if (!ok) return;

        var created = dlg.Result;
        created.Id = Guid.NewGuid();
        created.ClientId = VM.SelectedClient.Id;
        created.CriadoEm = DateTime.UtcNow;
        created.AtualizadoEm = DateTime.UtcNow;

        VM.Accesses.Add(created);
        VM.SaveAll();
        VM.RefreshAccesses();
        VM.SelectedAccess = created;
        ApplyConnectivityToVisibleAccesses();
        _auditLog.Append("create", "access", created.Apelido, $"Tipo={created.Tipo}");
    }

    private void OnCloneAccess(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        if (VM.SelectedAccess is null) return;

        var source = VM.SelectedAccess;
        var clone = new AccessEntry
        {
            Id = Guid.NewGuid(),
            ClientId = source.ClientId,
            Tipo = source.Tipo,
            Apelido = BuildCloneAlias(source.Apelido),
            Host = source.Host,
            Porta = source.Porta,
            Usuario = source.Usuario,
            Dominio = source.Dominio,
            RdpIgnoreCert = source.RdpIgnoreCert,
            RdpFullScreen = source.RdpFullScreen,
            RdpDynamicResolution = source.RdpDynamicResolution,
            RdpWidth = source.RdpWidth,
            RdpHeight = source.RdpHeight,
            Url = source.Url,
            Observacoes = source.Observacoes,
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow,
            ConnectivityStatus = ConnectivityStatus.Unknown
        };

        VM.Accesses.Add(clone);
        VM.SaveAll();
        VM.RefreshAccesses();
        VM.SelectedAccess = VM.Accesses.FirstOrDefault(a => a.Id == clone.Id) ?? clone;
        ApplyConnectivityToVisibleAccesses();
        _auditLog.Append("create", "access", clone.Apelido, $"Clonado de {source.Apelido}");
    }

    /// <summary>
    /// Handler para editar acesso selecionado.
    /// Permite modificar todos os campos de configuração.
    /// </summary>
    private async void OnEditAccess(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        if (VM.SelectedAccess is null) return;

        var dlg = new AccessDialog(VM.SelectedAccess);
        var ok = await dlg.ShowDialog<bool>(this);
        if (!ok) return;

        var edited = dlg.Result;

        // Atualiza todos os campos do acesso
        VM.SelectedAccess.Tipo = edited.Tipo;
        VM.SelectedAccess.Apelido = edited.Apelido;
        VM.SelectedAccess.Host = edited.Host;
        VM.SelectedAccess.Porta = edited.Porta;
        VM.SelectedAccess.Usuario = edited.Usuario;
        VM.SelectedAccess.Dominio = edited.Dominio;
        VM.SelectedAccess.Url = edited.Url;
        VM.SelectedAccess.Observacoes = edited.Observacoes;
        VM.SelectedAccess.AtualizadoEm = DateTime.UtcNow;

        VM.SaveAll();
        VM.RefreshAccesses();
        _auditLog.Append("update", "access", edited.Apelido, $"Tipo={edited.Tipo}");
    }

    /// <summary>
    /// Handler para excluir acesso selecionado.
    /// Exibe confirmação antes de remover.
    /// </summary>
    private async void OnDeleteAccess(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        if (VM.SelectedAccess is null) return;

        var a = VM.SelectedAccess;
        var ok = await new ConfirmDialog($"Excluir o acesso '{a.Apelido}'?", "Excluir Acesso")
            .ShowDialog<bool>(this);

        if (!ok) return;

        VM.Accesses.Remove(a);
        _connectivityByAccess.Remove(a.Id);
        VM.SaveAll();
        VM.RefreshAccesses();
        ApplyConnectivityToVisibleAccesses();
        ApplyClientConnectivityIndicators();
        _auditLog.Append("delete", "access", a.Apelido, "Acesso removido");
    }

    private void OnToggleFavorite(object? sender, RoutedEventArgs e)
    {
        var access = ResolveAccessFromSender(sender) ?? VM.SelectedAccess;
        if (access == null) return;

        access.IsFavorite = !access.IsFavorite;
        VM.SaveAll();
        VM.ApplyAccessesFilter();
        _auditLog.Append("favorite", "access", access.Apelido, access.IsFavorite ? "Favoritado" : "Desfavoritado");
    }

    private async void OnCheckConnectivity(object? sender, RoutedEventArgs e)
    {
        CloseMenus();

        if (VM.SelectedClient is null)
        {
            await new ConfirmDialog("Selecione um cliente para checar conectividade.", "Atenção")
                .ShowDialog<bool>(this);
            return;
        }

        var scope = new ConnectivityScopeDialog();
        var mode = await scope.ShowDialog<ConnectivityScopeMode>(this);
        if (mode == ConnectivityScopeMode.Cancel) return;

        if (mode == ConnectivityScopeMode.SelectedClient)
        {
            await PerformConnectivityCheck(VM.Accesses.ToList(), onlySelectedClient: true);
            return;
        }

        var allAccesses = _repo.Load().accesses;
        await PerformConnectivityCheck(allAccesses, onlySelectedClient: false);
    }

    private async Task PerformConnectivityCheck(List<AccessEntry> accesses, bool onlySelectedClient)
    {
        if (accesses.Count == 0)
        {
            await new ConfirmDialog("Nenhum acesso disponível para checar.", "Conectividade")
                .ShowDialog<bool>(this);
            return;
        }

        foreach (var access in accesses)
            _connectivityByAccess[access.Id] = ConnectivityStatus.Checking;

        if (onlySelectedClient && VM.SelectedClient is not null)
            VM.SelectedClient.ConnectivityStatus = ConnectivityStatus.Checking;
        else
            foreach (var client in VM.Clients)
                client.ConnectivityStatus = ConnectivityStatus.Checking;

        ApplyConnectivityToVisibleAccesses();
        VM.ApplyClientFilter();

        var results = await ConnectivityChecker.CheckAllAsync(accesses);
        foreach (var pair in results)
            _connectivityByAccess[pair.Key] = pair.Value ? ConnectivityStatus.Online : ConnectivityStatus.Offline;

        VM.LastConnectivityCheckText = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

        ApplyConnectivityToVisibleAccesses();
        ApplyClientConnectivityIndicators();
    }

    private void ApplyConnectivityToVisibleAccesses()
    {
        foreach (var access in VM.Accesses)
        {
            access.ConnectivityStatus = _connectivityByAccess.TryGetValue(access.Id, out var status)
                ? status
                : ConnectivityStatus.Unknown;
        }

        VM.ApplyAccessesFilter();
    }

    private void ApplyClientConnectivityIndicators()
    {
        var allAccesses = _repo.Load().accesses;

        foreach (var client in VM.Clients)
        {
            var clientAccesses = allAccesses.Where(a => a.ClientId == client.Id).ToList();
            if (clientAccesses.Count == 0)
            {
                client.ConnectivityStatus = ConnectivityStatus.Unknown;
                continue;
            }

            var statuses = clientAccesses.Select(a =>
                    _connectivityByAccess.TryGetValue(a.Id, out var st) ? st : ConnectivityStatus.Unknown)
                .ToList();

            if (statuses.Contains(ConnectivityStatus.Checking))
                client.ConnectivityStatus = ConnectivityStatus.Checking;
            else if (statuses.All(s => s == ConnectivityStatus.Online))
                client.ConnectivityStatus = ConnectivityStatus.Online;
            else if (statuses.Contains(ConnectivityStatus.Offline))
                client.ConnectivityStatus = ConnectivityStatus.Offline;
            else
                client.ConnectivityStatus = ConnectivityStatus.Unknown;
        }

        VM.ApplyClientFilter();
    }

    private string BuildCloneAlias(string aliasBase)
    {
        var normalized = (aliasBase ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = "Acesso";

        var used = VM.Accesses
            .Select(a => a.Apelido.Trim())
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var first = $"{normalized}-copia";
        if (!used.Contains(first))
            return first;

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{normalized}-copia-{i}";
            if (!used.Contains(candidate))
                return candidate;
        }

        return $"{normalized}-copia-{Guid.NewGuid().ToString("N")[..6]}";
    }

    /// <summary>
    /// Handler para abrir/conectar ao acesso selecionado.
    /// Detecta tipo (SSH, RDP, URL) e executa aktion apropriada.
    /// Fecha menus depois de executar.
    /// </summary>
    private void OnOpenAccess(object? sender, RoutedEventArgs e)
    {
        CloseMenus();
        if (VM.SelectedAccess is null) return;

        try
        {
            // Abre/conecta ao acesso usando o serviço de launcher
            AccessLauncher.Open(VM.SelectedAccess);
            MarkAccessOpened(VM.SelectedAccess);
        }
        catch (Exception ex)
        {
            // Exibe erro se falhar
            _ = new ConfirmDialog($"Falha ao abrir:\n{ex.Message}", "Erro").ShowDialog<bool>(this);
        }
    }

    private void OnQuickOpenAccess(object? sender, RoutedEventArgs e)
    {
        var access = ResolveAccessFromSender(sender);
        if (access == null) return;

        VM.SelectedAccess = access;
        OnOpenAccess(sender, e);
    }

    private async void OnQuickCopyHost(object? sender, RoutedEventArgs e)
    {
        var access = ResolveAccessFromSender(sender);
        var text = access?.Host;
        if (string.IsNullOrWhiteSpace(text)) return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;
        await clipboard.SetTextAsync(text);
    }

    private async void OnQuickCopyUser(object? sender, RoutedEventArgs e)
    {
        var access = ResolveAccessFromSender(sender);
        var text = access?.Usuario;
        if (string.IsNullOrWhiteSpace(text)) return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;
        await clipboard.SetTextAsync(text);
    }

    private async void OnQuickCopyUrl(object? sender, RoutedEventArgs e)
    {
        var access = ResolveAccessFromSender(sender);
        var text = access?.Url;
        if (string.IsNullOrWhiteSpace(text)) return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;
        await clipboard.SetTextAsync(text);
    }

    private AccessEntry? ResolveAccessFromSender(object? sender)
    {
        if (sender is Control { DataContext: AccessEntry fromDataContext })
            return fromDataContext;

        if (sender is Button { CommandParameter: AccessEntry fromButtonParam })
            return fromButtonParam;

        return null;
    }

    private void MarkAccessOpened(AccessEntry access)
    {
        access.OpenCount++;
        access.LastOpenedAt = DateTime.UtcNow;
        VM.SaveAll();
        VM.ApplyAccessesFilter();
        _auditLog.Append("open", "access", access.Apelido, access.Tipo.ToString());
    }

    private async Task ShowAuditLog()
    {
        var events = _auditLog.Load()
            .OrderByDescending(x => x.TimestampUtc)
            .Take(120)
            .ToList();

        var lastAccesses = events
            .Where(x => string.Equals(x.Action, "open", StringComparison.OrdinalIgnoreCase))
            .Take(40)
            .ToList();

        var lines = new List<string>
        {
            "LOG DE EVENTOS (ultimos 120)",
            "════════════════════════════════════════════",
            "",
            "ULTIMOS ACESSOS:",
        };

        if (lastAccesses.Count == 0)
        {
            lines.Add("- Nenhum acesso recente registrado.");
        }
        else
        {
            foreach (var item in lastAccesses)
                lines.Add($"- {item.TimestampUtc.ToLocalTime():dd/MM HH:mm:ss} | {item.EntityName} | {item.Details}");
        }

        lines.Add("");
        lines.Add("EVENTOS GERAIS:");

        if (events.Count == 0)
        {
            lines.Add("- Nenhum evento registrado.");
        }
        else
        {
            foreach (var item in events)
                lines.Add($"- {item.TimestampUtc.ToLocalTime():dd/MM HH:mm:ss} | {item.Action} | {item.EntityType} | {item.EntityName} | {item.Details}");
        }

        lines.Add("");
        lines.Add($"Arquivo: {AppPaths.AuditLogPath}");

        var dialog = new HelpDialog(string.Join(Environment.NewLine, lines));
        await dialog.ShowDialog<bool>(this);
    }

    private void CycleMenuIcon(string buttonName, IReadOnlyList<string> icons)
    {
        if (icons.Count == 0) return;

        var button = this.FindControl<Button>(buttonName);
        if (button == null) return;

        var current = button.Content?.ToString() ?? string.Empty;
        var index = -1;
        for (var i = 0; i < icons.Count; i++)
        {
            if (icons[i] != current) continue;
            index = i;
            break;
        }

        var nextIndex = index >= 0 ? (index + 1) % icons.Count : 0;
        button.Content = icons[nextIndex];
        SavePreferences();
    }

    private void LoadPreferences()
    {
        _preferences = _preferencesService.Load();

        var clientsBtn = this.FindControl<Button>("ClientsMenuBtn");
        if (clientsBtn != null && !string.IsNullOrWhiteSpace(_preferences.ClientsIcon))
            clientsBtn.Content = _preferences.ClientsIcon;

        var accessesBtn = this.FindControl<Button>("AccessesMenuBtn");
        if (accessesBtn != null && !string.IsNullOrWhiteSpace(_preferences.AccessesIcon))
            accessesBtn.Content = _preferences.AccessesIcon;

        RequestedThemeVariant = _preferences.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase)
            ? ThemeVariant.Light
            : ThemeVariant.Dark;

        var accessList = this.FindControl<ListBox>("AccessList");
        if (accessList != null)
            accessList.FontSize = _preferences.CompactAccessRows ? 12 : 13;
    }

    private void SavePreferences()
    {
        var clientsBtn = this.FindControl<Button>("ClientsMenuBtn");
        var accessesBtn = this.FindControl<Button>("AccessesMenuBtn");

        _preferences.ClientsIcon = clientsBtn?.Content?.ToString() ?? _preferences.ClientsIcon;
        _preferences.AccessesIcon = accessesBtn?.Content?.ToString() ?? _preferences.AccessesIcon;
        _preferences.Theme = RequestedThemeVariant == ThemeVariant.Light ? "Light" : "Dark";

        _preferencesService.Save(_preferences);
    }
}
