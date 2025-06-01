using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace RustEditProCrack
{
    public class ObfuscatedNames
    {
        public string ClassName { get; set; }
        public string PropertyName { get; set; }
    }

    public class DllAnalyzer : IDisposable
    {
        private AssemblyDefinition assembly;
        
        public DllAnalyzer(string dllPath)
        {
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(dllPath) ?? ".");
            
            var readerParameters = new ReaderParameters
            {
                AssemblyResolver = resolver
            };
            
            assembly = AssemblyDefinition.ReadAssembly(dllPath, readerParameters);
        }

        public void DeepAnalyzeDll()
        {
            Console.WriteLine("=== ГЛУБОКИЙ АНАЛИЗ DLL ===");
            Console.WriteLine($"Assembly: {assembly.FullName}");
            Console.WriteLine($"Количество типов: {assembly.MainModule.Types.Count}");
            Console.WriteLine();

            // Глубокий анализ согласно гайду
            DeepFindDiscordPresence();
            DeepFindProModeElements();
            DeepFindPrefabValidation();
            DeepFindPasswordProtection();
            
            // Дополнительный поиск обфусцированных элементов
            FindObfuscatedClasses();
        }

        private void DeepFindDiscordPresence()
        {
            Console.WriteLine("=== 1. ПОИСК DISCORDPRESENCE.UPDATEACTIVITY (КОНСТАНТА) ===");
            
            // Ищем DiscordPresence класс по имени (это не меняется)
            var discordType = assembly.MainModule.Types
                .FirstOrDefault(t => t.Name.Contains("DiscordPresence"));
                
            if (discordType == null)
            {
                Console.WriteLine("✗ Класс DiscordPresence не найден по имени, ищем по содержимому...");
                // Ищем по содержимому методов (строка "512_2" как маркер)
                discordType = FindTypeByMethodStrings("512_2");
            }

            if (discordType != null)
            {
                Console.WriteLine($"✓ Найден класс DiscordPresence: {discordType.FullName}");
                
                // Ищем метод UpdateActivity (тоже константа)
                var updateMethod = FindUpdateActivityMethod(discordType);
                    
                if (updateMethod != null)
                {
                    Console.WriteLine($"✓ Найден метод UpdateActivity: {updateMethod.Name}");
                    
                    // Извлекаем обфусцированные имена из этого метода
                    var obfuscatedNames = ExtractObfuscatedNamesFromUpdateActivity(updateMethod);
                    
                    if (obfuscatedNames.ClassName != null && obfuscatedNames.PropertyName != null)
                    {
                        Console.WriteLine($"✓ НАЙДЕНЫ ОБФУСЦИРОВАННЫЕ ИМЕНА:");
                        Console.WriteLine($"  → Класс PRO режима: {obfuscatedNames.ClassName}");
                        Console.WriteLine($"  → Свойство PRO режима: {obfuscatedNames.PropertyName}");
                        
                        // Анализируем найденный класс
                        AnalyzeProModeClassDeep(obfuscatedNames.ClassName, obfuscatedNames.PropertyName);
                    }
                    else
                    {
                        Console.WriteLine("✗ Не удалось извлечь обфусцированные имена");
                    }
                }
                else
                {
                    Console.WriteLine("✗ Метод UpdateActivity не найден");
                }
            }
            else
            {
                Console.WriteLine("✗ Класс DiscordPresence не найден");
            }
            Console.WriteLine();
        }

        private MethodDefinition FindUpdateActivityMethod(TypeDefinition discordType)
        {
            // Сначала ищем по имени UpdateActivity
            var updateMethod = discordType.Methods
                .FirstOrDefault(m => m.Name.Contains("UpdateActivity"));
                
            if (updateMethod != null)
            {
                return updateMethod;
            }
            
            // Если не найден по имени, ищем по содержимому (строка "512_2")
            return discordType.Methods
                .FirstOrDefault(m => ContainsMethodStrings(m, "512_2"));
        }

        private (string ClassName, string PropertyName) ExtractObfuscatedNamesFromUpdateActivity(MethodDefinition method)
        {
            Console.WriteLine("  Анализ кода UpdateActivity для извлечения обфусцированных имен:");
            
            if (!method.HasBody) return (null, null);
            
            var instructions = method.Body.Instructions;
            string foundClassName = null;
            string foundPropertyName = null;
            
            // Ищем паттерн: SmallImage = (NNBAAFILCKO.KJNNJFFHMDD ? "512_2" : string.Empty)
            // Стратегия: ищем "512_2", затем ищем рядом get_ вызовы
            
            for (int i = 0; i < instructions.Count; i++)
            {
                var inst = instructions[i];
                
                // Ищем строку "512_2" как маркер
                if (inst.OpCode == OpCodes.Ldstr && inst.Operand?.ToString() == "512_2")
                {
                    Console.WriteLine($"    ✓ Найдена строка '512_2' на позиции {i}");
                    
                    // Ищем в окрестности (обычно перед строкой) вызовы get_ методов
                    // Проверяем инструкции назад и вперед от найденной строки
                    for (int j = Math.Max(0, i - 15); j < Math.Min(instructions.Count, i + 5); j++)
                    {
                        var checkInst = instructions[j];
                        if (checkInst.OpCode == OpCodes.Call || checkInst.OpCode == OpCodes.Callvirt)
                        {
                            var methodRef = checkInst.Operand as MethodReference;
                            if (methodRef?.Name.StartsWith("get_") == true)
                            {
                                // Это должен быть getter обфусцированного свойства
                                foundClassName = methodRef.DeclaringType.Name;
                                foundPropertyName = methodRef.Name.Substring(4); // убираем "get_"
                                
                                Console.WriteLine($"    ✓ Найден getter: {foundClassName}.get_{foundPropertyName}() в позиции {j}");
                                
                                // Показываем контекст вокруг найденного вызова
                                Console.WriteLine($"    Контекст кода:");
                                for (int k = Math.Max(0, j - 3); k <= Math.Min(instructions.Count - 1, j + 3); k++)
                                {
                                    string marker = k == j ? " → " : (k == i ? " ★ " : "   ");
                                    Console.WriteLine($"    {marker}{k}: {instructions[k].OpCode} {instructions[k].Operand}");
                                }
                                
                                break;
                            }
                        }
                    }
                    
                    if (foundClassName != null) break;
                }
            }
            
            // Дополнительная проверка: ищем также строку "SmallImage" для подтверждения
            bool foundSmallImage = false;
            for (int i = 0; i < instructions.Count; i++)
            {
                var inst = instructions[i];
                if (inst.OpCode == OpCodes.Ldstr && inst.Operand?.ToString() == "SmallImage")
                {
                    foundSmallImage = true;
                    Console.WriteLine($"    ✓ Подтверждение: найдена строка 'SmallImage' на позиции {i}");
                    break;
                }
            }
            
            if (!foundSmallImage)
            {
                Console.WriteLine($"    ⚠ ПРЕДУПРЕЖДЕНИЕ: строка 'SmallImage' не найдена, возможно это не тот метод");
            }
            
            if (foundClassName == null || foundPropertyName == null)
            {
                Console.WriteLine("    ✗ Не удалось извлечь обфусцированные имена");
                
                // Показываем весь метод для отладки
                Console.WriteLine("    Полный код метода для анализа:");
                for (int i = 0; i < Math.Min(instructions.Count, 50); i++)
                {
                    Console.WriteLine($"    {i}: {instructions[i].OpCode} {instructions[i].Operand}");
                }
            }
            
            return (foundClassName, foundPropertyName);
        }

        private void AnalyzeProModeClassDeep(string className, string propertyName)
        {
            Console.WriteLine($"  Анализ класса {className}:");
            
            var proModeClass = assembly.MainModule.Types
                .FirstOrDefault(t => t.Name == className);
                
            if (proModeClass != null)
            {
                Console.WriteLine($"    ✓ Класс найден: {proModeClass.FullName}");
                
                // Детальный анализ класса для патчинга
                AnalyzeClassForPatching(proModeClass, propertyName);
            }
            else
            {
                Console.WriteLine($"    ✗ Класс {className} не найден");
            }
        }

        private void AnalyzeClassForPatching(TypeDefinition proModeClass, string propertyName)
        {
            Console.WriteLine($"\n=== ДЕТАЛЬНЫЙ АНАЛИЗ КЛАССА {proModeClass.Name} ДЛЯ ПАТЧИНГА ===");
            
            // 1. Общая информация о классе
            Console.WriteLine($"Полное имя: {proModeClass.FullName}");
            Console.WriteLine($"Модификаторы: {proModeClass.Attributes}");
            Console.WriteLine($"Базовый класс: {proModeClass.BaseType?.FullName ?? "нет"}");
            Console.WriteLine($"Методов: {proModeClass.Methods.Count}");
            Console.WriteLine($"Свойств: {proModeClass.Properties.Count}");
            Console.WriteLine($"Полей: {proModeClass.Fields.Count}");
            Console.WriteLine();

            // 2. Анализ целевого свойства
            AnalyzeTargetProperty(proModeClass, propertyName);
            
            // 3. Анализ всех полей (возможные backing fields)
            AnalyzeClassFields(proModeClass);
            
            // 4. Анализ всех свойств
            AnalyzeClassProperties(proModeClass);
            
            // 5. Анализ конструкторов
            AnalyzeClassConstructors(proModeClass);
            
            // 6. Предложение стратегии патчинга
            ProposePatchingStrategy(proModeClass, propertyName);
        }

        private void AnalyzeTargetProperty(TypeDefinition proModeClass, string propertyName)
        {
            Console.WriteLine($"=== АНАЛИЗ ЦЕЛЕВОГО СВОЙСТВА {propertyName} ===");
            
            var property = proModeClass.Properties
                .FirstOrDefault(p => p.Name == propertyName);
                
            if (property != null)
            {
                Console.WriteLine($"✓ Свойство найдено: {property.Name}");
                Console.WriteLine($"  Тип: {property.PropertyType.FullName}");
                Console.WriteLine($"  Атрибуты: {property.Attributes}");
                Console.WriteLine($"  Имеет getter: {property.GetMethod != null}");
                Console.WriteLine($"  Имеет setter: {property.SetMethod != null}");
                Console.WriteLine();
                
                // Анализ getter метода
                if (property.GetMethod?.HasBody == true)
                {
                    AnalyzeGetterMethod(property.GetMethod);
                }
                
                // Анализ setter метода
                if (property.SetMethod?.HasBody == true)
                {
                    AnalyzeSetterMethod(property.SetMethod);
                }
            }
            else
            {
                Console.WriteLine($"✗ Свойство {propertyName} не найдено");
                
                // Ищем метод get_ напрямую
                var getterMethod = proModeClass.Methods
                    .FirstOrDefault(m => m.Name == $"get_{propertyName}");
                    
                if (getterMethod != null)
                {
                    Console.WriteLine($"✓ Найден getter метод: {getterMethod.Name}");
                    AnalyzeGetterMethod(getterMethod);
                }
            }
            Console.WriteLine();
        }

        private void AnalyzeGetterMethod(MethodDefinition getterMethod)
        {
            Console.WriteLine($"  АНАЛИЗ GETTER МЕТОДА: {getterMethod.Name}");
            Console.WriteLine($"    Статический: {getterMethod.IsStatic}");
            Console.WriteLine($"    Публичный: {getterMethod.IsPublic}");
            Console.WriteLine($"    Возвращаемый тип: {getterMethod.ReturnType.FullName}");
            Console.WriteLine();
            
            if (getterMethod.HasBody)
            {
                var instructions = getterMethod.Body.Instructions;
                Console.WriteLine($"    IL код getter метода ({instructions.Count} инструкций):");
                
                for (int i = 0; i < instructions.Count; i++)
                {
                    var inst = instructions[i];
                    Console.WriteLine($"      {i}: {inst.OpCode} {inst.Operand}");
                }
                Console.WriteLine();
                
                // Анализ что возвращает getter
                AnalyzeGetterBehavior(instructions);
            }
        }

        private void AnalyzeGetterBehavior(IList<Instruction> instructions)
        {
            Console.WriteLine($"    ПОВЕДЕНИЕ GETTER:");
            
            bool returnsConstantTrue = false;
            bool returnsConstantFalse = false;
            bool returnsField = false;
            string fieldName = null;
            
            for (int i = 0; i < instructions.Count; i++)
            {
                var inst = instructions[i];
                
                // Проверяем константы
                if (inst.OpCode == OpCodes.Ldc_I4_1)
                {
                    returnsConstantTrue = true;
                    Console.WriteLine($"      ✓ Возвращает константу TRUE");
                }
                else if (inst.OpCode == OpCodes.Ldc_I4_0)
                {
                    returnsConstantFalse = true;
                    Console.WriteLine($"      ✓ Возвращает константу FALSE");
                }
                else if (inst.OpCode == OpCodes.Ldsfld || inst.OpCode == OpCodes.Ldfld)
                {
                    returnsField = true;
                    fieldName = inst.Operand?.ToString();
                    Console.WriteLine($"      ✓ Возвращает поле: {fieldName}");
                }
            }
            
            if (!returnsConstantTrue && !returnsConstantFalse && !returnsField)
            {
                Console.WriteLine($"      ⚠ Сложная логика - требует детального анализа");
            }
            
            Console.WriteLine();
        }

        private void AnalyzeSetterMethod(MethodDefinition setterMethod)
        {
            Console.WriteLine($"  АНАЛИЗ SETTER МЕТОДА: {setterMethod.Name}");
            Console.WriteLine($"    Статический: {setterMethod.IsStatic}");
            Console.WriteLine($"    Публичный: {setterMethod.IsPublic}");
            Console.WriteLine();
            
            if (setterMethod.HasBody)
            {
                var instructions = setterMethod.Body.Instructions;
                Console.WriteLine($"    IL код setter метода ({instructions.Count} инструкций):");
                
                for (int i = 0; i < instructions.Count; i++)
                {
                    var inst = instructions[i];
                    Console.WriteLine($"      {i}: {inst.OpCode} {inst.Operand}");
                }
            }
            Console.WriteLine();
        }

        private void AnalyzeClassFields(TypeDefinition proModeClass)
        {
            Console.WriteLine($"=== АНАЛИЗ ПОЛЕЙ КЛАССА ===");
            
            if (proModeClass.Fields.Any())
            {
                Console.WriteLine($"Найдено {proModeClass.Fields.Count} полей:");
                
                foreach (var field in proModeClass.Fields)
                {
                    Console.WriteLine($"  - {field.Name}");
                    Console.WriteLine($"    Тип: {field.FieldType.FullName}");
                    Console.WriteLine($"    Статическое: {field.IsStatic}");
                    Console.WriteLine($"    Публичное: {field.IsPublic}");
                    Console.WriteLine($"    Приватное: {field.IsPrivate}");
                    
                    // Проверяем является ли это backing field для нашего свойства
                    if (field.FieldType.FullName == "System.Boolean")
                    {
                        Console.WriteLine($"    ★ BOOL поле - возможный backing field!");
                    }
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("Полей не найдено");
            }
            Console.WriteLine();
        }

        private void AnalyzeClassProperties(TypeDefinition proModeClass)
        {
            Console.WriteLine($"=== АНАЛИЗ ВСЕХ СВОЙСТВ КЛАССА ===");
            
            if (proModeClass.Properties.Any())
            {
                Console.WriteLine($"Найдено {proModeClass.Properties.Count} свойств:");
                
                foreach (var prop in proModeClass.Properties)
                {
                    Console.WriteLine($"  - {prop.Name} : {prop.PropertyType.FullName}");
                    Console.WriteLine($"    Getter: {prop.GetMethod?.IsStatic} static, {prop.GetMethod?.IsPublic} public");
                    Console.WriteLine($"    Setter: {prop.SetMethod?.IsStatic} static, {prop.SetMethod?.IsPublic} public");
                }
            }
            else
            {
                Console.WriteLine("Свойств не найдено");
            }
            Console.WriteLine();
        }

        private void AnalyzeClassConstructors(TypeDefinition proModeClass)
        {
            Console.WriteLine($"=== АНАЛИЗ КОНСТРУКТОРОВ ===");
            
            var constructors = proModeClass.Methods
                .Where(m => m.IsConstructor)
                .ToList();
                
            if (constructors.Any())
            {
                Console.WriteLine($"Найдено {constructors.Count} конструкторов:");
                
                foreach (var ctor in constructors)
                {
                    Console.WriteLine($"  - {ctor.Name}");
                    Console.WriteLine($"    Статический: {ctor.IsStatic}");
                    Console.WriteLine($"    Параметры: {ctor.Parameters.Count}");
                    
                    if (ctor.HasBody && ctor.Body.Instructions.Count > 3)
                    {
                        Console.WriteLine($"    Содержит код инициализации");
                    }
                }
            }
            else
            {
                Console.WriteLine("Конструкторов не найдено");
            }
            Console.WriteLine();
        }

        private void ProposePatchingStrategy(TypeDefinition proModeClass, string propertyName)
        {
            Console.WriteLine($"=== СТРАТЕГИЯ ПАТЧИНГА ===");
            
            var property = proModeClass.Properties.FirstOrDefault(p => p.Name == propertyName);
            var getterMethod = proModeClass.Methods.FirstOrDefault(m => m.Name == $"get_{propertyName}");
            
            if (property != null || getterMethod != null)
            {
                Console.WriteLine($"РЕКОМЕНДУЕМАЯ СТРАТЕГИЯ для {propertyName}:");
                Console.WriteLine();
                
                Console.WriteLine($"1. ПРОСТАЯ ЗАМЕНА GETTER:");
                Console.WriteLine($"   - Найти метод get_{propertyName}");
                Console.WriteLine($"   - Заменить весь IL код на:");
                Console.WriteLine($"     ldc.i4.1  // загрузить true");
                Console.WriteLine($"     ret       // вернуть");
                Console.WriteLine();
                
                Console.WriteLine($"2. АЛЬТЕРНАТИВА - ДОБАВИТЬ СТАТИЧЕСКОЕ ПОЛЕ:");
                Console.WriteLine($"   - Добавить поле: private static bool _{propertyName} = true;");
                Console.WriteLine($"   - Изменить getter чтобы возвращал это поле");
                Console.WriteLine();
                
                Console.WriteLine($"3. СОГЛАСНО ГАЙДУ - СОЗДАТЬ КОНСТРУКТОР:");
                Console.WriteLine($"   - Создать статический конструктор класса {proModeClass.Name}");
                Console.WriteLine($"   - В конструкторе прописать {propertyName} = true;");
                Console.WriteLine();
                
                // Определяем лучший способ
                if (getterMethod?.HasBody == true)
                {
                    var instructions = getterMethod.Body.Instructions;
                    bool isSimpleReturn = instructions.Count <= 3;
                    
                    if (isSimpleReturn)
                    {
                        Console.WriteLine($"✅ РЕКОМЕНДАЦИЯ: Использовать ПРОСТУЮ ЗАМЕНУ GETTER");
                        Console.WriteLine($"   Getter простой ({instructions.Count} инструкций) - легко заменить");
                    }
                    else
                    {
                        Console.WriteLine($"⚠ РЕКОМЕНДАЦИЯ: Использовать СТАТИЧЕСКИЙ КОНСТРУКТОР");
                        Console.WriteLine($"   Getter сложный ({instructions.Count} инструкций) - безопаснее добавить конструктор");
                    }
                }
            }
            else
            {
                Console.WriteLine($"✗ Свойство {propertyName} не найдено для анализа стратегии");
            }
            
            Console.WriteLine();
        }

        private void DeepFindProModeElements()
        {
            Console.WriteLine("=== 2. ПОИСК БЛОКИРОВКИ PRO РЕЖИМА ===");
            
            var blockingMethods = new List<(TypeDefinition Type, MethodDefinition Method)>();
            
            // Ищем методы с проверками PRO режима
            foreach (var type in assembly.MainModule.Types)
            {
                foreach (var method in type.Methods)
                {
                    if (method.HasBody && ContainsProModeCheckDeep(method))
                    {
                        blockingMethods.Add((type, method));
                    }
                }
            }
            
            if (blockingMethods.Any())
            {
                Console.WriteLine($"✓ Найдено {blockingMethods.Count} методов с проверкой PRO режима:");
                
                foreach (var (type, method) in blockingMethods)
                {
                    Console.WriteLine($"  - {type.Name}.{method.Name}");
                    AnalyzeProModeBlockingMethod(method);
                }
            }
            else
            {
                Console.WriteLine("✗ Методы с блокировкой PRO режима не найдены");
                
                // Дополнительный поиск по паттернам
                SearchProModePatterns();
            }
            Console.WriteLine();
        }

        private void SearchProModePatterns()
        {
            Console.WriteLine("  Дополнительный поиск паттернов PRO режима...");
            
            // Ищем методы с вызовами обфусцированных классов
            var suspiciousMethods = new List<(TypeDefinition Type, MethodDefinition Method)>();
            
            foreach (var type in assembly.MainModule.Types)
            {
                // Ищем классы с короткими обфусцированными именами
                if (IsObfuscatedClassName(type.Name) && type.Methods.Any())
                {
                    foreach (var method in type.Methods)
                    {
                        if (method.HasBody && ContainsLicenseCheck(method))
                        {
                            suspiciousMethods.Add((type, method));
                        }
                    }
                }
            }
            
            if (suspiciousMethods.Any())
            {
                Console.WriteLine($"  ✓ Найдено {suspiciousMethods.Count} подозрительных методов:");
                foreach (var (type, method) in suspiciousMethods)
                {
                    Console.WriteLine($"    - {type.Name}.{method.Name}");
                }
            }
        }

        private bool ContainsLicenseCheck(MethodDefinition method)
        {
            if (!method.HasBody) return false;
            
            var instructions = method.Body.Instructions;
            
            // Ищем паттерны проверки лицензии
            for (int i = 0; i < instructions.Count - 3; i++)
            {
                var inst1 = instructions[i];
                var inst2 = instructions[i + 1];
                var inst3 = instructions[i + 2];
                
                // Паттерн: if (condition) { exit/return }
                if (inst1.OpCode == OpCodes.Call &&
                    (inst2.OpCode == OpCodes.Brfalse || inst2.OpCode == OpCodes.Brtrue) &&
                    inst3.OpCode == OpCodes.Ldstr)
                {
                    return true;
                }
            }
            
            return false;
        }

        private void AnalyzeProModeBlockingMethod(MethodDefinition method)
        {
            Console.WriteLine($"    Анализ блокирующего метода {method.Name}:");
            
            if (!method.HasBody) return;
            
            var instructions = method.Body.Instructions;
            bool foundIfCheck = false;
            
            for (int i = 0; i < instructions.Count - 5; i++)
            {
                var inst = instructions[i];
                
                // Ищем вызов проверки PRO режима
                if (inst.OpCode == OpCodes.Call)
                {
                    var methodRef = inst.Operand as MethodReference;
                    if (methodRef?.Name.StartsWith("get_") == true)
                    {
                        // Проверяем следующие инструкции на наличие условного перехода
                        for (int j = i + 1; j < Math.Min(instructions.Count, i + 10); j++)
                        {
                            var nextInst = instructions[j];
                            if (nextInst.OpCode == OpCodes.Brfalse || nextInst.OpCode == OpCodes.Brtrue)
                            {
                                foundIfCheck = true;
                                Console.WriteLine($"      ✓ Найдена проверка if в позиции {i}-{j}");
                                
                                // Показываем блок кода
                                for (int k = i; k <= j + 3 && k < instructions.Count; k++)
                                {
                                    Console.WriteLine($"        {k}: {instructions[k].OpCode} {instructions[k].Operand}");
                                }
                                break;
                            }
                        }
                        
                        if (foundIfCheck) break;
                    }
                }
            }
            
            if (!foundIfCheck)
            {
                Console.WriteLine($"      ✗ Условная проверка не найдена");
            }
        }

        private void DeepFindPrefabValidation()
        {
            Console.WriteLine("=== 3. ПОИСК ВАЛИДАЦИИ ПРЕФАБОВ ===");
            
            // Ищем все методы с "Prefab.FileDoesntExist"
            var prefabMethods = FindAllMethodsByString("Prefab.FileDoesntExist");
            
            if (prefabMethods.Any())
            {
                Console.WriteLine($"✓ Найдено {prefabMethods.Count} методов с 'Prefab.FileDoesntExist':");
                
                foreach (var method in prefabMethods)
                {
                    Console.WriteLine($"  - {method.DeclaringType.Name}.{method.Name}");
                    AnalyzePrefabValidationMethod(method);
                }
            }
            else
            {
                Console.WriteLine("✗ Методы с 'Prefab.FileDoesntExist' не найдены");
            }
            Console.WriteLine();
        }

        private void AnalyzePrefabValidationMethod(MethodDefinition method)
        {
            Console.WriteLine($"    Анализ метода {method.Name}:");
            
            if (!method.HasBody) return;
            
            var instructions = method.Body.Instructions;
            bool hasFileExists = false;
            bool hasFileCheck = false;
            
            // Ищем File.Exists и связанную логику
            for (int i = 0; i < instructions.Count; i++)
            {
                var inst = instructions[i];
                
                if (inst.OpCode == OpCodes.Call)
                {
                    var methodRef = inst.Operand as MethodReference;
                    var methodStr = methodRef?.ToString() ?? "";
                    
                    if (methodStr.Contains("File") && methodStr.Contains("Exists"))
                    {
                        hasFileExists = true;
                        Console.WriteLine($"      ✓ Найден File.Exists в позиции {i}");
                        
                        // Показываем окружающий код
                        int start = Math.Max(0, i - 5);
                        int end = Math.Min(instructions.Count - 1, i + 15);
                        
                        Console.WriteLine($"      Код блока проверки файла:");
                        for (int j = start; j <= end; j++)
                        {
                            string marker = j == i ? " → " : "   ";
                            Console.WriteLine($"      {marker}{j}: {instructions[j].OpCode} {instructions[j].Operand}");
                        }
                        
                        hasFileCheck = true;
                        break;
                    }
                }
            }
            
            if (!hasFileExists)
            {
                Console.WriteLine($"      ✗ File.Exists не найден в этом методе");
            }
            
            // Ищем строку "Prefab.FileDoesntExist" для контекста
            for (int i = 0; i < instructions.Count; i++)
            {
                var inst = instructions[i];
                if (inst.OpCode == OpCodes.Ldstr && 
                    inst.Operand?.ToString().Contains("Prefab.FileDoesntExist") == true)
                {
                    Console.WriteLine($"      ✓ Строка 'Prefab.FileDoesntExist' в позиции {i}");
                    break;
                }
            }
        }

        private void DeepFindPasswordProtection()
        {
            Console.WriteLine("=== 4. ПОИСК ЗАЩИТЫ ПАРОЛЕМ ===");
            
            var worldSaveLoadType = assembly.MainModule.Types
                .FirstOrDefault(t => t.Name.Contains("WorldSaveLoad"));
                
            if (worldSaveLoadType != null)
            {
                Console.WriteLine($"✓ Найден класс: {worldSaveLoadType.FullName}");
                
                var loadWorldMethods = worldSaveLoadType.Methods
                    .Where(m => m.Name.Contains("LoadWorld"))
                    .ToList();
                    
                if (loadWorldMethods.Any())
                {
                    Console.WriteLine($"✓ Найдено {loadWorldMethods.Count} методов LoadWorld:");
                    
                    foreach (var method in loadWorldMethods)
                    {
                        Console.WriteLine($"  - {method.Name}");
                        AnalyzeLoadWorldMethod(method);
                    }
                }
                else
                {
                    Console.WriteLine("✗ Методы LoadWorld не найдены");
                }
            }
            else
            {
                Console.WriteLine("✗ Класс WorldSaveLoad не найден");
            }
            Console.WriteLine();
        }

        private void AnalyzeLoadWorldMethod(MethodDefinition method)
        {
            Console.WriteLine($"    Анализ метода {method.Name}:");
            
            if (!method.HasBody) return;
            
            var instructions = method.Body.Instructions;
            bool foundStartCoroutine = false;
            bool foundPasswordCall = false;
            
            // Ищем StartCoroutine и KGOBDPHHLBD
            for (int i = 0; i < instructions.Count; i++)
            {
                var inst = instructions[i];
                
                if (inst.OpCode == OpCodes.Call || inst.OpCode == OpCodes.Callvirt)
                {
                    var methodRef = inst.Operand as MethodReference;
                    var methodStr = methodRef?.ToString() ?? "";
                    
                    if (methodStr.Contains("StartCoroutine"))
                    {
                        foundStartCoroutine = true;
                        Console.WriteLine($"      ✓ Найден StartCoroutine в позиции {i}");
                        
                        // Ищем в окрестности KGOBDPHHLBD или другие обфусцированные вызовы
                        for (int j = Math.Max(0, i - 10); j < Math.Min(instructions.Count, i + 10); j++)
                        {
                            var checkInst = instructions[j];
                            if (checkInst.Operand?.ToString().Contains("KGOBDPHHLBD") == true)
                            {
                                foundPasswordCall = true;
                                Console.WriteLine($"      ✓ Найден вызов KGOBDPHHLBD в позиции {j}");
                                break;
                            }
                        }
                        
                        // Показываем код блока
                        int start = Math.Max(0, i - 3);
                        int end = Math.Min(instructions.Count - 1, i + 7);
                        
                        Console.WriteLine($"      Код блока StartCoroutine:");
                        for (int k = start; k <= end; k++)
                        {
                            string marker = k == i ? " → " : "   ";
                            Console.WriteLine($"      {marker}{k}: {instructions[k].OpCode} {instructions[k].Operand}");
                        }
                    }
                }
            }
            
            if (!foundStartCoroutine)
            {
                Console.WriteLine($"      ✗ StartCoroutine не найден");
            }
            
            if (!foundPasswordCall)
            {
                Console.WriteLine($"      ✗ Вызов пароля KGOBDPHHLBD не найден");
            }
        }

        private void FindObfuscatedClasses()
        {
            Console.WriteLine("=== 5. ПОИСК ОБФУСЦИРОВАННЫХ КЛАССОВ ===");
            
            var obfuscatedClasses = assembly.MainModule.Types
                .Where(t => IsObfuscatedClassName(t.Name) && t.Methods.Any())
                .Take(20) // Показываем первые 20
                .ToList();
                
            if (obfuscatedClasses.Any())
            {
                Console.WriteLine($"✓ Найдено {obfuscatedClasses.Count} обфусцированных классов:");
                
                foreach (var type in obfuscatedClasses)
                {
                    Console.WriteLine($"  - {type.Name} ({type.Methods.Count} методов, {type.Properties.Count} свойств)");
                }
            }
            else
            {
                Console.WriteLine("✗ Обфусцированные классы не найдены");
            }
            Console.WriteLine();
        }

        // Вспомогательные методы
        private TypeDefinition FindTypeByMethodStrings(params string[] strings)
        {
            foreach (var type in assembly.MainModule.Types)
            {
                if (type.Methods.Any(m => ContainsMethodStrings(m, strings)))
                {
                    return type;
                }
            }
            return null;
        }

        private bool ContainsMethodStrings(MethodDefinition method, params string[] strings)
        {
            if (!method.HasBody) return false;
            
            var stringLiterals = method.Body.Instructions
                .Where(i => i.OpCode == OpCodes.Ldstr)
                .Select(i => i.Operand?.ToString())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
                
            return strings.All(str => stringLiterals.Any(lit => lit.Contains(str)));
        }

        private bool ContainsProModeCheckDeep(MethodDefinition method)
        {
            if (!method.HasBody) return false;
            
            var instructions = method.Body.Instructions;
            
            // Ищем различные паттерны проверки PRO режима
            for (int i = 0; i < instructions.Count - 2; i++)
            {
                var inst1 = instructions[i];
                var inst2 = instructions[i + 1];
                
                if (inst1.OpCode == OpCodes.Call && 
                    (inst2.OpCode == OpCodes.Brfalse || inst2.OpCode == OpCodes.Brtrue))
                {
                    var methodRef = inst1.Operand as MethodReference;
                    if (methodRef?.Name.StartsWith("get_") == true ||
                        methodRef?.Name.Contains("KJNNJFFHMDD") == true)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }

        private List<MethodDefinition> FindAllMethodsByString(string searchString)
        {
            var methods = new List<MethodDefinition>();
            
            foreach (var type in assembly.MainModule.Types)
            {
                foreach (var method in type.Methods)
                {
                    if (method.HasBody && 
                        method.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr && 
                                                         i.Operand?.ToString().Contains(searchString) == true))
                    {
                        methods.Add(method);
                    }
                }
            }
            
            return methods;
        }

        private bool IsObfuscatedClassName(string name)
        {
            return name.Length >= 8 && name.Length <= 15 && 
                   name.All(c => char.IsUpper(c) || char.IsDigit(c)) &&
                   name.Any(char.IsLetter);
        }

        public ObfuscatedNames FindObfuscatedNamesFromDiscord()
        {
            Console.WriteLine("🔍 Ищем обфусцированные имена через DiscordPresence...");
            
            var result = new ObfuscatedNames();
            
            // Ищем DiscordPresence класс
            var discordType = assembly.MainModule.Types
                .FirstOrDefault(t => t.Name.Contains("DiscordPresence"));
                
            if (discordType == null)
            {
                Console.WriteLine("❌ Класс DiscordPresence не найден");
                return result;
            }
            
            Console.WriteLine($"✅ Найден класс: {discordType.FullName}");
            
            // Ищем метод UpdateActivity
            var updateMethod = discordType.Methods
                .FirstOrDefault(m => m.Name == "UpdateActivity");
                
            if (updateMethod?.HasBody != true)
            {
                Console.WriteLine("❌ Метод UpdateActivity не найден или не имеет тела");
                return result;
            }
            
            Console.WriteLine($"✅ Найден метод: {updateMethod.Name}");
            
            // Анализируем IL код для поиска паттерна SmallImage = (КЛАСС.СВОЙСТВО ? "512_2" : string.Empty)
            var instructions = updateMethod.Body.Instructions;
            
            for (int i = 0; i < instructions.Count; i++)
            {
                var inst = instructions[i];
                
                // Ищем строку "512_2"
                if (inst.OpCode == OpCodes.Ldstr && inst.Operand?.ToString() == "512_2")
                {
                    Console.WriteLine($"✅ Найдена строка '512_2' в позиции {i}");
                    
                    // Показываем широкий контекст вокруг найденной строки
                    Console.WriteLine("📋 КОНТЕКСТ ВОКРУГ '512_2':");
                    int start = Math.Max(0, i - 20);
                    int end = Math.Min(instructions.Count - 1, i + 10);
                    
                    for (int k = start; k <= end; k++)
                    {
                        string marker = k == i ? " → 512_2: " : "       ";
                        var contextInst = instructions[k];
                        Console.WriteLine($"  {marker}{k:D2}: {contextInst.OpCode,-12} {contextInst.Operand}");
                        
                        // Анализируем каждый вызов в контексте
                        if ((contextInst.OpCode == OpCodes.Call || contextInst.OpCode == OpCodes.Callvirt) && 
                            contextInst.Operand is MethodReference methodRef)
                        {
                            if (methodRef.Name.StartsWith("get_"))
                            {
                                var className = methodRef.DeclaringType.Name;
                                var propertyName = methodRef.Name.Substring(4);
                                
                                // Проверяем, является ли это обфусцированным именем
                                if (IsObfuscatedClassName(className) && IsObfuscatedPropertyName(propertyName))
                                {
                                    Console.WriteLine($"  ★★★ НАЙДЕН ОБФУСЦИРОВАННЫЙ GETTER: {className}.get_{propertyName}()");
                                    result.ClassName = className;
                                    result.PropertyName = propertyName;
                                    
                                    // Показываем дополнительный контекст для этого getter
                                    Console.WriteLine($"  🎯 ДЕТАЛИ НАЙДЕННОГО ПАТТЕРНА:");
                                    Console.WriteLine($"     Класс: {className}");
                                    Console.WriteLine($"     Свойство: {propertyName}");
                                    Console.WriteLine($"     Тип: {methodRef.ReturnType?.FullName}");
                                    Console.WriteLine($"     Расстояние от '512_2': {Math.Abs(k - i)} инструкций");
                                    
                                    return result;
                                }
                                else
                                {
                                    Console.WriteLine($"      (не обфусцированный: {className}.get_{propertyName})");
                                }
                            }
                            // НОВОЕ: ищем также обфусцированные boolean методы (которые могут быть getter без префикса get_)
                            else if (methodRef.ReturnType?.FullName == "System.Boolean" && 
                                     IsObfuscatedClassName(methodRef.DeclaringType.Name) && 
                                     IsObfuscatedPropertyName(methodRef.Name) &&
                                     methodRef.Parameters.Count == 0) // getter не имеет параметров
                            {
                                var className = methodRef.DeclaringType.Name;
                                var methodName = methodRef.Name;
                                
                                Console.WriteLine($"  ★★★ НАЙДЕН ОБФУСЦИРОВАННЫЙ BOOLEAN МЕТОД: {className}.{methodName}()");
                                Console.WriteLine($"      Это может быть обфусцированный getter для PRO режима!");
                                
                                result.ClassName = className;
                                result.PropertyName = methodName; // используем имя метода как имя свойства
                                
                                // Показываем дополнительный контекст для этого getter
                                Console.WriteLine($"  🎯 ДЕТАЛИ НАЙДЕННОГО ПАТТЕРНА:");
                                Console.WriteLine($"     Класс: {className}");
                                Console.WriteLine($"     Метод/Свойство: {methodName}");
                                Console.WriteLine($"     Тип: {methodRef.ReturnType?.FullName}");
                                Console.WriteLine($"     Расстояние от '512_2': {Math.Abs(k - i)} инструкций");
                                Console.WriteLine($"     Параметры: {methodRef.Parameters.Count}");
                                
                                return result;
                            }
                            else
                            {
                                Console.WriteLine($"      (не обфусцированный: {methodRef.DeclaringType.Name}.{methodRef.Name})");
                            }
                        }
                    }
                    
                    // Дополнительный поиск: ищем также условные переходы (ternary operator)
                    Console.WriteLine("\n📋 ПОИСК УСЛОВНЫХ ПЕРЕХОДОВ (ternary operator):");
                    for (int k = start; k <= end; k++)
                    {
                        var contextInst = instructions[k];
                        if (contextInst.OpCode == OpCodes.Brtrue || contextInst.OpCode == OpCodes.Brfalse)
                        {
                            Console.WriteLine($"  {k:D2}: {contextInst.OpCode} - условный переход");
                        }
                    }
                    
                    break; // нашли первое вхождение "512_2", достаточно
                }
            }
            
            Console.WriteLine("❌ Паттерн с обфусцированным getter не найден");
            return result;
        }

        private bool IsObfuscatedPropertyName(string name)
        {
            // Обфусцированные имена свойств обычно тоже состоят из заглавных букв
            return name.Length >= 8 && name.Length <= 15 && 
                   name.All(c => char.IsUpper(c) || char.IsDigit(c)) &&
                   name.Any(char.IsLetter);
        }

        public void AnalyzeProModeClassDetailed(string className, string propertyName)
        {
            Console.WriteLine($"\n=== ДЕТАЛЬНЫЙ АНАЛИЗ КЛАССА {className} ===");
            
            var proModeClass = assembly.MainModule.Types
                .FirstOrDefault(t => t.Name == className);
                
            if (proModeClass == null)
            {
                Console.WriteLine($"❌ Класс {className} не найден");
                return;
            }
            
            Console.WriteLine($"✅ Класс найден: {proModeClass.FullName}");
            Console.WriteLine($"   Базовый класс: {proModeClass.BaseType?.FullName ?? "нет"}");
            Console.WriteLine($"   Модификаторы: {proModeClass.Attributes}");
            Console.WriteLine($"   Методов: {proModeClass.Methods.Count}");
            Console.WriteLine($"   Свойств: {proModeClass.Properties.Count}");
            Console.WriteLine($"   Полей: {proModeClass.Fields.Count}");
            
            // Анализ целевого свойства
            AnalyzeTargetPropertyDetailed(proModeClass, propertyName);
            
            // Анализ всех полей класса
            AnalyzeClassFieldsDetailed(proModeClass);
            
            // Анализ конструкторов
            AnalyzeConstructorsDetailed(proModeClass);
            
            // Стратегия патчинга
            ProposePatchingStrategyDetailed(proModeClass, propertyName);
        }

        private void AnalyzeTargetPropertyDetailed(TypeDefinition proModeClass, string propertyName)
        {
            Console.WriteLine($"\n=== АНАЛИЗ СВОЙСТВА/МЕТОДА {propertyName} ===");
            
            var property = proModeClass.Properties
                .FirstOrDefault(p => p.Name == propertyName);
                
            if (property != null)
            {
                Console.WriteLine($"✅ Свойство найдено: {property.Name}");
                Console.WriteLine($"   Тип: {property.PropertyType.FullName}");
                Console.WriteLine($"   Атрибуты: {property.Attributes}");
                Console.WriteLine($"   Имеет getter: {property.GetMethod != null}");
                Console.WriteLine($"   Имеет setter: {property.SetMethod != null}");
                
                if (property.GetMethod?.IsStatic == true)
                {
                    Console.WriteLine($"   ⭐ СТАТИЧЕСКОЕ СВОЙСТВО!");
                }
                
                // Детальный анализ getter
                if (property.GetMethod?.HasBody == true)
                {
                    AnalyzeGetterDetailed(property.GetMethod);
                }
            }
            else
            {
                // Ищем getter метод напрямую (get_ИмяСвойства)
                var getterMethod = proModeClass.Methods
                    .FirstOrDefault(m => m.Name == $"get_{propertyName}");
                    
                if (getterMethod != null)
                {
                    Console.WriteLine($"✅ Найден getter метод: {getterMethod.Name}");
                    AnalyzeGetterDetailed(getterMethod);
                }
                else
                {
                    // Ищем метод напрямую по имени (обфусцированные методы могут не иметь префикса get_)
                    var directMethod = proModeClass.Methods
                        .FirstOrDefault(m => m.Name == propertyName);
                        
                    if (directMethod != null)
                    {
                        Console.WriteLine($"✅ Найден ПРЯМОЙ МЕТОД: {directMethod.Name}");
                        Console.WriteLine($"   ⭐ Это обфусцированный метод без префикса get_!");
                        AnalyzeGetterDetailed(directMethod);
                    }
                    else
                    {
                        Console.WriteLine($"❌ Свойство/метод {propertyName} не найдено");
                        
                        // Показываем все методы класса для отладки
                        Console.WriteLine("\n📋 ВСЕ МЕТОДЫ КЛАССА:");
                        foreach (var method in proModeClass.Methods.Take(10)) // показываем первые 10
                        {
                            Console.WriteLine($"   - {method.Name} : {method.ReturnType?.FullName} (статический: {method.IsStatic})");
                        }
                        if (proModeClass.Methods.Count > 10)
                        {
                            Console.WriteLine($"   ... и еще {proModeClass.Methods.Count - 10} методов");
                        }
                    }
                }
            }
        }

        private void AnalyzeGetterDetailed(MethodDefinition getterMethod)
        {
            Console.WriteLine($"\n--- АНАЛИЗ GETTER: {getterMethod.Name} ---");
            Console.WriteLine($"Статический: {getterMethod.IsStatic}");
            Console.WriteLine($"Публичный: {getterMethod.IsPublic}");
            Console.WriteLine($"Возвращаемый тип: {getterMethod.ReturnType.FullName}");
            Console.WriteLine($"Инструкций: {getterMethod.Body.Instructions.Count}");
            
            Console.WriteLine("\nIL КОД:");
            var instructions = getterMethod.Body.Instructions;
            for (int i = 0; i < instructions.Count; i++)
            {
                var inst = instructions[i];
                Console.WriteLine($"  {i:D2}: {inst.OpCode,-12} {inst.Operand}");
            }
            
            // Анализ поведения
            AnalyzeGetterBehaviorDetailed(instructions);
        }

        private void AnalyzeGetterBehaviorDetailed(IList<Instruction> instructions)
        {
            Console.WriteLine("\n--- АНАЛИЗ ПОВЕДЕНИЯ ---");
            
            bool returnsConstantTrue = false;
            bool returnsConstantFalse = false;
            bool returnsField = false;
            bool hasComplexLogic = false;
            string fieldName = null;
            
            var constants = new List<object>();
            var fieldReferences = new List<string>();
            var methodCalls = new List<string>();
            
            foreach (var inst in instructions)
            {
                switch (inst.OpCode.Code)
                {
                    case Code.Ldc_I4_0:
                        returnsConstantFalse = true;
                        constants.Add(false);
                        break;
                    case Code.Ldc_I4_1:
                        returnsConstantTrue = true;
                        constants.Add(true);
                        break;
                    case Code.Ldsfld:
                    case Code.Ldfld:
                        returnsField = true;
                        fieldName = inst.Operand?.ToString();
                        fieldReferences.Add(fieldName);
                        break;
                    case Code.Call:
                    case Code.Callvirt:
                        methodCalls.Add(inst.Operand?.ToString());
                        break;
                    case Code.Br:
                    case Code.Brfalse:
                    case Code.Brtrue:
                        hasComplexLogic = true;
                        break;
                }
            }
            
            Console.WriteLine("РЕЗУЛЬТАТ АНАЛИЗА:");
            
            if (returnsConstantTrue)
                Console.WriteLine("  ✅ Возвращает константу TRUE");
                
            if (returnsConstantFalse)
                Console.WriteLine("  ❌ Возвращает константу FALSE");
                
            if (returnsField)
            {
                Console.WriteLine($"  📦 Использует поле: {fieldName}");
                foreach (var field in fieldReferences.Distinct())
                {
                    Console.WriteLine($"     - {field}");
                }
            }
            
            if (methodCalls.Any())
            {
                Console.WriteLine("  📞 Вызывает методы:");
                foreach (var call in methodCalls.Distinct())
                {
                    Console.WriteLine($"     - {call}");
                }
            }
            
            if (hasComplexLogic)
                Console.WriteLine("  ⚠️  Содержит условную логику (ветвления)");
                
            if (constants.Any())
            {
                Console.WriteLine($"  📋 Константы: {string.Join(", ", constants)}");
            }
        }

        private void AnalyzeClassFieldsDetailed(TypeDefinition proModeClass)
        {
            Console.WriteLine($"\n=== АНАЛИЗ ПОЛЕЙ КЛАССА ===");
            
            if (!proModeClass.Fields.Any())
            {
                Console.WriteLine("❌ Полей не найдено");
                return;
            }
            
            Console.WriteLine($"✅ Найдено {proModeClass.Fields.Count} полей:");
            
            foreach (var field in proModeClass.Fields)
            {
                Console.WriteLine($"\n  📦 {field.Name}");
                Console.WriteLine($"     Тип: {field.FieldType.FullName}");
                Console.WriteLine($"     Статическое: {field.IsStatic}");
                Console.WriteLine($"     Публичное: {field.IsPublic}");
                Console.WriteLine($"     Приватное: {field.IsPrivate}");
                
                if (field.FieldType.FullName == "System.Boolean")
                {
                    Console.WriteLine($"     ⭐ BOOLEAN ПОЛЕ - возможный backing field для PRO mode!");
                }
                
                if (field.HasConstant)
                {
                    Console.WriteLine($"     🔒 Константа: {field.Constant}");
                }
            }
        }

        private void AnalyzeConstructorsDetailed(TypeDefinition proModeClass)
        {
            Console.WriteLine($"\n=== АНАЛИЗ КОНСТРУКТОРОВ ===");
            
            var constructors = proModeClass.Methods
                .Where(m => m.IsConstructor)
                .ToList();
                
            if (!constructors.Any())
            {
                Console.WriteLine("❌ Конструкторов не найдено");
                return;
            }
            
            Console.WriteLine($"✅ Найдено {constructors.Count} конструкторов:");
            
            foreach (var ctor in constructors)
            {
                Console.WriteLine($"\n  🏗️ {ctor.Name}");
                Console.WriteLine($"     Статический: {ctor.IsStatic}");
                Console.WriteLine($"     Публичный: {ctor.IsPublic}");
                Console.WriteLine($"     Параметры: {ctor.Parameters.Count}");
                
                if (ctor.HasBody)
                {
                    Console.WriteLine($"     Инструкций: {ctor.Body.Instructions.Count}");
                    
                    if (ctor.IsStatic)
                    {
                        Console.WriteLine($"     ⭐ СТАТИЧЕСКИЙ КОНСТРУКТОР - идеально для патчинга!");
                    }
                }
            }
        }

        private void ProposePatchingStrategyDetailed(TypeDefinition proModeClass, string propertyName)
        {
            Console.WriteLine($"\n=== 🎯 СТРАТЕГИЯ ПАТЧИНГА ===");
            
            var property = proModeClass.Properties.FirstOrDefault(p => p.Name == propertyName);
            var getterMethod = proModeClass.Methods.FirstOrDefault(m => m.Name == $"get_{propertyName}");
            var directMethod = proModeClass.Methods.FirstOrDefault(m => m.Name == propertyName);
            var hasStaticCtor = proModeClass.Methods.Any(m => m.IsConstructor && m.IsStatic);
            
            var targetMethod = getterMethod ?? directMethod;
            var methodType = getterMethod != null ? "getter" : (directMethod != null ? "прямой метод" : "неизвестно");
            
            Console.WriteLine($"ЦЕЛЬ: Заставить {propertyName} всегда возвращать TRUE");
            Console.WriteLine($"Тип метода: {methodType}");
            Console.WriteLine();
            
            if (targetMethod?.HasBody == true)
            {
                var instructions = targetMethod.Body.Instructions;
                bool isSimple = instructions.Count <= 3;
                
                Console.WriteLine("📋 ДОСТУПНЫЕ ВАРИАНТЫ:");
                Console.WriteLine();
                
                Console.WriteLine("1️⃣ ПРОСТАЯ ЗАМЕНА МЕТОДА (рекомендуется):");
                Console.WriteLine($"   - Очистить тело метода {propertyName}");
                Console.WriteLine($"   - Заменить на:");
                Console.WriteLine($"     ldc.i4.1  // загрузить true");
                Console.WriteLine($"     ret       // вернуть");
                Console.WriteLine($"   ✅ Простота: {(isSimple ? "Высокая" : "Средняя")}");
                Console.WriteLine($"   ✅ Надежность: Высокая");
                Console.WriteLine($"   📋 Статический: {targetMethod.IsStatic}");
                Console.WriteLine();
                
                Console.WriteLine("2️⃣ МОДИФИКАЦИЯ BACKING FIELD:");
                var boolFields = proModeClass.Fields.Where(f => f.FieldType.FullName == "System.Boolean").ToList();
                if (boolFields.Any())
                {
                    Console.WriteLine($"   ✅ Найдено {boolFields.Count} Boolean полей:");
                    foreach (var field in boolFields)
                    {
                        Console.WriteLine($"     - {field.Name} (статическое: {field.IsStatic})");
                        if (field.Name.Contains("BackingField"))
                        {
                            Console.WriteLine($"       ⭐ Это backing field! Можно изменить его значение");
                        }
                    }
                    Console.WriteLine($"   - Установить нужное поле в true при загрузке");
                    Console.WriteLine($"   ✅ Простота: Средняя");
                }
                else
                {
                    Console.WriteLine($"   ❌ Boolean полей не найдено");
                }
                Console.WriteLine();
                
                Console.WriteLine("3️⃣ СОЗДАНИЕ СТАТИЧЕСКОГО КОНСТРУКТОРА:");
                if (hasStaticCtor)
                {
                    Console.WriteLine($"   ⚠️ Статический конструктор уже существует");
                    Console.WriteLine($"   - Модифицировать существующий конструктор");
                }
                else
                {
                    Console.WriteLine($"   - Создать новый статический конструктор");
                }
                Console.WriteLine($"   - Добавить код для установки backing field в true");
                Console.WriteLine($"   ✅ Простота: Средняя");
                Console.WriteLine($"   ✅ Надежность: Высокая");
                Console.WriteLine();
                
                // Рекомендация
                Console.WriteLine("🎯 РЕКОМЕНДАЦИЯ:");
                if (isSimple)
                {
                    Console.WriteLine($"   ✅ Использовать ВАРИАНТ 1 - Простая замена метода");
                    Console.WriteLine($"   Причина: Метод простой ({instructions.Count} инструкций), легко заменить");
                }
                else
                {
                    Console.WriteLine($"   ✅ Использовать ВАРИАНТ 2 - Модификация backing field");
                    Console.WriteLine($"   Причина: Метод сложный ({instructions.Count} инструкций)");
                    Console.WriteLine($"   Найдено backing field, можно изменить его значение");
                }
                
                Console.WriteLine();
                Console.WriteLine("💡 ПРИМЕР РЕАЛИЗАЦИИ ДЛЯ MONO.CECIL:");
                Console.WriteLine($"   // Вариант 1: Замена тела метода");
                Console.WriteLine($"   var method = type.Methods.First(m => m.Name == \"{propertyName}\");");
                Console.WriteLine($"   method.Body.Instructions.Clear();");
                Console.WriteLine($"   method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_1));");
                Console.WriteLine($"   method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));");
            }
            else
            {
                Console.WriteLine($"❌ Метод {propertyName} не найден или не имеет тела");
                Console.WriteLine($"   Невозможно создать стратегию патчинга");
            }
        }

        /// <summary>
        /// Находит класс-блокировщик PRO режима (аналог NJFSINOIPNMDA)
        /// </summary>
        public void FindProModeBlockerClass()
        {
            Console.WriteLine("\n🚫 === ПОИСК КЛАССА-БЛОКИРОВЩИКА PRO РЕЖИМА ===");
            Console.WriteLine("Ищем класс аналогичный NJFSINOIPNMDA, который блокирует PRO режим");
            Console.WriteLine();

            var candidates = new List<(TypeDefinition type, MethodDefinition method, int score)>();

            foreach (var type in assembly.MainModule.Types)
            {
                if (type.Name.Length < 8 || !IsObfuscatedName(type.Name))
                    continue;

                foreach (var method in type.Methods)
                {
                    if (!method.HasBody) continue;

                    int score = AnalyzeProBlockerMethod(method);
                    if (score > 0)
                    {
                        candidates.Add((type, method, score));
                    }
                }
            }

            // Сортируем кандидатов по score
            candidates = candidates.OrderByDescending(c => c.score).ToList();

            Console.WriteLine($"📊 Найдено {candidates.Count} кандидатов:");
            Console.WriteLine();

            foreach (var (type, method, score) in candidates.Take(5))
            {
                Console.WriteLine($"🎯 Класс: {type.Name} | Метод: {method.Name} | Score: {score}");
                Console.WriteLine($"   Статический: {method.IsStatic} | Возврат: {method.ReturnType?.Name}");
                
                // Показываем IL код этого метода
                Console.WriteLine($"   📋 IL КОД метода {method.Name}:");
                for (int i = 0; i < Math.Min(method.Body.Instructions.Count, 20); i++)
                {
                    var inst = method.Body.Instructions[i];
                    Console.WriteLine($"     {i}: {inst.OpCode} {inst.Operand}");
                }
                
                Console.WriteLine();
            }

            if (candidates.Any())
            {
                var topCandidate = candidates.First();
                Console.WriteLine($"🏆 ЛУЧШИЙ КАНДИДАТ: {topCandidate.type.Name}");
                Console.WriteLine($"   Анализ показывает, что это скорее всего блокировщик PRO режима");
                Console.WriteLine($"   Этот класс нужно отредактировать для активации PRO режима");
            }
            else
            {
                Console.WriteLine("❌ Класс-блокировщик PRO режима не найден");
            }
        }

        /// <summary>
        /// Анализирует метод на предмет блокировки PRO режима
        /// </summary>
        private int AnalyzeProBlockerMethod(MethodDefinition method)
        {
            int score = 0;
            var instructions = method.Body.Instructions;

            // Ищем паттерны характерные для блокировщика PRO режима
            for (int i = 0; i < instructions.Count; i++)
            {
                var inst = instructions[i];

                // Вызовы булевых методов обфусцированных классов
                if ((inst.OpCode == OpCodes.Call || inst.OpCode == OpCodes.Callvirt) &&
                    inst.Operand is MethodReference methodRef)
                {
                    // Проверка boolean методов с обфусцированными именами
                    if (methodRef.ReturnType?.FullName == "System.Boolean" &&
                        IsObfuscatedName(methodRef.DeclaringType.Name) &&
                        IsObfuscatedName(methodRef.Name))
                    {
                        score += 10;
                    }

                    // Характерные методы для проверки лицензии
                    if (methodRef.Name.Contains("MCBTJCJNTHJ") || 
                        methodRef.Name.Contains("KOJPNKHPGCL") ||
                        methodRef.Name.Contains("HTONOBICOGM") ||
                        methodRef.Name.Contains("HBIHPPNHFPE"))
                    {
                        score += 20;
                    }
                }

                // Условные переходы (if statements)
                if (inst.OpCode == OpCodes.Brfalse || inst.OpCode == OpCodes.Brtrue)
                {
                    score += 2;
                }

                // Проверки строк (text + text != b)
                if (inst.OpCode == OpCodes.Ldstr)
                {
                    score += 1;
                }

                // Вызовы IsNullOrEmpty
                if (inst.OpCode == OpCodes.Call && 
                    inst.Operand?.ToString().Contains("IsNullOrEmpty") == true)
                {
                    score += 5;
                }
            }

            // Дополнительный анализ структуры метода
            if (instructions.Count > 10 && instructions.Count < 100) // Разумный размер
                score += 5;

            if (method.IsStatic) // Часто статические методы
                score += 3;

            return score;
        }

        /// <summary>
        /// Проверяет, является ли имя обфусцированным
        /// </summary>
        private bool IsObfuscatedName(string name)
        {
            return name.Length >= 8 && name.Length <= 15 &&
                   name.All(c => char.IsUpper(c) || char.IsDigit(c)) &&
                   name.Any(char.IsLetter);
        }

        /// <summary>
        /// Находит методы, которые вызывают пропатченный PRO метод
        /// </summary>
        public void FindMethodsCallingProMode()
        {
            Console.WriteLine("\n🔍 === ПОИСК МЕТОДОВ, ВЫЗЫВАЮЩИХ PRO РЕЖИМ ===");
            Console.WriteLine("Ищем методы, которые вызывают NIPAPEJDFCK.ODAFOACPJCL()");
            Console.WriteLine();

            var callers = new List<(TypeDefinition type, MethodDefinition method)>();

            foreach (var type in assembly.MainModule.Types)
            {
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody) continue;

                    // Проверяем все инструкции на вызов PRO метода
                    foreach (var instruction in method.Body.Instructions)
                    {
                        if ((instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) &&
                            instruction.Operand is MethodReference methodRef)
                        {
                            // Ищем вызов нашего PRO метода
                            if (methodRef.DeclaringType?.Name == "NIPAPEJDFCK" && 
                                methodRef.Name == "ODAFOACPJCL")
                            {
                                callers.Add((type, method));
                                break;
                            }
                        }
                    }
                }
            }

            Console.WriteLine($"📊 Найдено {callers.Count} методов, вызывающих PRO режим:");
            Console.WriteLine();

            foreach (var (type, method) in callers)
            {
                Console.WriteLine($"🎯 Класс: {type.Name} | Метод: {method.Name}");
                Console.WriteLine($"   Статический: {method.IsStatic} | Возврат: {method.ReturnType?.Name}");
                Console.WriteLine($"   Инструкций: {method.Body.Instructions.Count}");
                
                // Показываем IL код этого метода
                Console.WriteLine($"   📋 ПОЛНЫЙ IL КОД метода {method.Name}:");
                for (int i = 0; i < method.Body.Instructions.Count; i++)
                {
                    var inst = method.Body.Instructions[i];
                    string highlight = "";
                    if ((inst.OpCode == OpCodes.Call || inst.OpCode == OpCodes.Callvirt) &&
                        inst.Operand?.ToString().Contains("NIPAPEJDFCK") == true)
                    {
                        highlight = " ← ВЫЗОВ PRO МЕТОДА";
                    }
                    Console.WriteLine($"     {i}: {inst.OpCode} {inst.Operand}{highlight}");
                }
                
                Console.WriteLine();
                Console.WriteLine("🚫 ЭТОТ МЕТОД НУЖНО ОТРЕДАКТИРОВАТЬ ДЛЯ АКТИВАЦИИ PRO РЕЖИМА!");
                Console.WriteLine("   Удалите условие if (NIPAPEJDFCK.ODAFOACPJCL()) и весь блокирующий код внутри");
                Console.WriteLine();
            }

            if (!callers.Any())
            {
                Console.WriteLine("❌ Методы, вызывающие PRO режим, не найдены");
                Console.WriteLine("   Возможно, PRO проверка уже отключена или используется другой механизм");
            }
        }

        /// <summary>
        /// ПОЛНОЕ ИССЛЕДОВАНИЕ ВСЕХ PRO БЛОКИРОВОК
        /// Ищет ВСЕ обфусцированные boolean методы и анализирует их вызовы
        /// </summary>
        public void FindAllProModeBlockers()
        {
            Console.WriteLine("\n🔬 === ПОЛНОЕ ИССЛЕДОВАНИЕ ВСЕХ PRO БЛОКИРОВОК ===");
            Console.WriteLine("Поиск ВСЕХ обфусцированных boolean методов и их вызовов");
            Console.WriteLine();

            // Шаг 1: Найти все обфусцированные boolean методы
            var obfuscatedBoolMethods = FindAllObfuscatedBooleanMethods();
            
            Console.WriteLine($"📊 Найдено {obfuscatedBoolMethods.Count} обфусцированных boolean методов:");
            Console.WriteLine();

            // Шаг 2: Для каждого метода найти кто его вызывает
            var allBlockers = new List<(string className, string methodName, List<(TypeDefinition type, MethodDefinition method)> callers)>();

            foreach (var boolMethod in obfuscatedBoolMethods)
            {
                var callers = FindCallersOfMethod(boolMethod.DeclaringType.Name, boolMethod.Name);
                if (callers.Any())
                {
                    allBlockers.Add((boolMethod.DeclaringType.Name, boolMethod.Name, callers));
                    
                    Console.WriteLine($"🎯 {boolMethod.DeclaringType.Name}.{boolMethod.Name}()");
                    Console.WriteLine($"   Тип: {boolMethod.ReturnType.FullName}");
                    Console.WriteLine($"   Статический: {boolMethod.IsStatic}");
                    Console.WriteLine($"   Вызывается в {callers.Count} местах:");
                    
                    foreach (var (type, method) in callers.Take(5)) // показываем первые 5
                    {
                        Console.WriteLine($"     - {type.Name}.{method.Name}");
                    }
                    if (callers.Count > 5)
                    {
                        Console.WriteLine($"     ... и еще {callers.Count - 5} мест");
                    }
                    Console.WriteLine();
                }
            }

            // Шаг 3: Детальный анализ каждой группы блокировщиков
            Console.WriteLine("🔍 === ДЕТАЛЬНЫЙ АНАЛИЗ КАЖДОЙ ГРУППЫ ===");
            Console.WriteLine();

            int groupNumber = 1;
            foreach (var (className, methodName, callers) in allBlockers)
            {
                Console.WriteLine($"📋 ГРУППА {groupNumber}: {className}.{methodName}()");
                AnalyzeBlockerGroup(className, methodName, callers);
                groupNumber++;
            }

            // Шаг 4: Сводка всех найденных блокировщиков
            Console.WriteLine("📊 === СВОДКА ВСЕХ PRO БЛОКИРОВЩИКОВ ===");
            Console.WriteLine();
            
            var totalCallers = allBlockers.SelectMany(x => x.callers).Count();
            Console.WriteLine($"🎯 Всего найдено {allBlockers.Count} обфусцированных boolean методов");
            Console.WriteLine($"🚫 Всего найдено {totalCallers} методов-блокировщиков");
            Console.WriteLine();

            Console.WriteLine("📋 СПИСОК ВСЕХ МЕТОДОВ ДЛЯ УДАЛЕНИЯ PRO БЛОКИРОВОК:");
            foreach (var (className, methodName, callers) in allBlockers)
            {
                Console.WriteLine($"\n🔧 {className}.{methodName}() - используется в {callers.Count} местах:");
                foreach (var (type, method) in callers)
                {
                    Console.WriteLine($"   ❌ {type.Name}.{method.Name} - УДАЛИТЬ PRO БЛОКИРОВКУ");
                }
            }
        }

        /// <summary>
        /// Находит все обфусцированные boolean методы без параметров
        /// </summary>
        private List<MethodDefinition> FindAllObfuscatedBooleanMethods()
        {
            Console.WriteLine("🔍 Поиск всех обфусцированных boolean методов...");
            
            var obfuscatedBoolMethods = new List<MethodDefinition>();
            var foundClasses = new HashSet<string>();

            foreach (var type in assembly.MainModule.Types)
            {
                // Ищем только классы с обфусцированными именами
                if (!IsObfuscatedName(type.Name) || type.Name.Length < 8)
                    continue;

                foreach (var method in type.Methods)
                {
                    // Ищем статические boolean методы без параметров с обфусцированными именами
                    if (method.IsStatic &&
                        method.ReturnType?.FullName == "System.Boolean" &&
                        method.Parameters.Count == 0 &&
                        IsObfuscatedName(method.Name) &&
                        method.Name.Length >= 8)
                    {
                        obfuscatedBoolMethods.Add(method);
                        foundClasses.Add(type.Name);
                        
                        Console.WriteLine($"  ✅ {type.Name}.{method.Name}() - обфусцированный boolean метод");
                    }
                }
            }

            Console.WriteLine($"\n📊 Итого найдено:");
            Console.WriteLine($"   - {obfuscatedBoolMethods.Count} обфусцированных boolean методов");
            Console.WriteLine($"   - В {foundClasses.Count} различных классах");
            Console.WriteLine();

            return obfuscatedBoolMethods;
        }

        /// <summary>
        /// Находит все методы, которые вызывают указанный метод
        /// </summary>
        private List<(TypeDefinition type, MethodDefinition method)> FindCallersOfMethod(string targetClassName, string targetMethodName)
        {
            var callers = new List<(TypeDefinition type, MethodDefinition method)>();

            foreach (var type in assembly.MainModule.Types)
            {
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody) continue;

                    // Проверяем все инструкции на вызов целевого метода
                    foreach (var instruction in method.Body.Instructions)
                    {
                        if ((instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) &&
                            instruction.Operand is MethodReference methodRef)
                        {
                            // Ищем вызов целевого метода
                            if (methodRef.DeclaringType?.Name == targetClassName && 
                                methodRef.Name == targetMethodName)
                            {
                                callers.Add((type, method));
                                break; // один вызов на метод достаточно
                            }
                        }
                    }
                }
            }

            return callers;
        }

        /// <summary>
        /// Детально анализирует группу блокировщиков для одного boolean метода
        /// </summary>
        private void AnalyzeBlockerGroup(string className, string methodName, List<(TypeDefinition type, MethodDefinition method)> callers)
        {
            Console.WriteLine($"   🎯 Анализ группы для {className}.{methodName}()");
            Console.WriteLine($"   📊 Количество вызовов: {callers.Count}");
            Console.WriteLine();

            // Анализируем паттерны использования
            var patterns = new Dictionary<string, int>();
            var complexMethods = new List<(TypeDefinition type, MethodDefinition method)>();

            foreach (var (type, method) in callers)
            {
                var pattern = AnalyzeUsagePattern(method, className, methodName);
                if (patterns.ContainsKey(pattern))
                    patterns[pattern]++;
                else
                    patterns[pattern] = 1;

                // Собираем сложные методы для детального анализа
                if (method.Body.Instructions.Count > 10)
                {
                    complexMethods.Add((type, method));
                }
            }

            Console.WriteLine($"   📋 ПАТТЕРНЫ ИСПОЛЬЗОВАНИЯ:");
            foreach (var kvp in patterns.OrderByDescending(x => x.Value))
            {
                Console.WriteLine($"     - {kvp.Key}: {kvp.Value} методов");
            }
            Console.WriteLine();

            // Показываем несколько примеров кода
            Console.WriteLine($"   💡 ПРИМЕРЫ КОДА (первые 3 метода):");
            foreach (var (type, method) in callers.Take(3))
            {
                Console.WriteLine($"\n     🔧 {type.Name}.{method.Name}:");
                ShowMethodCallContext(method, className, methodName);
            }

            // Специальный анализ для сложных методов
            if (complexMethods.Any())
            {
                Console.WriteLine($"\n   ⚠️ СЛОЖНЫЕ МЕТОДЫ (требуют особого внимания):");
                foreach (var (type, method) in complexMethods.Take(2))
                {
                    Console.WriteLine($"     - {type.Name}.{method.Name} ({method.Body.Instructions.Count} инструкций)");
                }
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Анализирует паттерн использования boolean метода
        /// </summary>
        private string AnalyzeUsagePattern(MethodDefinition method, string targetClass, string targetMethod)
        {
            if (!method.HasBody) return "Без тела";

            var instructions = method.Body.Instructions;
            if (instructions.Count <= 3) return "Простой возврат";

            // Ищем паттерн использования
            for (int i = 0; i < instructions.Count - 1; i++)
            {
                var inst = instructions[i];
                if ((inst.OpCode == OpCodes.Call || inst.OpCode == OpCodes.Callvirt) &&
                    inst.Operand is MethodReference methodRef &&
                    methodRef.DeclaringType?.Name == targetClass &&
                    methodRef.Name == targetMethod)
                {
                    var nextInst = instructions[i + 1];
                    
                    if (nextInst.OpCode == OpCodes.Ret)
                        return "Прямой возврат";
                    else if (nextInst.OpCode == OpCodes.Brfalse)
                        return "Условие if (false)";
                    else if (nextInst.OpCode == OpCodes.Brtrue)
                        return "Условие if (true)";
                    else if (nextInst.OpCode == OpCodes.Stfld || nextInst.OpCode == OpCodes.Stloc)
                        return "Присвоение в переменную";
                    else
                        return "Сложная логика";
                }
            }

            return "Неопределенный";
        }

        /// <summary>
        /// Показывает контекст вызова метода в IL коде
        /// </summary>
        private void ShowMethodCallContext(MethodDefinition method, string targetClass, string targetMethod)
        {
            if (!method.HasBody) return;

            var instructions = method.Body.Instructions;
            
            // Ищем вызов и показываем контекст вокруг него
            for (int i = 0; i < instructions.Count; i++)
            {
                var inst = instructions[i];
                if ((inst.OpCode == OpCodes.Call || inst.OpCode == OpCodes.Callvirt) &&
                    inst.Operand is MethodReference methodRef &&
                    methodRef.DeclaringType?.Name == targetClass &&
                    methodRef.Name == targetMethod)
                {
                    // Показываем контекст: 3 инструкции до и 5 после
                    int start = Math.Max(0, i - 3);
                    int end = Math.Min(instructions.Count - 1, i + 5);
                    
                    for (int k = start; k <= end; k++)
                    {
                        string marker = k == i ? "   ➤ " : "     ";
                        var contextInst = instructions[k];
                        Console.WriteLine($"{marker}{k:D2}: {contextInst.OpCode,-12} {contextInst.Operand}");
                    }
                    break;
                }
            }
        }

        public void Dispose()
        {
            assembly?.Dispose();
        }

        /// <summary>
        /// Находит все места, где вызывается указанный метод
        /// </summary>
        private List<(TypeDefinition type, MethodDefinition method)> FindMethodCallers(string className, string methodName)
        {
            var callers = new List<(TypeDefinition type, MethodDefinition method)>();

            foreach (var type in assembly.MainModule.Types)
            {
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody) continue;

                    foreach (var instruction in method.Body.Instructions)
                    {
                        if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
                        {
                            if (instruction.Operand is MethodReference methodRef)
                            {
                                if (methodRef.DeclaringType.Name == className && methodRef.Name == methodName)
                                {
                                    callers.Add((type, method));
                                    break; // Не добавляем один метод несколько раз
                                }
                            }
                        }
                    }
                }
            }

            return callers;
        }

        /// <summary>
        /// Находит класс NJFSINOIPNMDA (или его текущий аналог) и показывает ВСЕ его методы
        /// </summary>
        public void FindNJFSINOIPNMDAClass()
        {
            Console.WriteLine("\n🎯 === ПОИСК КЛАССА NJFSINOIPNMDA И ВСЕХ ЕГО МЕТОДОВ ===");
            Console.WriteLine("Ищем класс-блокировщик NJFSINOIPNMDA и анализируем ВСЕ его методы");
            Console.WriteLine();

            TypeDefinition targetClass = null;

            // Сначала попробуем найти по точному имени
            targetClass = assembly.MainModule.Types.FirstOrDefault(t => t.Name == "NJFSINOIPNMDA");
            
            if (targetClass == null)
            {
                Console.WriteLine("⚠️ Класс NJFSINOIPNMDA не найден по точному имени.");
                Console.WriteLine("🔍 Ищем класс-аналог по характеристикам...");
                Console.WriteLine();

                // Ищем класс, который содержит много статических boolean методов
                var candidates = new List<(TypeDefinition type, int boolMethodCount)>();

                foreach (var type in assembly.MainModule.Types)
                {
                    if (!IsObfuscatedName(type.Name) || type.Name.Length < 8)
                        continue;

                    var boolMethodCount = type.Methods.Count(m => 
                        m.IsStatic && 
                        m.ReturnType.FullName == "System.Boolean" && 
                        m.Parameters.Count == 0 &&
                        IsObfuscatedName(m.Name));

                    if (boolMethodCount >= 3) // Класс с множественными boolean методами
                    {
                        candidates.Add((type, boolMethodCount));
                    }
                }

                if (candidates.Any())
                {
                    targetClass = candidates.OrderByDescending(c => c.boolMethodCount).First().type;
                    Console.WriteLine($"✅ Найден класс-кандидат: {targetClass.Name} с {candidates.First().boolMethodCount} boolean методами");
                }
            }
            else
            {
                Console.WriteLine($"✅ Найден точный класс: {targetClass.Name}");
            }

            if (targetClass == null)
            {
                Console.WriteLine("❌ Класс NJFSINOIPNMDA не найден!");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"📋 === АНАЛИЗ КЛАССА {targetClass.Name} ===");
            Console.WriteLine($"📁 Namespace: {targetClass.Namespace}");
            Console.WriteLine($"🔢 Всего методов: {targetClass.Methods.Count}");
            Console.WriteLine();

            // Показываем ВСЕ методы класса
            int methodIndex = 1;
            foreach (var method in targetClass.Methods)
            {
                Console.WriteLine($"🔹 Метод {methodIndex}: {method.Name}");
                Console.WriteLine($"   └─ Тип возврата: {method.ReturnType.Name}");
                Console.WriteLine($"   └─ Статический: {method.IsStatic}");
                Console.WriteLine($"   └─ Параметры: {method.Parameters.Count}");
                
                // Показываем IL код для первых 5 методов (исключая конструктор и Awake)
                if (methodIndex <= 5 && method.HasBody && method.Name != ".ctor" && method.Name != "Awake")
                {
                    Console.WriteLine($"   └─ IL КОД ({method.Body.Instructions.Count} инструкций):");
                    for (int i = 0; i < method.Body.Instructions.Count && i < 20; i++)
                    {
                        var inst = method.Body.Instructions[i];
                        Console.WriteLine($"      {i}: {inst.OpCode} {inst.Operand}");
                    }
                    if (method.Body.Instructions.Count > 20)
                    {
                        Console.WriteLine($"      ... и ещё {method.Body.Instructions.Count - 20} инструкций");
                    }
                    Console.WriteLine();
                }
                
                Console.WriteLine();
                methodIndex++;
            }
        }

        /// <summary>
        /// Показывает детальный IL код всех найденных PRO блокировщиков
        /// </summary>
        public void ShowBlockersCode()
        {
            Console.WriteLine("\n📋 === ДЕТАЛЬНЫЙ КОД ВСЕХ PRO БЛОКИРОВЩИКОВ ===");
            Console.WriteLine("Показываем IL код методов, которые блокируют PRO функции");
            Console.WriteLine();

            // Сначала покажем код методов в NJFSINOIPNMDA
            var njfClass = assembly.MainModule.Types.FirstOrDefault(t => t.Name == "NJFSINOIPNMDA");
            if (njfClass != null)
            {
                Console.WriteLine($"🎯 === КЛАСС NJFSINOIPNMDA ({njfClass.Methods.Count} методов) ===");
                Console.WriteLine();

                // Показываем только методы, которые вызывают PRO проверки
                var blockingMethods = new List<MethodDefinition>();

                foreach (var method in njfClass.Methods)
                {
                    if (method.HasBody && ContainsProModeCall(method))
                    {
                        blockingMethods.Add(method);
                    }
                }

                Console.WriteLine($"📊 Найдено {blockingMethods.Count} методов с PRO блокировками:");
                Console.WriteLine();

                foreach (var method in blockingMethods.Take(10)) // Показываем первые 10
                {
                    ShowMethodCode(method, $"NJFSINOIPNMDA.{method.Name}");
                }

                if (blockingMethods.Count > 10)
                {
                    Console.WriteLine($"\n... и ещё {blockingMethods.Count - 10} методов с аналогичным кодом");
                }
            }

            Console.WriteLine("\n🔧 === ДРУГИЕ КЛАССЫ С PRO БЛОКИРОВКАМИ ===");
            Console.WriteLine();

            // Показываем блокировщики в других классах
            var otherBlockers = new List<(TypeDefinition type, MethodDefinition method)>();

            foreach (var type in assembly.MainModule.Types)
            {
                if (type.Name == "NJFSINOIPNMDA") continue; // уже показали

                foreach (var method in type.Methods)
                {
                    if (method.HasBody && ContainsProModeCall(method))
                    {
                        otherBlockers.Add((type, method));
                    }
                }
            }

            Console.WriteLine($"📊 Найдено {otherBlockers.Count} блокировщиков в других классах:");
            Console.WriteLine();

            // Группируем по классам
            var groupedByClass = otherBlockers.GroupBy(x => x.type.Name).Take(5);

            foreach (var group in groupedByClass)
            {
                Console.WriteLine($"🎯 КЛАСС: {group.Key} ({group.Count()} методов)");

                foreach (var (type, method) in group.Take(3)) // показываем первые 3 из каждого класса
                {
                    ShowMethodCode(method, $"{type.Name}.{method.Name}");
                }

                if (group.Count() > 3)
                {
                    Console.WriteLine($"   ... и ещё {group.Count() - 3} методов\n");
                }
            }
        }

        /// <summary>
        /// Показывает детальный IL код конкретного метода
        /// </summary>
        private void ShowMethodCode(MethodDefinition method, string fullName)
        {
            Console.WriteLine($"🔧 === {fullName} ===");
            Console.WriteLine($"   Возврат: {method.ReturnType.Name} | Статический: {method.IsStatic} | Инструкций: {method.Body.Instructions.Count}");
            Console.WriteLine();

            var instructions = method.Body.Instructions;
            
            // Показываем весь IL код с выделением PRO вызовов
            for (int i = 0; i < instructions.Count; i++)
            {
                var inst = instructions[i];
                string highlight = "";
                string prefix = "   ";

                // Выделяем вызовы PRO методов
                if ((inst.OpCode == OpCodes.Call || inst.OpCode == OpCodes.Callvirt) &&
                    inst.Operand is MethodReference methodRef)
                {
                    if (methodRef.DeclaringType?.Name == "NIPAPEJDFCK" ||
                        (methodRef.ReturnType?.FullName == "System.Boolean" && IsObfuscatedName(methodRef.DeclaringType?.Name)))
                    {
                        highlight = " ← 🚫 PRO ПРОВЕРКА!";
                        prefix = "➤ ";
                    }
                }

                // Выделяем условные переходы
                if (inst.OpCode == OpCodes.Brfalse || inst.OpCode == OpCodes.Brtrue)
                {
                    if (string.IsNullOrEmpty(highlight))
                        highlight = " ← условный переход";
                }

                // Выделяем возвраты
                if (inst.OpCode == OpCodes.Ret)
                {
                    if (string.IsNullOrEmpty(highlight))
                        highlight = " ← возврат";
                }

                Console.WriteLine($"{prefix}{i:D2}: {inst.OpCode,-12} {inst.Operand}{highlight}");
            }

            Console.WriteLine();
            AnalyzeMethodPattern(method);
            Console.WriteLine();
        }

        /// <summary>
        /// Анализирует паттерн блокировки в методе
        /// </summary>
        private void AnalyzeMethodPattern(MethodDefinition method)
        {
            if (!method.HasBody) return;

            var instructions = method.Body.Instructions;
            
            Console.WriteLine("📋 АНАЛИЗ ПАТТЕРНА:");

            // Ищем PRO проверку и что происходит после неё
            for (int i = 0; i < instructions.Count - 1; i++)
            {
                var inst = instructions[i];
                
                if ((inst.OpCode == OpCodes.Call || inst.OpCode == OpCodes.Callvirt) &&
                    inst.Operand is MethodReference methodRef &&
                    methodRef.DeclaringType?.Name == "NIPAPEJDFCK")
                {
                    var nextInst = instructions[i + 1];
                    
                    if (nextInst.OpCode == OpCodes.Brfalse)
                    {
                        Console.WriteLine("   🔍 ПАТТЕРН: if (!proMode) { ... }");
                        Console.WriteLine("   💡 ДЕЙСТВИЕ: Блокирует выполнение когда PRO режим ВЫКЛЮЧЕН");
                        
                        // Ищем что происходит в блоке if
                        var target = nextInst.Operand as Instruction;
                        if (target != null)
                        {
                            var targetIndex = instructions.ToList().IndexOf(target);
                            Console.WriteLine($"   📍 Переход на инструкцию {targetIndex} при PRO режиме");
                        }
                    }
                    else if (nextInst.OpCode == OpCodes.Brtrue)
                    {
                        Console.WriteLine("   🔍 ПАТТЕРН: if (proMode) { ... }");
                        Console.WriteLine("   💡 ДЕЙСТВИЕ: Выполняет код только когда PRO режим ВКЛЮЧЕН");
                    }
                    else if (nextInst.OpCode == OpCodes.Ret)
                    {
                        Console.WriteLine("   🔍 ПАТТЕРН: return proMode;");
                        Console.WriteLine("   💡 ДЕЙСТВИЕ: Возвращает состояние PRO режима");
                    }
                    
                    break;
                }
            }

            // Ищем строки для понимания контекста
            var strings = instructions
                .Where(i => i.OpCode == OpCodes.Ldstr)
                .Select(i => i.Operand?.ToString())
                .Where(s => !string.IsNullOrEmpty(s))
                .Take(3)
                .ToList();

            if (strings.Any())
            {
                Console.WriteLine("   📝 СТРОКИ В МЕТОДЕ:");
                foreach (var str in strings)
                {
                    Console.WriteLine($"      - \"{str}\"");
                }
            }
        }

        /// <summary>
        /// Проверяет, содержит ли метод вызов PRO режима
        /// </summary>
        private bool ContainsProModeCall(MethodDefinition method)
        {
            if (!method.HasBody) return false;

            foreach (var instruction in method.Body.Instructions)
            {
                if ((instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) &&
                    instruction.Operand is MethodReference methodRef)
                {
                    // Проверяем вызовы NIPAPEJDFCK (основной PRO класс)
                    if (methodRef.DeclaringType?.Name == "NIPAPEJDFCK")
                    {
                        return true;
                    }

                    // Проверяем другие обфусцированные boolean методы
                    if (methodRef.ReturnType?.FullName == "System.Boolean" &&
                        IsObfuscatedName(methodRef.DeclaringType?.Name) &&
                        IsObfuscatedName(methodRef.Name) &&
                        methodRef.Parameters.Count == 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
} 