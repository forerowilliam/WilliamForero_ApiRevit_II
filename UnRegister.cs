#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using static WilliamForero_ApiRevit_II.DMU;

#endregion

namespace WilliamForero_ApiRevit_II
{
    [Transaction(TransactionMode.Manual)]
    public class UnRegister : IExternalCommand
    {
        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            // Se crea una nueva instancia de la clase DMUUpdater
            DMUUpdater dMUUpdater = new DMUUpdater(app.ActiveAddInId);

            //Desregistramos el Updater
            UpdaterRegistry.UnregisterUpdater(dMUUpdater.GetUpdaterId());

            // Se apaga el interruptor global para control de los botones del Ribbon
            WilliamForero_ApiRevit_II.DMU.EstaActivado = false;

            return Result.Succeeded;
        }
    }
}