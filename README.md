# MenuProUI v1.7.5

MenuProUI é um gerenciador de acessos (SSH, RDP e URLs) organizado por clientes, desenvolvido em .NET + Avalonia e multiplataforma.

Funcionalidades
----------------
- Gerenciamento de clientes e seus acessos (SSH, RDP, URLs)
- Clonagem rápida de acesso (gera apelido `-copia` automaticamente)
- Checagem manual de conectividade TCP por cliente ou para todos os clientes
- Busca global (`Ctrl+K`) + busca local por clientes/acessos
- Busca global com prioridade sobre filtros locais, pesquisando alias, host, usuário, URL, domínio, observações e porta
- Favoritos de acesso (estrela amarela quando ativo)
- Ações rápidas por item: abrir, copiar host, copiar usuário, copiar URL
- Log de eventos em CSV (`eventos.csv`) com atalho de consulta (`Ctrl+Alt+J`)
- Exportação e importação de backup em ZIP pelo menu
- Atalho `Ctrl+Shift+B` para exportar backup ZIP com `clientes.csv`, `acessos.csv` e `eventos.csv`
- Validação rígida no cadastro:
	- Host: apenas IPv4 válido
	- Porta: faixa de `0` a `65535`
	- URL: apenas `http://` ou `https://`
- Publicação desktop para Linux e Windows (artifacts em `publish/` e `dist/`)

Resumo rápido
-------------

- Executável publicado: `MenuProUI` (Linux) / `MenuProUI.exe` (Windows) na pasta de `publish`.
- Dados do usuário: diretório de aplicação (`AppPaths.AppDir`) — por exemplo `~/.config/MenuProUI` (Linux), `~/Library/Application Support/MenuProUI` (macOS) ou `%APPDATA%\\MenuProUI` (Windows).
- Arquivos de dados principais:
	- `clientes.csv`
	- `acessos.csv`
	- `eventos.csv` (auditoria)
	- `preferences.json` (tema, ícones e densidade)

Requisitos
----------
- .NET SDK 8.0+ ou 10.0+ instalado
- Para empacotar no Windows: ferramentas WiX (opcional) ou use `dotnet publish` + criador de instalador de sua preferência

Build e execução (desenvolvimento)
----------------------------------
Para compilar a solução localmente (modo desenvolvimento):

```bash
dotnet build MenuProUI.sln -c Release
dotnet run --project MenuProUI.csproj -c Release
```

Publicar (criando artefatos)
----------------------------
Publicar para Linux (exemplo x64, não autocontido):

```bash
dotnet publish MenuProUI.csproj -c Release -r linux-x64 --self-contained false -o publish/linux-x64
```

Publicar para Windows (exemplo win-x64, self-contained):

```powershell
dotnet publish MenuProUI.csproj -c Release -r win-x64 --self-contained true -o publish\\win-x64
# Em PowerShell/CLI do Windows
```

Depois de publicar, execute o binário correspondente na pasta `publish/*`.

Empacotamento para Linux (.deb)
--------------------------------
Existe um script `build-deb.sh` para gerar `.deb`. No host Linux:

```bash
chmod +x build-deb.sh
./build-deb.sh            # empacota para a arquitetura padrão
./build-deb.sh --all      # empacota para todas as arquiteturas suportadas
```

Empacotamento para Windows
--------------------------
Para Windows você pode:
- Usar `dotnet publish` e distribuir o diretório `publish\\win-x64` como ZIP;
- Ou criar um instalador MSI com WiX usando os arquivos em `Installer/` (requer WiX Toolset e `candle`/`light`).

Release `dist` (padrão da versão MAC)
-------------------------------------
Para gerar artefatos de distribuição no diretório `dist` (ZIP + checksums e MSI quando disponível):

Windows (PowerShell):

```powershell
./build-dist-win.ps1 -Version 1.7.5
```

macOS/Linux (bash, gera ZIP/checksums e tenta MSI via `pwsh` se houver):

```bash
./build-dist-win.sh 1.7.5
```

Arquivos esperados em `dist/`:
- `MenuProUI-<versão>-win-x64.zip`
- `MenuProUI-<versão>-x64.msi` (quando gerado em Windows com WiX)
- `SHA256SUMS`
- `SHA512SUMS`

Instalação
---------
- Linux (.deb):
	```bash
	sudo dpkg -i menupro-ui_1.7.5_amd64.deb
	sudo apt-get install -f
	```
- Windows (ZIP): extraia `publish\\win-x64` e execute `MenuProUI.exe`.

Atalhos de teclado
------------------
- F1 = Ajuda
- Esc = Fechar menus
- Ctrl+Q = Sair
- Ctrl+R = Recarregar
- Ctrl+K = Focar busca global
- Ctrl+F = Focar busca de clientes
- Ctrl+Shift+F = Focar busca de acessos
- Ctrl+Shift+N = Novo acesso
- Enter = Abrir acesso selecionado
- Ctrl+Shift+K = Checar conectividade
- Ctrl+Shift+B = Exportar backup (clientes.csv, acessos.csv, eventos.csv)
- Ctrl+Alt+C = Alternar ícone do menu de clientes
- Ctrl+Alt+A = Alternar ícone do menu de acessos
- Ctrl+Alt+T = Alternar tema claro/escuro
- Ctrl+Alt+J = Exibir log de eventos / últimos acessos

Documentação
-------------
Consulte `MANUAL.md` para detalhes sobre o formato CSV, caminhos de dados e troubleshooting.

GitHub & Suporte
----------------
Repositório: https://github.com/zolinhos/MenuProUi-Win

Contribuição
------------
Abra issues e PRs no repositório para melhorias no empacotamento, traduções ou funcionalidades.

Changelog
---------
Versão atual: `v1.7.5`
