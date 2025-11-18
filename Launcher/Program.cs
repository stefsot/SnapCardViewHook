using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Launcher
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // wait for marvel snap process
            while (Loader.GetActiveSnapProcess() == null)
            {
                ConsoleWriteAndWait("MarvelSnap is not running, launch MarvelSnap and then press any key to retry", 
                                    ConsoleColor.Yellow);
            }

            try
            {
                if (Loader.Inject())
                    return;
            }
            catch (Exception e)
            {
                ConsoleWriteAndWait("An exception has occured. It might help running as administrator, " +
                                    "please make sure your antivirus is not blocking any required files.\n\n" +
                                    $"Exception details:\n{e}", 
                                    ConsoleColor.Red);
                return;
            }

            // injection failed 
            // should almost never happen
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Injection failed, make sure your antivirus hasn't deleted any required files.");
            Console.WriteLine("!!! No support is provided for this software, don't contact me for fixes, help or support !!!");
            
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey(true);
        }

        private static void ConsoleWriteAndWait(string text, ConsoleColor? color = null)
        { 
            if(color.HasValue)
                Console.ForegroundColor = color.Value;

            Console.WriteLine(text);
            Console.ReadKey(true);
        }
    }
}
