using Avalonia.Controls;
using Centriku.ViewModels;

namespace Centriku.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
        }
        protected override async void OnDataContextChanged(System.EventArgs e)
        {
            base.OnDataContextChanged(e);
            // When the view loads and connects to the ViewModel, fetch the live data!
            if (DataContext is DashboardViewModel vm)
            {
                await vm.LoadDashboardDataAsync();
            }
        }
    }
}