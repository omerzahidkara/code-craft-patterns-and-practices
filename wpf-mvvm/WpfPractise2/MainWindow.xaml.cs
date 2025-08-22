using System.Windows;
using System.Windows.Threading;

namespace WpfPractise2
{
    public partial class MainWindow : Window
    {
        private bool _searchOpenedBefore = false;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            // önce bayrağı ayarla, sonra Register
            WindowManager.Instance.FocusModeEnabled = false;
            WindowManager.Instance.Register(this);
        }


        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void OpenSearch_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;

            var win = new PrimeSearchWindow
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowInTaskbar = true
            };
            win.DataContext = new PrimeSearchViewModel(vm);

            WindowManager.Instance.Register(win);

            win.Closed += (_, __) => { _searchOpenedBefore = false; };

            win.Show();
            win.Activate();
            win.Topmost = true; win.Topmost = false;

            // Sadece Odak Modu açıksa ve ilk kez ise ana pencereyi indir
            if (WindowManager.Instance.FocusModeEnabled && !_searchOpenedBefore)
            {
                _searchOpenedBefore = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    this.WindowState = WindowState.Minimized;
                }), DispatcherPriority.ApplicationIdle);
            }
        }

    }
}
