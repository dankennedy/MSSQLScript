using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace MSSQLScript
{
    internal class CommandLineArguments : CommandLineArgumentsBase
    {
        private const string ARG_HELP = "?";
        private const string ARG_SERVER = "s";
        private const string ARG_DATABASE = "d";
        private const string ARG_TRUSTEDCONNECTION = "e";
        private const string ARG_USERID = "u";
        private const string ARG_PASSWORD = "p";
        private const string ARG_OUTDIR = "o";
        private const string ARG_FULL = "full";
        private const string ARG_SEP = "sep";
        private const string ARG_TYPES = "types";
        private const string ARG_FILTER = "filter";
        private const string ARG_VERBOSE = "v";

        private static readonly string[] AllArgs = {
                                                       ARG_HELP,
                                                       ARG_SERVER,
                                                       ARG_DATABASE,
                                                       ARG_TRUSTEDCONNECTION,
                                                       ARG_USERID,
                                                       ARG_PASSWORD,
                                                       ARG_OUTDIR,
                                                       ARG_FULL,
                                                       ARG_SEP,
                                                       ARG_TYPES,
                                                       ARG_FILTER,
                                                       ARG_VERBOSE
                                                   };

        /// <summary>
        ///   Creates a new instance from the tokenized array of command line arguments
        /// </summary>
        /// <param name = "args">Tokenized arguments</param>
        public CommandLineArguments(string[] args) : base(args)
        {
            Array.Sort(AllArgs);
        }

        /// <summary>
        ///   Creates a new instance from the command line string used to execute the application
        /// </summary>
        /// <param name = "commandLine">Full command line string including the executable path and arguments</param>
        public CommandLineArguments(string commandLine) : base(commandLine)
        {
            Array.Sort(AllArgs);
        }

        #region Argument Properties

        /// <summary>
        ///   Was help or usage options requested
        /// </summary>
        public bool HelpRequested
        {
            get { return Parameters.ContainsKey(ARG_HELP); }
        }

        /// <summary>
        ///   Name of SQL Server instance to connect to
        /// </summary>
        public string Server
        {
            get { return Parameters[ARG_SERVER] ?? String.Empty; }
        }

        /// <summary>
        ///   Name of database to connect to
        /// </summary>
        public string Database
        {
            get { return Parameters[ARG_DATABASE] ?? String.Empty; }
        }

        /// <summary>
        ///   Whether to use integrated security when connecting
        /// </summary>
        public bool TrustedConnection
        {
            get
            {
                return Parameters.ContainsKey(ARG_TRUSTEDCONNECTION) ||
                       (string.IsNullOrEmpty(UserId) && string.IsNullOrEmpty(Password));
            }
        }

        /// <summary>
        ///   User name of standard SQL security
        /// </summary>
        public string UserId
        {
            get { return Parameters[ARG_USERID] ?? String.Empty; }
        }

        /// <summary>
        ///   Password of standard SQL security
        /// </summary>
        public string Password
        {
            get { return Parameters[ARG_PASSWORD] ?? String.Empty; }
        }

        /// <summary>
        ///   Path to root temp directory to use for output files
        /// </summary>
        public string OutputDirectory
        {
            get { return Parameters[ARG_OUTDIR] ?? Path.GetTempPath(); }
        }

        /// <summary>
        ///   Whether to output verbose debugging/trace statements
        /// </summary>
        public bool Verbose
        {
            get { return Parameters.ContainsKey(ARG_VERBOSE); }
        }

        /// <summary>
        ///   Whether to generate one file containing the script to create all database objects
        /// </summary>
        public bool GenerateFullDatabaseScript
        {
            get { return Parameters.ContainsKey(ARG_FULL); }
        }

        /// <summary>
        ///   Whether to generate one file per database object
        /// </summary>
        public bool GenerateSeparateDatabaseScripts
        {
            get { return Parameters.ContainsKey(ARG_SEP); }
        }

        /// <summary>
        ///   Types to script as searchable list
        /// </summary>
        public List<DatabaseObjectType> GetTypes()
        {
            var types = new List<DatabaseObjectType>();
            foreach (var enumCode in TypeString.Split(','))
                types.Add((DatabaseObjectType)(int.Parse(enumCode)));
            return types;
        }

        /// <summary>
        ///   Types to script as comma delimited string
        /// </summary>
        public string TypeString
        {
            get
            {
                if (Parameters[ARG_TYPES] == null)
                {
                    var concatenatedValues = string.Empty;
                    foreach (int enumValue in Enum.GetValues(typeof (DatabaseObjectType)))
                        concatenatedValues += enumValue.ToString() + ",";
                    return concatenatedValues.Substring(0, concatenatedValues.Length - 1);
                }
                
                return Parameters[ARG_TYPES];
            }
        }

        /// <summary>
        ///   Inclusive regular expression filter for object names
        /// </summary>
        public string Filter
        {
            get { return Parameters[ARG_FILTER] ?? String.Empty; }
        }

        /// <summary>
        ///   The message to display in the console describing how to run the program
        /// </summary>
        public override string UsageMessage
        {
            get
            {
                return string.Concat("MSSQLScript - Command line SQL Server scripting utility\r\n\r\n",
                                     "Usage: MSSQLScript.exe\r\n",
                                     "             [-?]\r\n",
                                     "             [-s server]\r\n",
                                     "             [-d database]\r\n",
                                     "             [-e use trusted connection]\r\n",
                                     "             [-u sql server userid]\r\n",
                                     "             [-p sql server password]\r\n",
                                     "             [-o output directory]\r\n",
                                     "             [-sep output separate file per object]\r\n",
                                     "             [-full output one file for all objects]\r\n",
                                     "             [-types comma separated list of objects to script]\r\n",
                                     "             [-filter regular expression to filter the objects selected]\r\n",
                                     "             [-v verbose logging]\r\n",
                                     "\r\n",
                                     "Types:\r\n",
                                     "Pass the following codes to the \"types\" parameter or leave blank for all:\r\n",
                                     "1 = Tables\r\n",
                                     "2 = Views\r\n",
                                     "3 = Stored Procedures\r\n",
                                     "4 = User Defined Functions\r\n",
                                     "5 = Schemas\r\n",
                                     "\r\n",
                                     "Examples:\r\n",
                                     "MSSQLScript.exe -s SERVER -d DATABASE -sep -full -types 1,2,3\r\n",
                                     "MSSQLScript.exe -s SERVER -d DATABASE -u UserID -p Password -tmp C:\\Temp -sep -full\r\n");
            }
        }

        #endregion

        #region Methods

        public override bool Validate(IList validationErrors)
        {
            if (validationErrors == null)
                validationErrors = new List<string>();

            var errorCountOnEntry = validationErrors.Count;

            foreach (string param in Parameters.Keys)
            {
                if (Array.BinarySearch(AllArgs, param) < 0)
                    validationErrors.Add(String.Format("Invalid argument '{0}'", param));
            }

            if (string.IsNullOrEmpty(Server) || string.IsNullOrEmpty(Database))
                validationErrors.Add(
                    "Invalid connection information supplied. Please specify server and database to connect to.");

            if (!GenerateFullDatabaseScript && !GenerateSeparateDatabaseScripts)
                validationErrors.Add(
                    "No scripting options selected. Please specify full database script or separate files for each object.");

            if (!TrustedConnection && (string.IsNullOrEmpty(UserId)))
                validationErrors.Add("No user id specified for sql user connection.");

            if (!Regex.IsMatch(TypeString, "^[\\d,]+$"))
                validationErrors.Add("Invalid types parameters. Specify a comma separated list of codes with no spaces.");

            return validationErrors.Count == errorCountOnEntry;
        }

        #endregion
    }
}