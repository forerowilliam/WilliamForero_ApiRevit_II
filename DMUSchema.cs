using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using System;

namespace WilliamForero_ApiRevit_II
{
    public static class DMUSchema
    {
        // GUID fijo, generado una sola vez, nunca modificar
        private static readonly Guid SchemaGuid = new Guid("e47c4741-2a1e-416c-bb13-3adc8acd8f7b");
        private const string SchemaName = "DMU_ColumnaSchema";

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

            idCotaX = entidad.Get<long>("IdCotaX");
            idCotaY = entidad.Get<long>("IdCotaY");
            idSuelo = entidad.Get<long>("IdSuelo");

            return true;
        }


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