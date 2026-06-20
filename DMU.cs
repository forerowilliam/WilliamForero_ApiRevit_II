#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
#endregion

namespace WilliamForero_ApiRevit_II
{
    [Transaction(TransactionMode.Manual)]
    public class DMU : IExternalCommand
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

            // Registramos el Updater. No es opcional
            UpdaterRegistry.RegisterUpdater(dMUUpdater, doc, false);

            // Creamos un filtro de categoria para Structural Columns
            ElementCategoryFilter elementCategoryFilterRun = new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns);

            // Agragamos disparadores: StructualColumns, Cuando se agregan nuevas
            UpdaterRegistry.AddTrigger(dMUUpdater.GetUpdaterId(), elementCategoryFilterRun, Element.GetChangeTypeElementAddition());

            // Agragamos disparadores: StructualColumns, Para cambios geométricos
            UpdaterRegistry.AddTrigger(dMUUpdater.GetUpdaterId(), elementCategoryFilterRun, Element.GetChangeTypeGeometry());

            return Result.Succeeded;
        }



        static internal void AgregarCotas(Document doc, ElementId elemId, List<FamilyInstance> todasLasColumnas)
        {
            if (doc.GetElement(elemId) is FamilyInstance pilarEstructural)
            {
                // 1. Analizar suelos
                AnalizadorSuelos.ResultadoAnalisis resultado = AnalizadorSuelos.Analizar(doc);

                try
                {
                    switch (resultado.Estado)
                    {
                        case AnalizadorSuelos.SueloElegido.SinSuelos:
                            // No hay suelo, no hacemos nada
                            break;

                        case AnalizadorSuelos.SueloElegido.SueloEstructuralUnico:
                            ProcesarCotas(doc, resultado.SuelosEstructurales[0], todasLasColumnas);
                            break;

                        case AnalizadorSuelos.SueloElegido.SueloEstructuralMultiple:
                            // Por ahora usamos el primero, luego se puede implementar selección
                            ProcesarCotas(doc, resultado.SuelosEstructurales[0], todasLasColumnas);
                            break;

                        case AnalizadorSuelos.SueloElegido.SueloUnico:
                            // No es estructural, no hacemos nada
                            break;

                        case AnalizadorSuelos.SueloElegido.SueloMultiple:
                            // No son estructurales, no hacemos nada
                            break;
                    }
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("DMU - Error inesperado", ex.Message);
                }
            }
        }

        private static void ProcesarCotas(Document doc, Floor suelo, List<FamilyInstance> todasLasColumnas)
        {
            // Forzar regeneración para asegurar que la geometría esté lista
            doc.Regenerate();

            // 1. Buscar si ya existe una cota anterior en cualquier columna y eliminarla
            foreach (FamilyInstance columna in todasLasColumnas)
            {
                DMUSchema.EliminarCotasAnteriores(doc, columna);
            }

            // 2. Obtener vértices del suelo
            List<VerticeConArista> vertices = GeometriaSuelos.ObtenerVerticesConArista(suelo, doc);


            // 3. Crear referencias para la cota — una por cada columna más el suelo
            ReferenceArray refArrayX = new ReferenceArray();
            ReferenceArray refArrayY = new ReferenceArray();

            //// 4. Agregar referencia del vértice del suelo
            ////    usando la primera columna como referencia de posición
            //XYZ puntoReferencia = GeometriaSuelos.ObtenerCentroideColumnas(todasLasColumnas);

            //VerticeConArista verticeSueloX = GeometriaSuelos.ObtenerVerticeMasCercanoEnX(vertices, puntoReferencia);
            //VerticeConArista verticeSueloY = GeometriaSuelos.ObtenerVerticeMasCercanoEnY(vertices, puntoReferencia);

            //refArrayX.Append(verticeSueloX.Arista.Reference);
            //refArrayY.Append(verticeSueloY.Arista.Reference);

            // 4. Agregar referencia del vértice del suelo utilizando la distancia real
            XYZ puntoReferencia = GeometriaSuelos.ObtenerCentroideColumnas(todasLasColumnas);

            // Obtenemos el único vértice más cercano en distancia real (sirve para ambos ejes)
            VerticeConArista verticeSueloMasCercano = GeometriaSuelos.ObtenerVerticeMasCercano(vertices, puntoReferencia);

            // Agregamos la referencia del VÉRTICE (punto) para las cotas X e Y
            if (verticeSueloMasCercano.ReferenciaPunto != null)
            {
                refArrayX.Append(verticeSueloMasCercano.ReferenciaPunto);
                refArrayY.Append(verticeSueloMasCercano.ReferenciaPunto);
            }


            // 5. Agregar referencia del eje de cada columna
            foreach (FamilyInstance columna in todasLasColumnas)
            {
                Reference refX = GeometriaSuelos.ObtenerReferenciaEjeColumna(columna, /*doc,*/ true);
                if (refX != null)
                    refArrayX.Append(refX);

                Reference refY = GeometriaSuelos.ObtenerReferenciaEjeColumna(columna, /*doc,*/ false);
                if (refY != null)
                    refArrayY.Append(refY);
            }

            // 6. Obtener vista de planta del nivel de la primera columna
            ViewPlan vistaPlanta = GeometriaSuelos.ObtenerVistaPlanta(doc, todasLasColumnas[0]);
            if (vistaPlanta == null) return;



            // 7. Crear líneas de cota - VERSIÓN PERIMETRAL GEOMÉTRICA TOTAL
            double margenExterno = 3.0; // 5 pies de holgura por fuera de absolutamente todo

            XYZ puntoSuelo = verticeSueloMasCercano.Punto;

            // A. Inicializar los límites con el punto acotado y la referencia
            double minX = Math.Min(puntoSuelo.X, puntoReferencia.X);
            double minY = Math.Min(puntoSuelo.Y, puntoReferencia.Y);

            // B.1. Inspeccionar TODOS los pilares del modelo
            foreach (FamilyInstance col in todasLasColumnas)
            {
                XYZ posCol = GeometriaSuelos.ObtenerPuntoColumna(col);
                if (posCol.X < minX) minX = posCol.X;
                if (posCol.Y < minY) minY = posCol.Y;
            }

            // B.2. INSPECCIONAR TODOS LOS VÉRTICES DEL SUELO (Para evitar que la cota lo atraviese)
            foreach (VerticeConArista vSuelo in vertices)
            {
                if (vSuelo.Punto.X < minX) minX = vSuelo.Punto.X;
                if (vSuelo.Punto.Y < minY) minY = vSuelo.Punto.Y;
            }

            // C. Definir la posición de las líneas de cota (Garantizado fuera del pilar y del suelo entero)
            double posicionFinalY = minY - margenExterno;
            double posicionFinalX = minX - margenExterno;



            // D. Crear las líneas de cota ortogonales puras proyectadas al exterior
            Line lineaCotaX = Line.CreateBound(
                new XYZ(puntoSuelo.X, posicionFinalY, 0),
                new XYZ(puntoReferencia.X, posicionFinalY, 0));

            Line lineaCotaY = Line.CreateBound(
                new XYZ(posicionFinalX, puntoSuelo.Y, 0),
                new XYZ(posicionFinalX, puntoReferencia.Y, 0));





            // 8. Crear las cotas

            // 8. Crear las cotas - Versión mejorada
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

            
            // 9. Guardar Ids en el schema de todas las columnas
            foreach (FamilyInstance columna in todasLasColumnas)
            {
                DMUSchema.GuardarDatos(doc, columna,
                    cotaX.Id.Value,
                    cotaY.Id.Value,
                    suelo.Id.Value);
            }
        }



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