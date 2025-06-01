using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;

namespace RustEditProCrack
{
    public class DllPatcher : IDisposable
    {
        private AssemblyDefinition assembly;
        private string assemblyPath;
        private bool isModified = false;
        
        public DllPatcher(string dllPath)
        {
            assemblyPath = dllPath;
            Console.WriteLine($"Loading assembly: {dllPath}");
            
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(dllPath) ?? ".");
            
            var readerParameters = new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadWrite = false,
                ReadingMode = ReadingMode.Immediate
            };
            
            assembly = AssemblyDefinition.ReadAssembly(dllPath, readerParameters);
            Console.WriteLine($"Assembly loaded successfully: {assembly.FullName}");
        }

        /// <summary>
        /// Applies all patches according to the guide
        /// </summary>
        public void ApplyAllPatches()
        {
            Console.WriteLine("=== Starting application of all patches ===");
            
            try
            {
                // Patch 1: Enable PRO mode
                EnableProMode();
                
                // Patch 2: Unlock all prefabs
                UnlockAllPrefabs();
                
                // Patch 3: Remove password protection
                RemovePasswordProtection();
                
                Console.WriteLine("=== All patches applied successfully ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying patches: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Patch 1: Enable PRO mode
        /// Finds NNBAAFILCKO.KJNNJFFHMDD and replaces it with public static bool = true
        /// </summary>
        private void EnableProMode()
        {
            Console.WriteLine("\n--- Patch 1: Enable PRO mode ---");
            
            // First find DiscordPresence.UpdateActivity and extract class name
            var discordPresenceType = FindTypeByMethodContent("SmallImage", "512_2");
            if (discordPresenceType == null)
            {
                Console.WriteLine("DiscordPresence class or UpdateActivity method not found");
                return;
            }

            // Find NNBAAFILCKO class (or its equivalent)
            var proModeClassName = ExtractClassNameFromDiscordPresence(discordPresenceType);
            if (string.IsNullOrEmpty(proModeClassName))
            {
                Console.WriteLine("Failed to extract PRO mode class name");
                return;
            }

            var proModeClass = assembly.MainModule.GetType(proModeClassName);
            if (proModeClass == null)
            {
                Console.WriteLine($"Class {proModeClassName} not found, creating new one");
                proModeClass = CreateProModeClass(proModeClassName);
            }

            // Add or modify KJNNJFFHMDD property
            ModifyProModeProperty(proModeClass);

            // Remove PRO mode blocking methods
            RemoveProModeBlocking();
            
            Console.WriteLine("PRO mode successfully enabled");
        }

        /// <summary>
        /// Patch 2: Unlock all prefabs
        /// Removes validation checks in method containing "Prefab.FileDoesntExist"
        /// </summary>
        private void UnlockAllPrefabs()
        {
            Console.WriteLine("\n--- Patch 2: Unlock all prefabs ---");
            
            var method = FindMethodByString("Prefab.FileDoesntExist");
            if (method == null)
            {
                Console.WriteLine("Method with 'Prefab.FileDoesntExist' not found");
                return;
            }

            Console.WriteLine($"Found method: {method.DeclaringType.Name}.{method.Name}");
            
            // Remove File.Exists check and related code
            RemovePrefabValidation(method);
            
            Console.WriteLine("All prefabs unlocked");
        }

        /// <summary>
        /// Patch 3: Remove password protection
        /// Removes call to CAIFJOAHPAO.KGOBDPHHLBD from WorldSaveLoad.LoadWorld
        /// </summary>
        private void RemovePasswordProtection()
        {
            Console.WriteLine("\n--- Patch 3: Remove password protection ---");
            
            var worldSaveLoadType = assembly.MainModule.Types
                .FirstOrDefault(t => t.Name.Contains("WorldSaveLoad"));
                
            if (worldSaveLoadType == null)
            {
                Console.WriteLine("WorldSaveLoad class not found");
                return;
            }

            var loadWorldMethod = worldSaveLoadType.Methods
                .FirstOrDefault(m => m.Name.Contains("LoadWorld"));
                
            if (loadWorldMethod == null)
            {
                Console.WriteLine("LoadWorld method not found");
                return;
            }

            Console.WriteLine($"Found method: {worldSaveLoadType.Name}.{loadWorldMethod.Name}");
            
            // Remove StartCoroutine(CAIFJOAHPAO.KGOBDPHHLBD(data)) call
            RemovePasswordCoroutine(loadWorldMethod);
            
            Console.WriteLine("Password protection removed");
        }

        private TypeDefinition FindTypeByMethodContent(string searchString1, string searchString2)
        {
            foreach (var type in assembly.MainModule.Types)
            {
                foreach (var method in type.Methods)
                {
                    if (method.HasBody && ContainsStrings(method, searchString1, searchString2))
                    {
                        return type;
                    }
                }
            }
            return null;
        }

        private bool ContainsStrings(MethodDefinition method, params string[] strings)
        {
            if (!method.HasBody) return false;
            
            var bodyText = method.Body.Instructions
                .Where(i => i.OpCode == OpCodes.Ldstr)
                .Select(i => i.Operand?.ToString())
                .Where(s => !string.IsNullOrEmpty(s));
                
            return strings.All(str => bodyText.Any(bt => bt.Contains(str)));
        }

        private string ExtractClassNameFromDiscordPresence(TypeDefinition discordType)
        {
            // Find UpdateActivity method and extract class name
            var updateActivityMethod = discordType.Methods
                .FirstOrDefault(m => ContainsStrings(m, "512_2"));
                
            if (updateActivityMethod?.HasBody != true) return null;

            // Look for instructions that reference static property
            foreach (var instruction in updateActivityMethod.Body.Instructions)
            {
                if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
                {
                    var methodRef = instruction.Operand as MethodReference;
                    if (methodRef?.Name.Contains("get_") == true)
                    {
                        return methodRef.DeclaringType.Name;
                    }
                }
            }
            
            return "NNBAAFILCKO"; // Fallback to name from guide
        }

        private TypeDefinition CreateProModeClass(string className)
        {
            var newType = new TypeDefinition("", className,
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit,
                assembly.MainModule.TypeSystem.Object);
                
            assembly.MainModule.Types.Add(newType);
            return newType;
        }

        private void ModifyProModeProperty(TypeDefinition proModeClass)
        {
            // Look for existing KJNNJFFHMDD property
            var existingProperty = proModeClass.Properties
                .FirstOrDefault(p => p.Name.Contains("KJNNJFFHMDD"));
                
            if (existingProperty != null)
            {
                // Modify existing getter to always return true
                if (existingProperty.GetMethod?.HasBody == true)
                {
                    var ilProcessor = existingProperty.GetMethod.Body.GetILProcessor();
                    ilProcessor.Clear();
                    ilProcessor.Append(ilProcessor.Create(OpCodes.Ldc_I4_1)); // Load true
                    ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));
                }
            }
            else
            {
                // Create new property
                CreateProModeProperty(proModeClass);
            }
        }

        private void CreateProModeProperty(TypeDefinition proModeClass)
        {
            // Create backing field
            var backingField = new FieldDefinition("_kjnnjffhmdd",
                FieldAttributes.Private | FieldAttributes.Static,
                assembly.MainModule.TypeSystem.Boolean);
            proModeClass.Fields.Add(backingField);

            // Create getter
            var getter = new MethodDefinition("get_KJNNJFFHMDD",
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.SpecialName,
                assembly.MainModule.TypeSystem.Boolean);
            
            var getterIL = getter.Body.GetILProcessor();
            getterIL.Append(getterIL.Create(OpCodes.Ldc_I4_1)); // Always return true
            getterIL.Append(getterIL.Create(OpCodes.Ret));

            // Create setter
            var setter = new MethodDefinition("set_KJNNJFFHMDD",
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.SpecialName,
                assembly.MainModule.TypeSystem.Void);
            setter.Parameters.Add(new ParameterDefinition(assembly.MainModule.TypeSystem.Boolean));
            
            var setterIL = setter.Body.GetILProcessor();
            setterIL.Append(setterIL.Create(OpCodes.Ldarg_0));
            setterIL.Append(setterIL.Create(OpCodes.Stsfld, backingField));
            setterIL.Append(setterIL.Create(OpCodes.Ret));

            // Create property
            var property = new PropertyDefinition("KJNNJFFHMDD",
                PropertyAttributes.None, assembly.MainModule.TypeSystem.Boolean)
            {
                GetMethod = getter,
                SetMethod = setter
            };

            proModeClass.Methods.Add(getter);
            proModeClass.Methods.Add(setter);
            proModeClass.Properties.Add(property);
        }

        private void RemoveProModeBlocking()
        {
            // Find class that blocks PRO mode (in example NJFSINOIPNMDA)
            foreach (var type in assembly.MainModule.Types)
            {
                var methodsToModify = type.Methods
                    .Where(m => m.HasBody && ContainsProModeCheck(m))
                    .ToList();

                foreach (var method in methodsToModify)
                {
                    Console.WriteLine($"Removing PRO blocking in method: {type.Name}.{method.Name}");
                    // Use new method with bool return
                    bool success = RemoveProModeCheckFromMethod(method);
                    if (success)
                    {
                        Console.WriteLine($"✅ PRO blocking successfully removed");
                    }
                    else
                    {
                        Console.WriteLine($"❌ Error removing PRO blocking");
                    }
                }
            }
        }

        private bool ContainsProModeCheck(MethodDefinition method)
        {
            if (!method.HasBody) return false;
            
            // Look for pattern: NIPAPEJDFCK.Method() + brfalse.s
            var instructions = method.Body.Instructions;
            for (int i = 0; i < instructions.Count - 1; i++)
            {
                var inst = instructions[i];
                var nextInst = instructions[i + 1];
                
                // First instruction: NIPAPEJDFCK method call
                if (inst.OpCode == OpCodes.Call &&
                    inst.Operand is MethodReference methodRef &&
                    methodRef.DeclaringType?.Name == "NIPAPEJDFCK" &&
                    methodRef.ReturnType?.FullName == "System.Boolean")
                {
                    // Second instruction: brfalse (conditional jump on false)
                    if (nextInst.OpCode == OpCodes.Brfalse_S || nextInst.OpCode == OpCodes.Brfalse)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool RemoveProModeCheckFromMethod(MethodDefinition method)
        {
            try
            {
                var instructions = method.Body.Instructions;
                bool foundAndRemoved = false;

                // Find and remove pattern: NIPAPEJDFCK.Method() + brfalse
                for (int i = 0; i < instructions.Count - 1; i++)
                {
                    var inst = instructions[i];
                    var nextInst = instructions[i + 1];

                    // Look for NIPAPEJDFCK method call + brfalse
                    if (inst.OpCode == OpCodes.Call &&
                        inst.Operand is MethodReference methodRef &&
                        methodRef.DeclaringType?.Name == "NIPAPEJDFCK" &&
                        methodRef.ReturnType?.FullName == "System.Boolean")
                    {
                        if (nextInst.OpCode == OpCodes.Brfalse_S || nextInst.OpCode == OpCodes.Brfalse)
                        {
                            Console.WriteLine($"     🎯 Found PRO blocking pattern at positions {i}-{i+1}");
                            Console.WriteLine($"        {inst.OpCode} {methodRef.Name}");
                            Console.WriteLine($"        {nextInst.OpCode} {nextInst.Operand}");
                            
                            // STRATEGY: Replace both instructions with NOP (no operation)
                            instructions[i] = Instruction.Create(OpCodes.Nop);
                            instructions[i + 1] = Instruction.Create(OpCodes.Nop);
                            
                            Console.WriteLine($"     ✅ PRO blocking replaced with NOP");
                            foundAndRemoved = true;
                        }
                    }
                }

                return foundAndRemoved;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"     ❌ Error removing PRO check: {ex.Message}");
                return false;
            }
        }

        private MethodDefinition FindMethodByString(string searchString)
        {
            foreach (var type in assembly.MainModule.Types)
            {
                foreach (var method in type.Methods)
                {
                    if (method.HasBody && ContainsString(method, searchString))
                    {
                        return method;
                    }
                }
            }
            return null;
        }

        private bool ContainsString(MethodDefinition method, string searchString)
        {
            if (!method.HasBody) return false;
            
            return method.Body.Instructions
                .Where(i => i.OpCode == OpCodes.Ldstr)
                .Any(i => i.Operand?.ToString().Contains(searchString) == true);
        }

        private void RemovePrefabValidation(MethodDefinition method)
        {
            var ilProcessor = method.Body.GetILProcessor();
            var instructions = method.Body.Instructions.ToList();
            
            // Look for pattern: if (File.Exists(text))
            for (int i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];
                
                if (instruction.OpCode == OpCodes.Call &&
                    instruction.Operand?.ToString().Contains("File") == true &&
                    instruction.Operand?.ToString().Contains("Exists") == true)
                {
                    // Find start and end of validation block
                    int startIndex = FindBlockStart(instructions, i);
                    int endIndex = FindBlockEnd(instructions, i);
                    
                    // Remove entire validation block
                    for (int j = endIndex - 1; j >= startIndex; j--)
                    {
                        ilProcessor.Remove(instructions[j]);
                    }
                    
                    Console.WriteLine($"Removed prefab validation block from {startIndex} to {endIndex}");
                    break;
                }
            }
        }

        private void RemovePasswordCoroutine(MethodDefinition method)
        {
            var ilProcessor = method.Body.GetILProcessor();
            var instructions = method.Body.Instructions.ToList();
            
            // Look for yield return base.StartCoroutine(CAIFJOAHPAO.KGOBDPHHLBD(data))
            for (int i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];
                
                if (instruction.OpCode == OpCodes.Call &&
                    instruction.Operand?.ToString().Contains("StartCoroutine") == true)
                {
                    // Check following instructions for KGOBDPHHLBD
                    bool foundPasswordCall = false;
                    for (int j = Math.Max(0, i - 5); j < Math.Min(instructions.Count, i + 5); j++)
                    {
                        if (instructions[j].Operand?.ToString().Contains("KGOBDPHHLBD") == true)
                        {
                            foundPasswordCall = true;
                            break;
                        }
                    }
                    
                    if (foundPasswordCall)
                    {
                        // Remove coroutine call
                        int startIndex = FindBlockStart(instructions, i);
                        int endIndex = FindBlockEnd(instructions, i);
                        
                        for (int j = endIndex - 1; j >= startIndex; j--)
                        {
                            ilProcessor.Remove(instructions[j]);
                        }
                        
                        Console.WriteLine($"Removed password protection call from {startIndex} to {endIndex}");
                        break;
                    }
                }
            }
        }

        private int FindBlockStart(System.Collections.Generic.List<Instruction> instructions, int currentIndex)
        {
            // Simple heuristic to find block start
            for (int i = currentIndex; i >= 0; i--)
            {
                if (instructions[i].OpCode == OpCodes.Nop ||
                    instructions[i].Previous?.OpCode == OpCodes.Br ||
                    instructions[i].Previous?.OpCode == OpCodes.Brfalse ||
                    instructions[i].Previous?.OpCode == OpCodes.Brtrue)
                {
                    return i;
                }
            }
            return Math.Max(0, currentIndex - 10);
        }

        private int FindBlockEnd(System.Collections.Generic.List<Instruction> instructions, int currentIndex)
        {
            // Simple heuristic to find block end
            for (int i = currentIndex; i < instructions.Count; i++)
            {
                if (instructions[i].OpCode == OpCodes.Br ||
                    instructions[i].OpCode == OpCodes.Brfalse ||
                    instructions[i].OpCode == OpCodes.Brtrue ||
                    instructions[i].OpCode == OpCodes.Ret)
                {
                    return i + 1;
                }
            }
            return Math.Min(instructions.Count, currentIndex + 10);
        }

        public void SavePatched(string outputPath = null)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = assemblyPath.Replace(".dll", "_Patched.dll");
            }
            
            Console.WriteLine($"Saving patched DLL: {outputPath}");
            assembly.Write(outputPath);
            Console.WriteLine("File saved successfully");
        }

        /// <summary>
        /// AUTONOMOUS PRO MODE PATCHER
        /// Automatically finds and patches obfuscated PRO mode
        /// </summary>
        public bool AutonomousProModePatch()
        {
            Console.WriteLine("🤖 === AUTONOMOUS PRO MODE PATCHER ===");
            Console.WriteLine("Starting automatic search and patching...");
            Console.WriteLine();

            try
            {
                // Step 1: Automatically find obfuscated names through DiscordPresence
                Console.WriteLine("📡 Step 1: Finding obfuscated names through DiscordPresence...");
                var obfuscatedNames = FindObfuscatedNames();
                
                if (obfuscatedNames.className == null || obfuscatedNames.methodName == null)
                {
                    Console.WriteLine("❌ Failed to find obfuscated names automatically");
                    return false;
                }

                Console.WriteLine($"✅ Found obfuscated names:");
                Console.WriteLine($"   Class: {obfuscatedNames.className}");
                Console.WriteLine($"   Method: {obfuscatedNames.methodName}");
                Console.WriteLine();

                // Step 2: Find and analyze target class
                Console.WriteLine("🔍 Step 2: Finding target class...");
                var targetClass = assembly.MainModule.Types
                    .FirstOrDefault(t => t.Name == obfuscatedNames.className);

                if (targetClass == null)
                {
                    Console.WriteLine($"❌ Class {obfuscatedNames.className} not found");
                    return false;
                }

                Console.WriteLine($"✅ Found target class: {targetClass.FullName}");

                // Step 3: Find and analyze target method
                Console.WriteLine("🎯 Step 3: Finding target method...");
                var targetMethod = targetClass.Methods
                    .FirstOrDefault(m => m.Name == obfuscatedNames.methodName);

                if (targetMethod?.HasBody != true)
                {
                    Console.WriteLine($"❌ Method {obfuscatedNames.methodName} not found or has no body");
                    return false;
                }

                Console.WriteLine($"✅ Found target method: {targetMethod.Name}");
                Console.WriteLine($"   Static: {targetMethod.IsStatic}");
                Console.WriteLine($"   Return type: {targetMethod.ReturnType.FullName}");
                Console.WriteLine($"   Instructions: {targetMethod.Body.Instructions.Count}");

                // Show current code
                Console.WriteLine("\n📋 CURRENT IL CODE:");
                for (int i = 0; i < targetMethod.Body.Instructions.Count; i++)
                {
                    var inst = targetMethod.Body.Instructions[i];
                    Console.WriteLine($"  {i}: {inst.OpCode} {inst.Operand}");
                }

                // Step 4: Automatic patching
                Console.WriteLine("\n⚡ Step 4: Applying patch...");
                bool patchSuccess = PatchMethodToReturnTrue(targetMethod);

                if (!patchSuccess)
                {
                    Console.WriteLine("❌ Error patching method");
                    return false;
                }

                Console.WriteLine("✅ Patch applied successfully!");

                // Show new code
                Console.WriteLine("\n🎉 NEW IL CODE:");
                for (int i = 0; i < targetMethod.Body.Instructions.Count; i++)
                {
                    var inst = targetMethod.Body.Instructions[i];
                    Console.WriteLine($"  {i}: {inst.OpCode} {inst.Operand}");
                }

                isModified = true;
                Console.WriteLine("\n🎯 PRO MODE SUCCESSFULLY ACTIVATED!");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in autonomous patcher: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Automatically finds obfuscated names through DiscordPresence analysis
        /// </summary>
        private (string className, string methodName) FindObfuscatedNames()
        {
            // Find DiscordPresence class
            var discordType = assembly.MainModule.Types
                .FirstOrDefault(t => t.Name.Contains("DiscordPresence"));

            if (discordType == null)
            {
                Console.WriteLine("❌ DiscordPresence class not found");
                return (null, null);
            }

            // Find UpdateActivity method
            var updateMethod = discordType.Methods
                .FirstOrDefault(m => m.Name == "UpdateActivity");

            if (updateMethod?.HasBody != true)
            {
                Console.WriteLine("❌ UpdateActivity method not found or has no body");
                return (null, null);
            }

            // Analyze IL code to find obfuscated boolean method near "512_2"
            var instructions = updateMethod.Body.Instructions;

            for (int i = 0; i < instructions.Count; i++)
            {
                var inst = instructions[i];

                // Look for "512_2" string
                if (inst.OpCode == OpCodes.Ldstr && inst.Operand?.ToString() == "512_2")
                {
                    // Look for obfuscated boolean method calls in vicinity
                    int start = Math.Max(0, i - 20);
                    int end = Math.Min(instructions.Count - 1, i + 10);

                    for (int k = start; k <= end; k++)
                    {
                        var contextInst = instructions[k];

                        if ((contextInst.OpCode == OpCodes.Call || contextInst.OpCode == OpCodes.Callvirt) &&
                            contextInst.Operand is MethodReference methodRef)
                        {
                            // Check: this is a boolean method without parameters with obfuscated name
                            if (methodRef.ReturnType?.FullName == "System.Boolean" &&
                                IsObfuscatedName(methodRef.DeclaringType.Name) &&
                                IsObfuscatedName(methodRef.Name) &&
                                methodRef.Parameters.Count == 0)
                            {
                                Console.WriteLine($"🎯 Found obfuscated boolean method: {methodRef.DeclaringType.Name}.{methodRef.Name}()");
                                return (methodRef.DeclaringType.Name, methodRef.Name);
                            }
                        }
                    }
                    break;
                }
            }

            return (null, null);
        }

        /// <summary>
        /// Checks if name is obfuscated
        /// </summary>
        private bool IsObfuscatedName(string name)
        {
            return name.Length >= 8 && name.Length <= 15 &&
                   name.All(c => char.IsUpper(c) || char.IsDigit(c)) &&
                   name.Any(char.IsLetter);
        }

        /// <summary>
        /// Patches method to always return true
        /// </summary>
        private bool PatchMethodToReturnTrue(MethodDefinition method)
        {
            try
            {
                // Clear all instructions
                method.Body.Instructions.Clear();

                // Add new instructions: load true and return
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_1)); // load 1 (true)
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));      // return

                // Clear variables and exception handlers if they exist
                method.Body.Variables.Clear();
                method.Body.ExceptionHandlers.Clear();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error patching method: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Autonomous saving of patched DLL
        /// </summary>
        public bool AutonomousSave()
        {
            if (!isModified)
            {
                Console.WriteLine("⚠️ No changes to save");
                return false;
            }

            try
            {
                Console.WriteLine("\n💾 === AUTONOMOUS SAVING ===");

                // Create new path with _Modifi suffix
                string directory = Path.GetDirectoryName(assemblyPath);
                string fileName = Path.GetFileNameWithoutExtension(assemblyPath);
                string extension = Path.GetExtension(assemblyPath);
                string modifiedPath = Path.Combine(directory, $"{fileName}_Modifi{extension}");

                Console.WriteLine($"💾 Saving patched DLL: {Path.GetFileName(modifiedPath)}");
                Console.WriteLine($"📂 Path: {modifiedPath}");
                
                // Save to new file with correct parameters
                var writerParameters = new WriterParameters 
                {
                    WriteSymbols = false,
                    DeterministicMvid = false
                };
                
                assembly.Write(modifiedPath, writerParameters);

                Console.WriteLine("✅ AUTONOMOUS SAVING COMPLETED!");
                Console.WriteLine($"   Original preserved: {Path.GetFileName(assemblyPath)}");
                Console.WriteLine($"   Patched DLL: {Path.GetFileName(modifiedPath)}");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in autonomous saving: {ex.Message}");
                Console.WriteLine($"📋 Details: {ex.StackTrace}");
                
                // Attempt alternative saving
                try
                {
                    Console.WriteLine("🔄 Attempting alternative saving...");
                    string directory = Path.GetDirectoryName(assemblyPath);
                    string fileName = Path.GetFileNameWithoutExtension(assemblyPath);
                    string extension = Path.GetExtension(assemblyPath);
                    string tempPath = Path.Combine(directory, $"{fileName}_Modifi_temp{extension}");
                    string finalPath = Path.Combine(directory, $"{fileName}_Modifi{extension}");
                    
                    assembly.Write(tempPath);
                    if (File.Exists(finalPath)) File.Delete(finalPath);
                    File.Move(tempPath, finalPath);
                    Console.WriteLine($"✅ Alternative saving successful: {Path.GetFileName(finalPath)}");
                    return true;
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"❌ Alternative saving also failed: {ex2.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// FULLY AUTONOMOUS EXECUTION
        /// Find, patch and save ALL PATCHES without user intervention
        /// </summary>
        public bool FullyAutonomousExecution()
        {
            Console.WriteLine("🤖 === FULLY AUTONOMOUS EXECUTION ===");
            Console.WriteLine("Automatic search, patching and saving of ALL PATCHES");
            Console.WriteLine("WITHOUT USER INTERVENTION");
            Console.WriteLine();

            // Step 1: Autonomous PRO mode patching
            Console.WriteLine("1️⃣ === PATCH 1: PRO MODE ===");
            bool patchSuccess = AutonomousProModePatch();
            if (!patchSuccess)
            {
                Console.WriteLine("❌ Autonomous PRO mode patching failed");
                return false;
            }

            // Step 2: Unlock all prefabs
            Console.WriteLine("\n2️⃣ === PATCH 2: UNLOCK PREFABS ===");
            UnlockAllPrefabs();

            // Step 3: Remove password protection
            Console.WriteLine("\n3️⃣ === PATCH 3: REMOVE PASSWORD ===");
            RemovePasswordProtection();

            // Step 4: Remove PRO mode blocks
            Console.WriteLine("\n4️⃣ === PATCH 4: REMOVE PRO BLOCKS ===");
            RemoveAllProModeBlocks();

            // Step 5: Autonomous saving
            Console.WriteLine("\n💾 === SAVING ===");
            bool saveSuccess = AutonomousSave();
            if (!saveSuccess)
            {
                Console.WriteLine("❌ Autonomous saving failed");
                return false;
            }

            Console.WriteLine("\n🎉 === ALL PATCHES APPLIED AND SAVED ===");
            Console.WriteLine("✅ 1. PRO MODE ACTIVATED!");
            Console.WriteLine("✅ 2. ALL PREFABS UNLOCKED!");
            Console.WriteLine("✅ 3. PASSWORD PROTECTION REMOVED!");
            Console.WriteLine("✅ 4. PRO BLOCKS REMOVED!");
            Console.WriteLine("🚀 DLL ready for use!");

            return true;
        }

        /// <summary>
        /// AUTOMATIC REMOVAL OF ALL PRO MODE BLOCKS
        /// Finds NJFSINOIPNMDA class and removes PRO checks from all its methods
        /// </summary>
        public bool RemoveAllProModeBlocks()
        {
            Console.WriteLine("\n🚫 === AUTOMATIC REMOVAL OF ALL PRO MODE BLOCKS ===");
            Console.WriteLine("Searching for NJFSINOIPNMDA class and removing PRO checks from its methods...");
            Console.WriteLine();

            // Find NJFSINOIPNMDA class
            var njfClass = assembly.MainModule.Types.FirstOrDefault(t => t.Name == "NJFSINOIPNMDA");
            if (njfClass == null)
            {
                Console.WriteLine("❌ NJFSINOIPNMDA class not found!");
                return false;
            }

            Console.WriteLine($"✅ Found NJFSINOIPNMDA class with {njfClass.Methods.Count} methods");
            Console.WriteLine();

            var modifiedMethods = 0;

            // Process all methods in NJFSINOIPNMDA class
            foreach (var method in njfClass.Methods)
            {
                if (!method.HasBody || method.Name == ".ctor" || method.Name == "Awake")
                    continue;

                // Check if method contains PRO checks
                bool hasProCheck = ContainsProModeCheck(method);
                
                if (hasProCheck)
                {
                    Console.WriteLine($"🎯 Found blocker: {method.Name}()");
                    Console.WriteLine($"   📋 Current instructions: {method.Body.Instructions.Count}");
                    
                    // REMOVE ALL PRO CHECKS FROM METHOD
                    if (RemoveProModeCheckFromMethod(method))
                    {
                        modifiedMethods++;
                        Console.WriteLine($"   ✅ PRO checks removed from method!");
                    }
                    else
                    {
                        Console.WriteLine($"   ❌ Error removing PRO checks");
                    }
                }
            }

            Console.WriteLine($"\n📊 TOTAL: Modified {modifiedMethods} methods in NJFSINOIPNMDA class");
            
            if (modifiedMethods > 0)
            {
                Console.WriteLine("✅ ALL PRO BLOCKS IN NJFSINOIPNMDA SUCCESSFULLY REMOVED!");
                return true;
            }
            else
            {
                Console.WriteLine("❌ PRO blocks in NJFSINOIPNMDA not found");
                return false;
            }
        }

        public void Dispose()
        {
            assembly?.Dispose();
        }
    }
} 