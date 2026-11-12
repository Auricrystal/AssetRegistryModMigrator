using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace AssetRegistryModMigrator.Windows
{
    public partial class DarkMessageBox :Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        private DarkMessageBox(string message, string title, MessageBoxButton buttons, MessageBoxImage icon)
        {
            InitializeComponent();

            Title = title;
            MessageText.Text = message;

            // Attach to active or main window so CenterOwner works reliably
            var activeWindow = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                               ?? Application.Current?.MainWindow;
            if (activeWindow != null && activeWindow != this && activeWindow.IsLoaded)
            {
                Owner = activeWindow;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            ConfigureIcon(icon);
            ConfigureButtons(buttons);
        }

        private void ConfigureIcon(MessageBoxImage icon)
        {
            switch (icon)
            {
                case MessageBoxImage.Error:
                IconBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A1260D"));
                IconGlyph.Text = "✕";
                break;
                case MessageBoxImage.Warning:
                IconBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E6A03"));
                IconGlyph.Text = "!";
                break;
                case MessageBoxImage.Question:
                IconBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D4F3E"));
                IconGlyph.Text = "?";
                break;
                default: // Information / None
                IconBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0E639C"));
                IconGlyph.Text = "ℹ";
                break;
            }
        }

        private void ConfigureButtons(MessageBoxButton buttons)
        {
            switch (buttons)
            {
                case MessageBoxButton.OK:
                OkButton.Visibility = Visibility.Visible;
                OkButton.IsDefault = true;
                OkButton.IsCancel = true;
                break;

                case MessageBoxButton.OKCancel:
                OkButton.Visibility = Visibility.Visible;
                OkButton.IsDefault = true;
                CancelButton.Visibility = Visibility.Visible;
                CancelButton.IsCancel = true;
                break;

                case MessageBoxButton.YesNo:
                YesButton.Visibility = Visibility.Visible;
                YesButton.IsDefault = true;
                NoButton.Visibility = Visibility.Visible;
                NoButton.IsCancel = true;
                break;

                case MessageBoxButton.YesNoCancel:
                YesButton.Visibility = Visibility.Visible;
                YesButton.IsDefault = true;
                NoButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Visible;
                CancelButton.IsCancel = true;
                break;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.OK; Close(); }
        private void Yes_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.Yes; Close(); }
        private void No_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.No; Close(); }
        private void Cancel_Click(object sender, RoutedEventArgs e) { Result = MessageBoxResult.Cancel; Close(); }

        public static MessageBoxResult Show(
            string messageBoxText,
            string caption = "",
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.Information)
        {
            var dialog = new DarkMessageBox(messageBoxText, caption, button, icon);
            dialog.ShowDialog();
            return dialog.Result;
        }
    }
}
