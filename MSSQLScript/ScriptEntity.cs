using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MSSQLScript
{
    [DebuggerDisplay("{DatabaseObjectType}, {Schema}.{Name}")]
    internal class ScriptEntity : IEquatable<ScriptEntity>
    {
        public ScriptEntity()
        {
            Name = string.Empty;
            Schema = string.Empty;
            HasBeenScripted = false;
            EntitiesWhichDependOnMe = new List<ScriptEntity>();
            EntitiesWhichIDependOn = new List<ScriptEntity>();
        }

        public DatabaseObjectType DatabaseObjectType { get; set; }

        public string Name { get; set; }

        public string Schema { get; set; }

        public bool HasBeenScripted { get; set; }

        public IList<ScriptEntity> EntitiesWhichIDependOn { get; set; }

        public IList<ScriptEntity> EntitiesWhichDependOnMe { get; set; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (ScriptEntity)) return false;
            return Equals((ScriptEntity)obj);
        }

        public bool Equals(ScriptEntity other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.DatabaseObjectType, DatabaseObjectType) && string.Equals(other.Name, Name, StringComparison.OrdinalIgnoreCase) && string.Equals(other.Schema, Schema, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = DatabaseObjectType.GetHashCode();
                result = (result*397) ^ (Name != null ? Name.ToLowerInvariant().GetHashCode() : 0);
                result = (result * 397) ^ (Schema != null ? Schema.ToLowerInvariant().GetHashCode() : 0);
                return result;
            }
        }

        public static bool operator ==(ScriptEntity left, ScriptEntity right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ScriptEntity left, ScriptEntity right)
        {
            return !Equals(left, right);
        }
    }
}