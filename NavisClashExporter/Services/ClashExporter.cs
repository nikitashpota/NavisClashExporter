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
            Logger.Log($"Export: {project.Name}");
            OnProgress?.Invoke("Удаление старых данных...");
            _db.DeleteClashDataForProject(project.Id);

            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc == null) throw new Exception("Документ не открыт");

            doc.Models.ResetAllHidden();
            var testsData = doc.GetClash().TestsData;
            testsData.TestsRunAllTests();
            testsData.TestsCompactAllTests();

            var tests = testsData.Tests;
            int i = 0;
            foreach (var test in tests)
            {
                i++;
                if (!(test is ClashTest ct)) continue;
                OnProgress?.Invoke($"Тест {i}/{tests.Count}: {ct.DisplayName}");
                ProcessTest(doc, ct, project.Id);
            }
            OnProgress?.Invoke("Готово");
        }

        private void ProcessTest(Document doc, ClashTest ct, int projectId)
        {
            var children = ct.Children.Cast<ClashResult>().ToList();
            int total = children.Count;
            int newC = children.Count(x => x.Status == ClashResultStatus.New);
            int active = children.Count(x => x.Status == ClashResultStatus.Active);
            int reviewed = children.Count(x => x.Status == ClashResultStatus.Reviewed);
            int approved = children.Count(x => x.Status == ClashResultStatus.Approved);
            int resolved = children.Count(x => x.Status == ClashResultStatus.Resolved);

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

            _db.UpsertClashTestHistory(projectId, ct.DisplayName,
                total, newC, active, reviewed, approved, resolved);

            var results = children
                .Where(c => c.Status == ClashResultStatus.New || c.Status == ClashResultStatus.Active)
                .Select(clash =>
                {
                    var m1 = clash.CompositeItem1 as ModelItem;
                    var m2 = clash.CompositeItem2 as ModelItem;
                    return new ClashResultModel
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
                        Image = _exportImages ? GenerateImage(doc, m1, m2) : null,
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
                    };
                }).ToList();

            _db.InsertClashResults(results);
        }

        private string GetLocator(ClashSelection sel)
        {
            if (sel == null) return "";
            try { var items = sel.Selection?.GetSelectedItems(); return items?.Count > 0 ? items[0]?.DisplayName ?? "" : ""; }
            catch { return ""; }
        }

        private string Prop(ModelItem item, string cat, string prop)
        {
            if (item == null) return "";
            try { return item.PropertyCategories.FindPropertyByDisplayName(cat, prop)?.Value?.ToDisplayString()?.Replace(",", ".") ?? ""; }
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

                // ✅ Исправлено: правильная сигнатура GenerateImage
                var img = doc.ActiveView.GenerateImage(ImageGenerationStyle.Scene, 500, 500);

                // ✅ Исправлено: using-блок вместо using-объявления (C# 7.3)
                using (var ms = new MemoryStream())
                {
                    img.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
            catch (Exception ex) { Logger.LogError(ex); return null; }
        }
    }
}