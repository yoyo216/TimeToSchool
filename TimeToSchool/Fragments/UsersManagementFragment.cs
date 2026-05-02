using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Firebase.Firestore;
using Google.Android.Material.Chip;
using Google.Android.Material.TextField;
using System;
using System.Collections.Generic;
using System.Linq;
using TimeToSchool.Adapter;
using TimeToSchool.BusinessLogic;
using TimeToSchool.Model;
using TimeToSchool.Service;

namespace TimeToSchool.Fragments
{
    public class UsersManagementFragment : AndroidX.Fragment.App.Fragment
    {
        // RecyclerView
        private RecyclerView _recyclerView;
        private UsersRViewAdapter _adapter;

        // Count label
        private TextView _tvUserCount;

        // Search
        private TextInputEditText _etSearch;

        // Filter + sort chips
        private Chip _chipAll, _chipAdmin, _chipUser, _chipSort;

        // Data
        private List<User> _allUsers      = new List<User>();
        private List<User> _filteredUsers = new List<User>();
        private List<User> _displayedUsers = new List<User>();

        // Pagination
        private const int PageSize = 5;
        private int _currentPage = 0;
        private Android.Views.View _layoutPagination;
        private ImageButton _btnPrevPage;
        private ImageButton _btnNextPage;
        private TextView _tvPageIndicator;

