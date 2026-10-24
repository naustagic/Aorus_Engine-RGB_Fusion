using System;
using System.IO;
using System.Windows.Forms;
using AorusCoolerConverter.Core;

namespace AorusCoolerConverter;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // If CLI arguments are provided, run headless CLI tool
        if (args.Length > 0)
        {
            return RunCommandLine(args);
        }

        // Otherwise launch GUI
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
        return 0;
    }

    static int RunCommandLine(string[] args)
    {
        string command = args[0].ToLowerInvariant();
        if (command == "--help" || command == "-h" || command == "help")
        {
            PrintUsage();
            return 0;
        }

        if (command == "convert")
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Erro: Especifique o caminho da imagem de entrada.");
                return 1;
            }

            string inputPath = args[1];
            string outputPath = Path.ChangeExtension(inputPath, ".bin");
            int angle = 0;

            for (int i = 2; i < args.Length; i++)
            {
                if ((args[i] == "-o" || args[i] == "--output") && i + 1 < args.Length)
                {
                    outputPath = args[++i];
                }
                else if ((args[i] == "-r" || args[i] == "--rotate") && i + 1 < args.Length)
                {
                    int.TryParse(args[++i], out angle);
                }
            }

            try
            {
                Console.WriteLine($"Convertendo '{inputPath}' para formato AORUS LCD...");
                var opts = new AorusBinOptions { RotationAngle = angle };
                byte[] bin = AorusImageProcessor.ProcessImageToBin(inputPath, opts);
                File.WriteAllBytes(outputPath, bin);
                Console.WriteLine($"Sucesso! Arquivo gerado: '{outputPath}' ({bin.Length / 1024} KB)");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro na conversão: {ex.Message}");
                return 2;
            }
        }

        if (command == "detect")
        {
            Console.WriteLine("Procurando bomba AORUS LCD_PLATFORM...");
            var dev = AorusScsiTransport.DetectDevice();
            if (dev.IsFound)
            {
                Console.WriteLine($"Encontrado: {dev.ModelName} em {dev.DevicePath}");
                return 0;
            }
            else
            {
                Console.WriteLine("Dispositivo não encontrado.");
                return 1;
            }
        }

        Console.WriteLine($"Comando desconhecido: '{command}'. Use --help para ver as opções.");
        return 1;
    }

    static void PrintUsage()
    {
        Console.WriteLine("==================================================================");
        Console.WriteLine($"AORUS Liquid Cooler LCD Image & Animation Tool CLI v{AorusImageProcessor.AppVersion}");
        Console.WriteLine("==================================================================");
        Console.WriteLine("Uso:");
        Console.WriteLine("  AorusCoolerConverter.exe                       (Abre a interface gráfica)");
        Console.WriteLine("  AorusCoolerConverter.exe convert <imagem> [-o <out.bin>] [-r <0|90|180|270>]");
        Console.WriteLine("  AorusCoolerConverter.exe detect");
        Console.WriteLine();
        Console.WriteLine("Formatos suportados: PNG, JPG, JPEG, BMP, WebP e GIF animado.");
    }
}