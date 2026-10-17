using System.Text;
using InpaDroid.Diag;

namespace InpaDroid.Ui.Chassis;

/// <summary>Estado de una centralita en el escaneo rápido.</summary>
public enum ScanState { Pending, Ok, Faults, NoResponse, NotFound }

/// <summary>Resultado del escaneo de una centralita. Faults solo cuenta si State es Faults; Detail es el error, si lo hay.</summary>
public sealed record ScanEntry(string Title, string Sgbd, string Variant, ScanState State, int Faults = 0, string Detail = "");

/// <summary>Lógica pura (sin Android) del resumen del escaneo rápido de un chasis.</summary>
public static class ScanSummary
{
    /// <summary>Clasifica la respuesta de FS_LESEN; mismo criterio que la pantalla de la centralita (un set con F_ORT_NR por fallo).</summary>
    public static (ScanState State, int Faults, string Detail) Classify(JobResult r, string failedText)
    {
        if (DiagLogic.StatusError(r, failedText) is string err)
            return (ScanState.NoResponse, 0, err);
        int faults = r.Sets.Count(FaultInfo.IsFault);
        return faults == 0 ? (ScanState.Ok, 0, "") : (ScanState.Faults, faults, "");
    }

    /// <summary>Sin variante: si el .grp/.prg no está en la carpeta ECU es NotFound; si está y no contesta, NoResponse.</summary>
    public static ScanState NoVariantState(string sgbd, IEnumerable<string> ecuFiles) =>
        ecuFiles.Contains(sgbd, StringComparer.OrdinalIgnoreCase) ? ScanState.NoResponse : ScanState.NotFound;

    /// <summary>CSV (RFC 4180, CRLF) con una fila por centralita; el escapado de celdas lo aporta quien llama.</summary>
    public static string ToCsv(IEnumerable<ScanEntry> entries, Func<string, string> cell)
    {
        var sb = new StringBuilder("ecu,sgbd,variant,state,faults,detail\r\n");
        foreach (var e in entries)
        {
            string state = e.State switch
            {
                ScanState.Ok => "ok",
                ScanState.Faults => "faults",
                ScanState.NoResponse => "no_response",
                ScanState.NotFound => "not_found",
                _ => "not_scanned",
            };
            sb.Append(cell(e.Title)).Append(',').Append(cell(e.Sgbd)).Append(',').Append(cell(e.Variant)).Append(',')
              .Append(state).Append(',').Append(e.Faults).Append(',').Append(cell(e.Detail)).Append("\r\n");
        }
        return sb.ToString();
    }
}
