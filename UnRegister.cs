#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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
            // Verificamos de forma segura si el DMU está corriendo en la sesión
            if (DMU.ContenedorUpdaterId != null && UpdaterRegistry.IsUpdaterRegistered(DMU.ContenedorUpdaterId))
            {
                // Desregistramos el Updater usando directamente el ID estático global
                UpdaterRegistry.UnregisterUpdater(DMU.ContenedorUpdaterId);
            }

            // Se apaga el interruptor global para actualizar el estado visual de los botones del Ribbon
            DMU.EstaActivado = false;

            return Result.Succeeded;
        }
    }
}