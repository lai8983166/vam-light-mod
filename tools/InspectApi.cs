using System;
using System.Reflection;
class InspectApi {
 static void Main(string[] args) {
  string root=args[0];
  AppDomain.CurrentDomain.AssemblyResolve += delegate(object s, ResolveEventArgs e) { string p=System.IO.Path.Combine(root,new AssemblyName(e.Name).Name+".dll"); return System.IO.File.Exists(p)?Assembly.LoadFrom(p):null; };
  Assembly a=Assembly.LoadFrom(System.IO.Path.Combine(root,"Assembly-CSharp.dll"));
  foreach(string name in new[]{"MVRScript","Atom","FreeControllerV3","JSONStorableStringChooser","JSONStorableFloat","SuperController"}) {
   Type t=a.GetType(name); Console.WriteLine("TYPE "+name);
   foreach(MemberInfo m in t.GetMembers(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)) {
    string x=m.ToString(); if(name=="FreeControllerV3" || name.StartsWith("JSON") || x.Contains("Create") || x.Contains("Register") || x.Contains("rigid") || x.Contains("Controller") || x.Contains("GetAtom") || x.Contains("GetStorable") || x.Contains("on")) Console.WriteLine(x);
   }
  }
 }
}
