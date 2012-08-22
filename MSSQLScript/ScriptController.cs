using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Threading;
using MSSQLScript.Scripters;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

namespace MSSQLScript
{
    /// <summary>
    ///   Main controller class that initiates the scripting of database objects
    /// </summary>
    internal class ScriptController
    {
        private readonly CommandLineArguments options;

        private delegate void ScripterScriptHandler();

        private delegate void FullDatabaseScriptHandler(string filePath, IList<ScriptEntity> entityTree);

        /// <summary>
        ///   Creates a new instance with the specified options set
        /// </summary>
        /// <param name = "options"></param>
        public ScriptController(CommandLineArguments options)
        {
            this.options = options;
        }

        private Server GetServerConnection()
        {
            if (options.TrustedConnection)
                return new Server(options.Server);
            
            return new Server(new ServerConnection(options.Server, options.UserId, options.Password));
        }

        /// <summary>
        ///   Synchronous method that calls the scripting of each database object 
        ///   type asynnchronously
        /// </summary>
        public void Start()
        {
            Console.WriteLine("Connecting to '{0}'...", options.Server);

            var targetServer = GetServerConnection();

            Console.WriteLine("Connected to '{0}' OK. System version {1}. Opening database '{2}'...",
                                options.Server,
                                targetServer.Information.VersionString,
                                options.Database);

            var targetDatabase = targetServer.Databases[options.Database];
            try
            {
                Console.WriteLine("Opened '{0}' OK. Building object dependency tree...", targetDatabase.Name);
            }
            catch (Exception)
            {
                Console.WriteLine("Failed to open '{0}'. Check your connection details.", options.Database);
                return;                
            }
            
            var entityTree = BuildEntityTree(targetDatabase);

            // collection of wait handles to monitor while scripting completes
            var waitHandles = new List<WaitHandle>();

            if (options.GenerateFullDatabaseScript)
            {
                var fullDatabaseScriptPath = String.Concat(
                    Path.Combine(options.OutputDirectory, targetDatabase.Name), ".sql");

                Console.WriteLine("Generating full database script to '{0}'...", fullDatabaseScriptPath);

                FullDatabaseScriptHandler asyncHandler = ScriptFullDatabase;
                waitHandles.Add(asyncHandler.BeginInvoke(fullDatabaseScriptPath, entityTree, null, null).AsyncWaitHandle);
            }

            // a new instance of the server and database objects are created because each forces
            // a new connection to the database which is required as there are multiple
            // datareaders open at any one time

            if (options.GenerateSeparateDatabaseScripts)
            {
                foreach (var scripter in GetScripters())
                {
                    ScripterScriptHandler asyncHandler = scripter.Script;
                    waitHandles.Add(asyncHandler.BeginInvoke(null, null).AsyncWaitHandle);
                }
            }

            WaitHandle.WaitAll(waitHandles.ToArray());

            Console.WriteLine("All scripting complete");
        }

