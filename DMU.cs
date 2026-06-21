#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
#endregion

namespace WilliamForero_ApiRevit_II
{
    [Transaction(TransactionMode.Manual)]
    public class DMU : IExternalCommand
    {
        // Interruptor global: Guarda el estado del DMU en la sesión de Revit
        public static bool EstaActivado { get; set; } = false;

        // Variable para guardar el UpdaterId del DMU
        public static UpdaterId ContenedorUpdaterId { get; set; } = null;

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

            // Se guarda el UpdaterId en la variable global para usarla posteriormente en el control de botones
            ContenedorUpdaterId = dMUUpdater.GetUpdaterId();
            // Registramos el Updater
            UpdaterRegistry.RegisterUpdater(dMUUpdater, doc, false);

            // Creamos un filtro de categoria para Structural Columns
            ElementCategoryFilter elementCategoryFilterRun = new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns);

            // Agregamos disparadores: StructualColumns, Cuando se agregan nuevas
            UpdaterRegistry.AddTrigger(dMUUpdater.GetUpdaterId(), elementCategoryFilterRun, Element.GetChangeTypeElementAddition());

            // Agregamos disparadores: StructualColumns, Para cambios geométricos
            UpdaterRegistry.AddTrigger(dMUUpdater.GetUpdaterId(), elementCategoryFilterRun, Element.GetChangeTypeGeometry());

            // Se enciende el interruptor global para control de los botones del Ribbon
            EstaActivado = true;

