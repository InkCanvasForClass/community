using System.Reflection;
using System.IO;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

/// <summary>
/// Inspect the actual compiled monitor, including its async/lambda bodies, without
/// starting Office or loading the WPF application. Monitoring must not control the
/// lifetime or window state of a shared PowerPoint COM server.
/// </summary>
internal static class OfficeLifecycleGuard
{
    internal static int Verify(string assemblyPath)
    {
        using var file = File.OpenRead(assemblyPath);
        using var image = new PEReader(file);
        var metadata = image.GetMetadataReader();
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null))
            .ToDictionary(code => unchecked((ushort)code.Value));
        string[] scopes = { "CreatePowerPointApplication", "ClosePowerPointApplication", "CloseOwnedPowerPointAsync",
            "DisconnectPowerPointApplication", "ReleasePowerPointApplicationAsync" };
        var forbidden = new HashSet<string> { "Quit", "Close", "set_Visible", "set_WindowState" };
        int inspected = 0;
        foreach (var typeHandle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(typeHandle);
            string typeName = metadata.GetString(type.Name);
            foreach (var methodHandle in type.GetMethods())
            {
                var method = metadata.GetMethodDefinition(methodHandle);
                string methodName = metadata.GetString(method.Name);
                if (method.RelativeVirtualAddress == 0 || !scopes.Any(scope => typeName.Contains(scope) || methodName.Contains(scope))) continue;
                inspected++;
                var bytes = image.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();
                for (int offset = 0; offset < bytes.Length;)
                {
                    ushort value = bytes[offset++];
                    if (value == 0xFE) value = (ushort)(0xFE00 | bytes[offset++]);
                    var opcode = opcodes[value];
                    if (opcode.OperandType == OperandType.InlineMethod)
                    {
                        var target = MetadataTokens.EntityHandle(BitConverter.ToInt32(bytes, offset));
                        string targetName = null;
                        EntityHandle parent = default;
                        if (target.Kind == HandleKind.MemberReference)
                        {
                            var member = metadata.GetMemberReference((MemberReferenceHandle)target);
                            targetName = metadata.GetString(member.Name);
                            parent = member.Parent;
                        }
                        else if (target.Kind == HandleKind.MethodDefinition)
                        {
                            var member = metadata.GetMethodDefinition((MethodDefinitionHandle)target);
                            targetName = metadata.GetString(member.Name);
                            parent = member.GetDeclaringType();
                        }
                        if (targetName != null && forbidden.Contains(targetName))
                        {
                            string ns = parent.Kind switch
                            {
                                HandleKind.TypeReference => metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)parent).Namespace),
                                HandleKind.TypeDefinition => metadata.GetString(metadata.GetTypeDefinition((TypeDefinitionHandle)parent).Namespace),
                                _ => ""
                            };
                            if (ns == "Microsoft.Office.Interop.PowerPoint")
                                throw new InvalidOperationException($"PowerPoint monitor must not call {targetName}: {typeName}.{methodName}");
                        }
                    }
                    offset += opcode.OperandType switch
                    {
                        OperandType.InlineNone => 0,
                        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                        OperandType.InlineVar => 2,
                        OperandType.InlineI8 or OperandType.InlineR => 8,
                        OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, offset),
                        _ => 4
                    };
                }
            }
        }
        if (inspected < 3) throw new InvalidOperationException("PowerPoint lifecycle methods were not found in the supplied assembly.");
        return inspected;
    }
}