        // Progress dialog
        private Dialog _progressDialog;

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.fragment_users_management, container, false);
            InitializeViews(view);
            return view;
        }

        private void InitializeViews(View view)
        {
            _tvUserCount  = view.FindViewById<TextView>(Resource.Id.tvUserCount);
            _etSearch     = view.FindViewById<TextInputEditText>(Resource.Id.etSearch);
            _chipAll      = view.FindViewById<Chip>(Resource.Id.chipAll);
            _chipAdmin    = view.FindViewById<Chip>(Resource.Id.chipAdmin);
            _chipUser     = view.FindViewById<Chip>(Resource.Id.chipUser);
            _chipSort     = view.FindViewById<Chip>(Resource.Id.chipSort);
            _recyclerView = view.FindViewById<RecyclerView>(Resource.Id.recyclerView);

            // Pagination bar
            _layoutPagination = view.FindViewById(Resource.Id.layoutPagination);
            _btnPrevPage      = view.FindViewById<ImageButton>(Resource.Id.btnPrevPage);
            _btnNextPage      = view.FindViewById<ImageButton>(Resource.Id.btnNextPage);
            _tvPageIndicator  = view.FindViewById<TextView>(Resource.Id.tvPageIndicator);
            _btnPrevPage.Click += (s, e) => GoToPage(_currentPage - 1);
            _btnNextPage.Click += (s, e) => GoToPage(_currentPage + 1);

            // Adapter
            _adapter = new UsersRViewAdapter(Activity, _displayedUsers);
            _adapter.ItemEditClick   += OnEditClick;
            _adapter.ItemDeleteClick += OnDeleteClick;
            _recyclerView.SetLayoutManager(new LinearLayoutManager(Activity));
            _recyclerView.SetAdapter(_adapter);

            // Search — filter on every keystroke
            _etSearch.TextChanged += (s, e) => ApplyFilters();

            // Role chips — only react when a chip becomes checked (avoids double-fire on deselect)
            _chipAll.CheckedChange   += (s, e) => { if (e.IsChecked) ApplyFilters(); };
            _chipAdmin.CheckedChange += (s, e) => { if (e.IsChecked) ApplyFilters(); };
            _chipUser.CheckedChange  += (s, e) => { if (e.IsChecked) ApplyFilters(); };

            // Sort chip — react on both check and uncheck
            _chipSort.CheckedChange += (s, e) => ApplyFilters();
        }

        public override void OnResume()
        {
            base.OnResume();

            if (ProManager.CurrentUser == null)
            {
                StartActivity(new Intent(Activity, typeof(SignInActivity)));
                return;
            }

            ShowProgressBar(true);
            FetchUsersFromDB();
        }

        public override void OnPause()
        {
            base.OnPause();
            UsersRepository.StopUsersListener();
        }

        private void FetchUsersFromDB()
        {
            UsersRepository.FetchUsersListener();
            UsersRepository.FirestoreEventListener.getEvent += (sender, args) =>
            {
                Activity?.RunOnUiThread(() =>
                {
                    ShowProgressBar(false);

                    if (args.Error != null)
                    {
                        Log.Debug(ProManager.TAG, $"FetchUsersFromDB listener error: {args.Error.Message}");
                        return;
                    }

                    _allUsers.Clear();

                    try
                    {
                        var snapshot = (QuerySnapshot)args.Result;
                        if (snapshot != null && !snapshot.IsEmpty)
                        {
                            foreach (DocumentSnapshot item in snapshot.Documents)
                            {
                                _allUsers.Add(new User
                                {
                                    Id         = item.Id,
                                    FirstName  = item.Get("FirstName")?.ToString(),
                                    LastName   = item.Get("LastName")?.ToString(),
                                    UserEmail  = item.Get("UserEmail")?.ToString(),
                                    UserMobile = item.Get("UserMobile")?.ToString(),
                                    UserPass   = item.Get("UserPassword")?.ToString(),
                                    IsAdmin    = bool.Parse(item.Get("IsAdmin")?.ToString() ?? "false"),
                                    ImageId    = Resource.Drawable.ic_icon_person
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ProManager.TAG, $"FetchUsersFromDB failed: {ex.Message}");
                    }

                    ApplyFilters();
                });
            };
        }

        private void ApplyFilters()
        {
            if (_allUsers == null) return;

            string query   = _etSearch?.Text?.ToLower() ?? string.Empty;
            bool showAdmin = _chipAdmin?.Checked == true;
            bool showUser  = _chipUser?.Checked  == true;
            bool sortAZ    = _chipSort?.Checked  == true;

            IEnumerable<User> filtered = _allUsers;

            if (!string.IsNullOrWhiteSpace(query))
                filtered = filtered.Where(u =>
                    (u.FirstName?.ToLower().Contains(query) == true) ||
                    (u.LastName?.ToLower().Contains(query)  == true) ||
                    (u.UserEmail?.ToLower().Contains(query) == true));

            if (showAdmin)      filtered = filtered.Where(u => u.IsAdmin);
            else if (showUser)  filtered = filtered.Where(u => !u.IsAdmin);

            if (sortAZ) filtered = filtered.OrderBy(u => u.FirstName);

            _filteredUsers = filtered.ToList();
            _currentPage   = 0;
            RefreshPage();
        }

        private void RefreshPage()
        {
            int totalPages = Math.Max(1, (int)Math.Ceiling(_filteredUsers.Count / (double)PageSize));
            _currentPage   = Math.Max(0, Math.Min(_currentPage, totalPages - 1));

            _displayedUsers = _filteredUsers
                .Skip(_currentPage * PageSize)
                .Take(PageSize)
                .ToList();

            _adapter.UpdateData(_displayedUsers);
            _recyclerView.ScrollToPosition(0);
            UpdateCountLabel();
            UpdatePaginationBar(totalPages);
        }

        private void GoToPage(int page)
        {
            _currentPage = page;
            RefreshPage();
        }

        private void UpdatePaginationBar(int totalPages)
        {
            if (totalPages <= 1)
            {
                _layoutPagination.Visibility = ViewStates.Gone;
                return;
            }

            _layoutPagination.Visibility = ViewStates.Visible;
            _btnPrevPage.Enabled          = _currentPage > 0;
            _btnNextPage.Enabled          = _currentPage < totalPages - 1;
            _btnPrevPage.Alpha            = _currentPage > 0             ? 1f : 0.3f;
            _btnNextPage.Alpha            = _currentPage < totalPages - 1 ? 1f : 0.3f;
            _tvPageIndicator.Text         = $"עמוד {_currentPage + 1} מתוך {totalPages}";
        }

        private void UpdateCountLabel()
        {
            if (_tvUserCount == null) return;
            int total    = _allUsers.Count;
            int filtered = _filteredUsers.Count;
            _tvUserCount.Text = filtered == total
                ? $"{total} Users"
                : $"Showing {filtered} of {total} Users";
        }

        private void OnEditClick(object sender, int position)
        {
            if (position < 0 || position >= _displayedUsers.Count) return;
            var user = _displayedUsers[position];

            if (user.Id == ProManager.CurrentUser?.Id)
            {
                Toast.MakeText(Activity, "אינך יכול לערוך את החשבון שלך", ToastLength.Short).Show();
                return;
            }

            var dialogView = LayoutInflater.From(Activity).Inflate(Resource.Layout.dialog_edit_user, null);
            var tvName    = dialogView.FindViewById<TextView>(Resource.Id.tvDialogUserName);
            var swIsAdmin = dialogView.FindViewById<Switch>(Resource.Id.switchIsAdmin);

            tvName.Text       = $"{user.FirstName} {user.LastName}";
            swIsAdmin.Checked = user.IsAdmin;

            new AlertDialog.Builder(Activity)
                .SetTitle("Edit User")
                .SetView(dialogView)
                .SetPositiveButton("Save", async (s, e) =>
                {
                    user.IsAdmin = swIsAdmin.Checked;
                    try { await UsersRepository.UpdateUser(user); }
                    catch (Exception ex)
                    {
                        Toast.MakeText(Activity, "Update failed: " + ex.Message, ToastLength.Short).Show();
                    }
                })
                .SetNegativeButton("Cancel", (s, e) => { })
                .Show();
        }

        private void OnDeleteClick(object sender, int position)
        {
            if (position < 0 || position >= _displayedUsers.Count) return;
            var user = _displayedUsers[position];

            if (user.Id == ProManager.CurrentUser?.Id)
            {
                Toast.MakeText(Activity, "אינך יכול למחוק את החשבון שלך", ToastLength.Short).Show();
                return;
            }

            new AlertDialog.Builder(Activity)
                .SetTitle("Delete User")
                .SetMessage($"Are you sure you want to delete {user.FirstName} {user.LastName}?")
                .SetPositiveButton("Delete", async (s, e) =>
                {
                    UsersRepository.StopUsersListener();
                    try
                    {
                        await UsersRepository.Delete(user);
                    }
                    catch (Exception ex)
                    {
                        Toast.MakeText(Activity, "Delete failed: " + ex.Message, ToastLength.Short).Show();
                    }
                    finally
                    {
                        FetchUsersFromDB();
                    }
                })
                .SetNegativeButton("Cancel", (s, e) => { })
                .Show();
        }

        private void ShowProgressBar(bool show)
        {
            if (show)
            {
                _progressDialog = new Dialog(Activity, Android.Resource.Style.ThemeNoTitleBar);
                var v = LayoutInflater.From(Activity).Inflate(Resource.Layout.fb_progressbar, null);
                _progressDialog.Window.SetBackgroundDrawable(new Android.Graphics.Drawables.ColorDrawable(Android.Graphics.Color.Transparent));
                _progressDialog.SetContentView(v);
                _progressDialog.SetCancelable(false);
                _progressDialog.Show();
            }
            else
            {
                _progressDialog?.Dismiss();
            }
        }
    }
}
