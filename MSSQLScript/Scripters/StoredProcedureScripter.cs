using System;
using System.Collections;
using System.IO;
using Microsoft.SqlServer.Management.Smo;

namespace MSSQLScript.Scripters
{
    internal class StoredProcedureScripter : ScripterBase
    {
        public StoredProcedureScripter(ICollection objects) : base(objects, DatabaseObjectType.StoredProcedure)
        {
            StoredProcedures.Parent.Parent.SetDefaultInitFields(typeof(StoredProcedure),
                                                                new[] { "Name", "Schema" });
        }

        StoredProcedureCollection StoredProcedures
        {
            get { return (StoredProcedureCollection)Objects; }
        }

        public override void Script()
        {
            ScriptOptionsDrop.ToFileOnly = true;
            ScriptOptionsDrop.AppendToFile = false;

            ScriptOptionsCreate.ToFileOnly = true;
            ScriptOptionsCreate.AppendToFile = true;

            foreach (StoredProcedure proc in StoredProcedures)
            {
                if (Filter.IsMatch(proc.Name))
                {
                    if (!CheckIsSystemObject(proc))
                    {
                        ScriptOptionsDrop.FileName = Path.Combine(OutputFolder,
                                                                  string.Format("{0}.{1}.sql", proc.Schema, proc.Name));
                        LogMessage("Scripting stored procedure '{0}.{1}' to {2}", proc.Schema, proc.Name,
                                   ScriptOptionsDrop.FileName);
                        PrepareOutputFile(ScriptOptionsDrop.FileName);
                        proc.Script(ScriptOptionsDrop);
                        ScriptOptionsCreate.FileName = ScriptOptionsDrop.FileName;
                        proc.Script(ScriptOptionsCreate);
                    }
                    else
                    {
                        if (Program.Options.Verbose)
                            LogMessage("Skipping stored procedure '{0}.{1}'. System object.", proc.Schema, proc.Name);
                    }
                }
                else
                {
                    if (Program.Options.Verbose)
                        LogMessage("Skipping stored procedure '{0}.{1}'. Filtered object.", proc.Schema, proc.Name);
                }
            }

            Console.WriteLine("Scripting stored procedures complete");
        }
        
        protected override ScriptNameObjectBase GetObjectByName(string schema, string name)
        {
            foreach (StoredProcedure storedProcedure in StoredProcedures)
            {
                if (storedProcedure.Schema.Equals(schema, StringComparison.OrdinalIgnoreCase) && 
                    storedProcedure.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return storedProcedure;
            }
            return null;
        }

        protected override bool CheckIsSystemObject(ScriptNameObjectBase dbobject)
        {
            var o = dbobject as StoredProcedure;
            if (o == null)
                return true;

            return o.Schema.Equals("sys") || o.Name.StartsWith("dt_") ||
                   (o.Schema.Equals("dbo") && o.Name.StartsWith("sp_"));
        }
    }
}