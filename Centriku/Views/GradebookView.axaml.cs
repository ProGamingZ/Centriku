using Avalonia.Controls;
using Avalonia.Controls.Notifications; 
using Centriku.ViewModels;

namespace Centriku.Views
{
    public partial class GradebookView : UserControl
    {
        private WindowNotificationManager? _notificationManager;

        public GradebookView()
        {
            InitializeComponent();
        }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                _notificationManager = new WindowNotificationManager(topLevel) { MaxItems = 3 };
            }
        }

        protected override void OnDataContextChanged(System.EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is GradebookViewModel vm)
            {
                vm.ShowToastMessage = (msg) => 
                {
                    _notificationManager?.Show(new Notification("Notification", msg, NotificationType.Warning));
                };
            }
        }
    }
}