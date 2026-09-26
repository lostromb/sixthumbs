using System.Windows;

namespace Sixthumbs.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel();
        DataContext = vm;
        Closed += (_, _) => vm.Dispose();
    }
}
