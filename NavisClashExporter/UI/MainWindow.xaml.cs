using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using NavisClashExporter.Models;
using NavisClashExporter.Services;
using NavisClashExporter.UI;

namespace NavisClashExporter.UI
{
    public partial class MainWindow : Window
    {
        private PgDatabaseService _db;
        private DatabaseConnectionService _connService;
        private TaskPoller _poller;
        private List<NavisworksProjectModel> _projects = new List<NavisworksProjectModel>();

        public MainWindow()
        {
            InitializeComponent();
            _connService = new DatabaseConnectionService();
            TryConnect();
        }

        private void TryConnect()
        {
            bool loaded = _connService.Load();
            if (!loaded || _connService.Config == null)
            {
                ShowSettings(firstRun: true);
                return;
            }

            Connect(_connService.Config.ToConnectionString());
        }

        private void Connect(string cs)
        {
            try
            {
                _db?.Dispose();
                _db = new PgDatabaseService(cs);
                _db.EnsureSchema();
                LoadProjects();
                Log("Подключено к БД");
            }
            catch (Exception ex)
            {
                Log($"Ошибка подключения: {ex.Message}");
                MessageBox.Show($"Не удалось подключиться к БД:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProjects()
        {
            if (_db == null) return;
            try
            {
                _projects = _db.GetAllProjects();
                ProjectsGrid.ItemsSource = null;
                ProjectsGrid.ItemsSource = _projects;
            }
            catch (Exception ex) { Log($"Ошибка загрузки проектов: {ex.Message}"); }
        }

        private void ShowSettings(bool firstRun = false)
        {
            var vm = new SettingsWindow(_connService);
            vm.Owner = this;
            if (vm.ShowDialog() == true)
                Connect(_connService.Config.ToConnectionString());
            else if (firstRun)
                Close();
        }

        private void Log(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                LogBox.Items.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
                LogBox.ScrollIntoView(LogBox.Items[LogBox.Items.Count - 1]);
            });
        }

        // ── КНОПКИ ────────────────────────────────────────────────────
        private void OnSettings_Click(object s, RoutedEventArgs e) => ShowSettings();
        private void OnRefresh_Click(object s, RoutedEventArgs e) => LoadProjects();

        private void ProjectsGrid_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            bool sel = ProjectsGrid.SelectedItem != null;
            btnEdit.IsEnabled = sel;
            btnDelete.IsEnabled = sel;
            btnRun.IsEnabled = sel;
        }

        private void OnAdd_Click(object s, RoutedEventArgs e)
        {
            if (_db == null) return;
            var win = new ProjectEditorWindow(null, _db);
            win.Owner = this;
            if (win.ShowDialog() == true) LoadProjects();
        }

        private void OnEdit_Click(object s, RoutedEventArgs e)
        {
            var p = ProjectsGrid.SelectedItem as NavisworksProjectModel;
            if (p == null) return;
            var win = new ProjectEditorWindow(p, _db);
            win.Owner = this;
            if (win.ShowDialog() == true) LoadProjects();
        }

        private void OnDelete_Click(object s, RoutedEventArgs e)
        {
            var p = ProjectsGrid.SelectedItem as NavisworksProjectModel;
            if (p == null) return;
            if (MessageBox.Show($"Удалить проект '{p.Name}'?\nВсе результаты коллизий будут удалены.",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            _db.DeleteProject(p.Id);
            LoadProjects();
        }

        private void OnRunNow_Click(object s, RoutedEventArgs e)
        {
            var p = ProjectsGrid.SelectedItem as NavisworksProjectModel;
            if (p == null) return;
            if (_db == null) return;

            try
            {
                Log($"Запуск вручную: {p.Name}");
                var exporter = new ClashExporter(_db, chkImages.IsChecked == true);
                exporter.OnProgress += msg => Log(msg);
                exporter.Export(p);
                Log($"✓ Готово: {p.Name}");
            }
            catch (Exception ex)
            {
                Log($"✗ Ошибка: {ex.Message}");
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnPollerStart_Click(object s, RoutedEventArgs e)
        {
            if (_db == null) { MessageBox.Show("Сначала подключитесь к БД."); return; }
            _poller?.Dispose();
            _poller = new TaskPoller(_db, chkImages.IsChecked == true);
            _poller.OnStatusChanged += msg =>
            {
                Dispatcher.Invoke(() => { txtStatus.Text = msg; Log(msg); });
            };
            _poller.Start();
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
        }

        private void OnPollerStop_Click(object s, RoutedEventArgs e)
        {
            _poller?.Stop();
            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
            txtStatus.Text = "Слушатель остановлен";
        }

        protected override void OnClosed(EventArgs e)
        {
            _poller?.Dispose();
            _db?.Dispose();
            base.OnClosed(e);
        }
    }
}