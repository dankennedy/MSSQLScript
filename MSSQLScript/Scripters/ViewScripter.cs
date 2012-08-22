using System;
using System.Collections;
using System.IO;
using Microsoft.SqlServer.Management.Smo;

namespace MSSQLScript.Scripters
{
    internal class ViewScripter : ScripterBase
    {
        public ViewScripter(ICollection objects) : base(objects, DatabaseObjectType.View)
        {
            Views.Parent.Parent.SetDefaultInitFields(typeof(View), new[] { "Name", "Schema" });
        }

        ViewCollection Views { get { return (ViewCollection)Objects; } }

        public override void Script()
        {
            ScriptOptionsDrop.ToFileOnly = true;
            ScriptOptionsDrop.AppendToFile = false;

            ScriptOptionsCreate.ToFileOnly = true;
            ScriptOptionsCreate.AppendToFile = true;

            foreach (View vw in Views)
            {
                if (Filter.IsMatch(vw.Name))
                {
                    if (!CheckIsSystemObject(vw))
                    {
                        ScriptOptionsDrop.FileName = Path.Combine(OutputFolder,
                                                                  string.Format("{0}.{1}.sql", vw.Schema, vw.Name));
                        LogMessage("Scripting view '{0}.{1}' to {2}", vw.Schema, vw.Name, ScriptOptionsDrop.FileName);
                        PrepareOutputFile(ScriptOptionsDrop.FileName);
                        vw.Script(ScriptOptionsDrop);
                        ScriptOptionsCreate.FileName = ScriptOptionsDrop.FileName;
                        vw.Script(ScriptOptionsCreate);
                    }
                    else
                    {
                        if (Program.Options.Verbose)
                            LogMessage("Skipping view '{0}.{1}'. System object.", vw.Schema, vw.Name);
                    }
                }
                else
                {
                    if (Program.Options.Verbose)
                        LogMessage("Skipping view '{0}.{1}'. Filtered object.", vw.Schema, vw.Name);
                }
            }

            Console.WriteLine("Scripting views complete");
        }
        
        protected override ScriptNameObjectBase GetObjectByName(string schema, string name)
        {
            foreach (View view in Views)
            {
                if (view.Schema.Equals(schema, StringComparison.OrdinalIgnoreCase) &&
                    view.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return view;
            }
            return null;
        }

        protected override bool CheckIsSystemObject(ScriptNameObjectBase dbobject)
        {
            var o = dbobject as View;
            if (o == null)
                return true;

            return o.Schema.Equals("sys") || o.Schema.Equals("INFORMATION_SCHEMA");
        }
    }
}