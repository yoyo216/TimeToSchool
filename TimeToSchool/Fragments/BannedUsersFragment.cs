using Android.App;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Firebase.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using TimeToSchool.Adapter;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Model;
using TimeToSchool.Service;
using static TimeToSchool.Service.FireBaseHelper;

namespace TimeToSchool.Fragments
{
    public class BannedUsersFragment : AndroidX.Fragment.App.Fragment
    {
        private RecyclerView _recyclerView;
        private BannedEmailsRViewAdapter _adapter;
        private TextView _tvCount;
        private TextView _tvEmpty;

        private List<BannedEmail> _items = new List<BannedEmail>();

        private IListenerRegistration _registration;
        private FirestoreEventListener _listener;

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.fragment_banned_users, container, false);

            _tvCount     = view.FindViewById<TextView>(Resource.Id.tvBannedCount);
            _tvEmpty     = view.FindViewById<TextView>(Resource.Id.tvBannedEmpty);
            _recyclerView = view.FindViewById<RecyclerView>(Resource.Id.recyclerBanned);

            _adapter = new BannedEmailsRViewAdapter(Activity, _items);
            _adapter.ItemUnbanClick += OnUnbanClick;

            _recyclerView.SetLayoutManager(new LinearLayoutManager(Activity));
            _recyclerView.SetAdapter(_adapter);

            return view;
        }

        public override void OnResume()
        {
            base.OnResume();
            StartListening();
        }

        public override void OnPause()
        {
            base.OnPause();
            StopListening();
        }

        private void StartListening()
        {
            _listener = new FirestoreEventListener();
            _listener.getEvent += OnSnapshot;
            _registration = FirebaseFirestore.Instance
                .Collection("bannedEmails")
                .AddSnapshotListener(_listener);
        }

        private void StopListening()
        {
            _registration?.Remove();
            _registration = null;
            if (_listener != null) _listener.getEvent -= OnSnapshot;
            _listener = null;
        }

        private void OnSnapshot(object sender, FirestoreEventListener.TaskListenerEventArgs args)
        {
            Activity?.RunOnUiThread(() =>
            {
                if (args.Error != null)
                {
                    Log.Debug(ProManager.TAG, $"BannedUsers listener error: {args.Error.Message}");
                    return;
                }

                _items.Clear();
                try
                {
                    var snapshot = (QuerySnapshot)args.Result;
                    if (snapshot != null && !snapshot.IsEmpty)
                    {
                        foreach (DocumentSnapshot item in snapshot.Documents)
                        {
                            DateTime bannedAt = default;
                            string raw = item.Get("BannedAt")?.ToString();
                            if (!string.IsNullOrWhiteSpace(raw))
                                DateTime.TryParse(raw, null,
                                    System.Globalization.DateTimeStyles.RoundtripKind, out bannedAt);

                            _items.Add(new BannedEmail
                            {
                                Email = item.Get("Email")?.ToString() ?? item.Id,
                                BannedAt = bannedAt,
                                BannedByEmail = item.Get("BannedByEmail")?.ToString()
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ProManager.TAG, $"BannedUsers parse failed: {ex.Message}");
                }

                _adapter.UpdateData(_items.ToList());
                UpdateCountAndEmpty();
            });
        }

        private void UpdateCountAndEmpty()
        {
            _tvCount.Text = $"{_items.Count} Banned";
            _tvEmpty.Visibility  = _items.Count == 0 ? ViewStates.Visible : ViewStates.Gone;
            _recyclerView.Visibility = _items.Count == 0 ? ViewStates.Gone : ViewStates.Visible;
        }

        private void OnUnbanClick(object sender, int position)
        {
            if (position < 0 || position >= _items.Count) return;
            var entry = _items[position];

            new AlertDialog.Builder(Activity)
                .SetTitle("Unban Email")
                .SetMessage($"Unban {entry.Email}? They will be able to sign up again.")
                .SetPositiveButton("Unban", async (s, e) =>
                {
                    try
                    {
                        await BannedEmailsRepository.RemoveBannedEmail(entry.Email);
                    }
                    catch (Exception ex)
                    {
                        Toast.MakeText(Activity, "Unban failed: " + ex.Message, ToastLength.Short).Show();
                    }
                })
                .SetNegativeButton("Cancel", (s, e) => { })
                .Show();
        }
    }
}
