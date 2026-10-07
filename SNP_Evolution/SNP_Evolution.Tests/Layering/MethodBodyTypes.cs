using System.Reflection;
using System.Reflection.Emit;

namespace SnpEvolution.Tests.Layering
{
    // Reads the types a method body names, which reflection over signatures alone misses.
    internal static class MethodBodyTypes
    {
        private static readonly Dictionary<short, OpCode> OpCodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (OpCode)field.GetValue(null)!)
            .ToDictionary(code => code.Value);

        // The types and members the method's IL names by token.
        public static IEnumerable<Type> Of(MethodBase method)
        {
            byte[]? il = method.GetMethodBody()?.GetILAsByteArray();
            if (il == null)
            {
                yield break;
            }
            foreach (LocalVariableInfo local in method.GetMethodBody()!.LocalVariables)
            {
                yield return local.LocalType;
            }
            Type[]? typeArguments = method.DeclaringType?.IsGenericType == true ? method.DeclaringType.GetGenericArguments() : null;
            Type[]? methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
            int at = 0;
            while (at < il.Length)
            {
                short value = il[at] == 0xFE ? (short)(0xFE00 | il[at + 1]) : il[at];
                OpCode code = OpCodes[value];
                at += code.Size;
                switch (code.OperandType)
                {
                    case OperandType.InlineMethod:
                    case OperandType.InlineField:
                    case OperandType.InlineType:
                    case OperandType.InlineTok:
                        if (Resolve(method.Module, BitConverter.ToInt32(il, at), typeArguments, methodArguments) is Type resolved)
                        {
                            yield return resolved;
                        }
                        at += 4;
                        break;
                    case OperandType.InlineSwitch:
                        at += 4 + 4 * BitConverter.ToInt32(il, at);
                        break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                        at += 8;
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar:
                        at += 1;
                        break;
                    case OperandType.InlineVar:
                        at += 2;
                        break;
                    case OperandType.InlineNone:
                        break;
                    default:
                        at += 4;
                        break;
                }
            }
        }

        private static Type? Resolve(Module module, int token, Type[]? typeArguments, Type[]? methodArguments)
        {
            try
            {
                return module.ResolveMember(token, typeArguments, methodArguments) switch
                {
                    Type type => type,
                    MemberInfo member => member.DeclaringType,
                    _ => null,
                };
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
