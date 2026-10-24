# AORUS Liquid Cooler Enhanced Suite 💧🖥️

Conjunto moderno de ferramentas, engenharia reversa e código-fonte aprimorado para os water coolers **GIGABYTE Aorus Liquid Cooler (240 / 280 / 360)**.

Este projeto substitui a dependência do software legado da Gigabyte por soluções limpas, rápidas e sem bugs, permitindo o envio de imagens em **PNG (com transparência)**, **JPG**, **WebP**, **BMP** e **GIFs animados** diretamente para o display circular de 320×320 pixels sem necessidade de conversão manual prévia.

---

## 📑 Índice
1. [Visão Geral e Engenharia Reversa](#-visão-geral-e-engenharia-reversa)
2. [Análise dos Softwares Originais](#-análise-dos-softwares-originais)
3. [Estrutura do Repositório](#-estrutura-do-repositório)
4. [AorusCoolerConverter (Ferramenta Universal)](#-aoruscoolerconverter-ferramenta-universal)
5. [Aprimoramentos nos Softwares Nativos](#-aprimoramentos-nos-softwares-nativos)
6. [Verificador de Atualizações via GitHub](#-verificador-de-atualizações-via-github)
7. [Compilação e Testes Automatizados](#-compilação-e-testes-automatizados)
8. [GitHub Actions CI/CD](#-github-actions-cicd)
9. [Segurança e Diretrizes de Uso](#-segurança-e-diretrizes-de-uso)

---

## 🔍 Visão Geral e Engenharia Reversa

Os water coolers Aorus utilizam internamente um microcontrolador **Holtek HT32F** conectado a um display LCD de 320×320 pixels e a uma memória **SPI Flash** externa de alta velocidade.

### Por que os softwares originais rejeitavam PNG/JPG e exigiam BMP 16 bits?
O hardware do LCD opera nativamente com buffers de pixels em **RGB565** (2 bytes por pixel, 320×320 = 204.800 bytes por frame) e não possui decodificador JPEG ou PNG embutido na controladora do display.

Os softwares antigos da Gigabyte (`FlashAIOImage.exe` e `GP-AORUS LIQUID COOLER 360G.exe`) tentavam converter as imagens usando a API legada do Windows GDI+ chamando `Bitmap.LockBits` diretamente no arquivo carregado:
- Se a imagem fosse um PNG de 32 bits (com canal alfa) ou JPG de 24 bits, o GDI+ falhava ou gerava ruído visual.
- O software tratava o erro abortando com a mensagem *"不支援的檔案類型!"* (Tipo de arquivo não suportado).
- Além disso, havia um bug crítico de cálculo na gravação de blocos (`(Length - 1) / blockSize`), que corrompia o último bloco de 64 KB caso o arquivo tivesse tamanho múltiplo exato do bloco.

### O que foi corrigido nesta suíte:
- **Suporte Universal de Imagens:** Leitura de PNG (com transparência mesclada suavemente sobre fundo preto), JPG, BMP (8/16/24/32 bits), WebP e GIFs animados.
- **Suporte Completo a GIF Animado:** O formato `.bin` do cooler suporta nativamente animações de até centenas de quadros (como demonstrado pelo pacote original `0.bin` com 129 frames).
- **Correção dos Bugs de Gravação:** Corrigido o cálculo de limite de blocos de 64 KB em `FlashAIOImage`.
- **Modernização:** Migração completa para **.NET 8.0 Windows**, eliminando travamentos de compatibilidade e dependências obsoletas do .NET Framework 4.0.

Consulte o documento completo [PROTOCOL_GUIDE.md](PROTOCOL_GUIDE.md) para detalhes técnicos aprofundados dos comandos SCSI e pacotes USB HID.

---

## 📦 Análise dos Softwares Originais

Na pasta `originais/`, encontram-se os dois arquivos analisados:

1. **`AORUS_ENGINE_SETUP_V2.3.1.0_B251226_x86_Signed.exe`** (Aorus Engine Oficial):
   - Contém a pilha completa de comunicação com o water cooler.
   - Dentro de seu pacote de atualização (`UpdPack.exe`), contém tanto os utilitários de gravação do LCD (`FlashAIOImage.exe` e `GP-AORUS LIQUID COOLER 360G.exe`) quanto o instalador offline completo e oficial do **RGB Fusion** (131 MB).

2. **`rgb-fusion-b24.1202.1-installer_g-Fy5J6.exe`**:
   - A engenharia reversa com o descompactador `innounp 2.70.1` comprovou que este executável **não é o instalador autônomo da Gigabyte**, mas sim um wrapper web-installer da Kopetra Ltd ("Download-Manager"), frequentemente associado a adware.
   - O RGB Fusion genuíno offline foi isolado e preservado em `rgb_fusion/extracted/offline_package/setup.exe`.

---

## 📁 Estrutura do Repositório

O repositório é organizado de forma limpa e modular:

```text
eng-rev-watercooler/
├── .github/
│   └── workflows/
│       └── build.yml               <- CI/CD com compilação cruzada x86/x64 e releases
├── aorus_engine/
│   ├── extracted/                  <- Arquivos extraídos do instalador Aorus Engine
│   └── src/
│       ├── FlashAIOImage/          <- Utilitário original de gravação LCD modernizado (.NET 8)
│       └── 360G_LCDPlatform/       <- Software de configuração 360G modernizado (.NET 8)
├── rgb_fusion/
│   ├── extracted/
│   │   ├── installer_stub/         <- Análise do instalador stub da web
│   │   └── offline_package/        <- Instalador genuíno offline do RGB Fusion (131 MB)
│   └── docs/
├── tools/
│   ├── AorusCoolerConverter/       <- Conversor universal e uploader SCSI moderno (GUI + CLI)
│   └── tests/                      <- Testes unitários automatizados (xUnit)
├── originais/                      <- Instaladores originais de referência
├── AorusWaterCoolerEnhanced.slnx   <- Solução unificada moderna do Visual Studio
├── PROTOCOL_GUIDE.md               <- Especificação técnica reversa do protocolo SCSI e HID
└── README.md                       <- Documentação principal
```

---

## 🛠️ AorusCoolerConverter (Ferramenta Universal)

Localizada em `tools/AorusCoolerConverter/`, esta ferramenta opera tanto com **Interface Gráfica (GUI moderna)** quanto em **Modo Linha de Comando (CLI)**.

### Recursos:
- **Pré-visualização Circular 320×320:** Exibe em tempo real exatamente como a imagem ficará na tela redonda do cooler.
- **Player de GIF Animado Integrado:** Carrega GIFs animados e permite visualizar a animação rodando ao vivo antes de enviar.
- **Ajuste de Rotação:** Gira a imagem em 0°, 90°, 180° ou 270° com interpolação bicúbica de alta qualidade.
- **Exportação Dupla:** Salva como arquivo `.bin` (pronto para gravação no cooler) ou `.bmp` 16 bits RGB565.
- **Detecção do Hardware LCD:** Localiza automaticamente a unidade `LCD_PLATFORM` e o status da conexão via SCSI.
- **Verificador de Atualizações GitHub:** Permite colar o repositório do projeto para checar se há novos builds publicados.

### Uso via Linha de Comando (CLI):
```powershell
# Exibir ajuda:
dotnet run --project tools/AorusCoolerConverter/AorusCoolerConverter.csproj -- --help

# Converter PNG ou GIF para .bin do cooler:
dotnet run --project tools/AorusCoolerConverter/AorusCoolerConverter.csproj -- convert minha_arte.png -o animacao.bin -r 90

# Detectar se o cooler está conectado no barramento SCSI:
dotnet run --project tools/AorusCoolerConverter/AorusCoolerConverter.csproj -- detect
```

---

## ⚡ Aprimoramentos nos Softwares Nativos

Os dois aplicativos nativos da Gigabyte foram restaurados e modernizados em `aorus_engine/src/`:

### 1. `FlashAIOImage` (`aorus_engine/src/FlashAIOImage/`)
- Atualizado de .NET Framework 4.0 para **.NET 8.0 Windows**.
- Integrado ao motor `AorusImageProcessor`: agora permite abrir qualquer imagem diretamente (PNG, JPG, BMP, GIF, WebP) na interface. O aplicativo prepara o buffer RGB565 automaticamente em memória.
- Corrigido o bug na linha 180 de divisão truncada do bloco de gravação.

### 2. `360G_LCDPlatform` (`aorus_engine/src/360G_LCDPlatform/`)
- Atualizado para **.NET 8.0 Windows**.
- Substituída a classe legada `BmpConvert` e a biblioteca `RLE_Compress` por implementações modernas em C#.
- Diálogo de abertura de arquivos configurado para aceitar todos os formatos modernos de imagem.
- Rótulo de versão (`label_version`) transformado em botão interativo que verifica atualizações diretamente nas Releases do GitHub.

---

## 🔄 Verificador de Atualizações via GitHub

Tanto a ferramenta independente `AorusCoolerConverter` quanto o aplicativo `360G_LCDPlatform` possuem verificação de atualização integrada conectada à API do GitHub:

1. Ao clicar no rótulo de versão ou na barra de atualizações, uma janela solicita o endereço do repositório no formato:
   ```text
   naustagic/Aorus_Engine-RGB_Fusion
   ```
2. O aplicativo consulta `https://api.github.com/repos/{owner}/{repo}/releases/latest`.
3. Se houver uma nova release lançada pelo GitHub Actions (ex: `v2.4.0`), o app exibe as notas da versão e disponibiliza um botão para abrir a página de download diretamente no navegador.

---

## 🧪 Compilação e Testes Automatizados

### Pré-requisitos
- .NET 8.0 SDK ou superior instalado.

### Compilando a Solução Completa
```powershell
dotnet build AorusWaterCoolerEnhanced.slnx
```

### Executando a Suíte de Testes Unitários
Os testes verificam a integridade dos cabeçalhos binários, descompactação do `0.bin` original da Gigabyte e fidelidade de cores RGB565:
```powershell
dotnet test tools/tests/tests.csproj
```
Resultado: **4/4 testes aprovados (100% de sucesso)**.

---

## 🚀 GitHub Actions CI/CD

O fluxo de integração contínua está configurado em `.github/workflows/build.yml`.

### O que o workflow executa a cada Push ou Pull Request:
1. Executa a suíte de testes unitários automatizados no Windows Runner.
2. Compila a solução para arquiteturas **`win-x86`** e **`win-x64`**.
3. Gera duas variantes de binários para cada arquitetura:
   - **Standalone (Single-File):** Executável único autocontido que roda em qualquer PC com Windows sem precisar instalar o .NET Runtime.
   - **Portable (Framework-Dependent):** Pacote leve dependente do .NET 8 instalado.
4. Gera arquivos `.zip` compactados e armazena como artefatos da build.

### Publicação Automática de Releases:
Ao criar e enviar uma tag de versão para o GitHub:
```powershell
git tag -a v2.3.1 -m "Release v2.3.1 com suporte universal a imagens e correções SCSI"
git push origin v2.3.1
```
O GitHub Actions criará automaticamente uma **GitHub Release**, publicando todos os arquivos `.zip` compilados para download imediato.

---

## ⚠️ Segurança e Diretrizes de Uso

> [!IMPORTANT]
> **Proteção de Hardware:** Todos os testes automatizados incluídos nesta suíte operam exclusivamente em memória e com arquivos locais, sem efetuar gravações na SPI Flash física durante a compilação ou execução de testes.
>
> Ao utilizar os botões de gravação de firmware ou imagem no hardware real:
> 1. Não desligue o computador nem desconecte o cabo USB durante o processo de upload.
> 2. Certifique-se de que nenhum outro software de monitoramento (Aorus Engine original, HWMonitor ou HWiNFO em modo de acesso exclusivo) esteja concorrendo pelo barramento USB no mesmo instante.
