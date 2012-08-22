using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.SqlServer.Management.Smo;

namespace MSSQLScript.Scripters
{
    internal abstract class ScripterBase
    {
        protected static object SyncRoot = new object();

        public DatabaseObjectType ObjectType { get; set; }
        public ICollection Objects { get; set; }
        public Regex Filter { get; set; }
        public ScriptingOptions ScriptOptionsDrop { get; set; }
        public ScriptingOptions ScriptOptionsCreate { get; set; }
        public string OutputFolder { get; set; }

        protected ScripterBase(ICollection objects, DatabaseObjectType objectType)
        {
            ObjectType = objectType;
            Objects = objects;
            OutputFolder = Path.Combine(Program.Options.OutputDirectory, objectType.ToString());

            Filter = new Regex(Program.Options.Filter, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            ScriptOptionsDrop = new ScriptingOptions
                                    {
                                        ScriptDrops = true,
                                        IncludeIfNotExists = true,
                                        AllowSystemObjects = false,
                                        NoCommandTerminator = false,
                                        AgentJobId = false,
                                        Statistics = false,
                                        ContinueScriptingOnError = true
                                    };

            ScriptOptionsCreate = new ScriptingOptions
                                      {
                                          ScriptDrops = false,
                                          SchemaQualify = true,
                                          AllowSystemObjects = false,
                                          NoCommandTerminator = false,
                                          ContinueScriptingOnError = true
                                      };

            lock (SyncRoot)
            {
                if (OutputFolder.IndexOf("\\", System.StringComparison.Ordinal) > -1 && !Directory.Exists(OutputFolder))
                    Directory.CreateDirectory(OutputFolder);
            }
        }

        public abstract void Script();

        protected abstract ScriptNameObjectBase GetObjectByName(string schema, string name);

        protected virtual StringCollection ScriptImpl(Server server, 
            ScriptingOptions scriptOptions, 
            IList<ScriptEntity> entityTree, 
            bool parents)
        {
            var output = new StringCollection();

            foreach (var entity in entityTree.Where(x => x.DatabaseObjectType == ObjectType))
            {
                RecurseScript(server, output,
                    GetObjectByName(entity.Schema, entity.Name), scriptOptions, entity, parents);
            }

            return output;
        }

        protected abstract bool CheckIsSystemObject(ScriptNameObjectBase dbobject);

        internal static void OutputStringCollectionToFile(StringCollection output, string filePath, bool append)
        {
            using (var outputFile = new StreamWriter(filePath, append))
            {
                foreach (var entry in output)
                {
                    outputFile.WriteLine(entry);
                    if (entry.StartsWith("SET ANSI_NULLS") || entry.StartsWith("SET QUOTED_IDENTIFIER"))
                        outputFile.WriteLine("GO");
                }
            }
        }

        public virtual StringCollection ScriptCreates(Server server, IList<ScriptEntity> entityTree)
        {
            ScriptOptionsCreate.ToFileOnly = false;
            ScriptOptionsDrop.AppendToFile = false;

            return ScriptImpl(server, ScriptOptionsCreate, entityTree, true);
        }

        public virtual StringCollection ScriptDrops(Server server, IList<ScriptEntity> entityTree)
        {
            ScriptOptionsCreate.ToFileOnly = false;
            ScriptOptionsDrop.AppendToFile = false;

            return ScriptImpl(server, ScriptOptionsDrop, entityTree, false);
        }

        /// <summary>
        ///   Recursively scripts all objects in the collection while walking the dependency tree to ensure
        ///   child or parent objects are dropped/created appropriately
        /// </summary>
        protected virtual void RecurseScript(Server server, 
                                             StringCollection output,
                                             ScriptNameObjectBase dbobject, 
                                             ScriptingOptions scriptOptions,
                                             ScriptEntity entity,
                                             bool parents)
        {
            if (dbobject == null || entity == null)
                return;

            LogMessage("Checking {0} '{1}'", dbobject.GetType().Name, dbobject.Name);

            if (CheckIsSystemObject(dbobject) || entity.HasBeenScripted || !Filter.IsMatch(dbobject.Name)) 
                return;

            entity.HasBeenScripted = true;

            var dependencies = parents ? entity.EntitiesWhichIDependOn : entity.EntitiesWhichDependOnMe;

            foreach (var dependencyEntity in dependencies)
            {
                if (dependencyEntity != entity &&
                    dependencyEntity.HasBeenScripted == false)
                {
                    RecurseScript(server,
                                  output,
                                  GetObjectByName(dependencyEntity.Schema, dependencyEntity.Name),
                                  scriptOptions,
                                  dependencyEntity,
                                  parents);
                }
            }                

            LogMessage("Scripting {0} '{1}'", dbobject.GetType().Name, dbobject.Name);

            var scriptOutput = ((IScriptable)dbobject).Script(scriptOptions);
            var entries = new string[scriptOutput.Count + 1];
            scriptOutput.CopyTo(entries, 0);
            entries[entries.Length - 1] = "GO";
            output.AddRange(entries);
        }

        protected void LogMessage(string format, params string[] args)
        {
            LogMessage(String.Format(format, args));
        }

        protected void LogMessage(string msg)
        {
            Console.WriteLine(String.Concat("ThreadId:", 
                Thread.CurrentThread.ManagedThreadId, " ", msg));
        }

        protected void PrepareOutputFile(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            FileAttributes attributes = File.GetAttributes(filePath);

            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                File.SetAttributes(filePath, attributes ^ FileAttributes.ReadOnly);

            File.Delete(filePath);
        }

    }
}