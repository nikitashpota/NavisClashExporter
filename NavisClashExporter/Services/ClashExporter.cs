using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using NavisClashExporter.Models;
using NavisClashExporter.Services;

namespace NavisClashExporter.Services
{
    public class ClashExporter
    {
        private readonly PgDatabaseService _db;
        private readonly bool _exportImages;
        public event Action<string> OnProgress;

        public ClashExporter(PgDatabaseService db, bool exportImages = true)
        {
            _db = db;
            _exportImages = exportImages;
        }

        public void Export(NavisworksProjectModel project)
        {
            try
            {
                Logger.Log($"=== Export START: {project.Name} ===");
                OnProgress?.Invoke("Удаление старых данных...");

                Logger.Log("Шаг 1: DeleteClashDataForProject");
                _db.DeleteClashDataForProject(project.Id);
                Logger.Log("Шаг 1: OK");

                Logger.Log("Шаг 2: Получение ActiveDocument");
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                    throw new Exception("Документ не открыт");

                // ✅ Открываем NWF файл если не открыт или открыт другой
                if (string.IsNullOrEmpty(doc.FileName) ||
                    !doc.FileName.Equals(project.NwfPath, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Log($"Шаг 2: Открываем файл: {project.NwfPath}");
                    OnProgress?.Invoke($"Открытие файла: {Path.GetFileName(project.NwfPath)}");

                    if (!File.Exists(project.NwfPath))
                        throw new Exception($"NWF файл не найден: {project.NwfPath}");

                    doc.Clear();
                    doc.AppendFile(project.NwfPath);
                    Logger.Log("Шаг 2: Файл открыт");
                }
                Logger.Log($"Шаг 2: OK — {doc.FileName}");

                Logger.Log("Шаг 3: ResetAllHidden");
                doc.Models.ResetAllHidden();
                Logger.Log("Шаг 3: OK");

                Logger.Log("Шаг 4: GetClash().TestsData");
                var testsData = doc.GetClash().TestsData;
                Logger.Log($"Шаг 4: OK — testsData is null: {testsData == null}");

                Logger.Log("Шаг 5: TestsRunAllTests");
                testsData.TestsRunAllTests();
                Logger.Log("Шаг 5: OK");

                Logger.Log("Шаг 6: TestsCompactAllTests");
                testsData.TestsCompactAllTests();
                Logger.Log("Шаг 6: OK");

                var tests = testsData.Tests;
                Logger.Log($"Шаг 7: Тестов найдено: {tests.Count}");

                int i = 0;
                foreach (var test in tests)
                {
                    i++;
                    if (!(test is ClashTest ct))
                    {
                        Logger.Log($"Тест {i}: не ClashTest, пропускаем");
                        continue;
                    }

                    Logger.Log($"Тест {i}/{tests.Count}: {ct.DisplayName}");
                    OnProgress?.Invoke($"Тест {i}/{tests.Count}: {ct.DisplayName}");

                    try
                    {
                        ProcessTest(doc, ct, project.Id);
                        Logger.Log($"Тест {i}: обработан успешно");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex);
                        Logger.Log($"Тест {i}: ОШИБКА — {ex.Message}");
                        // Продолжаем со следующим тестом
                    }
                }

                OnProgress?.Invoke("Готово");
                Logger.Log("=== Export DONE ===");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                OnProgress?.Invoke($"Ошибка: {ex.Message}");
                throw;
            }
        }

