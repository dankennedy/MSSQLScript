using System;
using System.Collections.Generic;

namespace MSSQLScript
{
    /// <summary>
    ///   Application instance
    /// </summary>
    internal class Program
    {
        public static CommandLineArguments Options { get; private set; }

        /// <summary>
        ///   Application entry point
        /// </summary>
        /// <param name = "args">Command line arguments</param>
        [MTAThread]
        private static void Main(string[] args)
        {
            try
            {
                Options = new CommandLineArguments(args);

                // if help has been requested then display and quit
                if (Options.HelpRequested)
                {
                    Console.WriteLine(Options.UsageMessage);
                    return;
                }

                // validate input parameters and exit if invalid
                var validationErrors = new List<string>();
                if (!Options.Validate(validationErrors))
                {
                    foreach (var error in validationErrors)
                        Console.WriteLine("Error. {0}", error);
                    return;
                }

                var controller = new ScriptController(Options);
                controller.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                // keep the window open if we're in debug so we can see what happened
#if (DEBUG)
                Console.WriteLine("Hit enter to exit");
                Console.ReadLine();
#endif
            }
        }
    }
}