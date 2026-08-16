using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Centriku.ViewModels;

namespace Centriku.Views.Gradebook
{
    public partial class RecitationTabView : UserControl
    {
        private GradebookViewModel? _vm;
        private RotateTransform _wheelTransform;
        private double _currentAngle = 0;

        public RecitationTabView()
        {
            InitializeComponent();
            var canvas = this.FindControl<Canvas>("WheelCanvas");
            _wheelTransform = canvas?.RenderTransform as RotateTransform ?? new RotateTransform();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            
            // Clean up old subscriptions to prevent memory leaks
            if (_vm != null)
            {
                _vm.OnSpinWheelRequested -= OnSpinWheelRequested;
                
                // Add these two lines to hook up the reset!
                _vm.OnWheelResetRequested -= OnWheelResetRequested;
                if (_vm.RemainingStudents != null) _vm.RemainingStudents.CollectionChanged -= RemainingStudents_CollectionChanged;
            }

            _vm = DataContext as GradebookViewModel;

            if (_vm != null)
            {
                _vm.OnSpinWheelRequested += OnSpinWheelRequested;
                _vm.OnWheelResetRequested += OnWheelResetRequested;
                
                if (_vm.RemainingStudents != null) _vm.RemainingStudents.CollectionChanged += RemainingStudents_CollectionChanged;

                // NOTE: this can fire before the control is attached to the visual tree
                // (e.g. the first time a TabItem's content is realized). Theme-dictionary
                // resources (App.axaml) aren't reachable via FindResource/TryFindResource
                // until the logical-tree parent chain is fully wired up, so only draw here
                // if we're already attached; otherwise OnAttachedToVisualTree will do it.
                if (this.IsAttachedToVisualTree())
                {
                    DrawWheel();
                }
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            // Ensures the wheel (re)draws with correct theme colors once resource
            // resolution can actually reach App.axaml's ThemeDictionaries.
            if (_vm != null)
            {
                DrawWheel();
            }
        }

        // Instantly redraw the wheel if a student is chosen or reset
        private void RemainingStudents_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(DrawWheel);
        }

        private void DrawWheel()
        {
            var canvas = this.FindControl<Canvas>("WheelCanvas");
            if (canvas == null || _vm == null) return;

            canvas.Children.Clear();
            int count = _vm.RemainingStudents.Count;

            if (count == 0)
            {
                var circle = new Ellipse { Width = 400, Height = 400 };
                circle[!Ellipse.FillProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("SurfaceVariantBrush");
                canvas.Children.Add(circle);
                return;
            }
            
            if (count == 1)
            {
                var circle = new Ellipse { Width = 400, Height = 400 };
                circle[!Ellipse.FillProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("DataGridAltRowBrush");
                canvas.Children.Add(circle);
                
                var text = new TextBlock { Text = _vm.RemainingStudents[0].FullName, FontWeight = FontWeight.Bold, FontSize = 24, TextAlignment = TextAlignment.Center, Width = 300 };
                text[!TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextMainBrush");
                Canvas.SetLeft(text, 50); Canvas.SetTop(text, 180);
                canvas.Children.Add(text);
                return;
            }

            double anglePerSlice = 360.0 / count;
            double radius = 200;
            var center = new Point(radius, radius);

            for (int i = 0; i < count; i++)
            {
                double startAngle = i * anglePerSlice;
                double endAngle = (i + 1) * anglePerSlice;

                var path = new Path { StrokeThickness = 1 };
                
                // Directly bind the fill to our XAML Theme resources!
                string brushName = (i % 2 == 0) ? "SurfaceVariantBrush" : "DataGridAltRowBrush";
                path[!Path.FillProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(brushName);
                path[!Path.StrokeProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("BorderBrush");

                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(center, true);
                    double startRad = (startAngle - 90) * Math.PI / 180.0;
                    double endRad = (endAngle - 90) * Math.PI / 180.0;

                    ctx.LineTo(new Point(center.X + radius * Math.Cos(startRad), center.Y + radius * Math.Sin(startRad)));
                    ctx.ArcTo(new Point(center.X + radius * Math.Cos(endRad), center.Y + radius * Math.Sin(endRad)), new Size(radius, radius), 0, anglePerSlice > 180, SweepDirection.Clockwise);
                }
                path.Data = geometry;
                canvas.Children.Add(path);

                double textAngle = startAngle + (anglePerSlice / 2);
                double textRad = (textAngle - 90) * Math.PI / 180.0;
                double textRadius = radius * 0.55; 

                var tb = new TextBlock 
                { 
                    Text = _vm.RemainingStudents[i].StudentInfo.FirstName, 
                    FontWeight = FontWeight.Bold, 
                    FontSize = count > 15 ? 10 : 14, 
                    TextAlignment = TextAlignment.Right, 
                    Width = 140 
                };
                
                // Bind the text color dynamically
                tb[!TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("TextMainBrush");
                tb.RenderTransformOrigin = new RelativePoint(1.0, 0.5, RelativeUnit.Relative);
                tb.RenderTransform = new RotateTransform(textAngle - 90); 
                
                Canvas.SetLeft(tb, (center.X + textRadius * Math.Cos(textRad)) - 140); 
                Canvas.SetTop(tb, (center.Y + textRadius * Math.Sin(textRad)) - 10);
                canvas.Children.Add(tb);
            }
        }

        private async void OnSpinWheelRequested(int winnerIndex)
        {
            if (_vm == null || _vm.RemainingStudents.Count == 0) return;

            int count = _vm.RemainingStudents.Count;
            double anglePerSlice = 360.0 / count;

            // Math to force the chosen slice to perfectly align with the Right Pointer (Angle 90)
            double targetAngle = 450 - (winnerIndex * anglePerSlice + anglePerSlice / 2);

            // Add 3600 degrees (10 full rapid spins!) 
            _currentAngle += 3600; 
            double baseAngle = _currentAngle - (_currentAngle % 360);
            _currentAngle = baseAngle + targetAngle;

            _wheelTransform.Angle = _currentAngle;

            await Task.Delay(3100);
            await _vm.WheelAnimationCompletedAsync(winnerIndex);
        }

        private void OnWheelResetRequested()
        {
            // Reset our tracking math
            _currentAngle = 0;
            
            // If the wheel transform exists, reset it to 0.
            // Because of the 3-second DoubleTransition in the XAML, 
            // this will create a satisfying "rewind" animation back to the start!
            if (_wheelTransform != null)
            {
                _wheelTransform.Angle = 0;
            }

            Dispatcher.UIThread.Post(DrawWheel);
        }
    }
}