        private void ProcessTest(Document doc, ClashTest ct, int projectId)
        {
            Logger.Log($"  ProcessTest: {ct.DisplayName}");

            var children = ct.Children.Cast<ClashResult>().ToList();
            Logger.Log($"  Коллизий: {children.Count}");

            int total = children.Count;
            int newC = children.Count(x => x.Status == ClashResultStatus.New);
            int active = children.Count(x => x.Status == ClashResultStatus.Active);
            int reviewed = children.Count(x => x.Status == ClashResultStatus.Reviewed);
            int approved = children.Count(x => x.Status == ClashResultStatus.Approved);
            int resolved = children.Count(x => x.Status == ClashResultStatus.Resolved);

            Logger.Log($"  Статистика: total={total} new={newC} active={active}");

            Logger.Log("  InsertClashTest...");
            int testId = _db.InsertClashTest(new ClashTestModel
            {
                NavisworksProjectId = projectId,
                Name = ct.DisplayName,
                TestType = ct.TestType.ToString(),
                Status = ct.Status.ToString(),
                Tolerance = ct.Tolerance,
                LeftLocator = GetLocator(ct.SelectionA),
                RightLocator = GetLocator(ct.SelectionB),
                SummaryTotal = total,
                SummaryNew = newC,
                SummaryActive = active,
                SummaryReviewed = reviewed,
                SummaryApproved = approved,
                SummaryResolved = resolved
            });
            Logger.Log($"  InsertClashTest OK: id={testId}");

            Logger.Log("  UpsertClashTestHistory...");
            _db.UpsertClashTestHistory(projectId, ct.DisplayName,
                total, newC, active, reviewed, approved, resolved);
            Logger.Log("  UpsertClashTestHistory OK");

            var filtered = children
                .Where(c => c.Status == ClashResultStatus.New || c.Status == ClashResultStatus.Active)
                .ToList();
            Logger.Log($"  Фильтрованных (New+Active): {filtered.Count}");

            var results = new List<ClashResultModel>();
            int clashIdx = 0;
            foreach (var clash in filtered)
            {
                clashIdx++;
                Logger.Log($"  Коллизия {clashIdx}/{filtered.Count}: {clash.DisplayName}");
                try
                {
                    var m1 = clash.CompositeItem1 as ModelItem;
                    var m2 = clash.CompositeItem2 as ModelItem;
                    Logger.Log($"    m1={m1?.DisplayName ?? "null"} m2={m2?.DisplayName ?? "null"}");

                    byte[] img = null;
                    if (_exportImages)
                    {
                        Logger.Log("    GenerateImage...");
                        img = GenerateImage(doc, m1, m2);
                        Logger.Log($"    GenerateImage: {(img != null ? img.Length + " bytes" : "null")}");
                    }

                    results.Add(new ClashResultModel
                    {
                        ClashTestId = testId,
                        Guid = clash.Guid.ToString(),
                        Name = clash.DisplayName,
                        Status = clash.Status.ToString(),
                        Distance = clash.Distance,
                        Description = clash.Description,
                        GridLocation = "",
                        PointX = clash.Center.X * 0.3048,
                        PointY = clash.Center.Y * 0.3048,
                        PointZ = clash.Center.Z * 0.3048,
                        CreatedDate = clash.CreatedTime,
                        Image = img,
                        Item1Id = Prop(m1, "ID объекта", "Значение"),
                        Item1Name = Prop(m1, "Элемент", "Имя"),
                        Item1Type = Prop(m1, "Элемент", "Тип"),
                        Item1Layer = Prop(m1, "Элемент", "Слой"),
                        Item1SourceFile = Prop(m1, "Элемент", "Файл источника"),
                        Item2Id = Prop(m2, "ID объекта", "Значение"),
                        Item2Name = Prop(m2, "Элемент", "Имя"),
                        Item2Type = Prop(m2, "Элемент", "Тип"),
                        Item2Layer = Prop(m2, "Элемент", "Слой"),
                        Item2SourceFile = Prop(m2, "Элемент", "Файл источника")
                    });
                }
                catch (Exception ex)
                {
                    Logger.Log($"    ОШИБКА коллизии {clashIdx}: {ex.Message}");
                }
            }

            Logger.Log($"  InsertClashResults: {results.Count} записей...");
            _db.InsertClashResults(results);
            Logger.Log("  InsertClashResults OK");
        }

        private string GetLocator(ClashSelection sel)
        {
            if (sel == null) return "";
            try
            {
                var items = sel.Selection?.GetSelectedItems();
                return items?.Count > 0 ? items[0]?.DisplayName ?? "" : "";
            }
            catch { return ""; }
        }

        private string Prop(ModelItem item, string cat, string prop)
        {
            if (item == null) return "";
            try
            {
                return item.PropertyCategories
                    .FindPropertyByDisplayName(cat, prop)?
                    .Value?.ToDisplayString()?
                    .Replace(",", ".") ?? "";
            }
            catch { return ""; }
        }

        private byte[] GenerateImage(Document doc, ModelItem m1, ModelItem m2)
        {
            if (m1 == null || m2 == null) return null;
            try
            {
                var items = new ModelItemCollection { m1, m2 };
                doc.CurrentSelection.Clear();
                doc.CurrentSelection.CopyFrom(items);
                doc.ActiveView.FocusOnCurrentSelection();
                doc.Models.ResetAllHidden();
                doc.Models.OverridePermanentColor(new[] { m1 }, Autodesk.Navisworks.Api.Color.Red);
                doc.Models.OverridePermanentColor(new[] { m2 }, Autodesk.Navisworks.Api.Color.Green);
                doc.ActiveView.LookFromFrontRightTop();
                doc.ActiveView.RequestDelayedRedraw(ViewRedrawRequests.All);

                var img = doc.ActiveView.GenerateImage(ImageGenerationStyle.Scene, 500, 500);

                using (var ms = new MemoryStream())
                {
                    img.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                return null;
            }
        }
    }
}