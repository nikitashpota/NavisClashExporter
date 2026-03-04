using System;
using System.Windows;
using NavisClashExporter.Models;
using NavisClashExporter.Services;

namespace NavisClashExporter.UI
{
    public partial class ProjectEditorWindow : Window
    {
        private readonly PgDatabaseService _db;
        private readonly NavisworksProjectModel _existing;

        public ProjectEditorWindow(NavisworksProjectModel existing, PgDatabaseService db)
        {
            InitializeComponent();
            _db = db;
            _existing = existing;

            // Загрузить директории
            var dirs = _db.GetAllDirectories();
            dirs.Insert(0, new DirectoryModel { Id = 0, Code = "(не выбрана)" });
            cmbDirectory.ItemsSource = dirs;
            cmbDirectory.SelectedIndex = 0;

            if (existing != null)
            {
                txtTitle.Text = "Редактирование проекта";
                txtName.Text = existing.Name;
                txtNwfPath.Text = existing.NwfPath;

                if (existing.DirectoryId.HasValue)
                {
                    foreach (var item in dirs)
                    {
                        if (item.Id == existing.DirectoryId.Value)
                        {
                            cmbDirectory.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
        }

        private void OnBrowse_Click(object s, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Выберите NWF файл",
                Filter = "Navisworks Files (*.nwf)|*.nwf|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
                txtNwfPath.Text = dlg.FileName;
        }

        private void OnSave_Click(object s, RoutedEventArgs e)
        {
            string name = txtName.Text.Trim();
            string nwfPath = txtNwfPath.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Укажите название проекта.", "Валидация",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(nwfPath))
            {
                MessageBox.Show("Укажите путь к NWF файлу.", "Валидация",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dir = cmbDirectory.SelectedItem as DirectoryModel;
            int? dirId = (dir != null && dir.Id > 0) ? dir.Id : (int?)null;

            try
            {
                if (_existing == null)
                    _db.CreateProject(name, nwfPath, dirId);
                else
                    _db.UpdateProject(_existing.Id, name, nwfPath, dirId);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения:\n{ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCancel_Click(object s, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}