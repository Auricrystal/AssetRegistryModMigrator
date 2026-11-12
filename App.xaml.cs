using AssetRegistryModMigrator.Classes;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Controls;namespace AssetRegistryModMigrator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App :Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Control.BackgroundProperty.OverrideMetadata(
                typeof(Window),
                new FrameworkPropertyMetadata((d, _) =>
                {
                    if (d is Window win)
                    {
                        DarkTitleBarHelper.ApplyDarkTitleBar(win);
                    }
                }));
        }
    }}
