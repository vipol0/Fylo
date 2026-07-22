using System.Windows;

namespace Fylo.Dialogs
{
    public partial class ConfirmDeleteDialog : Window
    {
        public bool Confirmed { get; private set; }

        public ConfirmDeleteDialog(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
            Owner = Application.Current.MainWindow;
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }
    }
}