            return Result.Succeeded;
        }

        /// <summary>
        /// Agrega cotas a los pilares estructurales, tomando el suelo más cercano.
        /// </summary>
        /// <param name="doc">Documento</param>
        /// <param name="elemId">Elemento que genero el disparador del Updater</param>
        /// <param name="todasLasColumnas">Lista de todas las columnas estructurales</param>
        static internal void AgregarCotas(Document doc, ElementId elemId, List<FamilyInstance> todasLasColumnas)
        {
            if (doc.GetElement(elemId) is FamilyInstance pilarEstructural)
            {
                // Se calcula primero el centroide de las columnas para usarlo en la decisión de que suelo elegir
                XYZ centroideColumnas = GeometriaSuelos.ObtenerCentroideColumnas(todasLasColumnas);

                // Analizar suelos pasando el centroide de referencia
                AnalizadorSuelos.ResultadoAnalisis resultado = AnalizadorSuelos.Analizar(doc, centroideColumnas);

                try
                {
                    // Se valida que existan suelos estructurales en el resultado del análisis
                    if (resultado.SuelosEstructurales == null || !resultado.SuelosEstructurales.Any())
                    {
                        return;
                    }

                    // Al estar la lista ordenada por distancia en los cálculos, el primero será el más cercano
                    Floor sueloSeleccionado = resultado.SuelosEstructurales.First();

                    if (sueloSeleccionado != null)
                    {
                        ProcesarCotas(doc, sueloSeleccionado, todasLasColumnas);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("DMU - Error inesperado controlado: " + ex.Message);
                }
            }
        }


        /// <summary>
        /// Método para procesar la creación de cotas para los pilares estructurales.
        /// Se elimina las cotas anteriores buscando el ID guardado en el schema.
        /// </summary>
        /// <param name="doc">Documento de Revit</param>
        /// <param name="suelo">Suelo mas cercano para la cota</param>
        /// <param name="todasLasColumnas">Lista de todas las columnas estructurales</param>
        private static void ProcesarCotas(Document doc, Floor suelo, List<FamilyInstance> todasLasColumnas)
        {
            // Se fuerza la regeneración para asegurar que la geometría esté lista
            doc.Regenerate();

            //Pasos internos para la creacion de las cotas:

            // 1. Buscar si ya existe una cota anterior en cualquier columna y eliminarla
            foreach (FamilyInstance columna in todasLasColumnas)
            {
                DMUSchema.EliminarCotasAnteriores(doc, columna);
            }

            // 2. Obtener los vértices del suelo con sus aristas
            List<VerticeConArista> vertices = GeometriaSuelos.ObtenerVerticesConArista(suelo, doc);


            // 3. Crear ReferenceArray para las cotas X e Y
            ReferenceArray refArrayX = new ReferenceArray();
            ReferenceArray refArrayY = new ReferenceArray();


            // 4. Obtener la referencia del punto del suelo más cercano al centroide de las columnas
            XYZ puntoReferencia = GeometriaSuelos.ObtenerCentroideColumnas(todasLasColumnas);

            // Se obtiene el único vértice del suelo, más cercano en distancia al centroide de las columnas
            VerticeConArista verticeSueloMasCercano = GeometriaSuelos.ObtenerVerticeMasCercano(vertices, puntoReferencia);

            // se agrega la referencia del punto del vértice para las cotas del eje X e Y
            if (verticeSueloMasCercano.ReferenciaPunto != null)
            {
                refArrayX.Append(verticeSueloMasCercano.ReferenciaPunto);
                refArrayY.Append(verticeSueloMasCercano.ReferenciaPunto);
            }


            // 5. Obtener referencias de los ejes de todas las columnas y agregarlas a los ReferenceArray
            foreach (FamilyInstance columna in todasLasColumnas)
            {
                Reference refX = GeometriaSuelos.ObtenerReferenciaEjeColumna(columna, true);
                if (refX != null)
                    refArrayX.Append(refX);

                Reference refY = GeometriaSuelos.ObtenerReferenciaEjeColumna(columna, false);
                if (refY != null)
                    refArrayY.Append(refY);
            }


            // 6. Obtener vista de planta del nivel de la primera columna
            ViewPlan vistaPlanta = GeometriaSuelos.ObtenerVistaPlanta(doc, todasLasColumnas[0]);
            if (vistaPlanta == null) return;


            // 7. Calcular la posición de las líneas de cota, con el fin de que estén fuera del pilar y del suelo

            // Variable calcular la posición de las líneas de cota por fuera del pilar y del suelo
            double margenExterno = 3.0;

            XYZ puntoSuelo = verticeSueloMasCercano.Punto;

            // 7A. Buscar los límites inferior e izquierdo del punto de referencia del suelo
            double minX = Math.Min(puntoSuelo.X, puntoReferencia.X);
            double minY = Math.Min(puntoSuelo.Y, puntoReferencia.Y);

            // 7B.1. Inspeccionar todos los pilares para ver si estan más a la izquierda o abajo que el suelo
            foreach (FamilyInstance col in todasLasColumnas)
            {
                XYZ posCol = GeometriaSuelos.ObtenerPuntoColumna(col);
                if (posCol.X < minX) minX = posCol.X;
                if (posCol.Y < minY) minY = posCol.Y;
            }

            // 7B.2. Inspeccionar los vertices del suelo para cer si estan más a la izquierda o abajo que el punto del suelo seleccionado
            foreach (VerticeConArista vSuelo in vertices)
            {
                if (vSuelo.Punto.X < minX) minX = vSuelo.Punto.X;
                if (vSuelo.Punto.Y < minY) minY = vSuelo.Punto.Y;
            }

            // 7C. Se definen la posición de las líneas de cota (Garantizado que queden fuera del pilar y del suelo entero)
            double posicionFinalY = minY - margenExterno;
            double posicionFinalX = minX - margenExterno;

            // 7D. Se crean las líneas para la ubicacion de cotas
            Line lineaCotaX = Line.CreateBound(
                new XYZ(puntoSuelo.X, posicionFinalY, 0),
                new XYZ(puntoReferencia.X, posicionFinalY, 0));

            Line lineaCotaY = Line.CreateBound(
                new XYZ(posicionFinalX, puntoSuelo.Y, 0),
                new XYZ(posicionFinalX, puntoReferencia.Y, 0));



            // 8. Se crean las cotas
            Dimension cotaX = null;
            Dimension cotaY = null;

            try
            {
                // Verificar que el ReferenceArray tenga al menos 2 elementos
                if (refArrayX.Size >= 2)
                {
                    cotaX = doc.Create.NewDimension(vistaPlanta, lineaCotaX, refArrayX);
                }

                if (refArrayY.Size >= 2)
                {
                    cotaY = doc.Create.NewDimension(vistaPlanta, lineaCotaY, refArrayY);
                }
            }
            catch (Exception ex)
            {
            }

            
            // 9. Se guardan los Ids en el schema de todas las columnas
            foreach (FamilyInstance columna in todasLasColumnas)
            {
                DMUSchema.GuardarDatos(doc, columna,
                    cotaX.Id.Value,
                    cotaY.Id.Value,
                    suelo.Id.Value);
            }
        }


        /// <summary>
        /// Interfaz que implementa el IUpdater para manejar los eventos de adición y modificación de pilares estructurales
        /// </summary>
        public class DMUUpdater : IUpdater
        {
            static AddInId m_appId;
            static UpdaterId m_updaterId;

            // Guid fijo, generado una sola vez, nunca modificar
            private static readonly Guid UpdaterGuid = new Guid("e48bd0d3-7d20-4aae-b96e-cd69d820cc76");

            public DMUUpdater(AddInId id)
            {
                m_appId = id;
                m_updaterId = new UpdaterId(m_appId, UpdaterGuid);
            }

            public void Execute(UpdaterData data)
            {
                Document doc = data.GetDocument();

                // Recolectamos TODAS las columnas estructurales del modelo
                FilteredElementCollector col = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .OfCategory(BuiltInCategory.OST_StructuralColumns);

                // Se agregan las columnas a una lista de FamilyInstance
                List<FamilyInstance> todasLasColumnas = col
                    .Cast<FamilyInstance>()
                    .ToList();

                foreach (ElementId addedElemId in data.GetAddedElementIds())
                {
                    DMU.AgregarCotas(doc, addedElemId, todasLasColumnas);
                }

                foreach (ElementId modifiedElemId in data.GetModifiedElementIds())
                {
                    DMU.AgregarCotas(doc, modifiedElemId, todasLasColumnas);
                }
            }

            public string GetAdditionalInformation()
            {
                return "Entrega de proyecto del modulo: API Revit II, del Master en programación BIM";
            }

            public ChangePriority GetChangePriority()
            {
                return ChangePriority.Structure;
            }

            public UpdaterId GetUpdaterId()
            {
                return m_updaterId;
            }

            public string GetUpdaterName()
            {
                return "Entrega API Revit II - William Forero";
            }
        }
    }
}