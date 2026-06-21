#region Namespaces
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
#endregion

namespace WilliamForero_ApiRevit_II
{

    /// <summary>
    /// Clase auxiliar para almacenar un punto de un vértice del suelo junto con la arista a la que pertenece y su referencia.
    /// </summary>
    public class VerticeConArista
    {
        public XYZ Punto { get; set; }
        public Edge Arista { get; set; }
        public Reference ReferenciaPunto { get; set; }
    }


    /// <summary>
    /// Clase estática para analizar los suelos estructurales del proyecto, 
    /// filtrarlos y ordenarlos por cercanía al centroide de las columnas.
    /// </summary>
    public static class AnalizadorSuelos
    {
        public class ResultadoAnalisis
        {
            public List<Floor> SuelosEstructurales { get; set; }
        }

        /// <summary>
        /// Analiza los suelos estructurales del proyecto, 
        /// filtrándolos y ordenándolos por cercanía al centroide de las columnas.
        /// </summary>
        /// <param name="doc">Documento</param>
        /// <param name="centroideColumnas">Centroide de las columnas</param>
        /// <returns>Resultado del análisis con los suelos estructurales ordenados</returns>
        public static ResultadoAnalisis Analizar(Document doc, XYZ centroideColumnas)
        {
            List<Floor> listaSuelosEstructurales = new List<Floor>();

            FilteredElementCollector col = new FilteredElementCollector(doc)
                .OfClass(typeof(Floor));

            // Filtrado por parámetro estructural
            foreach (Floor floor in col)
            {
                Parameter isStructural = floor.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
                if (isStructural != null && isStructural.AsInteger() == 1)
                {
                    listaSuelosEstructurales.Add(floor);
                }
            }

            // Ordenamos la lista para que el suelo cuyo centro esté más cerca del centroide sea el primero
            List<Floor> suelosOrdenados = listaSuelosEstructurales
                .OrderBy(suelo => ObtenerCentroSuelo(suelo).DistanceTo(centroideColumnas))
                .ToList();

            return new ResultadoAnalisis
            {
                SuelosEstructurales = suelosOrdenados
            };
        }


        /// <summary>
        /// Obtiene el punto central del BoundingBox del suelo, que se usará para calcular la distancia al centroide de las columnas.
        /// </summary>
        /// <param name="suelo">Suelo del cual se quiere obtener el centro</param>
        /// <returns>Punto central del BoundingBox del suelo</returns>
        private static XYZ ObtenerCentroSuelo(Floor suelo)
        {
            BoundingBoxXYZ bbox = suelo.get_BoundingBox(null);
            if (bbox == null) return XYZ.Zero;

            // El centro se calcula promediando las coordenadas mínimas y máximas del BoundingBox
            return (bbox.Min + bbox.Max) / 2.0;
        }
    }



    /// <summary>
    /// Clase estática para manejar la geometría de los suelos y columnas
    /// </summary>
    public static class GeometriaSuelos
    {

        /// <summary>
        /// Calcula el centroide del conjunto de columnas, 
        /// sumando las coordenadas de cada columna y dividiendo por el número total de columnas.
        /// </summary>
        /// <param name="columnas">Lista de columnas</param>
        /// <returns>Punto central del conjunto de columnas</returns>
        public static XYZ ObtenerCentroideColumnas(List<FamilyInstance> columnas)
        {
            double x = 0, y = 0, z = 0;
            foreach (FamilyInstance columna in columnas)
            {
                XYZ punto = ObtenerPuntoColumna(columna);
                x += punto.X;
                y += punto.Y;
                z += punto.Z;
            }
            int count = columnas.Count;
            return new XYZ(x / count, y / count, z / count);
        }


        /// <summary>
        /// Obtiene el punto de ubicación de la columna, se usará para calcular la distancia a los suelos.
        /// </summary>
        /// <param name="columna">Columna de la cual se quiere obtener el punto de ubicación</param>
        /// <returns>Punto de ubicación de la columna</returns>
        public static XYZ ObtenerPuntoColumna(FamilyInstance columna)
        {
            LocationPoint ubicacion = columna.Location as LocationPoint;
            return ubicacion.Point;
        }


