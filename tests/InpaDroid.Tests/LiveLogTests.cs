using InpaDroid.Diag;
using InpaDroid.Ui.Live;

namespace InpaDroid.Tests;

public class LiveLogTests
{
    static readonly DateTime T0 = new(2026, 1, 2, 3, 4, 5, 6);

    static string Id(string s) => s;

    [Fact]
    public void FileName_uses_start_time_and_avoids_existing_files()
    {
        Assert.Equal("inpadroid-live-20260102-030405.csv", LiveLog.FileName(T0, _ => false));
        var existing = new HashSet<string> { "inpadroid-live-20260102-030405.csv", "inpadroid-live-20260102-030405-2.csv" };
        Assert.Equal("inpadroid-live-20260102-030405-3.csv", LiveLog.FileName(T0, existing.Contains));
    }

    [Fact]
    public void Header_escapes_series_names_through_delegate()
    {
        Assert.Equal("timestamp,<A>,<=cmd>\r\n", LiveLog.Header(["A", "=cmd"], s => "<" + s + ">"));
        Assert.Equal("timestamp\r\n", LiveLog.Header([], Id));
    }

    [Fact]
    public void Row_follows_column_order_with_gaps_and_invariant_numbers()
    {
        var sample = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["b"] = -3, ["A"] = 1.5 };
        Assert.Equal("2026-01-02 03:04:05.006,1.5,,-3\r\n", LiveLog.Row(T0, ["A", "C", "B"], sample));
    }

    [Fact]
    public void Row_leaves_non_finite_values_empty()
    {
        var sample = new Dictionary<string, double> { ["A"] = double.NaN, ["B"] = double.PositiveInfinity };
        Assert.Equal("2026-01-02 03:04:05.006,,\r\n", LiveLog.Row(T0, ["A", "B"], sample));
    }

    [Fact]
    public void Columns_are_planned_series_or_first_sample_keys()
    {
        Assert.Equal(["X", "Y"], LiveLog.Columns(["X", "Y"], ["Y"]));
        Assert.Equal(["P", "Q"], LiveLog.Columns([], ["P", "Q"]));
    }

    [Fact]
    public void New_file_only_when_sample_has_an_uncovered_series()
    {
        Assert.False(LiveLog.NeedsNewFile(["A", "B"], ["b"]));
        Assert.False(LiveLog.NeedsNewFile(["A"], []));
        Assert.True(LiveLog.NeedsNewFile(["A"], ["A", "C"]));
    }

    [Fact]
    public void Recorded_file_matches_session_export()
    {
        var data = new LiveSeriesData();
        var s1 = new Dictionary<string, double> { ["A"] = 1, ["B"] = 2 };
        var s2 = new Dictionary<string, double> { ["B"] = 0.25 };
        data.AddSample(T0, s1);
        data.AddSample(T0.AddSeconds(1), s2);
        string[] columns = ["A", "B"];
        string log = LiveLog.Header(columns, Id) + LiveLog.Row(T0, columns, s1) + LiveLog.Row(T0.AddSeconds(1), columns, s2);
        Assert.Equal(data.ToCsv(Id), log);
    }
}
