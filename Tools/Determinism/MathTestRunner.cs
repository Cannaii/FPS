using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
class Program
{
    static int Main()
    {
        int passed=0, failed=0;
        foreach(var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Namespace == "AFPS.Tests.EditMode"))
        foreach(var method in type.GetMethods())
        {
            var cases=method.GetCustomAttributes<TestCaseAttribute>().ToArray();
            if(cases.Length==0 && method.GetCustomAttribute<TestAttribute>()==null) continue;
            var arguments=cases.Length==0 ? new[]{Array.Empty<object>()} : cases.Select(c=>c.Arguments).ToArray();
            foreach(var args in arguments)
            {
                try {method.Invoke(Activator.CreateInstance(type),args);passed++;}
                catch(Exception e){failed++;Console.WriteLine(type.Name+"."+method.Name+"("+string.Join(",",args)+"): "+(e.InnerException??e));}
            }
        }
        Console.WriteLine($"Passed: {passed}; Failed: {failed}");return failed==0?0:1;
    }
}