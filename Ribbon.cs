using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.ApplicationServices;

namespace WilliamForero_ApiRevit_II
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class Ribbon : IExternalApplication
    {
        // ExternalCommands assembly path
        static string AddInPath = typeof(Ribbon).Assembly.Location;


        public Autodesk.Revit.UI.Result OnStartup(UIControlledApplication application)
        {
            try
            {
                CreateRibbonSamplePanel(application);

                return Autodesk.Revit.UI.Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Entrega WilliamF", ex.ToString());

                return Autodesk.Revit.UI.Result.Failed;
            }
        }


        public Autodesk.Revit.UI.Result OnShutdown(UIControlledApplication application)
        {
            // Remover eventos al cerrar la aplicación
            try
            {
                // Se usa la referencia guardada al encender la DMU
                // Si es nula, significa que el usuario nunca activó las cotas en esta sesión
                if (DMU.ContenedorUpdaterId != null)
                {
                    // Verificamos con el registro global de Revit si el ID sigue activo
                    if (UpdaterRegistry.IsUpdaterRegistered(DMU.ContenedorUpdaterId))
                    {
                        // Desregistramos de forma segura usando el ID directo
                        UpdaterRegistry.UnregisterUpdater(DMU.ContenedorUpdaterId);
                    }
                }

                // Se apaga el interruptor global
                DMU.EstaActivado = false;
            }
            catch (Exception ex)
            {

            }

            return Autodesk.Revit.UI.Result.Succeeded;
        }


        private void CreateRibbonSamplePanel(UIControlledApplication application)
        {
            string firstPanelName = "Cotas x DMU";

            RibbonPanel ribbonSamplePanel = application.CreateRibbonPanel(firstPanelName);

            // BOTÓN 1: ACTIVAR DMU
            PushButtonData btnActivar = new PushButtonData(
                "btnActivarDMU",
                "Activar\r\nCotas",
                AddInPath,
                "WilliamForero_ApiRevit_II.DMU"
            );

            // Se asignan imágenes
            btnActivar.LargeImage = GetIconFromDll.GetEmbeddedImage("WilliamForero_ApiRevit_II.Resources.ActivarDMU_L.png");
            btnActivar.Image = GetIconFromDll.GetEmbeddedImage("WilliamForero_ApiRevit_II.Resources.ActivarDMU_S.png");
            btnActivar.ToolTip = "Registra el DMU para acotar pilares estructurales (nuevos o modificados) a suelos estructurales en tiempo real.";

            // Ayuda y descripción larga
            btnActivar.LongDescription = "PROYECTO API REVIT II: William Forero" +
                "\r\n -Botón para ACTIVAR el registro del DMU" +
                "\r\n -Funciona con suelos de formas irregulares y curvas" +
                "\r\n -Busca el punto más cercano del suelo al grupo de columnas" +
                "\r\n -Si hay más de un suelo estructural, busca el mas cercano" +
                "\r\n -Cada pilar almacena en un schema los IDs de cotas y suelo usado" +
                "\r\n -Los IDs almacenados se usan para eliminar las cotas y crear nuevas cotas";
            btnActivar.ToolTipImage = GetIconFromDll.GetEmbeddedImage("WilliamForero_ApiRevit_II.Resources.Info.png");

            // ASIGNAR DISPONIBILIDAD: Controla el estado encendido/apagado del botón
            btnActivar.AvailabilityClassName = "WilliamForero_ApiRevit_II.DisponibleParaEncender";


            // BOTÓN 2: DESACTIVAR DMU
            PushButtonData btnDesactivar = new PushButtonData(
                "btnDesactivarDMU",
                "Desactivar\r\nCotas",
                AddInPath,
                "WilliamForero_ApiRevit_II.UnRegister" // Ruta para desregistrar el DMU
            );

            // Asignar imágenes
            btnDesactivar.LargeImage = GetIconFromDll.GetEmbeddedImage("WilliamForero_ApiRevit_II.Resources.DesactivarDMU_L.png");
            btnDesactivar.Image = GetIconFromDll.GetEmbeddedImage("WilliamForero_ApiRevit_II.Resources.DesactivarDMU_S.png");
            btnDesactivar.ToolTip = "Elimina el registro DMU y detiene el acotado automático.";

            // Ayuda y descripción larga
            btnDesactivar.LongDescription = "PROYECTO API REVIT II:" +
                "\r\n -Botón para DESACTIVAR el registro del DMU";
            btnDesactivar.ToolTipImage = GetIconFromDll.GetEmbeddedImage("WilliamForero_ApiRevit_II.Resources.Info.png");


            // ASIGNAR DISPONIBILIDAD: Controla el estado encendido/apagado del botón
            btnDesactivar.AvailabilityClassName = "WilliamForero_ApiRevit_II.DisponibleParaApagar";


            // Se agregan botones al Panel del Ribbon
            ribbonSamplePanel.AddItem(btnActivar);
            ribbonSamplePanel.AddItem(btnDesactivar);
        }

    }


    /// <summary>
    /// Clase que controla la disponibilidad del botón de ENCENDER la DMU.
    /// </summary>
    public class DisponibleParaEncender : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            // El botón de encender estará ACTIVO (true) solo si la DMU está apagada (false)
            return !DMU.EstaActivado;
        }
    }

    /// <summary>
    /// Clase que controla la disponibilidad del botón de APAGAR la DMU.
    /// </summary>
    public class DisponibleParaApagar : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            // El botón de apagar estará ACTIVO (true) solo si la DMU ya está encendida (true)
            return DMU.EstaActivado;
        }
    }

}