        private IList<ScriptEntity> BuildEntityTree(Database targetDatabase)
        {
            string sql;
            using (var infoStream = Assembly.GetExecutingAssembly()
                                         .GetManifestResourceStream(
                                            GetType().Namespace + ".GetDatabaseObjectInfo.sql"))
            {
                Console.WriteLine("Opened resource stream OK. Loading script");

                if (infoStream == null)
                    throw new MissingManifestResourceException("Failed to load SQL script required to discover dependencies");

                using (var infoStreamReader = new StreamReader(infoStream))
                    sql = infoStreamReader.ReadToEnd();
            }

            var entityTree = new List<ScriptEntity>();

            Console.WriteLine("Script loaded OK. Executing..");
            var infoResults = targetDatabase.ExecuteWithResults(sql);

            Console.WriteLine("Executed OK. Loading dependencies..");

            // add schemas
            foreach (DataRow schemaRow in infoResults.Tables[0].Rows)
                entityTree.Add(new ScriptEntity
                {
                    DatabaseObjectType = DatabaseObjectType.Schema,
                    Name = (string)schemaRow["name"]
                });

            // add all other object types
            foreach (DataRow row in infoResults.Tables[1].Rows)
                entityTree.Add(new ScriptEntity
                {
                    DatabaseObjectType = (DatabaseObjectType)Enum.Parse(typeof(DatabaseObjectType), 
                                            (string)row["database_object_type"]),
                    Schema = (string)row["schema_name"],
                    Name = (string)row["name"]
                });


            // build dependencies
            foreach (DataRow row in infoResults.Tables[2].Rows)
            {
                var objectType = (DatabaseObjectType)Enum.Parse(typeof (DatabaseObjectType),
                                                                (string)row["referencing_database_object_type"]);
                var schema = (string)row["referencing_schema_name"];
                var name = (string)row["referencing_entity_name"];
                
                var referencingItem =
                    entityTree.FirstOrDefault(
                        x => x.DatabaseObjectType == objectType && x.Schema.Equals(schema, StringComparison.OrdinalIgnoreCase) && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (referencingItem == null)
                {
                    Console.WriteLine("Failed to find referencing item {0}, {1}.{2}", objectType, schema, name);
                    continue;
                }

                objectType = (DatabaseObjectType)Enum.Parse(typeof(DatabaseObjectType),
                                                                (string)row["referenced_database_object_type"]);
                schema = (string)row["referenced_schema_name"];
                name = (string)row["referenced_entity_name"];

                var referencedItem =
                    entityTree.FirstOrDefault(
                        x => x.DatabaseObjectType == objectType && x.Schema.Equals(schema, StringComparison.OrdinalIgnoreCase) && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (referencedItem == null)
                {
                    Console.WriteLine("Failed to find referenced item {0}, {1}.{2}", objectType, schema, name);
                    continue;
                }

                if (!referencedItem.EntitiesWhichDependOnMe.Contains(referencingItem))
                    referencedItem.EntitiesWhichDependOnMe.Add(referencingItem);

                if (!referencingItem.EntitiesWhichIDependOn.Contains(referencedItem))
                    referencingItem.EntitiesWhichIDependOn.Add(referencedItem);
            }

            return entityTree;
        }

        /// <summary>
        ///   Scripts drops and creates in one file
        /// </summary>
        private void ScriptFullDatabase(string filePath, IList<ScriptEntity> entityTree)
        {
            var targetServer = GetServerConnection();

            var scripters = GetScripters();

            FileAttributes attributes = File.GetAttributes(filePath);

            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                File.SetAttributes(filePath, attributes ^ FileAttributes.ReadOnly);

            if (File.Exists(filePath))
                File.Delete(filePath);

            Console.WriteLine("Scripting drops for full database...");

            for (var scripterIndex = 0; scripterIndex < scripters.Count; scripterIndex++)
                ScripterBase.OutputStringCollectionToFile(
                    scripters[scripterIndex].ScriptDrops(targetServer, entityTree), filePath, true);

            Console.WriteLine("Scripting creates for full database...");

            // reset scripted flag
            foreach (var scriptEntity in entityTree)
                scriptEntity.HasBeenScripted = false;

            for (var scripterIndex = scripters.Count - 1; scripterIndex >= 0; scripterIndex--)
                ScripterBase.OutputStringCollectionToFile(
                    scripters[scripterIndex].ScriptCreates(targetServer, entityTree), filePath, true);

            Console.WriteLine("Full database script complete");
        }

        private IList<ScripterBase> GetScripters()
        {
            var types = options.GetTypes();
            var scripters = new List<ScripterBase>(types.Count);

            // each scripter gets it's own SMO connection to allow multi threaded scripting
            // so don't replace each call to GetServerConnection().Databases[options.Database]
            // with a variable!

            if (types.Contains(DatabaseObjectType.UserDefinedFunction))
                scripters.Add(ScripterFactory.CreateScripter(
                    GetServerConnection().Databases[options.Database].UserDefinedFunctions, DatabaseObjectType.UserDefinedFunction));
            
            if (types.Contains(DatabaseObjectType.StoredProcedure))
                scripters.Add(ScripterFactory.CreateScripter(
                    GetServerConnection().Databases[options.Database].StoredProcedures, DatabaseObjectType.StoredProcedure));

            if (types.Contains(DatabaseObjectType.View))
                scripters.Add(ScripterFactory.CreateScripter(
                    GetServerConnection().Databases[options.Database].Views, DatabaseObjectType.View));

            if (types.Contains(DatabaseObjectType.Table))
                scripters.Add(ScripterFactory.CreateScripter(
                    GetServerConnection().Databases[options.Database].Tables, DatabaseObjectType.Table));

            if (types.Contains(DatabaseObjectType.Schema))
                scripters.Add(ScripterFactory.CreateScripter(
                    GetServerConnection().Databases[options.Database].Schemas, DatabaseObjectType.Schema));

            return scripters;
        }
    }
}