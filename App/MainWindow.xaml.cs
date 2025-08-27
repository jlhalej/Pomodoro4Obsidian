using System.Reflection;
using System.Windows;

namespace PomodoroForObsidian
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            SetVersionDisplay();
        }

        private void SetVersionDisplay()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionLabel.Text = $"v{version?.Major}.{version?.Minor}.{version?.Build}";
        }
    }
}
