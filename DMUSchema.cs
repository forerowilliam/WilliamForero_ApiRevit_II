using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using System;

namespace WilliamForero_ApiRevit_II
{
    /// <summary>
    /// Clase estática para manejar el almacenamiento de datos dentro de las columnas,
    /// se usará para leer y guardar los Ids de cotas y suelo en cada columna,
    /// tambien para eliminar las cotas anteriores usando esos Ids.
    /// </summary>
    public static class DMUSchema
    {
        // GUID fijo, generado una sola vez, no modificar
        private static readonly Guid SchemaGuid = new Guid("e47c4741-2a1e-416c-bb13-3adc8acd8f7b");
        private const string SchemaName = "DMU_ColumnaSchema";

        /// <summary>
        /// Metodo para obtener o crear el schema.
        /// </summary>
        /// <returns>El schema obtenido o creado</returns>
        public static Schema ObtenerSchema()
        {
            // Si ya existe, lo retornamos directamente
            Schema schema = Schema.Lookup(SchemaGuid);
            if (schema != null) return schema;

            // Si no existe, lo creamos
            SchemaBuilder schemaBuilder = new SchemaBuilder(SchemaGuid);
            schemaBuilder.SetVendorId("WilliamForero");
            schemaBuilder.SetReadAccessLevel(AccessLevel.Public);
            schemaBuilder.SetWriteAccessLevel(AccessLevel.Public);
            schemaBuilder.SetSchemaName(SchemaName);
            schemaBuilder.SetDocumentation("Schema para almacenar Ids de cotas y suelo.");

            //Construimos Field
            schemaBuilder.AddSimpleField("IdCotaX", typeof(long));
            schemaBuilder.AddSimpleField("IdCotaY", typeof(long));
            schemaBuilder.AddSimpleField("IdSuelo", typeof(long));

            return schemaBuilder.Finish();
        }


        /// <summary>
        /// Guarda los Ids de las cotas y suelo en el schema asociado a la columna.
        /// </summary>
        /// <param name="doc">Documento</param>
        /// <param name="columna">Columna donde se almacenarán los datos</param>
        /// <param name="idCotaX">Id de la cota en el eje X</param>
        /// <param name="idCotaY">Id de la cota en el eje Y</param>
        /// <param name="idSuelo">Id del suelo asociado</param>
        public static void GuardarDatos(Document doc, FamilyInstance columna, long idCotaX, long idCotaY, long idSuelo)
        {
            Schema schema = ObtenerSchema();
            Entity entidad = new Entity(schema);
            
            //Asignamos datos
            entidad.Set("IdCotaX", idCotaX);
            entidad.Set("IdCotaY", idCotaY);
            entidad.Set("IdSuelo", idSuelo);

            columna.SetEntity(entidad);
        }


        /// <summary>
        /// Lee los Ids de las cotas y suelo almacenados en el schema asociado a la columna.
        /// </summary>
        /// <param name="columna">Columna de la cual se leerán los datos</param>
        /// <param name="idCotaX">Id de la cota en el eje X</param>
        /// <param name="idCotaY">Id de la cota en el eje Y</param>
        /// <param name="idSuelo">Id del suelo asociado</param>
        /// <returns>True si hay datos para leer, false en caso contrario</returns>
        public static bool LeerDatos(FamilyInstance columna, out long idCotaX, out long idCotaY, out long idSuelo)
        {
            Schema schema = Schema.Lookup(SchemaGuid);

            // Inicializamos los out por si no hay datos
            idCotaX = -1;
            idCotaY = -1;
            idSuelo = -1;

            // Si el schema no existe aún, no hay nada que leer
            if (schema == null) return false;

            Entity entidad = columna.GetEntity(schema);

            // Si la columna no tiene datos guardados
            if (!entidad.IsValid()) return false;

            // Se leen los datos
            idCotaX = entidad.Get<long>("IdCotaX");
            idCotaY = entidad.Get<long>("IdCotaY");
            idSuelo = entidad.Get<long>("IdSuelo");

            return true;
        }


        /// <summary>
        /// Elimina las cotas anteriores asociadas a la columna, usando los Ids almacenados en el schema.
        /// </summary>
        /// <param name="doc">Documento donde se encuentran las cotas</param>
        /// <param name="columna">Columna de la cual se eliminarán las cotas</param>
        public static void EliminarCotasAnteriores(Document doc, FamilyInstance columna)
        {
            if (!LeerDatos(columna, out long idCotaX, out long idCotaY, out long idSuelo))
                return;

            try
            {
                ElementId elemIdCotaX = new ElementId(idCotaX);
                if (doc.GetElement(elemIdCotaX) != null)
                    doc.Delete(elemIdCotaX);
            }
            catch { }

            try
            {
                ElementId elemIdCotaY = new ElementId(idCotaY);
                if (doc.GetElement(elemIdCotaY) != null)
                    doc.Delete(elemIdCotaY);
            }
            catch { }
        }

    }

}