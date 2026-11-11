using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SolarSystem3D.App.Controllers;
using SolarSystem3D.App.Rendering;
using SolarSystem3D.App.ViewModels;

namespace SolarSystem3D.App;

/// <summary>Main window: wires the view model to the 3D scene controller. Contains only view glue.</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly SceneController _controller;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _controller = new SceneController(Viewport, new LabelOverlay(Overlay), _vm);
        Loaded += async (_, _) =>
        {
            try
            {
                await _controller.LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load the catalogs:\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            LoadingText.Visibility = Visibility.Collapsed;
        };
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox or DatePicker or ComboBox) return;
        switch (e.Key)
        {
            case Key.Space:
                _vm.TogglePlayCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Home:
                _vm.ResetViewCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F:
                _vm.CenterOnSelectedCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnTimeBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TimeBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            Viewport.Focus();
        }
    }

    /// <summary>Pause the clock while the user edits the date or time so the text does not change under them.</summary>
    private void OnEditTimeFocus(object sender, KeyboardFocusChangedEventArgs e) => _vm.Clock.IsRunning = false;

    private void OnSearchDoubleClick(object sender, MouseButtonEventArgs e) => _vm.CenterOnSelectedCommand.Execute(null);
}
