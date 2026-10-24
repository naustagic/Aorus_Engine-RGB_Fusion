# Guia Técnico de Engenharia Reversa e Protocolo: Aorus Liquid Cooler LCD

Este documento detalha a arquitetura de hardware, o protocolo duplo de comunicação (SCSI Pass-Through + USB HID) e a estrutura binária dos dados para os water coolers da linha **GIGABYTE Aorus Liquid Cooler (240 / 280 / 360)** equipados com display LCD circular de 320×320 pixels.

---

## 1. Arquitetura de Hardware e Componentes

A cabeça da bomba e o display LCD integram os seguintes controladores:
- **Microcontrolador Principal (LCD & Flash):** Holtek HT32F Series (ARM Cortex-M0+/M3, USB VID: `0x04D9`, PID: `0x3020`).
- **Memória Flash Não-Volátil:** SPI NOR Flash externa conectada ao HT32F com tamanho de bloco de apagamento de 64 KB (`0x10000` = 65.536 bytes).
- **Controlador RGB e Bomba:** ITE IT8295 / IT8297 gerenciado via `GvLedLib.dll` no RGB Fusion.
- **Display LCD:** Matriz ativa circular com resolução física de **320 × 320 pixels**, profundidade de cor nativa de **16 bits (RGB565)**.

---

## 2. Canal de Comunicação 1: SCSI Pass-Through (Upload de Imagens e Firmware)

O microcontrolador Holtek expõe uma unidade de armazenamento USB Mass Storage identificada no Windows com o rótulo de volume **`LCD_PLATFORM`**.

Em vez de transferir arquivos por um sistema de arquivos comum (FAT/NTFS), o software proprietário envia blocos de comandos de 16 bytes (**CDB - Command Descriptor Block**) usando a API Win32 `DeviceIoControl` com o código de controle:
```c
#define IOCTL_SCSI_PASS_THROUGH_DIRECT 0x4D014
```

### Estrutura do SCSI Command Descriptor Block (CDB - 16 bytes)
```
Byte 0      : Código de Operação (Opcode)
Byte 1      : Flags / Modo
Bytes 2..5  : Endereço de Memória Flash (Big Endian na gravação, ou Little Endian conforme o comando)
Bytes 6..7  : Tamanho do Bloco / Transferência
Bytes 8..15 : Reservado / Preenchimento com zeros
```

### Tabela de Opcodes SCSI Específicos do Holtek HT32F

| Opcode | Nome da Operação | Finalidade e Descrição |
| :--- | :--- | :--- |
| **`0xF1`** | `CMD_TEST` / Handshake | Testa o estado da controladora. Retorna bytes de estado indicando se o dispositivo está em modo **AP** (Application Mode / Normal) ou **IAP** (In-Application Programming / Bootloader). |
| **`0xF2`** | `CMD_GET_ID` | Lê o Chip ID do microcontrolador e a versão do firmware em execução na ROM interna. |
| **`0xF3`** | `CMD_WRITE` | Grava um bloco de dados (até 4.096 bytes por pacote SCSI) no endereço especificado da SPI Flash ou memória de programa. |
| **`0xF4`** | `CMD_READ` | Lê um bloco de dados para verificação de integridade pós-gravação (CRC / comparação bit a bit). |
| **`0xF5`** | `CMD_ERASE` | Apaga blocos de 64 KB da SPI Flash. Endereços típicos: `0x00000`, `0x10000`, `0x20000`, etc. |
| **`0xF6`** | `CMD_ALTERNATE_MODE` | Comanda o microcontrolador a reiniciar no modo selecionado: `0x00` = Modo Aplicação (AP), `0x01` = Modo Bootloader (IAP). |

---

## 3. Canal de Comunicação 2: USB HID (Controle em Tempo Real e Rotação)

Enquanto o canal SCSI é reservado para uploads de imagens e firmware, o canal **USB HID** é utilizado para ajustes dinâmicos sem gravar na Flash:
- **Tamanho do Relatório:** 9 bytes de Feature Report via `HidD_SetFeature`.
- **Report ID:** `0x00`.

### Formato do Pacote HID (9 Bytes)

```
Byte 0: Report ID (0x00)
Byte 1: Opcode do Comando
Byte 2: Parâmetro 1 (Modo de exibição / Rotação)
Byte 3: Cor Primária (High Byte RGB565)
Byte 4: Cor Primária (Low Byte RGB565)
Byte 5: Cor Secundária (High Byte RGB565)
Byte 6: Cor Secundária (Low Byte RGB565)
Bytes 7..8: Checksum / Padding
```

