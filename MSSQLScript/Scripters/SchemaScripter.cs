using System;
using System.Collections;
using System.IO;
using Microsoft.SqlServer.Management.Smo;

namespace MSSQLScript.Scripters
{
    internal class SchemaScripter : ScripterBase
    {
        static readonly string[] SystemSchemaNames = new[] { "dbo", "guest", "INFORMATION_SCHEMA", "sys", };

        public SchemaScripter(ICollection objects) : base(objects, DatabaseObjectType.Schema) { }

        SchemaCollection Schemas { get { return (SchemaCollection)Objects; } }

        public override void Script()
        {
            ScriptOptionsDrop.ToFileOnly = true;
            ScriptOptionsDrop.AppendToFile = false;

            ScriptOptionsCreate.ToFileOnly = true;
            ScriptOptionsCreate.AppendToFile = true;

            foreach (Schema schema in Schemas)
            {
                if (Filter.IsMatch(schema.Name))
                {
                    if (!CheckIsSystemObject(schema))
                    {
                        ScriptOptionsDrop.FileName = Path.Combine(OutputFolder, string.Format("{0}.sql", schema.Name));
                        LogMessage("Scripting schema '{0}' to {1}", schema.Name, ScriptOptionsDrop.FileName);
                        PrepareOutputFile(ScriptOptionsDrop.FileName);
                        schema.Script(ScriptOptionsDrop);
                        ScriptOptionsCreate.FileName = ScriptOptionsDrop.FileName;
                        schema.Script(ScriptOptionsCreate);
                    }
                    else
                    {
                        if (Program.Options.Verbose)
                            LogMessage("Skipping schema '{0}'. System object.", schema.Name);
                    }
                }
                else
                {
                    if (Program.Options.Verbose)
                        LogMessage("Skipping schema '{0}'. Filtered object.", schema.Name);
                }
            }

            Console.WriteLine("Scripting schemas complete");
        }

        protected override ScriptNameObjectBase GetObjectByName(string schema, string name)
        {            
            return Schemas[name];
        }

        protected override bool CheckIsSystemObject(ScriptNameObjectBase dbobject)
        {
            var o = dbobject as Schema;
            if (o == null)
                return true;

            return Array.BinarySearch(SystemSchemaNames, o.Name) > -1 || o.Name.StartsWith("db_");
        }
    }
}