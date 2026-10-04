using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace InpaDroid.Ui;

/// <summary>Adaptador sencillo para listas "distintivo + nombre + comentario", con filtro de texto.</summary>
internal sealed class TwoLineAdapter : BaseAdapter
{
    public sealed record Row(string Title, string Sub, string? Badge, Color BadgeColor, object Tag);

    readonly Activity _activity;
    readonly List<Row> _all;
    List<Row> _shown;

    public TwoLineAdapter(Activity activity, IEnumerable<Row> rows)
    {
        _activity = activity;
        _all = rows.ToList();
        _shown = _all;
    }

    public int TotalCount => _all.Count;

    public void Filter(string? text)
    {
        var t = text?.Trim() ?? "";
        _shown = t.Length == 0
            ? _all
            : _all.Where(r => r.Title.Contains(t, StringComparison.OrdinalIgnoreCase)
                              || r.Sub.Contains(t, StringComparison.OrdinalIgnoreCase)).ToList();
        NotifyDataSetChanged();
    }

    public Row GetRow(int position) => _shown[position];

    public override int Count => _shown.Count;

    public override Java.Lang.Object? GetItem(int position) => null;

    public override long GetItemId(int position) => position;

    public override View GetView(int position, View? convertView, ViewGroup? parent)
    {
        var view = convertView ?? _activity.LayoutInflater.Inflate(Resource.Layout.item_two_line, parent, false)!;
        var row = _shown[position];
        var badge = view.FindViewById<TextView>(Resource.Id.item_badge)!;
        var title = view.FindViewById<TextView>(Resource.Id.item_title)!;
        var sub = view.FindViewById<TextView>(Resource.Id.item_sub)!;
        title.Text = row.Title;
        sub.Text = row.Sub;
        sub.Visibility = row.Sub.Length > 0 ? ViewStates.Visible : ViewStates.Gone;
        if (row.Badge != null)
        {
            badge.Text = row.Badge;
            badge.SetBackgroundColor(row.BadgeColor);
            badge.Visibility = ViewStates.Visible;
        }
        else
        {
            badge.Visibility = ViewStates.Gone;
        }
        return view;
    }
}