        /// <summary>
        /// Obtiene la referencia del plano interno de la columna que corresponde al eje X o eje Y, 
        /// </summary>
        /// <param name="columna">Columna de la cual se quiere obtener la referencia del eje</param>
        /// <param name="ejeX">Indica si se quiere la referencia del eje X (true) o del eje Y (false)</param>
        /// <returns>Referencia del plano interno de la columna correspondiente al eje especificado</returns>
        public static Reference ObtenerReferenciaEjeColumna(FamilyInstance columna, bool ejeX)
        {
            FamilyInstanceReferenceType tipoBuscado = ejeX
                ? FamilyInstanceReferenceType.CenterLeftRight
                : FamilyInstanceReferenceType.CenterFrontBack;

            IList<Reference> referencias = columna.GetReferences(tipoBuscado);

            // Se devuelve la primera referencia encontrada que coincida con el tipo
            return referencias.FirstOrDefault();
        }


        /// <summary>
        /// Obtiene una lista de vértices del suelo junto con su arista y su referencia
        /// </summary>
        /// <param name="suelo">Suelo del cual se quieren obtener los vértices</param>
        /// <param name="doc">Documento de Revit</param>
        /// <returns>Lista de vértices con su arista y referencia</returns>
        public static List<VerticeConArista> ObtenerVerticesConArista(Floor suelo, Document doc)
        {
            List<VerticeConArista> vertices = new List<VerticeConArista>();

            Options opciones = new Options();
            opciones.View = doc.ActiveView;
            opciones.ComputeReferences = true;

            GeometryElement geometria = suelo.get_Geometry(opciones);

            foreach (GeometryObject obj in geometria)
            {
                if (obj is Solid solido)
                {
                    foreach (Face cara in solido.Faces)
                    {
                        if (cara is PlanarFace caraPlana)
                        {
                            if (caraPlana.FaceNormal.Z > 0.9)
                            {
                                EdgeArrayArray bordes = cara.EdgeLoops;
                                foreach (EdgeArray loop in bordes)
                                {
                                    foreach (Edge arista in loop)
                                    {
                                        vertices.Add(new VerticeConArista
                                        {
                                            Punto = arista.AsCurve().GetEndPoint(0),
                                            Arista = arista,
                                            ReferenciaPunto = arista.GetEndPointReference(0)
                                        });
                                    }
                                }
                            }
                        }
                    }
                    break;
                }
            }

            return vertices;
        }


        /// <summary>
        /// Obtiene el vértice del suelo que está más cerca del punto de ubicación de la columna,
        /// </summary>
        /// <param name="vertices">Lista de vértices con su arista y referencia</param>
        /// <param name="puntoColumna">Punto de ubicación de la columna</param>
        /// <returns>Vértice más cercano al punto de la columna</returns>
        public static VerticeConArista ObtenerVerticeMasCercano(List<VerticeConArista> vertices, XYZ puntoColumna)
        {
            return vertices
                .OrderBy(v => v.Punto.DistanceTo(puntoColumna))
                .First();
        }


        /// <summary>
        /// Obtiene la vista de planta estructural del nivel de la columna, si la vista activa no es una vista de planta.
        /// </summary>
        /// <param name="doc">Documento</param>
        /// <param name="columna">Columna para la cual se quiere obtener la vista de planta</param>
        /// <returns>Vista de planta estructural del nivel de la columna</returns>
        public static ViewPlan ObtenerVistaPlanta(Document doc, FamilyInstance columna)
        {
            // Si la vista activa es una vista de planta, la usamos directamente
            if (doc.ActiveView is ViewPlan vistaActiva && !vistaActiva.IsTemplate)
                return vistaActiva;

            // Si no, buscamos una vista estructural del nivel de la columna
            Level nivel = doc.GetElement(columna.LevelId) as Level;
            if (nivel == null) return null;

            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .FirstOrDefault(v => v.GenLevel != null
                                  && v.GenLevel.Id == nivel.Id
                                  && !v.IsTemplate
                                  && v.Name.ToLower().Contains("structural"));
        }


    }
    
}