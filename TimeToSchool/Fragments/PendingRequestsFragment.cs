using Android.App;
using Android.Content;
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
    public class PendingRequestsFragment : AndroidX.Fragment.App.Fragment
    {
        private RecyclerView _recyclerView;
        private PendingRequestsRViewAdapter _adapter;
        private TextView _tvCount;
        private TextView _tvEmpty;

        private List<User> _pendingUsers = new List<User>();

        // Own listener — separate from UsersRepository's static one so the two fragments don't clobber each other
        private IListenerRegistration _registration;
        private FirestoreEventListener _listener;

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.fragment_pending_requests, container, false);

            _tvCount     = view.FindViewById<TextView>(Resource.Id.tvPendingCount);
            _tvEmpty     = view.FindViewById<TextView>(Resource.Id.tvPendingEmpty);
            _recyclerView = view.FindViewById<RecyclerView>(Resource.Id.recyclerPending);

            _adapter = new PendingRequestsRViewAdapter(Activity, _pendingUsers);
            _adapter.ItemApproveClick += OnApproveClick;
            _adapter.ItemDeleteClick  += OnDeleteClick;
            _adapter.ItemBanClick     += OnBanClick;

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
                .Collection("users")
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
                    Log.Debug(ProManager.TAG, $"PendingRequests listener error: {args.Error.Message}");
                    return;
                }

                _pendingUsers.Clear();
                try
                {
                    var snapshot = (QuerySnapshot)args.Result;
                    if (snapshot != null && !snapshot.IsEmpty)
                    {
                        foreach (DocumentSnapshot item in snapshot.Documents)
                        {
                            string status = item.Get("Status")?.ToString() ?? "approved";
                            if (status != "pending") continue;

                            _pendingUsers.Add(new User
                            {
                                Id         = item.Id,
                                FirstName  = item.Get("FirstName")?.ToString(),
                                LastName   = item.Get("LastName")?.ToString(),
                                UserEmail  = item.Get("UserEmail")?.ToString(),
                                UserMobile = item.Get("UserMobile")?.ToString(),
                                UserPass   = item.Get("UserPassword")?.ToString(),
                                IsAdmin    = bool.Parse(item.Get("IsAdmin")?.ToString() ?? "false"),
                                Status     = status
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ProManager.TAG, $"PendingRequests parse failed: {ex.Message}");
                }

                _adapter.UpdateData(_pendingUsers.ToList());
                UpdateCountAndEmpty();
            });
        }

        private void UpdateCountAndEmpty()
        {
            _tvCount.Text = $"{_pendingUsers.Count} Pending";
            _tvEmpty.Visibility  = _pendingUsers.Count == 0 ? ViewStates.Visible : ViewStates.Gone;
            _recyclerView.Visibility = _pendingUsers.Count == 0 ? ViewStates.Gone : ViewStates.Visible;
        }

        private void OnApproveClick(object sender, int position)
        {
            if (position < 0 || position >= _pendingUsers.Count) return;
            var user = _pendingUsers[position];

            new AlertDialog.Builder(Activity)
                .SetTitle("Approve User")
                .SetMessage($"Approve {user.FirstName} {user.LastName}?")
                .SetPositiveButton("Approve", async (s, e) =>
                {
                    try
                    {
                        await UsersRepository.UpdateUserStatus(user.Id, "approved");
                    }
                    catch (Exception ex)
                    {
                        Toast.MakeText(Activity, "Approve failed: " + ex.Message, ToastLength.Short).Show();
                    }
                })
                .SetNegativeButton("Cancel", (s, e) => { })
                .Show();
        }

        private void OnDeleteClick(object sender, int position)
        {
            if (position < 0 || position >= _pendingUsers.Count) return;
            var user = _pendingUsers[position];

            new AlertDialog.Builder(Activity)
                .SetTitle("Delete Request")
                .SetMessage($"Delete the pending request for {user.UserEmail}? They can sign up again.")
                .SetPositiveButton("Delete", async (s, e) =>
                {
                    try
                    {
                        await UsersRepository.Delete(user);
                    }
                    catch (Exception ex)
                    {
                        Toast.MakeText(Activity, "Delete failed: " + ex.Message, ToastLength.Short).Show();
                    }
                })
                .SetNegativeButton("Cancel", (s, e) => { })
                .Show();
        }

        private void OnBanClick(object sender, int position)
        {
            if (position < 0 || position >= _pendingUsers.Count) return;
            var user = _pendingUsers[position];

            new AlertDialog.Builder(Activity)
                .SetTitle("Ban Email")
                .SetMessage($"Ban {user.UserEmail}? They will not be able to sign up again unless an admin unbans the email.")
                .SetPositiveButton("Ban", async (s, e) =>
                {
                    try
                    {
                        await UsersRepository.BanUser(user, ProManager.CurrentUser?.UserEmail ?? string.Empty);
                    }
                    catch (Exception ex)
                    {
                        Toast.MakeText(Activity, "Ban failed: " + ex.Message, ToastLength.Short).Show();
                    }
                })
                .SetNegativeButton("Cancel", (s, e) => { })
                .Show();
        }
    }
}
