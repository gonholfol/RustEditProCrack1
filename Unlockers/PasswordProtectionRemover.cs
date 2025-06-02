using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace RustEditProCrack.Unlockers
{
    /// <summary>
    /// Класс для удаления защиты паролем в RustEdit Pro
    /// </summary>
    public class PasswordProtectionRemover
    {
        private readonly AssemblyDefinition assembly;

        public PasswordProtectionRemover(AssemblyDefinition assembly)
        {
            this.assembly = assembly;
        }

        /// <summary>
        /// Основной метод для удаления защиты паролем
        /// </summary>
        public bool RemovePasswordProtection()
        {
            Console.WriteLine("🔧 === УДАЛЕНИЕ PASSWORD PROTECTION ===");
            Console.WriteLine("🎯 Ищем корутину WorldSaveLoad.LoadWorld и удаляем yield return base.StartCoroutine({text1}.{text2}(data))");
            
            try
            {
                // Найти класс WorldSaveLoad
                var worldSaveLoadClass = assembly.MainModule.Types
                    .FirstOrDefault(t => t.Name.Contains("WorldSaveLoad"));
                    
                if (worldSaveLoadClass == null)
                {
                    Console.WriteLine("❌ WorldSaveLoad класс не найден");
                    return false;
                }

                Console.WriteLine($"✅ Найден класс: {worldSaveLoadClass.Name}");

                // Найти КОРУТИНУ LoadWorld (возвращает IEnumerator)
                var loadWorldCoroutine = worldSaveLoadClass.Methods
                    .FirstOrDefault(m => m.Name == "LoadWorld" && 
                                       m.ReturnType.FullName.Contains("IEnumerator"));

                if (loadWorldCoroutine == null)
                {
                    Console.WriteLine("❌ Корутина LoadWorld не найдена");
                    return false;
                }

                Console.WriteLine($"✅ Найдена корутина LoadWorld: {loadWorldCoroutine.Name}");
                Console.WriteLine($"   Возвращает: {loadWorldCoroutine.ReturnType.FullName}");

                // Получить имя state machine из корутины
                var stateMachineName = GetStateMachineName(loadWorldCoroutine);
                if (stateMachineName == null)
                {
                    Console.WriteLine("❌ State machine не найден в корутине LoadWorld");
                    return false;
                }

                Console.WriteLine($"✅ Найден state machine: {stateMachineName}");

                // Найти state machine класс
                var stateMachineClass = worldSaveLoadClass.NestedTypes
                    .FirstOrDefault(t => t.Name == stateMachineName);

                if (stateMachineClass == null)
                {
                    Console.WriteLine($"❌ State machine класс {stateMachineName} не найден");
                    return false;
                }

                // Найти MoveNext метод в state machine
                var moveNextMethod = stateMachineClass.Methods
                    .FirstOrDefault(m => m.Name == "MoveNext");

                if (moveNextMethod?.HasBody != true)
                {
                    Console.WriteLine("❌ MoveNext метод не найден в state machine");
                    return false;
                }

                Console.WriteLine($"✅ Найден MoveNext метод с {moveNextMethod.Body.Instructions.Count} инструкциями");

                // Удалить password protection из MoveNext
                return RemovePasswordFromMoveNext(moveNextMethod);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка удаления password protection: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Получить имя state machine класса из корутины
        /// </summary>
        private string? GetStateMachineName(MethodDefinition coroutine)
        {
            foreach (var inst in coroutine.Body.Instructions)
            {
                if (inst.OpCode == OpCodes.Newobj &&
                    inst.Operand?.ToString().Contains("WorldSaveLoad/") == true)
                {
                    var operandStr = inst.Operand.ToString();
                    var parts = operandStr.Split('/');
                    if (parts.Length > 1)
                    {
                        var className = parts[1].Split(':')[0];
                        return className;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Удалить password protection из MoveNext метода state machine
        /// </summary>
        private bool RemovePasswordFromMoveNext(MethodDefinition moveNext)
        {
            Console.WriteLine("🔍 Анализируем MoveNext для поиска password protection...");
            
            var instructions = moveNext.Body.Instructions;
            var ilProcessor = moveNext.Body.GetILProcessor();

            // Показать IL код для анализа
            Console.WriteLine("📋 IL код MoveNext:");
            for (int i = 0; i < Math.Min(instructions.Count, 50); i++)
            {
                var inst = instructions[i];
                Console.WriteLine($"  {i:D3}: {inst.OpCode,-15} {inst.Operand}");
            }

            // ЭТАП 1: Найти строку string.Empty
            int stringEmptyIndex = FindStringEmpty(instructions);
            if (stringEmptyIndex == -1)
            {
                Console.WriteLine("❌ string.Empty не найден - не можем определить позицию");
                return false;
            }

            Console.WriteLine($"✅ Найден string.Empty на позиции {stringEmptyIndex}");

            // ЭТАП 2: Искать ПОСЛЕ string.Empty паттерн: StartCoroutine + обфусцированный метод с data параметром
            int passwordBlockStart = FindPasswordProtectionAfterStringEmpty(instructions, stringEmptyIndex);
            if (passwordBlockStart == -1)
            {
                Console.WriteLine("❌ Password protection паттерн не найден ПОСЛЕ string.Empty");
                return false;
            }

            Console.WriteLine($"🎯 НАЙДЕН PASSWORD PROTECTION БЛОК начиная с позиции {passwordBlockStart}");

            // ПРАВИЛЬНЫЙ ПОДХОД: Заменяем ТОЛЬКО обфусцированный вызов метода
            // НЕ ТРОГАЕМ весь блок - только саму проблемную инструкцию!
            var obfuscatedCallInst = instructions[passwordBlockStart];
            Console.WriteLine($"🔧 АККУРАТНАЯ ЗАМЕНА ТОЛЬКО ОБФУСЦИРОВАННОГО ВЫЗОВА:");
            Console.WriteLine($"  ДО:  {passwordBlockStart:D3}: {obfuscatedCallInst.OpCode,-15} {obfuscatedCallInst.Operand}");
            
            // Заменяем ТОЛЬКО вызов обфусцированного метода на ldnull
            // Это превратит yield return base.StartCoroutine(OBFUSCATED(data)) в yield return base.StartCoroutine(null)
            // Что безопасно и не ломает state machine
            ilProcessor.Replace(instructions[passwordBlockStart], Instruction.Create(OpCodes.Ldnull));
            
            Console.WriteLine($"  ПОСЛЕ: {passwordBlockStart:D3}: ldnull");
            Console.WriteLine("✅ PASSWORD PROTECTION МЕТОД ЗАМЕНЕН НА NULL!");
            Console.WriteLine("   Результат: yield return base.StartCoroutine(null) // безопасно, state machine не поломан");
            
            return true;
        }

        /// <summary>
        /// Найти инструкцию string.Empty
        /// </summary>
        private int FindStringEmpty(System.Collections.Generic.IList<Instruction> instructions)
        {
            for (int i = 0; i < instructions.Count; i++)
            {
                var inst = instructions[i];
                
                // Ищем ldsfld string.Empty
                if (inst.OpCode == OpCodes.Ldsfld &&
                    inst.Operand?.ToString().Contains("String::Empty") == true)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Найти password protection ПОСЛЕ string.Empty
        /// </summary>
        private int FindPasswordProtectionAfterStringEmpty(System.Collections.Generic.IList<Instruction> instructions, int stringEmptyIndex)
        {
            Console.WriteLine($"🔍 Ищем password protection ПОСЛЕ позиции {stringEmptyIndex}...");
            
            // Искать в следующих 50 инструкциях после string.Empty
            for (int i = stringEmptyIndex + 1; i < Math.Min(instructions.Count - 5, stringEmptyIndex + 50); i++)
            {
                var inst = instructions[i];
                
                // Найдена инструкция call с обфусцированным методом
                if (inst.OpCode == OpCodes.Call &&
                    inst.Operand is MethodReference methodRef &&
                    IsObfuscatedName(methodRef.DeclaringType.Name) &&
                    IsObfuscatedName(methodRef.Name) &&
                    HasDataParameter(methodRef))
                {
                    Console.WriteLine($"🎯 НАЙДЕН обфусцированный метод с data параметром на позиции {i}: {methodRef.DeclaringType.Name}.{methodRef.Name}");
                    
                    // Проверить что ПОСЛЕ него есть StartCoroutine
                    var startCoroutineIndex = FindStartCoroutineAfter(instructions, i);
                    if (startCoroutineIndex != -1)
                    {
                        Console.WriteLine($"✅ Найден StartCoroutine на позиции {startCoroutineIndex} - это password protection!");
                        return i; // Возвращаем начало блока (обфусцированный вызов)
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// Найти полный блок yield return начиная с указанной позиции
        /// </summary>
        private (int start, int end) FindYieldReturnBlockFromStart(System.Collections.Generic.IList<Instruction> instructions, int startPos)
        {
            Console.WriteLine($"🔍 Поиск полного блока yield return начиная с позиции {startPos}...");
            
            // Начало: найти ldarg.0 или ldloc перед startPos
            int blockStart = startPos;
            for (int i = startPos - 1; i >= Math.Max(0, startPos - 5); i--)
            {
                var inst = instructions[i];
                if (inst.OpCode == OpCodes.Ldarg_0 || 
                    inst.OpCode.ToString().StartsWith("Ldloc"))
                {
                    blockStart = i;
                    break;
                }
            }

            // Найти StartCoroutine после startPos
            int startCoroutineIndex = FindStartCoroutineAfter(instructions, startPos);
            if (startCoroutineIndex == -1)
            {
                return (-1, -1);
            }

            // Конец: найти stfld state после StartCoroutine
            int blockEnd = startCoroutineIndex;
            for (int i = startCoroutineIndex + 1; i < Math.Min(instructions.Count, startCoroutineIndex + 15); i++)
            {
                var inst = instructions[i];
                
                // Ищем stfld для current
                if (inst.OpCode == OpCodes.Stfld && 
                    inst.Operand?.ToString().Contains("current") == true)
                {
                    blockEnd = i;
                    continue;
                }
                
                // Ищем stfld для state (обычно последний в блоке)
                if (inst.OpCode == OpCodes.Stfld && 
                    inst.Operand?.ToString().Contains("state") == true)
                {
                    blockEnd = i;
                    continue;
                }
                
                // Если дошли до ldc.i4.1 и ret - это конец yield return блока
                if (inst.OpCode == OpCodes.Ldc_I4_1 && 
                    i + 1 < instructions.Count && 
                    instructions[i + 1].OpCode == OpCodes.Ret)
                {
                    blockEnd = i + 1;
                    break;
                }
            }
            
            Console.WriteLine($"📍 Блок yield return: {blockStart} - {blockEnd}");
            return (blockStart, blockEnd);
        }

        /// <summary>
        /// Проверить имеет ли метод параметр data типа
        /// </summary>
        private bool HasDataParameter(MethodReference methodRef)
        {
            if (methodRef.Parameters.Count == 0) return false;
            
            // Проверяем что есть параметр с типом содержащим обфусцированное имя (обычно это data)
            foreach (var param in methodRef.Parameters)
            {
                var paramType = param.ParameterType.FullName;
                if (paramType.Contains("PLOFBHPMKFD") || // Обычный тип data в этой игре
                    paramType.Contains("/") || // Nested type
                    IsObfuscatedName(param.ParameterType.Name))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Найти StartCoroutine ПОСЛЕ указанной позиции
        /// </summary>
        private int FindStartCoroutineAfter(System.Collections.Generic.IList<Instruction> instructions, int startPos)
        {
            // Искать в 10 инструкциях вперед
            for (int i = startPos + 1; i < Math.Min(instructions.Count, startPos + 10); i++)
            {
                var inst = instructions[i];
                if (inst.OpCode == OpCodes.Call &&
                    inst.Operand?.ToString().Contains("StartCoroutine") == true)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Получить читаемое имя обфусцированного метода
        /// </summary>
        private string GetObfuscatedMethodName(Instruction obfuscatedCall)
        {
            if (obfuscatedCall.Operand is MethodReference methodRef)
            {
                return $"{methodRef.DeclaringType.Name}.{methodRef.Name}";
            }
            return "OBFUSCATED.METHOD";
        }

        /// <summary>
        /// Проверка является ли имя обфусцированным
        /// </summary>
        private bool IsObfuscatedName(string name)
        {
            return name.Length >= 8 && name.Length <= 15 &&
                   name.All(c => char.IsUpper(c) || char.IsDigit(c)) &&
                   name.Any(char.IsLetter);
        }

        /// <summary>
        /// Получить эффект инструкции на стек
        /// </summary>
        private int GetStackEffect(Instruction instruction)
        {
            switch (instruction.OpCode.Code)
            {
                case Mono.Cecil.Cil.Code.Ldarg_0:
                case Mono.Cecil.Cil.Code.Ldloc:
                case Mono.Cecil.Cil.Code.Ldloc_0:
                case Mono.Cecil.Cil.Code.Ldloc_1:
                case Mono.Cecil.Cil.Code.Ldloc_2:
                case Mono.Cecil.Cil.Code.Ldloc_3:
                case Mono.Cecil.Cil.Code.Ldloc_S:
                case Mono.Cecil.Cil.Code.Ldc_I4:
                case Mono.Cecil.Cil.Code.Ldc_I4_0:
                case Mono.Cecil.Cil.Code.Ldc_I4_1:
                case Mono.Cecil.Cil.Code.Ldc_I4_6:
                case Mono.Cecil.Cil.Code.Ldnull:
                    return +1; // Добавляет 1 элемент в стек
                
                case Mono.Cecil.Cil.Code.Stfld:
                case Mono.Cecil.Cil.Code.Stloc:
                case Mono.Cecil.Cil.Code.Stloc_S:
                case Mono.Cecil.Cil.Code.Pop:
                    return -1; // Убирает 1 элемент из стека
                
                case Mono.Cecil.Cil.Code.Ldfld:
                    return 0;  // Заменяет 1 на 1
                    
                case Mono.Cecil.Cil.Code.Call:
                    // Для call нужно анализировать параметры и возвращаемое значение
                    if (instruction.Operand is MethodReference methodRef)
                    {
                        int paramCount = methodRef.Parameters.Count;
                        if (!methodRef.HasThis) paramCount++; // Добавляем this если метод не статичный
                        int returnCount = methodRef.ReturnType.FullName == "System.Void" ? 0 : 1;
                        return returnCount - paramCount;
                    }
                    return 0;
                    
                case Mono.Cecil.Cil.Code.Ret:
                case Mono.Cecil.Cil.Code.Nop:
                    return 0; // Не влияет на стек
                    
                default:
                    return 0; // По умолчанию не влияет
            }
        }
    }
} 