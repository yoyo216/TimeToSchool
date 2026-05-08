using Android.OS;
using Android.Views;
using Android.Widget;
using Google.Android.Material.BottomSheet;
using Google.Android.Material.SwitchMaterial;
using TimeToSchool.Model;
using TimeToSchool.Service;

namespace TimeToSchool.Fragments
{
    public class BusInfoBottomSheet : BottomSheetDialogFragment
    {
        private ActiveBus _bus;

        public static BusInfoBottomSheet NewInstance(ActiveBus bus)
        {
            return new BusInfoBottomSheet { _bus = bus };
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.fragment_bus_info_sheet, container, false);

            view.FindViewById<TextView>(Resource.Id.tv_bus_line).Text = _bus.BusLine;
            view.FindViewById<TextView>(Resource.Id.tv_driver_name).Text = _bus.DriverName;
            view.FindViewById<TextView>(Resource.Id.tv_school).Text = _bus.SchoolName;
            view.FindViewById<TextView>(Resource.Id.tv_town).Text = _bus.Town;
            view.FindViewById<TextView>(Resource.Id.tv_status).Text =
                _bus.Status == "Active" ? "פעיל" : "לא פעיל";
            view.FindViewById<TextView>(Resource.Id.tv_last_updated).Text =
                $"עודכן: {_bus.LastUpdatedTime}";

            var sw = view.FindViewById<SwitchMaterial>(Resource.Id.switch_visible);
            sw.Checked = _bus.IsVisible;
            sw.CheckedChange += async (s, e) =>
                await BusesRepository.UpdateBusVisibility(_bus.FirestoreDocId, e.IsChecked);

            return view;
        }
    }
}
