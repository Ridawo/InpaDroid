using InpaDroid.Ui.Live;
using InpaDroid.Ui.Chassis;

namespace InpaDroid.Tests;

public class LiveChartDataTests
{
    static readonly DateTime T0 = new(2026, 1, 2, 3, 4, 5, 6);

    static Dictionary<string, double> S(params (string, double)[] kv) => kv.ToDictionary(x => x.Item1, x => x.Item2);

    static string Id(string s) => s;

    [Fact]
    public void Window_is_clamped_and_selects_last_samples()
    {
        var d = new LiveSeriesData { Window = 1 };
        Assert.Equal(LiveSeriesData.MinWindow, d.Window);
        d.Window = 10;
        for (int i = 0; i < 25; i++)
            d.AddSample(T0, S(("A", i)));
        Assert.Equal(25, d.Count);
        Assert.Equal(15, d.WindowStart);
        d.Window = 60;
        Assert.Equal(0, d.WindowStart);
    }

    [Fact]
    public void MinMax_and_last_track_session()
    {
        var d = new LiveSeriesData();
        foreach (double v in new[] { 5, -2, 9, 3 })
            d.AddSample(T0, S(("A", v)));
        var a = d.Series[0];
        Assert.Equal(-2, a.Min);
        Assert.Equal(9, a.Max);
        Assert.Equal(3, a.Last);
    }

    [Fact]
    public void New_series_is_backfilled_and_missing_values_are_gaps()
    {
        var d = new LiveSeriesData();
        d.AddSample(T0, S(("A", 1)));
        d.AddSample(T0, S(("A", 2), ("B", 7)));
        d.AddSample(T0, S(("B", 8)));
        var a = d.Series[0];
        var b = d.Series[1];
        Assert.Equal(3, a.Values.Count);
        Assert.Equal(3, b.Values.Count);
        Assert.True(double.IsNaN(b.Values[0]));
        Assert.True(double.IsNaN(a.Values[2]));
        Assert.True(double.IsNaN(a.Last));
        Assert.Equal(2, a.Max);   // el hueco no afecta a min/max
        Assert.Equal(7, b.Min);
    }

    [Fact]
    public void Non_finite_values_become_gaps()
    {
        var d = new LiveSeriesData();
        d.AddSample(T0, S(("A", double.PositiveInfinity)));
        Assert.True(double.IsNaN(d.Series[0].Last));
        Assert.True(double.IsNaN(d.Series[0].Max));
    }

    [Fact]
    public void Session_is_capped_dropping_oldest()
    {
        var d = new LiveSeriesData();
        for (int i = 0; i < LiveSeriesData.MaxSessionSamples + 5; i++)
            d.AddSample(T0, S(("A", i)));
        Assert.Equal(LiveSeriesData.MaxSessionSamples, d.Count);
        Assert.Equal(5, d.Series[0].Values[0]);
    }

    [Fact]
    public void Clear_resets_everything()
    {
        var d = new LiveSeriesData();
        d.AddSample(T0, S(("A", 1)));
        d.Clear();
        Assert.Equal(0, d.Count);
        Assert.Empty(d.Series);
        d.AddSample(T0, S(("B", 4)));
        Assert.Equal(4, d.Series[0].Min);
    }

    [Fact]
    public void Visibility_toggle_does_not_drop_data_from_csv()
    {
        var d = new LiveSeriesData();
        d.AddSample(T0, S(("A", 1)));
        d.Series[0].Visible = false;
        string csv = d.ToCsv(Id);
        Assert.Contains(",1\r\n", csv);
    }

    [Fact]
    public void Csv_has_header_invariant_numbers_and_empty_gap_cells()
    {
        var d = new LiveSeriesData();
        d.AddSample(T0, S(("A", 1.5)));
        d.AddSample(T0.AddSeconds(1), S(("B", -3)));
        string csv = d.ToCsv(Id);
        Assert.Equal("timestamp,A,B\r\n" +
                     "2026-01-02 03:04:05.006,1.5,\r\n" +
                     "2026-01-02 03:04:06.006,,-3\r\n", csv);
    }

    [Fact]
    public void Csv_applies_injected_escape_to_series_names_only()
    {
        var d = new LiveSeriesData();
        d.AddSample(T0, S(("=cmd", 1)));
        string csv = d.ToCsv(s => "<" + s + ">");
        Assert.StartsWith("timestamp,<=cmd>\r\n", csv);
        Assert.Contains(",1\r\n", csv);
    }

    [Fact]
    public void Csv_of_empty_session_is_only_header()
    {
        Assert.Equal("timestamp\r\n", new LiveSeriesData().ToCsv(Id));
    }
}

public class LiveJobTests
{
    static ChassisValue V(string job, string result, string args = "") => new(job, result, result.ToLowerInvariant(), "", args);

    [Fact]
    public void Results_of_the_same_job_are_polled_together()
    {
        var plan = LiveJob.Group([V("STATUS_A", "R1"), V("STATUS_B", "R2"), V(" status_a ", "R3")]);
        Assert.Equal(2, plan.Count);
        Assert.Equal("STATUS_A", plan[0].Job);
        Assert.Equal(["R1", "R3"], plan[0].Results);
        Assert.Equal(["R1", "R3"], plan[0].Series);
        Assert.Equal(["R2"], plan[1].Results);
    }

    [Fact]
    public void Different_args_are_separate_jobs()
    {
        var plan = LiveJob.Group([V("STATUS_A", "R1", "1"), V("STATUS_A", "R2", "2")]);
        Assert.Equal(2, plan.Count);
        Assert.Equal("1", plan[0].Args);
        Assert.Equal("2", plan[1].Args);
    }

    [Fact]
    public void Shared_result_names_are_prefixed_with_job()
    {
        var plan = LiveJob.Group([V("STATUS_A", "WERT"), V("STATUS_B", "wert"), V("STATUS_C", "WERT", "7"), V("STATUS_A", "OTRO")]);
        Assert.Equal(["STATUS_A/WERT", "OTRO"], plan[0].Series);
        Assert.Equal(["STATUS_B/wert"], plan[1].Series);
        Assert.Equal(["STATUS_C(7)/WERT"], plan[2].Series);
    }

    [Fact]
    public void Duplicate_values_are_requested_once()
    {
        var plan = LiveJob.Group([V("STATUS_A", "R1"), V("status_a", "r1")]);
        Assert.Single(plan);
        Assert.Single(plan[0].Results);
    }

    [Fact]
    public void SeriesOf_maps_requested_results_and_passes_all_without_list()
    {
        var job = new LiveJob("STATUS_A", "", ["R1", "R2"], ["S1", "S2"]);
        Assert.Equal("S2", job.SeriesOf("r2"));
        Assert.Null(job.SeriesOf("R3"));
        Assert.Equal("ANY", new LiveJob("STATUS_A", "", [], []).SeriesOf("ANY"));
    }
}
