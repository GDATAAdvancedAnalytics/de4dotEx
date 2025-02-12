using de4dot.blocks;
using de4dot.blocks.cflow;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace de4dot.code.deobfuscators.VirtualGuard
{
    internal class ProxyCallFixer
    {
        public MethodDef moduleCctor;
        List<Block> allBlocks = new List<Block>();
        List<IMethod> objList = new List<IMethod>();
        FieldDef realObj;
        int stsfldIdx = -1;
        List<MethodDef> foundMethods = new List<MethodDef>();
        List<TypeDef> typesToRemove = new List<TypeDef>();

        public ProxyCallFixer(ModuleDefMD module)
        {
            foreach (var type in module.GetTypes())
            {
                foreach (var tprop in type.Properties)
                {
                    foreach (var method in tprop.GetMethods)
                    {
                        if (method.Body == null) continue;
                        foundMethods.Add(method);
                    }
                }
                foreach (var method in type.Methods)
                {
                    if (method.Body == null) continue;
                    foundMethods.Add(method);
                }
            }

            moduleCctor = DotNetUtils.GetModuleTypeCctor(module);
            var instrs = moduleCctor.Body.Instructions;
            var instrCount = instrs.Count;
            for (int i = 0; i < instrCount; i++)
            {
                if (instrs[i].OpCode == OpCodes.Ldftn)
                {
                    objList.Add(instrs[i].Operand as IMethod);
                }
                if (instrs[i].OpCode == OpCodes.Stsfld)
                {
                    realObj = instrs[i].Operand as FieldDef;
                    stsfldIdx = i;
                }
            }
            //if found nop out all instructions till stsfld instruction
            if (stsfldIdx != -1)
            {
                for (int i = 0; i <= stsfldIdx; i++)
                    instrs[i].OpCode = OpCodes.Nop;
            }
        }

        public void ReplaceMethodCalls(IMethod toFind, IMethod toReplace)
        {
            foreach (var method in foundMethods)
            {
                var instrs = method.Body.Instructions;
                foreach (var ins in instrs)
                {
                    if (ins.OpCode == OpCodes.Call && ins.Operand == toFind)
                        ins.Operand = toReplace;
                }
            }
        }

        internal List<TypeDef> Deobfuscate(Blocks blocks)
        {
            IList<Instruction> allInstructions;
            IList<ExceptionHandler> allExceptionHandlers;
            blocks.GetCode(out allInstructions, out allExceptionHandlers);

            var instrs = allInstructions;
            var instrCount = instrs.Count;
            int objIdx;
            for (int i = 0; i < instrCount; i++)
            {
                if (instrs[i].OpCode == OpCodes.Ldsfld && instrs[i].Operand == realObj)
                {
                    objIdx = instrs[i + 1].GetLdcI4Value();
                    typesToRemove.Add(blocks.Method.DeclaringType);
                    ReplaceMethodCalls(blocks.Method, objList[objIdx]);
                }
            }
            return typesToRemove;
        }
    }
}
