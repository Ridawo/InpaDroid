using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Widget;
using InpaDroid.Diag;

namespace InpaDroid.Ui;

/// <summary>Selección de centralita: .grp (grupos) arriba con distintivo, luego .prg; con buscador.</summary>
[Activity(Label = "Selección de centralita", Theme = "@style/InpaTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden)]
public class EcuSelectActivity : InpaActivity
{
    public const string ExtraEcuName = "ecu_name";
    public const string ExtraIsGroup = "ecu_is_group";

    static readonly Color GroupBadge = Color.ParseColor("#1C69D4");
    static readonly Color PrgBadge = Color.ParseColor("#808080");

    EditText _search = null!;
    TextView _count = null!;
    ListView _list = null!;
    TwoLineAdapter? _adapter;
    bool _loading;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        InitInpa(Resource.Layout.activity_ecu_select, "Selección de centralita");
        _search = FindViewById<EditText>(Resource.Id.ecu_search)!;
        _count = FindViewById<TextView>(Resource.Id.ecu_count)!;
        _list = FindViewById<ListView>(Resource.Id.ecu_list)!;

        _search.TextChanged += (_, _) => _adapter?.Filter(_search.Text);
        _list.ItemClick += (_, e) =>
        {
            if (_adapter?.GetRow(e.Position).Tag is EcuFile ecu)
                OpenEcu(ecu);
        };

        SetKey(5, "Actualizar", Load);
        SetKey(9, "Ajustes", () => StartActivity(new Intent(this, typeof(SettingsActivity))));
        SetKey(10, "Volver", Finish);
        RefreshKeys();
    }

    protected override void OnResume()
    {
        base.OnResume();
        // Se recarga siempre: puede volver de Ajustes con otra carpeta.
        Load();
    }

    async void Load()
    {
        if (_loading)
            return;
        _loading = true;
        string path = "";
        try
        {
            path = DiagHolder.LoadSettings(this).EcuPath;
            SetSubtitle(path);
            _count.SetTextColor(Color.Black);
            _count.Text = "Leyendo carpeta…";
            var svc = Diag;
            var files = await Task.Run(() => svc.ScanEcuFolder());

            _adapter = new TwoLineAdapter(this, files.Select(f => new TwoLineAdapter.Row(
                f.Name,
                f.IsGroup ? "Grupo: identifica la variante automáticamente" : System.IO.Path.GetFileName(f.FullPath),
                f.IsGroup ? "GRP" : "PRG",
                f.IsGroup ? GroupBadge : PrgBadge,
                f)));
            _list.Adapter = _adapter;
            _adapter.Filter(_search.Text);

            int groups = files.Count(f => f.IsGroup);
            if (files.Count == 0)
            {
                _count.SetTextColor(UiUtil.ErrorText);
                _count.Text = $"No hay archivos .prg/.grp en:\n{path}\nCópialos ahí o cambia la carpeta en Ajustes (F9).";
            }
            else
            {
                _count.Text = $"{groups} grupos (.grp), {files.Count - groups} programas (.prg)";
            }
        }
        catch (Exception ex)
        {
            _count.SetTextColor(UiUtil.ErrorText);
            _count.Text = $"Error leyendo {path}: {UiUtil.Describe(ex)}";
        }
        finally
        {
            _loading = false;
        }
    }

    void OpenEcu(EcuFile ecu)
    {
        var intent = new Intent(this, typeof(EcuActivity));
        intent.PutExtra(ExtraEcuName, ecu.Name);
        intent.PutExtra(ExtraIsGroup, ecu.IsGroup);
        StartActivity(intent);
    }
}
