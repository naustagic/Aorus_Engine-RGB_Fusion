using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;

namespace AorusCoolerConverter.Core;

public enum HoltekOperatingMode
{
    Nenhum,
    ModoNormal_PID3020,
    JaEmIap_PIDA126,
    AmbosDetectados,
    Outro
}

public class HoltekPnpStatus
{
    public HoltekOperatingMode Mode { get; set; } = HoltekOperatingMode.Nenhum;
    public string InstanceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string Status { get; set; } = "";
    public uint ConfigManagerErrorCode { get; set; }
    public string ErrorDescription { get; set; } = "";

    public override string ToString()
    {
        return Mode switch
        {
            HoltekOperatingMode.ModoNormal_PID3020 => $"MODO NORMAL (PID_3020) — {DeviceName}",
            HoltekOperatingMode.JaEmIap_PIDA126 => $"JÁ EM MODO IAP / BOOTLOADER (PID_A126) — {DeviceName}",
            HoltekOperatingMode.AmbosDetectados => "AMBOS DETECTADOS (PID_3020 e PID_A126)",
            HoltekOperatingMode.Outro => $"OUTRO DISPOSITIVO HOLTEK: {InstanceId}",
            _ => "NENHUM DISPOSITIVO HOLTEK DETECTADO VIA USB"
        };
    }
}

public static class AorusDiagnostics
{
    private static readonly string[] GigabyteProcessNames = new[]
    {
        "RGBFusion",
        "AorusEngine",
        "AIO_Service",
        "GLckIO2",
        "GvLedLib",
        "EasyTuneEngineService",
        "Aorus"
    };

    public static HoltekPnpStatus DetectHoltekUsbState()
    {
        var result = new HoltekPnpStatus();
        bool foundNormal = false;
        bool foundIap = false;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, Name, Status, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE DeviceID LIKE '%VID_04D9%'");

            foreach (ManagementObject dev in searcher.Get())
            {
                string id = dev["DeviceID"]?.ToString() ?? "";
                string name = dev["Name"]?.ToString() ?? "";
                string status = dev["Status"]?.ToString() ?? "";
                uint errCode = dev["ConfigManagerErrorCode"] is uint u ? u : Convert.ToUInt32(dev["ConfigManagerErrorCode"] ?? 0);

                result.InstanceId = id;
                result.DeviceName = name;
                result.Status = status;
                result.ConfigManagerErrorCode = errCode;
                result.ErrorDescription = TranslateCmProbCode(errCode);

                if (id.Contains("PID_3020", StringComparison.OrdinalIgnoreCase))
                {
                    foundNormal = true;
                }
                if (id.Contains("PID_A126", StringComparison.OrdinalIgnoreCase))
                {
                    foundIap = true;
                }
            }

            if (foundNormal && foundIap)
                result.Mode = HoltekOperatingMode.AmbosDetectados;
            else if (foundIap)
                result.Mode = HoltekOperatingMode.JaEmIap_PIDA126;
            else if (foundNormal)
                result.Mode = HoltekOperatingMode.ModoNormal_PID3020;
            else if (!string.IsNullOrEmpty(result.InstanceId))
                result.Mode = HoltekOperatingMode.Outro;
            else
                result.Mode = HoltekOperatingMode.Nenhum;
        }
        catch (Exception ex)
        {
            AorusLogger.Log("ERR", $"Falha ao consultar Win32_PnPEntity: {ex.Message}");
        }

        return result;
    }

    public static string TranslateCmProbCode(uint code)
    {
        return code switch
        {
            0 => "CM_PROB_NONE (0): Dispositivo funcionando normalmente sem erros de driver.",
            1 => "CM_PROB_NOT_CONFIGURED (1): Dispositivo não configurado.",
            10 => "CM_PROB_FAILED_START (10): O dispositivo não pôde ser iniciado pelo Windows.",
            18 => "CM_PROB_REINSTALL (18): Reinstale os drivers deste dispositivo.",
            28 => "CM_PROB_FAILED_INSTALL (28): Os drivers deste dispositivo não estão instalados.",
            43 => "CM_PROB_FAILED_POST_START (43): O Windows interrompeu o dispositivo porque ele relatou problemas (Falha na solicitação de descritor).",
            _ => $"CM_PROB ({code}): Código de problema PnP relatado pelo Windows."
        };
    }

    public static List<(int Pid, string Name)> GetRunningGigabyteProcesses()
    {
        var list = new List<(int, string)>();
        foreach (var procName in GigabyteProcessNames)
        {
            try
            {
                var procs = Process.GetProcessesByName(procName);
                foreach (var p in procs)
                {
                    list.Add((p.Id, p.ProcessName));
                }
            }
            catch
            {
            }
        }
        return list;
    }

    public static (bool Success, string Message) CloseGigabyteProcesses()
    {
        var running = GetRunningGigabyteProcesses();
        if (running.Count == 0)
        {
            return (true, "Nenhum processo Gigabyte concorrente em execução.");
        }

        int closed = 0;
        foreach (var (pid, name) in running)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                p.Kill();
                p.WaitForExit(2000);
                closed++;
                AorusLogger.Log("PROC", $"Processo concorrente encerrado: {name} (PID: {pid})");
            }
            catch (Exception ex)
            {
                AorusLogger.Log("WARN", $"Não foi possível encerrar {name} (PID: {pid}): {ex.Message}");
            }
        }

        return (true, $"{closed} processo(s) Gigabyte encerrado(s) com sucesso.");
    }

    /// <summary>
    /// Searches Win32_DiskDrive specifically for the HOLTEK LCD_PLATFORM drive,
    /// avoiding probing user's local primary drives (NVMe, SSDs, HDDs).
    /// </summary>
    public static string? FindLcdPlatformDiskDrive()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, Caption, Model, PNPDeviceID FROM Win32_DiskDrive");

            foreach (ManagementObject drive in searcher.Get())
            {
                string caption = drive["Caption"]?.ToString() ?? "";
                string model = drive["Model"]?.ToString() ?? "";
                string pnp = drive["PNPDeviceID"]?.ToString() ?? "";
                string devId = drive["DeviceID"]?.ToString() ?? "";

                if (caption.Contains("LCD_PLATFORM", StringComparison.OrdinalIgnoreCase) ||
                    model.Contains("LCD_PLATFORM", StringComparison.OrdinalIgnoreCase) ||
                    (pnp.Contains("VID_04D9", StringComparison.OrdinalIgnoreCase) && pnp.Contains("PID_A126", StringComparison.OrdinalIgnoreCase)))
                {
                    AorusLogger.Log("PNP", $"Dispositivo LCD_PLATFORM localizado em '{devId}' ({caption}).");
                    return devId;
                }
            }
        }
        catch (Exception ex)
        {
            AorusLogger.Log("WARN", $"Exceção ao consultar Win32_DiskDrive: {ex.Message}");
        }

        return null;
    }
}
