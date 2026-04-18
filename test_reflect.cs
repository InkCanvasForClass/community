using System;
using System.Reflection;
using System.Linq;

class Program
{
    static void Main()
    {
        try
        {
            var ass = Assembly.LoadFrom(@"packages\Microsoft.Toolkit.Wpf.UI.Controls.6.1.2\lib\net462\Microsoft.Toolkit.Wpf.UI.Controls.dll");
            var type = ass.GetType("Microsoft.Toolkit.Wpf.UI.Controls.InkCanvas");
            var prop = type.GetProperty("InkPresenter");
            Console.WriteLine("InkPresenter type: " + prop.PropertyType.FullName);
            
            var updateMethod = prop.PropertyType.GetMethod("UpdateDefaultDrawingAttributes");
            if (updateMethod != null) {
                Console.WriteLine("Method exists! Params: " + updateMethod.GetParameters()[0].ParameterType.FullName);
            } else {
                Console.WriteLine("Method UpdateDefaultDrawingAttributes NOT FOUND in " + prop.PropertyType.FullName);
                foreach (var m in prop.PropertyType.GetMethods()) {
                    if (m.Name.Contains("Update")) Console.WriteLine(" - " + m.Name);
                }
            }
            
            var modeProp = prop.PropertyType.GetProperty("InputProcessingConfiguration");
            if (modeProp != null) {
                Console.WriteLine("InputProcessingConfiguration type: " + modeProp.PropertyType.FullName);
                var modeProp2 = modeProp.PropertyType.GetProperty("Mode");
                if (modeProp2 != null) Console.WriteLine("Mode type: " + modeProp2.PropertyType.FullName);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }
}
