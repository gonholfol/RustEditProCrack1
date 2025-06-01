using System;
using System.IO;

namespace RustEditProCrack
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("🤖 === DLL EDITOR - UNITY ASSEMBLY PATCHER ===");
            Console.WriteLine("Advanced tool for patching Unity-based applications");
            Console.WriteLine("Built with Mono.Cecil for safe IL manipulation");
            Console.WriteLine();

            string dllPath = "Assembly-CSharp.dll";
            
            if (!File.Exists(dllPath))
            {
                Console.WriteLine($"❌ Error: {dllPath} not found!");
                Console.WriteLine("Please place Assembly-CSharp.dll in the project root directory.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            try
            {
                Console.WriteLine($"📂 Loading assembly: {dllPath}");
                Console.WriteLine("🔍 Starting fully autonomous patching process...");
                Console.WriteLine();

                // Initialize patcher and run fully autonomous execution
                using (var patcher = new DllPatcher(dllPath))
                {
                    bool success = patcher.FullyAutonomousExecution();
                    
                    if (success)
                    {
                        Console.WriteLine();
                        Console.WriteLine("🎉 === PATCHING COMPLETED SUCCESSFULLY ===");
                        Console.WriteLine("✅ All patches applied and saved automatically");
                        Console.WriteLine("📂 Output: Assembly-CSharp_Modifi.dll");
                        Console.WriteLine();
                        Console.WriteLine("🚀 Your patched DLL is ready to use!");
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine("❌ === PATCHING FAILED ===");
                        Console.WriteLine("Some patches could not be applied.");
                        Console.WriteLine("Check the output above for details.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Critical error: {ex.Message}");
                Console.WriteLine($"📋 Details: {ex.StackTrace}");
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
} 