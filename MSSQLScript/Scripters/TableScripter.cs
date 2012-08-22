using System;
using System.Collections;
using System.IO;
using Microsoft.SqlServer.Management.Smo;

namespace MSSQLScript.Scripters
{
    internal class TableScripter : ScripterBase
    {
        public TableScripter(ICollection objects) : base(objects, DatabaseObjectType.Table)
        {
            ScriptOptionsCreate.DriAll = true;
            ScriptOptionsCreate.ExtendedProperties = true;
            ScriptOptionsCreate.NoCollation = true;
            ScriptOptionsCreate.Indexes = true;
            ScriptOptionsCreate.NoFileGroup = true;
            ScriptOptionsCreate.SchemaQualifyForeignKeysReferences = true;

            Tables.Parent.Parent.SetDefaultInitFields(typeof(Table), new[] { "Name", "Schema" });
        }

        TableCollection Tables { get { return (TableCollection)Objects; } }

        public override void Script()
        {
            ScriptOptionsDrop.ToFileOnly = true;
            ScriptOptionsDrop.AppendToFile = false;

            ScriptOptionsCreate.ToFileOnly = true;
            ScriptOptionsCreate.AppendToFile = true;

            foreach (Table table in Tables)
            {                
                if (Filter.IsMatch(table.Name))
                {
                    if (!CheckIsSystemObject(table))
                    {
                        ScriptOptionsDrop.FileName = Path.Combine(OutputFolder,
                                                                  string.Format("{0}.{1}.sql", table.Schema, table.Name));
                        LogMessage("Scripting table '{0}.{1}' to {2}", table.Schema, table.Name,
                                   ScriptOptionsDrop.FileName);
                        PrepareOutputFile(ScriptOptionsDrop.FileName);
                        table.Script(ScriptOptionsDrop);
                        ScriptOptionsCreate.FileName = ScriptOptionsDrop.FileName;
                        table.Script(ScriptOptionsCreate);
                    }
                    else
                    {
                        if (Program.Options.Verbose)
                            LogMessage("Skipping table '{0}.{1}'. System object.", table.Schema, table.Name);
                    }
                }
                else
                {
                    if (Program.Options.Verbose)
                        LogMessage("Skipping table '{0}.{1}'. Filtered object.", table.Schema, table.Name);
                }
            }

            Console.WriteLine("Scripting tables complete");
        }
        
        protected override ScriptNameObjectBase GetObjectByName(string schema, string name)
        {
            foreach (Table table in Tables)
            {
                if (table.Schema.Equals(schema, StringComparison.OrdinalIgnoreCase) &&
                    table.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return table;
            }
            return null;
        }

        protected override bool CheckIsSystemObject(ScriptNameObjectBase dbobject)
        {
            var o = dbobject as Table;
            if (o == null)
                return true;

            return o.Schema.Equals("sys") || o.Name.Equals("dtproperties") || o.Name.Equals("sysdiagrams");
        }
    }
}