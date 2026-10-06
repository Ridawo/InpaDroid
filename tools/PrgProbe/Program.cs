// PrgProbe: vuelca, sin adaptador, la descripción de ficheros EDIABAS (.prg/.grp) a texto.
// Usa los jobs internos de EdiabasLib (_VERSIONINFO, _JOBS, _JOBCOMMENTS, _ARGUMENTS, _RESULTS,
// _TABLES, _TABLE), igual que DiagService.GetJobsAsync y EdiabasToolActivity.
//
// Uso: PrgProbe <carpeta_ecu> <carpeta_salida> NOMBRE [NOMBRE ...]
//   NOMBRE = fichero sin extensión (se busca .prg y luego .grp, sin distinguir mayúsculas).
//   Genera <carpeta_salida>/<NOMBRE>.txt por fichero.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EdiabasLib;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Uso: PrgProbe <carpeta_ecu> <carpeta_salida> NOMBRE [NOMBRE ...]");
            return 1;
        }
        string ecuDir = Path.GetFullPath(args[0]);
        string outDir = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(outDir);

        // Las cadenas de los .prg están en codepage 1252 (umlauts alemanes).
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var ediabas = new EdiabasNet { EdInterfaceClass = new EdInterfaceObd() };
        ediabas.SetConfigProperty("EcuPath", ecuDir);
        ediabas.NoInitForVJobs = true;

        Dictionary<string, string> files = Directory.GetFiles(ecuDir)
            .GroupBy(f => Path.GetFileName(f).ToUpperInvariant())
            .ToDictionary(g => g.Key, g => Path.GetFileName(g.First()));

        int failures = 0;
        foreach (string name in args.Skip(2))
        {
            string fileName = Find(files, name + ".prg") ?? Find(files, name + ".grp");
            if (fileName == null)
            {
                Console.Error.WriteLine($"{name}: no encontrado");
                failures++;
                continue;
            }
            try
            {
                string text = Dump(ediabas, fileName);
                File.WriteAllText(Path.Combine(outDir, name.ToUpperInvariant() + ".txt"), text);
                Console.WriteLine($"{fileName}: ok");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"{fileName}: {EdiabasNet.GetExceptionText(ex)}");
                failures++;
            }
        }
        return failures == 0 ? 0 : 2;
    }

    private static string Find(Dictionary<string, string> files, string name) =>
        files.TryGetValue(name.ToUpperInvariant(), out string real) ? real : null;

    private static string Dump(EdiabasNet ediabas, string fileName)
    {
        // Sin ResolveSgbdFile: un .grp se resolvería con IDENT (necesita el coche); se abre tal cual.
        ediabas.SgbdFileName = fileName;
        var sb = new StringBuilder();
        sb.AppendLine($"##### {fileName}");

        sb.AppendLine("== _VERSIONINFO");
        foreach (var set in Run(ediabas, "_VERSIONINFO", ""))
        {
            foreach (var kv in set.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"  {kv.Key}: {kv.Value.OpData}");
            }
        }

        List<string> jobs = Strings(Run(ediabas, "_JOBS", "ALL"), "JOBNAME");
        sb.AppendLine($"== JOBS ({jobs.Count})");
        foreach (string job in jobs)
        {
            sb.AppendLine();
            sb.AppendLine($"JOB {job}");
            foreach (var set in Run(ediabas, "_JOBCOMMENTS", job))
            {
                foreach (string c in Numbered(set, "JOBCOMMENT"))
                {
                    sb.AppendLine($"  # {c}");
                }
            }
            foreach (var set in Run(ediabas, "_ARGUMENTS", job))
            {
                sb.AppendLine($"  ARG {Str(set, "ARG")} ({Str(set, "ARGTYPE")}) {string.Join(" | ", Numbered(set, "ARGCOMMENT"))}");
            }
            foreach (var set in Run(ediabas, "_RESULTS", job))
            {
                sb.AppendLine($"  RES {Str(set, "RESULT")} ({Str(set, "RESULTTYPE")}) {string.Join(" | ", Numbered(set, "RESULTCOMMENT"))}");
            }
        }

        List<string> tables = Strings(Run(ediabas, "_TABLES", ""), "TABLE");
        sb.AppendLine();
        sb.AppendLine($"== TABLES ({tables.Count})");
        foreach (string table in tables)
        {
            sb.AppendLine();
            sb.AppendLine($"TABLE {table}");
            List<List<string>> lines = ediabas.GetTableLines(table) ?? [];
            foreach (List<string> line in lines)
            {
                sb.AppendLine("  " + string.Join(" ; ", line));
            }
        }
        return sb.ToString();
    }

    private static List<Dictionary<string, EdiabasNet.ResultData>> Run(EdiabasNet ediabas, string job, string arg)
    {
        ediabas.ArgString = arg;
        ediabas.ArgStringStd = "";
        ediabas.ResultsRequests = "";
        ediabas.ExecuteJob(job);
        // El set 0 son resultados de sistema.
        return (ediabas.ResultSets ?? []).Skip(1).ToList();
    }

    private static List<string> Strings(List<Dictionary<string, EdiabasNet.ResultData>> sets, string key) =>
        sets.Select(s => Str(s, key)).Where(s => s.Length > 0).ToList();

    private static string Str(Dictionary<string, EdiabasNet.ResultData> set, string key) =>
        set.TryGetValue(key, out EdiabasNet.ResultData data) && data.OpData is string s ? s : "";

    private static IEnumerable<string> Numbered(Dictionary<string, EdiabasNet.ResultData> set, string prefix)
    {
        for (int i = 0; set.TryGetValue(prefix + i, out EdiabasNet.ResultData data); i++)
        {
            yield return data.OpData as string ?? "";
        }
    }
}
