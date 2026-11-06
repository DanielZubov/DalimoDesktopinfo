using System.Windows;

namespace DalimoDesktopinfo
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Приложение не будет завершаться, если главное окно скрыто
            // (это нужно для работы иконки в трее)
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }
    }
}