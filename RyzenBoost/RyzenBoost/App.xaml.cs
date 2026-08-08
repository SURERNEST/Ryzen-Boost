using System;
using System.Windows;

namespace RyzenBoost
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Salvaguarda extra: aunque el manifest ya exige admin, verificamos
            // en runtime y avisamos con claridad si algo raro pasó (por ejemplo,
            // el usuario ejecutó una copia del .exe sin el manifest embebido).
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            bool isAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

            if (!isAdmin)
            {
                MessageBox.Show(
                    "RyzenBoost necesita permisos de administrador para aplicar los ajustes de sistema " +
                    "(plan de energía, GPU scheduling, servicios). Cierra la app y ábrela de nuevo; " +
                    "Windows debería mostrar el aviso de UAC automáticamente.",
                    "Se requieren permisos de administrador",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
            }
        }
    }
}
