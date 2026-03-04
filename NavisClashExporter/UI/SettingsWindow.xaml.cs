using System;
using System.Windows;
using NavisClashExporter.Models;
using NavisClashExporter.Services;
using Npgsql;

namespace NavisClashExporter.UI
{
    public partial class SettingsWindow : Window
    {
        private readonly DatabaseConnectionService _connService;

        public SettingsWindow(DatabaseConnectionService connService)
        {
            InitializeComponent();
            _connService = connService;

            var cfg = connService.Config;
            if (cfg != null)
            {
                txtHost.Text = cfg.Host;
                txtPort.Text = cfg.Port.ToString();
                txtDatabase.Text = cfg.Database;
                txtUsername.Text = cfg.Username;
                txtPassword.Password = cfg.Password;
            }
            else
            {
                txtHost.Text = "localhost";
                txtPort.Text = "5432";
                txtDatabase.Text = "progress";
                txtUsername.Text = "progress";
            }
        }

        private DbConnectionConfig BuildConfig() => new DbConnectionConfig
        {
            Host = txtHost.Text.Trim(),
            Port = int.TryParse(txtPort.Text, out var p) ? p : 5432,
            Database = txtDatabase.Text.Trim(),
            Username = txtUsername.Text.Trim(),
            Password = txtPassword.Password
        };

        private async void OnTest_Click(object s, RoutedEventArgs e)
        {
            txtTestResult.Text = "Проверяю...";
            var cfg = BuildConfig();
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    var conn = new NpgsqlConnection(cfg.ToConnectionString());
                    conn.Open();
                    var cmd = new NpgsqlCommand("SELECT version()", conn);
                    var ver = cmd.ExecuteScalar()?.ToString();
                    Dispatcher.Invoke(() =>
                    {
                        txtTestResult.Text = $"✓ Успешно!\n{ver}";
                        txtTestResult.Foreground = System.Windows.Media.Brushes.Green;
                    });
                });
            }
            catch (Exception ex)
            {
                txtTestResult.Text = $"✗ Ошибка:\n{ex.Message}";
                txtTestResult.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void OnSave_Click(object s, RoutedEventArgs e)
        {
            _connService.Save(BuildConfig());
            DialogResult = true;
            Close();
        }

        private void OnCancel_Click(object s, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}