# CaniveteSuico

> Aplicativo desktop multifunções para Windows — o canivete suíço do seu computador.

Aplicativo WPF + WebView2 que reúne diversas ferramentas de conversão e produtividade numa interface moderna e responsiva, sem precisar abrir browser ou instalar dezenas de apps separados.

---

## Ferramentas disponíveis

| Ferramenta | O que faz |
|---|---|
| **YouTube Downloader** | Baixa vídeos (MP4) e áudio (MP3) com seleção de qualidade (1080p, 720p, 480p, 360p) |
| **Conversor de Imagens** | Converte entre JPG, PNG, WEBP, BMP, GIF, TIFF |
| **PDF para Word** | Converte PDF em DOCX editável |
| **Word para PDF** | Converte DOCX em PDF com fidelidade ao layout |
| **HTML para PDF** | Converte arquivos HTML, URLs ou código HTML colado diretamente |
| **Mesclar PDF** | Une múltiplos PDFs em um único arquivo com reordenação visual |
| **Dividir PDF** | Extrai páginas selecionadas, divide em partes iguais ou um PDF por página |
| **Comprimir PDF** | Reduz o tamanho do PDF com compressão de streams |
| **Conversor de Vídeo** | Converte entre MP4, MKV, WebM, AVI, GIF, MP3, AAC, WAV, FLAC via FFmpeg |
| **Agendador de Downloads** | Agenda downloads do YouTube para horários específicos |

---

## Pré-requisitos de desenvolvimento

### Runtime obrigatório

| Dependência | Versão | Link |
|---|---|---|
| .NET SDK | 8.0 ou superior | https://dotnet.microsoft.com/download |
| Visual Studio | 2022 (recomendado) | https://visualstudio.microsoft.com |

> O **WebView2** já vem instalado em qualquer máquina com Windows 10/11 atualizado (faz parte do Edge). Não precisa instalar separadamente.

### Binários externos (não estão no repositório)

Coloque os arquivos na pasta `CaniveteSuico.App/tools/` antes de rodar o projeto:

| Arquivo | Para que serve | Download |
|---|---|---|
| `yt-dlp.exe` | Downloads do YouTube | https://github.com/yt-dlp/yt-dlp/releases/latest |
| `ffmpeg.exe` | Conversão de vídeo/áudio | https://ffmpeg.org/download.html → Windows builds |
| `pandoc.exe` | Conversão DOCX ↔ PDF | https://pandoc.org/installing.html |

> A pasta `tools/` está no `.gitignore` e **nunca sobe para o repositório** pois os arquivos são grandes demais (pandoc tem ~220 MB).

---

## Rodar em desenvolvimento

```bash
# 1. Clone o repositório
git clone https://github.com/Marc0zDev/smartkit-app.git
cd smartkit-app

# 2. Coloque os binários em CaniveteSuico.App/tools/
#    (yt-dlp.exe, ffmpeg.exe, pandoc.exe)

# 3. Restaure os pacotes NuGet
dotnet restore CaniveteSuico.App/CaniveteSuico.App.csproj

# 4. Rode em modo Debug
dotnet run --project CaniveteSuico.App/CaniveteSuico.App.csproj
```

Ou abra `CaniveteSuico.sln` no Visual Studio 2022 e pressione **F5**.

> Na primeira execução, o PuppeteerSharp pode demorar alguns segundos para inicializar o Microsoft Edge headless — isso é normal.

---

## Arquitetura

