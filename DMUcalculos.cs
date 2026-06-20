#region Namespaces
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
#endregion

namespace WilliamForero_ApiRevit_II
{
    public class VerticeConArista
    {
        public XYZ Punto { get; set; }
        public Edge Arista { get; set; }
    }

    public static class AnalizadorSuelos
    {
        public enum SueloElegido
        {
            SinSuelos,
            SueloEstructuralUnico,
            SueloEstructuralMultiple,
            SueloUnico,
            SueloMultiple
        }

        // Clase resultado que agrupa estado y listas
        public class ResultadoAnalisis
        {
            public SueloElegido Estado { get; set; }
            public List<Floor> SuelosEstructurales { get; set; }
            public List<Floor> Suelos { get; set; }
        }

        public static ResultadoAnalisis Analizar(Document doc)
        {
            List<Floor> listaSuelosEstructurales = new List<Floor>();
            List<Floor> listaSuelos = new List<Floor>();

            FilteredElementCollector col = new FilteredElementCollector(doc)
                .OfClass(typeof(Floor));

            foreach (Floor floor in col)
            {
                Parameter isStructural = floor.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
                if (isStructural != null && isStructural.AsInteger() == 1)
                    listaSuelosEstructurales.Add(floor);
                else
                    listaSuelos.Add(floor);
            }

            // Determinamos el estado
            SueloElegido estado;

            if (listaSuelosEstructurales.Count == 0 && listaSuelos.Count == 0)
                estado = SueloElegido.SinSuelos;
            else if (listaSuelosEstructurales.Count == 1)
                estado = SueloElegido.SueloEstructuralUnico;
            else if (listaSuelosEstructurales.Count > 1)
                estado = SueloElegido.SueloEstructuralMultiple;
            else if (listaSuelos.Count == 1)
                estado = SueloElegido.SueloUnico;
            else
                estado = SueloElegido.SueloMultiple;

            // Retornamos todo junto
            return new ResultadoAnalisis
            {
                Estado = estado,
                SuelosEstructurales = listaSuelosEstructurales,
                Suelos = listaSuelos
            };
        }
    }

    public static class AccionesSuelos
    {
        public static void EjecutarSinSuelos(Document doc, ElementId elemId)
        {
            if (doc.GetElement(elemId) is FamilyInstance pilar)
            {
                Parameter comentario = pilar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (comentario != null) comentario.Set("No hay suelos");
            }
        }

        public static void EjecutarSueloEstructuralUnico(Document doc, ElementId elemId, Floor suelo)
        {
            if (doc.GetElement(elemId) is FamilyInstance pilar)
            {
                Parameter comentario = pilar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (comentario != null) comentario.Set("Suelo estructural único: " + suelo.Name);
            }
        }

        public static void EjecutarSueloEstructuralMultiple(Document doc, ElementId elemId, List<Floor> suelos)
        {
            if (doc.GetElement(elemId) is FamilyInstance pilar)
            {
                Parameter comentario = pilar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (comentario != null) comentario.Set("Múltiples suelos estructurales: " + suelos.Count);
            }
        }

        public static void EjecutarSueloUnico(Document doc, ElementId elemId, Floor suelo)
        {
            if (doc.GetElement(elemId) is FamilyInstance pilar)
            {
                Parameter comentario = pilar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (comentario != null) comentario.Set("Suelo único no estructural: " + suelo.Name);
            }
        }

        public static void EjecutarSueloMultiple(Document doc, ElementId elemId, List<Floor> suelos)
        {
            if (doc.GetElement(elemId) is FamilyInstance pilar)
            {
                Parameter comentario = pilar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (comentario != null) comentario.Set("Múltiples suelos no estructurales: " + suelos.Count);
            }
        }
    }



    public static class GeometriaSuelos
    {

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
         

        public static XYZ ObtenerPuntoColumna(FamilyInstance columna)
        {
            LocationPoint ubicacion = columna.Location as LocationPoint;
            return ubicacion.Point;
        }



        public static Reference ObtenerReferenciaEjeColumna(FamilyInstance columna, bool ejeX)
        {
            // Revit ya clasifica los planos internos de las familias.
            // CenterLeftRight suele ser el eje Vertical en planta (Eje X de la columna)
            // CenterFrontBack suele ser el eje Horizontal en planta (Eje Y de la columna)

            FamilyInstanceReferenceType tipoBuscado = ejeX
                ? FamilyInstanceReferenceType.CenterLeftRight
                : FamilyInstanceReferenceType.CenterFrontBack;

            IList<Reference> referencias = columna.GetReferences(tipoBuscado);

            // Devolvemos la primera referencia encontrada que coincida con el tipo
            return referencias.FirstOrDefault();
        }

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
                                            Arista = arista
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

        public static VerticeConArista ObtenerVerticeMasCercanoEnX(List<VerticeConArista> vertices, XYZ puntoColumna)
        {
            return vertices
                .OrderBy(v => Math.Abs(v.Punto.X - puntoColumna.X))
                .First();
        }

        public static VerticeConArista ObtenerVerticeMasCercanoEnY(List<VerticeConArista> vertices, XYZ puntoColumna)
        {
            return vertices
                .OrderBy(v => Math.Abs(v.Punto.Y - puntoColumna.Y))
                .First();
        }


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