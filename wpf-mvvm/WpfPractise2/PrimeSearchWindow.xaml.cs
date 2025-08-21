using System.Windows;

namespace WpfPractise2
{
    public partial class PrimeSearchWindow : Window
    {
        public PrimeSearchWindow()
        {
            InitializeComponent();

            // WindowManager'a kaydet
            WindowManager.Instance.Register(this);
        }
    }
}