```
CaniveteSuico/
├── CaniveteSuico.App/
│   ├── App.xaml / App.xaml.cs       # Startup: registro de encodings, handlers de exceção
│   ├── Program.cs                   # Entry point customizado (obrigatório para Velopack)
│   ├── MainWindow.xaml/.cs          # Shell WPF: hospeda o WebView2
│   │
│   ├── Bridge/
│   │   ├── IBridgeHandler.cs        # Interface: Action (string) + HandleAsync(data, reply)
│   │   ├── BridgeMessage.cs         # DTO: { action, data }
│   │   ├── BridgeDispatcher.cs      # Router: recebe JSON do front, despacha para o handler certo
│   │   └── UpdateBridgeHandler.cs   # Handler: INSTALL_UPDATE → AppUpdater
│   │
│   ├── Services/
│   │   ├── YouTubeDownloaderService.cs   # yt-dlp.exe wrapper
│   │   ├── ImageConverterService.cs      # ImageSharp
│   │   ├── PdfConverterService.cs        # PDFPig (leitura)
│   │   ├── DocxToPdfService.cs           # Pandoc wrapper
│   │   ├── HtmlToPdfService.cs           # PuppeteerSharp (Edge headless)
│   │   ├── PdfInfoService.cs             # PDFsharp: lê metadados (nº páginas)
│   │   ├── MergePdfService.cs            # PDFsharp: mescla PDFs
│   │   ├── SplitPdfService.cs            # PDFsharp: divide PDFs (3 modos)
│   │   ├── CompressPdfService.cs         # PDFsharp: comprime PDF
│   │   ├── VideoConverterService.cs      # ffmpeg.exe wrapper
│   │   ├── DownloadSchedulerService.cs   # Timer + fila de jobs agendados
│   │   ├── AppUpdater.cs                 # Velopack: check + download + restart
│   │   ├── FileDialogService.cs          # Diálogos nativos de arquivo
│   │   └── OpenFileService.cs            # Diálogo de abrir arquivo
│   │
│   ├── Logging/
│   │   └── AppLogger.cs             # Logger simples para arquivo + console
│   │
│   ├── wwwroot/                     # Frontend (HTML/CSS/JS puro, sem frameworks)
│   │   ├── index.html               # Shell da UI + todos os painéis
│   │   ├── style.css                # Tema escuro GitHub-inspired com acento vermelho
│   │   ├── bridge.js                # Comunicação JS ↔ C# + sistema de toasts
│   │   └── pages/
│   │       ├── youtube.js           # Lógica do downloader
│   │       ├── image.js             # Conversor de imagens
│   │       ├── pdf.js               # PDF para Word
│   │       ├── docxpdf.js           # Word para PDF
│   │       ├── htmlpdf.js           # HTML para PDF
│   │       ├── mergepdf.js          # Mesclar PDFs
│   │       ├── splitpdf.js          # Dividir PDF (chips visuais de página)
│   │       ├── compresspdf.js       # Comprimir PDF
│   │       ├── video.js             # Conversor de vídeo
│   │       └── scheduler.js        # Agendador de downloads
│   │
│   └── tools/                       # ← NÃO está no git (ver .gitignore)
│       ├── yt-dlp.exe
│       ├── ffmpeg.exe
│       └── pandoc.exe
│
├── build-installer.ps1              # Script de build do instalador
├── CaniveteSuico.sln
└── README.md
```

### Como a comunicação frontend ↔ backend funciona

```
Frontend (JS)          Bridge                   Backend (C#)
─────────────────────────────────────────────────────────────
callCSharp(action, data)
    │
    └─► WebView2.PostMessage(JSON)
                               │
                               └─► BridgeDispatcher.DispatchAsync()
                                       │
                                       └─► handler.HandleAsync(data, reply)
                                                   │
                                      reply(objeto) ◄─── resultado / progresso
                                           │
                     WebView2.PostWebMessageAsJson(JSON)
                               │
               document.dispatchEvent('cs-message')
                               │
                   page module ouve e atualiza a UI
```

---

## Construindo o instalador

### Pré-requisitos de build

```powershell
# Instalar o Velopack CLI (apenas uma vez, global)
dotnet tool install -g vpk

# Verificar instalação
vpk --version
```

### Gerar o instalador

```powershell
# Na raiz do repositório:
.\build-installer.ps1
```

