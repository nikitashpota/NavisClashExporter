using System;
using System.IO;
using System.Reflection;
using Autodesk.Navisworks.Api.Plugins;
using NavisClashExporter.UI;

namespace NavisClashExporter
{
    [Plugin("NavisClashExporter", "Mosproekt", DisplayName = "ClashRunner — Автопроверка коллизий")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class Application : AddInPlugin
    {
        static Application()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        }

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            // Папка где лежит плагин
            string pluginFolder = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);

            // Берём имя dll без версии
            string assemblyName = new AssemblyName(args.Name).Name;
            string assemblyPath = Path.Combine(pluginFolder, assemblyName + ".dll");

            if (File.Exists(assemblyPath))
                return Assembly.LoadFrom(assemblyPath);

            return null;
        }

        public override int Execute(params string[] parameters)
        {
            var window = new MainWindow();
            System.Windows.Forms.Integration.ElementHost
                .EnableModelessKeyboardInterop(window);
            window.Show();
            return 0;
        }
    }
}