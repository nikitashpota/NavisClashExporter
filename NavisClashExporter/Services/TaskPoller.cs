using System;
using System.IO;
using System.Timers;
using System.Windows.Threading;
using NavisClashExporter.Models;

namespace NavisClashExporter.Services
{
    public class TaskPoller : IDisposable
    {
        private readonly PgDatabaseService _db;
        private readonly bool _exportImages;
        private readonly Timer _timer;
        private readonly Dispatcher _dispatcher;
        private bool _isProcessing;

        public event Action<string> OnStatusChanged;

        public TaskPoller(PgDatabaseService db, bool exportImages = true)
        {
            _db = db;
            _exportImages = exportImages;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _timer = new Timer(20_000);
            _timer.Elapsed += OnTick;
            _timer.AutoReset = true;
        }

        public void Start()
        {
            _timer.Start();
            Logger.Log("TaskPoller запущен");
            OnStatusChanged?.Invoke("Слушатель активен — опрос каждые 20 сек");
        }

        public void Stop()
        {
            _timer.Stop();
            Logger.Log("TaskPoller остановлен");
            OnStatusChanged?.Invoke("Слушатель остановлен");
        }

        private void OnTick(object sender, ElapsedEventArgs e)
        {
            if (_isProcessing) return;
            _isProcessing = true;
            try
            {
                var tasks = _db.GetPendingTasks();
                if (tasks.Count == 0) return;
                Logger.Log($"Найдено задач: {tasks.Count}");
                foreach (var task in tasks)
                    ProcessTask(task);
            }
            catch (Exception ex) { Logger.LogError(ex); }
            finally { _isProcessing = false; }
        }

        private void ProcessTask(ClashTaskModel task)
        {
            Logger.Log($"Задача: {task.ProjectName} [{task.Id}]");
            _db.UpdateTaskStatus(task.Id, ClashTaskStatus.Running);
            OnStatusChanged?.Invoke($"Обработка: {task.ProjectName}");

            try
            {
                var project = _db.GetProjectByName(task.ProjectName);
                if (project == null)
                    throw new Exception($"Проект '{task.ProjectName}' не найден.");

                if (string.IsNullOrEmpty(project.NwfPath) || !File.Exists(project.NwfPath))
                    throw new Exception($"NWF файл не найден: {project.NwfPath}");

                _dispatcher.Invoke(() => OpenAndExport(project, task));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                _db.UpdateTaskStatus(task.Id, ClashTaskStatus.Failed, ex.Message);
                OnStatusChanged?.Invoke($"✗ Ошибка: {task.ProjectName} — {ex.Message}");
            }
        }

        private void OpenAndExport(NavisworksProjectModel project, ClashTaskModel task)
        {
            try
            {
                Logger.Log($"Открываю: {project.NwfPath}");

                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                doc.Clear();
                doc.AppendFile(project.NwfPath);

                var exporter = new ClashExporter(_db, _exportImages);
                exporter.OnProgress += msg => { Logger.Log(msg); OnStatusChanged?.Invoke(msg); };
                exporter.Export(project);

                _db.UpdateTaskStatus(task.Id, ClashTaskStatus.Done);
                OnStatusChanged?.Invoke($"✓ Выполнено: {project.Name}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                _db.UpdateTaskStatus(task.Id, ClashTaskStatus.Failed, ex.Message);
                OnStatusChanged?.Invoke($"✗ Ошибка: {ex.Message}");
            }
        }

        public void Dispose() { _timer?.Dispose(); }
    }
}