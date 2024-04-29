using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MSDL_GUI
{
    /// <summary>
    /// DownloadOptionWindow.xaml etkileşim mantığı
    /// </summary>
    public partial class DownloadOptionWindow : Window
    {
        public string SelectedOption { get; private set; }

        // 32 bit seçeneğinin etkin olup olmadığını kontrol eden özellik
        public bool Is32BitEnabled
        {
            get { return (bool)GetValue(Is32BitEnabledProperty); }
            set { SetValue(Is32BitEnabledProperty, value); }
        }

        // Is32BitEnabled için dependency property
        public static readonly DependencyProperty Is32BitEnabledProperty =
            DependencyProperty.Register("Is32BitEnabled", typeof(bool), typeof(DownloadOptionWindow), new PropertyMetadata(true));

        public DownloadOptionWindow()
        {
            InitializeComponent();
            // DataContext'i bu sınıfa ayarla, böylece XAML'den özelliklere erişilebilir
            DataContext = this;
        }

        private void X86Button_Click(object sender, RoutedEventArgs e)
        {
            SelectedOption = "x32";
            DialogResult = true;
        }

        private void X64Button_Click(object sender, RoutedEventArgs e)
        {
            SelectedOption = "x64";
            DialogResult = true;
        }
    }


}
