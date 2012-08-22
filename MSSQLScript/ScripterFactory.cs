using System;
using System.Collections;
using MSSQLScript.Scripters;

namespace MSSQLScript
{
    /// <summary>
    ///   Static class used to generate ScripterBase classes based on the type
    ///   of objects being scripted
    /// </summary>
    internal static class ScripterFactory
    {
        /// <summary>
        ///   Creates a new ScripterBase base class of the type appropriate to
        ///   generate script for the SchemaCollection
        /// </summary>
        public static ScripterBase CreateScripter(ICollection objects, DatabaseObjectType objectType)
        {
            switch (objectType)
            {
                case DatabaseObjectType.Table:
                    return new TableScripter(objects);
                case DatabaseObjectType.View:
                    return new ViewScripter(objects);
                case DatabaseObjectType.StoredProcedure:
                    return new StoredProcedureScripter(objects);
                case DatabaseObjectType.UserDefinedFunction:
                    return new UserDefinedFunctionScripter(objects);
                case DatabaseObjectType.Schema:
                    return new SchemaScripter(objects);
                default:
                    throw new ArgumentOutOfRangeException("objectType");
            }
        }
    }

}