O script faz automaticamente:
1. **`dotnet publish`** — compila self-contained para `win-x64` (sem dependência de .NET instalado na máquina do usuário)
2. **`vpk pack`** — empacota com Velopack

Arquivos gerados em `./releases/`:

| Arquivo | Para quê |
|---|---|
| `CaniveteSuico-Setup.exe` | **Instalador para novos usuários** — distribua este |
| `CaniveteSuico-X.X.X-win-x64-full.nupkg` | Pacote completo de atualização |
| `CaniveteSuico-X.X.X-win-x64-delta.nupkg` | Pacote delta (só o diff — usuários já instalados baixam este) |
| `RELEASES-win-x64` | Manifesto de versões consultado pelo app para checar updates |

> **Atenção:** antes de buildar, copie os binários de `tools/` para dentro da pasta `publish/` manualmente, ou adicione uma etapa no `build-installer.ps1` para copiá-los. Os binários não estão no git.

### Customizar a versão antes de buildar

Edite o `<Version>` no arquivo `CaniveteSuico.App/CaniveteSuico.App.csproj`:

```xml
<Version>1.1.0</Version>
```

Ou passe diretamente no script:

```powershell
.\build-installer.ps1 -Version 1.1.0
```

### Adicionar ícone ao instalador

```powershell
.\build-installer.ps1 -Version 1.1.0 -Icon .\assets\icon.ico
```

---

## Publicando uma atualização (Velopack + GitHub Releases)

O app verifica atualizações automaticamente 5 segundos após abrir, consultando o GitHub Releases deste repositório.

### Passo a passo

```
1. Bump de versão
   └─► Edite <Version> no .csproj (ex: 1.0.0 → 1.1.0)

2. Commit + push na branch master
   └─► git commit -m "release: v1.1.0"
       git push origin master

3. Build do instalador
   └─► .\build-installer.ps1 -Version 1.1.0

4. Criar Release no GitHub
   └─► https://github.com/Marc0zDev/smartkit-app/releases/new
       Tag: v1.1.0
       Título: CaniveteSuico v1.1.0

5. Upload dos arquivos
   └─► Faça upload de TODOS os arquivos da pasta ./releases/
       (Setup.exe + .nupkg(s) + RELEASES-win-x64)

6. Publicar a Release
   └─► Clique em "Publish release"
```

Quando um usuário já instalado abrir o app, ele verá o banner azul de update no topo e poderá clicar em "Atualizar agora". O download e o restart acontecem automaticamente.

---

## Fluxo de branches

| Branch | Propósito |
|---|---|
| `master` | Código estável — base para releases e instaladores |
| `dev` | Desenvolvimento ativo — todo trabalho novo começa aqui |

### Contribuindo

```bash
# Sempre parta da branch dev
git checkout dev
git pull origin dev

# Crie uma branch para a feature
git checkout -b feature/nome-da-feature

# Ao terminar, abra um PR de feature → dev
# Depois de testado, dev → master para release
```

---

## Pacotes NuGet utilizados

| Pacote | Versão | Função |
|---|---|---|
| `Microsoft.Web.WebView2` | 1.0.x | Shell do browser embutido |
| `PuppeteerSharp` | 24.x | Controle do Edge headless para HTML→PDF |
| `PDFsharp` | 7.0.0-preview | Merge, Split, Compress de PDFs |
| `PdfPig` | 0.1.x | Leitura de conteúdo PDF |
| `DocumentFormat.OpenXml` | 3.x | Manipulação de arquivos DOCX |
| `SixLabors.ImageSharp` | 3.x | Conversão de imagens |
| `Velopack` | 0.0.x | Auto-update do instalador |

---

## Logs de diagnóstico

Os logs ficam em:
```
%LOCALAPPDATA%\CaniveteSuico\logs\app-YYYY-MM-DD.log
```

Em modo Debug, um console de diagnóstico abre junto com o app mostrando os logs em tempo real.

---

## Licença

Uso pessoal. Todos os direitos reservados.
