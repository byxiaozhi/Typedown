// Patches Typedown.XamlUI.dll (see README.md): show / patch for Dispatcher.PostTask (1.0.2), show-remove / patch-remove for Window.RemoveWindowMessageHook (1.0.3).
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// xamlui-patch show <dll> | patch <in> <out>
var module = ModuleDefinition.ReadModule(args[1], new ReaderParameters { ReadWrite = false });
var dispatcher = module.Types.First(t => t.FullName == "Typedown.XamlUI.Dispatcher");
var post = dispatcher.Methods.First(m => m.Name == "PostTask");
if (args[0] == "show-remove")
{
    var window = module.Types.First(t => t.FullName == "Typedown.XamlUI.Window");
    var remove = window.Methods.First(m => m.Name == "RemoveWindowMessageHook");
    foreach (var i in remove.Body.Instructions) Console.WriteLine(i);
    foreach (var h in remove.Body.ExceptionHandlers) Console.WriteLine($"handler {h.HandlerType} try {h.TryStart.Offset}-{h.TryEnd.Offset}");
}
else if (args[0] == "show")
{
    foreach (var r in module.AssemblyReferences) Console.WriteLine("ref " + r.FullName);
    foreach (var t in module.GetTypeReferences().Where(t => t.Namespace == "System.Threading")) Console.WriteLine("type " + t.FullName + " @ " + t.Scope.Name);
    foreach (var i in post.Body.Instructions) Console.WriteLine(i);
    Console.WriteLine("field type: " + dispatcher.Fields.First(f => f.Name == "curTaskId").FieldType);
}
else if (args[0] == "patch-remove")
{
    // RemoveWindowMessageHook on a window that never added a hook (a window closed as it opens: the caption buttons
    // unload before they loaded) ran Where over a null list, and the ArgumentNullException ended the process. With the
    // lock held, a null list now leaves the method as it would after removing.
    var window = module.Types.First(t => t.FullName == "Typedown.XamlUI.Window");
    var remove = window.Methods.First(m => m.Name == "RemoveWindowMessageHook");
    var il = remove.Body.Instructions;
    var enter = il.First(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference m && m.Name == "Enter" && m.DeclaringType.Name == "Monitor");
    var load = il.First(i => i.OpCode == OpCodes.Ldfld && i.Operand is FieldReference f && f.Name == "_windowMessagehooks");
    var leave = il.First(i => i.OpCode == OpCodes.Leave || i.OpCode == OpCodes.Leave_S);
    if (il.IndexOf(load) != il.IndexOf(enter) + 3) throw new Exception("RemoveWindowMessageHook is not the expected code");
    var proc = remove.Body.GetILProcessor();
    var next = il[il.IndexOf(enter) + 1];
    proc.InsertBefore(next, proc.Create(OpCodes.Ldarg_0));
    proc.InsertBefore(next, proc.Create(OpCodes.Ldfld, (FieldReference)load.Operand));
    proc.InsertBefore(next, proc.Create(OpCodes.Brfalse, leave));
    module.Write(args[2]);
    Console.WriteLine("patched -> " + args[2]);
}
else if (args[0] == "patch")
{
    // var taskId = curTaskId++;  ->  var taskId = (uint)(Interlocked.Increment(ref curTaskId) - 1);
    // Two threads posting at once read the same curTaskId, and the second TryAdd under that id dropped its action:
    // an await continuation posted that way never ran.
    var il = post.Body.Instructions;
    var expected = new[] { "ldarg.0", "ldarg.0", "ldfld", "stloc.1", "ldloc.1", "ldc.i4.1", "add", "stfld", "ldloc.1", "stloc.0" };
    var actual = il.Take(expected.Length).Select(i => i.OpCode.Name).ToArray();
    if (!expected.SequenceEqual(actual)) throw new Exception("PostTask is not the expected code: " + string.Join(" ", actual));
    var field = (FieldReference)il[2].Operand;
    var interlocked = module.GetTypeReferences().First(t => t.FullName == "System.Threading.Interlocked");
    var increment = new MethodReference("Increment", module.TypeSystem.Int32, interlocked);
    increment.Parameters.Add(new ParameterDefinition(new ByReferenceType(module.TypeSystem.Int32)));
    var proc = post.Body.GetILProcessor();
    var keep = il[9]; // stloc.0
    for (var n = 0; n < 9; n++) proc.Remove(il[0]);
    proc.InsertBefore(keep, proc.Create(OpCodes.Ldarg_0));
    proc.InsertBefore(keep, proc.Create(OpCodes.Ldflda, field));
    proc.InsertBefore(keep, proc.Create(OpCodes.Call, module.ImportReference(increment)));
    proc.InsertBefore(keep, proc.Create(OpCodes.Ldc_I4_1));
    proc.InsertBefore(keep, proc.Create(OpCodes.Sub));
    module.Write(args[2]);
    Console.WriteLine("patched -> " + args[2]);
}
