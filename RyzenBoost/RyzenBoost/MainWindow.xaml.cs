using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RyzenBoost.Models;
using RyzenBoost.Services;

namespace RyzenBoost
{
    public partial class MainWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly Optimizer _optimizer;
        private readonly SystemMonitor _monitor = new();
        private readonly DispatcherTimer _monitorTimer = new() { Interval = TimeSpan.FromSeconds(1.2) };
        private readonly DispatcherTimer _memoryTrimTimer = new() { Interval = TimeSpan.FromSeconds(30) };
        private MediaPlayerPool _sounds = null!;

        // Mapa checkbox <-> propiedad de settings, para generar los toggles dinámicamente
        // y no repetir XAML idéntico seis veces (evita inconsistencias = menos bugs de UI).
        private readonly List<(string Title, string Desc, Func<bool> Get, Action<bool> Set)> _optionDefs;

        public MainWindow()
        {
            InitializeComponent();

            _settings = AppSettings.Load();
            _optimizer = new Optimizer(_settings);

            _optionDefs = new()
            {
                ("Plan de energía: Rendimiento máximo", "Activa el plan de energía de mayor rendimiento de Windows.",
                    () => _settings.OptPowerPlan, v => _settings.OptPowerPlan = v),
                ("Modo de Juego de Windows", "Prioriza CPU/GPU para el juego en primer plano y silencia notificaciones.",
                    () => _settings.OptGameMode, v => _settings.OptGameMode = v),
                ("GPU Hardware Scheduling", "Reduce la latencia de render en GPUs compatibles (requiere reiniciar Windows).",
                    () => _settings.OptGpuScheduling, v => _settings.OptGpuScheduling = v),
                ("Prioridad de proceso alta", "Sube la prioridad de planificación del ejecutable del juego.",
                    () => _settings.OptProcessPriority, v => _settings.OptProcessPriority = v),
                ("Pausar servicios en segundo plano", "Detiene temporalmente SysMain y Windows Search. Reversible.",
                    () => _settings.OptStopBackgroundServices, v => _settings.OptStopBackgroundServices = v),
                ("Deshabilitar programas de inicio automático", "Quita aplicaciones pesadas del arranque para liberar RAM y reducir la carga inicial.",
                    () => _settings.OptDisableStartupPrograms, v => _settings.OptDisableStartupPrograms = v),
                ("Optimización de red", "Aplica ajustes generales de red para reducir latencia en interfaces activas.",
                    () => _settings.OptNetworkTweaks, v => _settings.OptNetworkTweaks = v),
                ("Optimización Wi-Fi", "Aplica ajustes de latencia de red a interfaces inalámbricas activas.",
                    () => _settings.OptNetworkWireless, v => _settings.OptNetworkWireless = v),
                ("Optimización Ethernet", "Aplica ajustes de latencia de red a conexiones de cable activas.",
                    () => _settings.OptNetworkEthernet, v => _settings.OptNetworkEthernet = v),
                ("Optimización Fortnite", "Modifica GameUserSettings.ini para bajar calidad visual y reducir carga del juego.",
                    () => _settings.OptFortniteSettings, v => _settings.OptFortniteSettings = v),
            };

            Loaded += MainWindow_Loaded;
            Closing += (_, _) =>
            {
                _monitorTimer.Stop();
                _memoryTrimTimer.Stop();
                _monitor.Dispose();
            };
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _sounds = new MediaPlayerPool();

                // ---- Bienvenida neutral para proteger privacidad ----
                WelcomeText.Text = "Bienvenido a RyzenBoost";
                SubtitleText.Text = "Sesión iniciada correctamente";

                // ---- Adaptación real a la resolución del usuario ----
                double screenW = SystemParameters.PrimaryScreenWidth;
                double screenH = SystemParameters.PrimaryScreenHeight;
                Width = Math.Clamp(screenW * 0.62, MinWidth, 1400);
                Height = Math.Clamp(screenH * 0.72, MinHeight, 940);
                ResolutionText.Text = $"{screenW:0}x{screenH:0} px · Escala DPI aplicada automáticamente";

                // ---- Poblar toggles dinámicamente ----
                BuildOptionToggles();
                TargetProcessBox.Text = _settings.TargetProcessName;
                SoundToggle.IsChecked = _settings.SoundEnabled;
                MemoryTrimToggle.IsChecked = _settings.OptMemoryTrim;

                // ---- Sonido de bienvenida ----
                PlaySound("welcome");

                // ---- Monitor en vivo ----
                _monitorTimer.Tick += (_, _) => RefreshDashboard();
                _monitorTimer.Start();
                RefreshDashboard();

                // ---- Liberación de memoria automática ----
                _memoryTrimTimer.Tick += (_, _) =>
                {
                    if (_settings.OptMemoryTrim)
                    {
                        MemoryManager.TrimCurrentProcessWorkingSet();
                    }
                };
                _memoryTrimTimer.Start();
            }
            catch (Exception ex)
            {
                // Cualquier fallo en el arranque se informa en vez de dejar la UI a medio construir.
                MessageBox.Show($"Ocurrió un problema iniciando la interfaz:\n{ex.Message}",
                    "RyzenBoost", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildOptionToggles()
        {
            OptionsStack.Children.Clear();
            foreach (var opt in _optionDefs)
            {
                var card = new Border
                {
                    Background = (Brush)FindResource("BgCardBrush"),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(20),
                    Margin = new Thickness(0, 0, 0, 12)
                };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var textStack = new StackPanel();
                textStack.Children.Add(new TextBlock
                {
                    Text = opt.Title,
                    Foreground = (Brush)FindResource("TextPrimaryBrush"),
                    FontWeight = FontWeights.SemiBold
                });
                textStack.Children.Add(new TextBlock
                {
                    Text = opt.Desc,
                    Foreground = (Brush)FindResource("TextSecondaryBrush"),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 0)
                });
                Grid.SetColumn(textStack, 0);

                var toggle = new CheckBox
                {
                    Style = (Style)FindResource("ToggleSwitch"),
                    VerticalAlignment = VerticalAlignment.Center,
                    IsChecked = opt.Get()
                };
                toggle.Checked += (_, _) => { opt.Set(true); _settings.Save(); PlaySound("toggle_on"); };
                toggle.Unchecked += (_, _) => { opt.Set(false); _settings.Save(); PlaySound("toggle_off"); };
                Grid.SetColumn(toggle, 1);

                grid.Children.Add(textStack);
                grid.Children.Add(toggle);
                card.Child = grid;
                OptionsStack.Children.Add(card);
            }
        }

        private void RefreshDashboard()
        {
            try
            {
                var snap = _monitor.Read();
                CpuUsageText.Text = snap.CpuUsagePercent >= 0 ? $"{snap.CpuUsagePercent:0}%" : "No disponible";
                RamUsageText.Text = snap.RamTotalGb > 0 && snap.RamUsedGb >= 0 && snap.RamUsedGb <= snap.RamTotalGb
                    ? $"{snap.RamUsedGb:0.0} GB"
                    : "No disponible";
                RamTotalText.Text = snap.RamTotalGb > 0 ? $"de {snap.RamTotalGb:0.0} GB" : "de no disponible";
                CpuNameText.Text = string.IsNullOrWhiteSpace(snap.CpuName) ? "CPU no disponible" : snap.CpuName;
                GpuNameShort.Text = string.IsNullOrWhiteSpace(snap.GpuName) ? "GPU no disponible" : snap.GpuName;
                GpuUsageText.Text = snap.GpuUsagePercent >= 0 ? $"{snap.GpuUsagePercent:0}%" : "No disponible";

                var hwSched = RegistryTweaks.GetGpuHardwareSchedulingState();
                GpuSchedText.Text = hwSched switch
                {
                    2 => "HW Scheduling: Activado",
                    1 => "HW Scheduling: Desactivado",
                    _ => "HW Scheduling: Desconocido"
                };

                UpdateProfileText(snap);
            }
            catch
            {
                // Un fallo de lectura puntual no debe romper el timer ni la UI.
            }
        }

        private void UpdateProfileText(SystemSnapshot snap)
        {
            string cpuShort = string.IsNullOrWhiteSpace(snap.CpuName) ? "tu CPU" : snap.CpuName;
            string gpuShort = string.IsNullOrWhiteSpace(snap.GpuName) ? "tu GPU" : snap.GpuName;

            ProfileTitleText.Text = $"Optimización para {cpuShort} + {gpuShort}";
            ProfileSubtitleText.Text = $"Aplica los ajustes recomendados para {cpuShort} y {gpuShort}: rendimiento, GPU scheduling, Modo Juego y prioridad de proceso.";
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelDashboard == null) return; // evita NullRef durante InitializeComponent
            var tag = (sender as RadioButton)?.Tag as string;
            PanelDashboard.Visibility = tag == "dashboard" ? Visibility.Visible : Visibility.Collapsed;
            PanelOptim.Visibility = tag == "optim" ? Visibility.Visible : Visibility.Collapsed;
            PanelSettings.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
            PanelAbout.Visibility = tag == "about" ? Visibility.Visible : Visibility.Collapsed;
            PlaySound("click");
        }

