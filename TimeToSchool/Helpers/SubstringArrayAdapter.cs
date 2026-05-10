using Android.Content;
using Android.Widget;
using Java.Lang;
using System.Collections.Generic;
using System.Linq;

namespace TimeToSchool.Helpers
{
    public class SubstringArrayAdapter : ArrayAdapter<string>
    {
        private readonly List<string> _original;
        private readonly SubstringFilter _filter;

        public SubstringArrayAdapter(Context context, int resource, string[] items)
            : base(context, resource, items.ToList())
        {
            _original = items.ToList();
            _filter = new SubstringFilter(this);
        }

        public override Filter Filter => _filter;

        private class SubstringFilter : Filter
        {
            private readonly SubstringArrayAdapter _adapter;
            public SubstringFilter(SubstringArrayAdapter adapter) => _adapter = adapter;

            protected override FilterResults PerformFiltering(ICharSequence constraint)
            {
                var query = constraint?.ToString().ToLower() ?? "";
                var count = string.IsNullOrEmpty(query)
                    ? _adapter._original.Count
                    : _adapter._original.Count(s => s.ToLower().Contains(query));
                return new FilterResults { Count = count };
            }

            protected override void PublishResults(ICharSequence constraint, FilterResults results)
            {
                var query = constraint?.ToString().ToLower() ?? "";
                var toShow = string.IsNullOrEmpty(query)
                    ? _adapter._original
                    : _adapter._original.Where(s => s.ToLower().Contains(query)).ToList();
                _adapter.SetNotifyOnChange(false);
                _adapter.Clear();
                foreach (var s in toShow)
                    _adapter.Add(s);
                _adapter.NotifyDataSetChanged();
            }
        }
    }
}
