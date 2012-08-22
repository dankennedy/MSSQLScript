using System;
using System.Collections;
using System.Linq;
using Microsoft.SqlServer.Management.Smo;

namespace MSSQLScript.Scripters
{
    internal class CheckConstraintScripter : ScripterBase
    {
        public CheckConstraintScripter(ICollection objects) : base(objects, DatabaseObjectType.CheckConstraint)
        {

        }

        TableCollection Tables { get { return (TableCollection)Objects; } }

        public override void Script()
        {
            // nothing to do here, individual drop and create statements will get handled
            // by the drop/create of the table
        }

        protected override ScriptNameObjectBase GetObjectByName(string schema, string name)
        {
            foreach (Table table in Tables)
                foreach (var checkConstraint in table.Checks.Cast<Check>())
                {
                    if (name.Equals(checkConstraint.Name, StringComparison.OrdinalIgnoreCase)) 
                        return checkConstraint;
                }
            return null;
        }

        protected override bool CheckIsSystemObject(ScriptNameObjectBase dbobject)
        {
            return false;
        }
    }

}