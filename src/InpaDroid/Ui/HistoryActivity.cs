using System.Text;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using InpaDroid.Diag;

namespace InpaDroid.Ui;

/// <summary>Historial de lecturas de errores: lista de sesiones, detalle y comparación con la lectura anterior de la misma centralita.</summary>
[Activity(Label = "@string/hs_title", Theme = "@style/InpaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden
                           | ConfigChanges.UiMode | ConfigChanges.Locale)]
public class HistoryActivity : InpaActivity
{
    TextView _empty = null!, _detail = null!;
    ListView _list = null!;
    View _detailScroll = null!;
    List<SessionRecord> _sessions = [];
    int _selected = -1;   // índice en _sessions de la sesión abierta; -1 = se ve la lista
    int _loadGen;         // descarta lecturas sustituidas por otra carga o por el borrado

    public static void Start(Context ctx) => ctx.StartActivity(new Intent(ctx, typeof(HistoryActivity)));

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        InitInpa(Resource.Layout.history_activity, GetString(Resource.String.hs_title));
        _empty = FindViewById<TextView>(Resource.Id.history_empty)!;
        _list = FindViewById<ListView>(Resource.Id.history_list)!;
        _detail = FindViewById<TextView>(Resource.Id.history_detail)!;
        _detailScroll = FindViewById(Resource.Id.history_detail_scroll)!;
        _list.ItemClick += (_, e) => ShowDetail(e.Position);
        SetShiftKey(4, GetString(Resource.String.hs_key_delete), ConfirmDeleteAll, Resource.Drawable.ic_delete);
        ShowList();
    }

    protected override void OnResume()
    {
        base.OnResume();
        // Con un detalle abierto no se recarga: los índices de _sessions podrían cambiar y se perdería la vista.
        if (_selected < 0)
            Load();
    }

    async void Load()
    {
        int gen = ++_loadGen;
        var ctx = ApplicationContext!;
        var sessions = await Task.Run(() => HistoryStore.Load(ctx));
        // Borrado o detalle abierto mientras se leía: cambiar _sessions ahora devolvería lo borrado o movería los índices.
        if (gen != _loadGen || _selected >= 0 || IsDestroyed)
            return;
        _sessions = sessions;
        var kindRead = GetString(Resource.String.hs_kind_read);
        var kindBackup = GetString(Resource.String.hs_kind_backup);
        var readColor = UiUtil.Res(this, Resource.Color.inpa_badge_prg);
        var backupColor = UiUtil.Res(this, Resource.Color.inpa_badge_grp);
        _list.Adapter = new TwoLineAdapter(this, _sessions.Select(s => new TwoLineAdapter.Row(
            Title(s),
            UiUtil.Str(this, Resource.String.hs_row_sub, FormatWhen(s), s.Faults.Count),
            s.Kind == SessionRecord.KindBackup ? kindBackup : kindRead,
            s.Kind == SessionRecord.KindBackup ? backupColor : readColor,
            s)));
        _empty.Text = GetString(Resource.String.hs_empty);
        _empty.Visibility = _sessions.Count == 0 && _selected < 0 ? ViewStates.Visible : ViewStates.Gone;
    }

    static string Title(SessionRecord s) => s.EcuTitle.Length > 0 ? s.EcuTitle : s.Sgbd;

    static string FormatWhen(SessionRecord s) => s.WhenUtc?.ToLocalTime().ToString("g") ?? s.When;

    void ShowList()
    {
        _selected = -1;
        _detailScroll.Visibility = ViewStates.Gone;
        _list.Visibility = ViewStates.Visible;
        _empty.Visibility = _sessions.Count == 0 ? ViewStates.Visible : ViewStates.Gone;
        SetSubtitle("");
        SetKey(5, GetString(Resource.String.g_key_refresh), Load, Resource.Drawable.ic_refresh);
        SetKey(10, GetString(Resource.String.g_key_back), Finish);
        RefreshKeys();
    }

    void ShowDetail(int index)
    {
        if (index < 0 || index >= _sessions.Count)
            return;
        _selected = index;
        var s = _sessions[index];
        var sb = new StringBuilder();
        sb.Append(UiUtil.Str(this, Resource.String.hs_detail_head, Title(s), FormatWhen(s),
            s.Kind == SessionRecord.KindBackup ? GetString(Resource.String.hs_kind_backup) : GetString(Resource.String.hs_kind_read)));
        sb.Append("\n\n");
        if (s.Faults.Count == 0)
            sb.Append(GetString(Resource.String.hs_no_faults));
        else
            AppendFaults(sb, s.Faults);
        _detail.Text = sb.ToString();
        _list.Visibility = ViewStates.Gone;
        _empty.Visibility = ViewStates.Gone;
        _detailScroll.Visibility = ViewStates.Visible;
        SetKey(5, GetString(Resource.String.hs_key_compare), Compare, Resource.Drawable.ic_list);
        // Al volver se recarga: una carga que terminó con el detalle abierto se descartó.
        SetKey(10, GetString(Resource.String.g_key_back), () => { ShowList(); Load(); });
        RefreshKeys();
    }

    void AppendFaults(StringBuilder sb, IEnumerable<FaultEntry> faults)
    {
        foreach (var f in faults)
        {
            var state = f.State switch
            {
                nameof(FaultState.Active) => Resource.String.hs_state_active,
                nameof(FaultState.History) => Resource.String.hs_state_history,
                _ => Resource.String.hs_state_unknown,
            };
            sb.Append(UiUtil.Str(this, Resource.String.hs_fault_line, f.Code, f.Description))
                .Append(' ').Append(UiUtil.Str(this, Resource.String.hs_fault_state, GetString(state))).Append('\n');
        }
    }

    void Compare()
    {
        if (_selected < 0)
            return;
        var current = _sessions[_selected];
        // Lista en orden descendente: la anterior es la primera posterior con la misma centralita.
        var previous = _sessions.Skip(_selected + 1).FirstOrDefault(
            s => string.Equals(s.Sgbd, current.Sgbd, StringComparison.OrdinalIgnoreCase));
        if (previous == null)
        {
            UiUtil.Toast(this, GetString(Resource.String.hs_no_previous));
            return;
        }
        var c = SessionRecord.Compare(previous, current);
        var sb = new StringBuilder(_detail.Text).Append("\n\n")
            .Append(UiUtil.Str(this, Resource.String.hs_compare_head, FormatWhen(previous))).Append("\n\n");
        Section(sb, Resource.String.hs_cmp_new, c.New);
        Section(sb, Resource.String.hs_cmp_fixed, c.Fixed);
        Section(sb, Resource.String.hs_cmp_persistent, c.Persistent);
        _detail.Text = sb.ToString();
        // Un segundo toque no debe duplicar la comparación: se vuelve a dibujar solo el detalle.
        SetKey(5, GetString(Resource.String.hs_key_compare), () => ShowDetail(_selected), Resource.Drawable.ic_list);
        RefreshKeys();
    }

    void Section(StringBuilder sb, int titleRes, IReadOnlyList<FaultEntry> faults)
    {
        sb.Append(UiUtil.Str(this, titleRes, faults.Count)).Append('\n');
        if (faults.Count == 0)
            sb.Append(GetString(Resource.String.hs_cmp_none)).Append('\n');
        else
            AppendFaults(sb, faults);
        sb.Append('\n');
    }

    void ConfirmDeleteAll()
    {
        JobDialogs.Confirm(this, GetString(Resource.String.hs_delete_title), GetString(Resource.String.hs_delete_message),
            GetString(Resource.String.hs_delete_yes), () =>
            {
                HistoryStore.DeleteAll(this);
                _loadGen++;
                _sessions = [];
                _list.Adapter = null;
                ShowList();
            });
    }
}