        private void BtnApplyProfile_Click(object sender, RoutedEventArgs e)
        {
            _settings.TargetProcessName = string.IsNullOrWhiteSpace(TargetProcessBox.Text)
                ? _settings.TargetProcessName
                : TargetProcessBox.Text.Trim();

            BtnApplyProfile.IsEnabled = false;
            ProfileStatusText.Text = "Aplicando ajustes...";
            try
            {
                var log = _optimizer.ApplyProfile();
                ProfileStatusText.Text = string.Join("\n", log.ConvertAll(l => (l.Ok ? "✔ " : "✘ ") + l.Text));
                PlaySound(log.TrueForAll(l => l.Ok) ? "success" : "click");
                RefreshDashboard();
            }
            catch (Exception ex)
            {
                ProfileStatusText.Text = $"Error inesperado: {ex.Message}";
            }
            finally
            {
                BtnApplyProfile.IsEnabled = true;
            }
        }

        private void BtnRevertAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var log = _optimizer.RevertProfile();
                MessageBox.Show(string.Join("\n", log.ConvertAll(l => l.Text)),
                    "Cambios revertidos", MessageBoxButton.OK, MessageBoxImage.Information);
                PlaySound("success");
                RefreshDashboard();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo revertir todo: {ex.Message}", "RyzenBoost",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SoundToggle_Changed(object sender, RoutedEventArgs e)
        {
            _settings.SoundEnabled = SoundToggle.IsChecked == true;
            _settings.Save();
            if (_settings.SoundEnabled) PlaySound("click");
        }

        private void MemoryTrimToggle_Changed(object sender, RoutedEventArgs e)
        {
            _settings.OptMemoryTrim = MemoryTrimToggle.IsChecked == true;
            _settings.Save();
            if (_settings.SoundEnabled)
            {
                PlaySound(MemoryTrimToggle.IsChecked == true ? "toggle_on" : "toggle_off");
            }
        }

        private void PlaySound(string name)
        {
            if (!_settings.SoundEnabled) return;
            try { _sounds.Play(name); } catch { /* el sonido nunca debe tumbar la app */ }
        }
    }
}