### Opcodes do Canal HID:
- **`0xE4`**: Ajusta o layout do LCD, rotação da tela (0°, 90°, 180°, 270°) e as cores RGB565 primária/secundária dos números de telemetria (RPM, Temperatura).
- **`0xCA`**: Alterna o modo de telemetria na tela (Modo Entusiasta, Modo Função, Modo Personalizado).
- **`0xD6`**: Pacote de sincronização / Keep-Alive.

---

## 4. Estrutura Binária dos Arquivos `.bin` do LCD

O display LCD não armazena arquivos PNG ou JPG comprimidos porque o microcontrolador Holtek não dispõe de decodificador JPEG ou libpng por hardware nem memória RAM suficiente para descomprimir fluxos complexos em tempo real.

Por esse motivo, o dispositivo armazena e lê um arquivo container `.bin` com quadros pré-convertidos em **RGB565**.

### Cabeçalho e Tabela de Quadros

```
+-------------------------------------------------------------------------+
| Offset (Bytes) | Tamanho | Descrição                                    |
+-------------------------------------------------------------------------+
| 0 .. 1         | 2 bytes | Total de Frames (ushort, Little Endian)      |
+-------------------------------------------------------------------------+
| A partir do byte 2, segue a TABELA DE FRAMES (10 bytes por frame):      |
| +---------------------------------------------------------------------+ |
| | Bytes 0..3 : uint endOffset   (Offset final acumulado - 1)          | |
| | Bytes 4..5 : ushort width     (Largura em pixels, padrão 320)       | |
| | Bytes 6..7 : ushort height    (Altura em pixels, padrão 320)        | |
| | Bytes 8..9 : ushort format    (Formato do pixel, 0x0001 = RGB565)   | |
| +---------------------------------------------------------------------+ |
+-------------------------------------------------------------------------+
| Seguindo a tabela, encontram-se os DADOS DE PIXELS CONCATENADOS:        |
| - Cada frame de 320x320 ocupa 320 * 320 * 2 = 204.800 bytes brutos.     |
| - Os pixels são sequenciais linha por linha em formato RGB565:          |
|   Bit 15..11: Red (5 bits)                                              |
|   Bit 10..5 : Green (6 bits)                                            |
|   Bit 4..0  : Blue (5 bits)                                             |
+-------------------------------------------------------------------------+
```

### Exemplo Prático com Base no Arquivo Original `0.bin` (Animação Aorus Oficial)
- **Número de quadros (`count`):** 129 frames.
- **Tamanho do cabeçalho:** `2 + (129 * 10) = 1.292 bytes`.
- **Tamanho total:** ~25 MB de animação pura em RGB565.
- **Frames de sobreposição:** O formato permite frames com dimensões menores (como o frame 45 que possui 174×20 para elementos HUD), o que economiza espaço na SPI Flash.

---

## 5. Causa-Raiz dos Erros nos Softwares Antigos da Gigabyte

Na análise de engenharia reversa das ferramentas originais (`FlashAIOImage.exe` e `GP-AORUS LIQUID COOLER 360G.exe`), foram identificadas as falhas que causavam a rejeição de arquivos e telas travadas:

1. **Rejeição de Imagens Modernas (JPG, PNG, WebP):**
   - O código em `BmpConvert.cs` utilizava a chamada:
     ```csharp
     BitmapData bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format16bppRgb565);
     ```
     diretamente no bitmap original carregado do disco.
   - Em arquivos PNG de 32 bits (com canal alfa) ou imagens JPG indexadas, a GDI+ do Windows não faz a conversão direta de formato via `LockBits` e dispara exceções de `ArgumentException` ou produz corrupção de memória.
   - O diálogo de seleção de arquivo limitava explicitamente as extensões a `*.bmp`, e quando o usuário selecionava imagens de 24 ou 32 bits, o aplicativo exibia a mensagem de erro: *"不支援的檔案類型!"* (Tipo de arquivo não suportado).

2. **Bug de Truncamento de 1 Byte (`FlashAIOImage` Linha 180):**
   - No loop de gravação:
     ```csharp
     int num6 = (int)(m_pCodData.Length - 1) / blockSize;
     ```
     Quando o arquivo `.bin` possuía um tamanho exatamente múltiplo do tamanho de bloco (65.536 bytes), o ` - 1` fazia com que o último bloco inteiro não fosse gravado na Flash, deixando a imagem corrompida ou com faixas pretas.

3. **Inexistência de Tratamento de Transparência (Alpha Blending):**
   - O aplicativo original não possuía nenhum compositor gráfico. Imagens PNG transparentes resultavam em ruído branco ou pixels aleatórios.
   - Na nossa versão aprimorada, o canal alfa é renderizado sobre fundo preto sólido (`Color.Black`), preservando contornos suaves e bordas anti-aliasing perfeitas no display circular.
