using Autodesk.Navisworks.Api.Plugins;
using NavisClashExporter.UI;

namespace NavisClashExporter
{
    [Plugin("NavisClashExporter", "getBIM", DisplayName = "ClashRunner — Автопроверка коллизий")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class Application : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            var window = new MainWindow();
            window.Show();
            return 0;
        }
    }
}