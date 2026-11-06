using System;
using Mono.Cecil;

class TestPluginEntry
{
    static int Main(string[] args)
    {
        AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(args[0]);
        foreach (TypeDefinition type in assembly.MainModule.Types)
        {
            if (type.Name == "<Module>") continue;
            // VaM's single-file loader uses Assembly.GetTypes()[0] as MainType.
            if (type.FullName != "LaiLighting.QuickFill" || type.BaseType == null ||
                type.BaseType.FullName != "MVRScript")
            {
                Console.Error.WriteLine("Invalid first plugin type: " + type.FullName);
                return 1;
            }
            Console.WriteLine("Plugin entry passed: first type is LaiLighting.QuickFill : MVRScript.");
            return 0;
        }
        Console.Error.WriteLine("Plugin assembly contains no entry type.");
        return 1;
    }
}
