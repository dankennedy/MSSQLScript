using System;
using System.Collections;
using System.IO;
using Microsoft.SqlServer.Management.Smo;

namespace MSSQLScript.Scripters
{
    internal class UserDefinedFunctionScripter : ScripterBase
    {
        public UserDefinedFunctionScripter(ICollection objects) : base(objects, DatabaseObjectType.UserDefinedFunction)
        {
            UserDefinedFunctions.Parent.Parent.SetDefaultInitFields(typeof(UserDefinedFunction),
                                                                            new[] { "Name", "Schema" });
        }

        private UserDefinedFunctionCollection UserDefinedFunctions
        {
            get { return (UserDefinedFunctionCollection)Objects; }
        }

        public override void Script()
        {
            ScriptOptionsDrop.ToFileOnly = true;
            ScriptOptionsDrop.AppendToFile = false;

            ScriptOptionsCreate.ToFileOnly = true;
            ScriptOptionsCreate.AppendToFile = true;

            foreach (UserDefinedFunction udf in UserDefinedFunctions)
            {
                if (Filter.IsMatch(udf.Name))
                {
                    if (!CheckIsSystemObject(udf))
                    {
                        ScriptOptionsDrop.FileName = Path.Combine(OutputFolder,
                                                                  string.Format("{0}.{1}.sql", udf.Schema, udf.Name));
                        LogMessage("Scripting user defined function '{0}.{1}' to {2}", udf.Schema, udf.Name,
                                   ScriptOptionsDrop.FileName);
                        PrepareOutputFile(ScriptOptionsDrop.FileName);
                        udf.Script(ScriptOptionsDrop);
                        ScriptOptionsCreate.FileName = ScriptOptionsDrop.FileName;
                        udf.Script(ScriptOptionsCreate);
                    }
                    else
                    {
                        if (Program.Options.Verbose)
                            LogMessage("Skipping user defined function '{0}.{1}'. System object.", udf.Schema, udf.Name);
                    }
                }
                else
                {
                    if (Program.Options.Verbose)
                        LogMessage("Skipping user defined function '{0}.{1}'. Filtered object.", udf.Schema, udf.Name);
                }
            }

            Console.WriteLine("Scripting user defined functions complete");
        }
        
        protected override ScriptNameObjectBase GetObjectByName(string schema, string name)
        {
            foreach (UserDefinedFunction udf in UserDefinedFunctions)
            {
                if (udf.Schema.Equals(schema, StringComparison.OrdinalIgnoreCase) &&
                    udf.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return udf;
            }
            return null;
        }

        protected override bool CheckIsSystemObject(ScriptNameObjectBase dbobject)
        {
            var o = dbobject as UserDefinedFunction;
            if (o == null)
                return true;

            return o.Schema.Equals("sys") || o.Name.Equals("fn_diagramobjects");
        }
    }
}